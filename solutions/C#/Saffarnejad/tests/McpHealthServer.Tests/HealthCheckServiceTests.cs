using McpHealthServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace McpHealthServer.Tests.Services
{
    public class HealthCheckServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<HealthCheckService>> _mockLogger;
        private readonly Mock<IOptions<HealthCheckOptions>> _mockOptions;
        private readonly Mock<ISecurityValidator> _mockSecurityValidator;
        private readonly HealthCheckService _service;

        public HealthCheckServiceTests()
        {
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<HealthCheckService>>();
            _mockOptions = new Mock<IOptions<HealthCheckOptions>>();
            _mockSecurityValidator = new Mock<ISecurityValidator>();

            _mockOptions.Setup(o => o.Value)
                .Returns(new HealthCheckOptions
                {
                    TimeoutMs = 3000,
                    MaxContentSizeBytes = 1048576
                });

            _mockSecurityValidator
                .Setup(v => v.ValidateUrl(It.IsAny<string>()))
                .Returns(SecurityValidationResult.Success());

            _service = new HealthCheckService(
                _mockHttpClientFactory.Object,
                _mockLogger.Object,
                _mockOptions.Object,
                _mockSecurityValidator.Object);
        }

        [Fact]
        public async Task CheckApiStatusAsync_WithValidUrl_ShouldReturnUp()
        {
            // Arrange
            var url = "https://example.com/health";
            var handler = new MockHttpMessageHandler();
            handler.SetupResponse(new HttpResponseMessage(HttpStatusCode.OK));

            var client = new HttpClient(handler);
            _mockHttpClientFactory.Setup(f => f.CreateClient("HealthCheck"))
                .Returns(client);

            // Act
            var result = await _service.CheckApiStatusAsync(url);

            // Assert
            Assert.Equal("UP", result.Status);
            Assert.Equal(200, result.HttpStatus);
            Assert.NotNull(result.LatencyMs);
        }

        [Fact]
        public async Task CheckApiStatusAsync_WithInvalidUrl_ShouldReturnDown()
        {
            // Arrange
            var url = "invalid-url";
            _mockSecurityValidator
                .Setup(v => v.ValidateUrl(url))
                .Returns(SecurityValidationResult.Failure("Invalid URL"));

            // Act
            var result = await _service.CheckApiStatusAsync(url);

            // Assert
            Assert.Equal("DOWN", result.Status);
            Assert.NotNull(result.Error);
        }

        [Fact]
        public async Task CheckApiStatusAsync_WithTimeout_ShouldReturnDown()
        {
            // Arrange
            var url = "https://example.com/health";
            var handler = new MockHttpMessageHandler();
            handler.SetupDelayedResponse(TimeSpan.FromSeconds(5));

            var client = new HttpClient(handler);
            _mockHttpClientFactory.Setup(f => f.CreateClient("HealthCheck"))
                .Returns(client);

            // Act
            var result = await _service.CheckApiStatusAsync(url);

            // Assert
            Assert.Equal("DOWN", result.Status);
            Assert.Contains("Timeout", result.Error);
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage? _response;
        private TimeSpan _delay = TimeSpan.Zero;

        public void SetupResponse(HttpResponseMessage response)
        {
            _response = response;
        }

        public void SetupDelayedResponse(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay + TimeSpan.FromSeconds(1), cancellationToken);
                throw new TaskCanceledException();
            }

            return _response ?? new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}