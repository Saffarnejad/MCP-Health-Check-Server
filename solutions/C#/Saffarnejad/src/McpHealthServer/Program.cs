using McpHealthServer.Middlewares;
using McpHealthServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure options
builder.Services.Configure<McpHealthServer.Services.SessionOptions>(
    builder.Configuration.GetSection("Session"));
builder.Services.Configure<HealthCheckOptions>(
    builder.Configuration.GetSection("HealthCheck"));
builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection("Security"));

// Register services
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();
builder.Services.AddSingleton<ISecurityValidator, SecurityValidator>();

// Configure HTTP client for health checks
builder.Services.AddHttpClient("HealthCheck", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromMilliseconds(
        builder.Configuration.GetValue<int>("HealthCheck:TimeoutMs", 3000));
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();
app.UseRequestLogging();
app.UseErrorHandling();

app.MapControllers();

app.MapGet("/", () => "MCP Health Check Server is running");
app.MapGet("/health", () => Results.Ok(new { status = "UP" }));

// Session monitoring endpoint (for debugging)
app.MapGet("/admin/sessions", (ISessionService sessionService) =>
{
    var sessions = sessionService.GetAllSessions();
    return Results.Ok(new
    {
        count = sessions.Count(),
        sessions = sessions.Select(s => new
        {
            s.SessionId,
            s.CreatedAt,
            s.LastAccessedAt,
            s.IsHandshakeCompleted,
            AgeMinutes = (DateTime.UtcNow - s.LastAccessedAt).TotalMinutes
        })
    });
});

app.Run();