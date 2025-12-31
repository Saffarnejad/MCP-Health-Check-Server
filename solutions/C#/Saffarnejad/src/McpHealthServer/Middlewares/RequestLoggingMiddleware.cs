namespace McpHealthServer.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            // Log request
            _logger.LogInformation("Request: {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await _next(context);

            var elapsed = DateTime.UtcNow - startTime;

            // Log response
            _logger.LogInformation("Response: {Method} {Path} => {StatusCode} ({ElapsedMs}ms)",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, elapsed.TotalMilliseconds);
        }
    }

    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}