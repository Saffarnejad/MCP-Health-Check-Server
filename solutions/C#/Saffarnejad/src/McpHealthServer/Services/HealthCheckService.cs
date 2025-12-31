using System.Net;
using Microsoft.Extensions.Options;

namespace McpHealthServer.Services
{
    public class HealthCheckResult
    {
        public string Url { get; set; } = string.Empty;
        public string Status { get; set; } = "DOWN";
        public int? HttpStatus { get; set; }
        public long? LatencyMs { get; set; }
        public DateTime CheckedAt { get; set; }
        public string? Error { get; set; }
    }

    public interface IHealthCheckService
    {
        Task<HealthCheckResult> CheckApiStatusAsync(string url, CancellationToken cancellationToken = default);
    }

    public class HealthCheckService : IHealthCheckService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly HealthCheckOptions _options;
        private readonly ISecurityValidator _securityValidator;

        public HealthCheckService(
            IHttpClientFactory httpClientFactory,
            ILogger<HealthCheckService> logger,
            IOptions<HealthCheckOptions> options,
            ISecurityValidator securityValidator)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
            _securityValidator = securityValidator;
        }

        public async Task<HealthCheckResult> CheckApiStatusAsync(string url, CancellationToken cancellationToken = default)
        {
            var result = new HealthCheckResult
            {
                Url = url,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                // Validate URL security
                var securityValidation = _securityValidator.ValidateUrl(url);
                if (!securityValidation.IsAllowed)
                {
                    result.Error = securityValidation.ErrorMessage;
                    return result;
                }

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using var client = _httpClientFactory.CreateClient("HealthCheck");
                client.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("MCP-Health-Check/1.0");

                using var response = await client.SendAsync(request, cancellationToken);
                stopwatch.Stop();

                result.LatencyMs = stopwatch.ElapsedMilliseconds;
                result.HttpStatus = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    result.Status = "UP";
                }
                else
                {
                    result.Status = "DOWN";
                    result.Error = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                }
            }
            catch (TaskCanceledException ex)
            {
                result.Error = $"Timeout after {_options.TimeoutMs}ms";
                _logger.LogWarning(ex, "Health check timeout for {Url}", url);
            }
            catch (HttpRequestException ex)
            {
                result.Error = ex.Message;
                _logger.LogWarning(ex, "HTTP request failed for {Url}", url);
            }
            catch (UriFormatException ex)
            {
                result.Error = "Invalid URL format";
                _logger.LogWarning(ex, "Invalid URL format: {Url}", url);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                _logger.LogError(ex, "Unexpected error checking {Url}", url);
            }

            return result;
        }
    }

    public class HealthCheckOptions
    {
        public int TimeoutMs { get; set; } = 3000;
        public int MaxContentSizeBytes { get; set; } = 1024 * 1024; // 1MB
    }

    public interface ISecurityValidator
    {
        SecurityValidationResult ValidateUrl(string url);
    }

    public class SecurityValidator : ISecurityValidator
    {
        private readonly SecurityOptions _options;
        private readonly ILogger<SecurityValidator> _logger;

        public SecurityValidator(IOptions<SecurityOptions> options, ILogger<SecurityValidator> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public SecurityValidationResult ValidateUrl(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return SecurityValidationResult.Failure("Invalid URL format");
                }

                // Check scheme
                if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    return SecurityValidationResult.Failure($"Unsupported scheme: {uri.Scheme}");
                }

                // Check against allowlist if configured
                if (_options.AllowedDomains?.Any() == true)
                {
                    var host = uri.Host;
                    if (!_options.AllowedDomains.Any(domain =>
                        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)))
                    {
                        return SecurityValidationResult.Failure($"Domain {host} is not in allowlist");
                    }
                }

                // Check against blocklist
                if (_options.BlockedDomains?.Any() == true)
                {
                    var host = uri.Host;
                    if (_options.BlockedDomains.Any(domain =>
                        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)))
                    {
                        return SecurityValidationResult.Failure($"Domain {host} is blocked");
                    }
                }

                // Check for private/loopback addresses
                if (!_options.AllowPrivateAddresses && IsPrivateAddress(uri))
                {
                    return SecurityValidationResult.Failure("Private/loopback addresses are not allowed");
                }

                return SecurityValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating URL: {Url}", url);
                return SecurityValidationResult.Failure("Error validating URL");
            }
        }

        private bool IsPrivateAddress(Uri uri)
        {
            try
            {
                var host = uri.Host;

                // Check for loopback
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("127.0.0.1") ||
                    host.Equals("::1"))
                {
                    return true;
                }

                // Check for private network addresses
                if (IPAddress.TryParse(host, out var ip))
                {
                    if (ip.IsIPv6SiteLocal || ip.IsIPv6LinkLocal)
                        return true;

                    var bytes = ip.GetAddressBytes();

                    // 10.0.0.0/8
                    if (bytes[0] == 10)
                        return true;

                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                        return true;

                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168)
                        return true;

                    // 169.254.0.0/16 (link-local)
                    if (bytes[0] == 169 && bytes[1] == 254)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public class SecurityValidationResult
    {
        public bool IsAllowed { get; }
        public string? ErrorMessage { get; }

        private SecurityValidationResult(bool isAllowed, string? errorMessage)
        {
            IsAllowed = isAllowed;
            ErrorMessage = errorMessage;
        }

        public static SecurityValidationResult Success() => new(true, null);
        public static SecurityValidationResult Failure(string errorMessage) => new(false, errorMessage);
    }

    public class SecurityOptions
    {
        public List<string> AllowedDomains { get; set; } = new();
        public List<string> BlockedDomains { get; set; } = new();
        public bool AllowPrivateAddresses { get; set; } = false;
    }
}