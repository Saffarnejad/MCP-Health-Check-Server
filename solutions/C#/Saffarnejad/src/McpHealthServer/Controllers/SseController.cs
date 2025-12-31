using System.Text;
using Microsoft.AspNetCore.Mvc;
using McpProtocol.Models;
using McpHealthServer.Services;
using System.Text.Json;

namespace McpHealthServer.Controllers
{
    [ApiController]
    [Route("mcp/sse")]
    public class SseController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly IHealthCheckService _healthCheckService;
        private readonly ILogger<SseController> _logger;

        public SseController(
            ISessionService sessionService,
            IHealthCheckService healthCheckService,
            ILogger<SseController> logger)
        {
            _sessionService = sessionService;
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        [HttpGet("{sessionId}")]
        public async Task GetSseStream(string sessionId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null)
            {
                Response.StatusCode = 404;
                await Response.WriteAsync("Session not found");
                return;
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            // Send initial handshake event
            await SendSseEvent("handshake", new { session_id = sessionId });

            // Mark handshake as completed
            session.IsHandshakeCompleted = true;
            await SendSseEvent("ready", new { message = "Session ready" });

            // Process tool calls via HTTP POST to this session
            session.MessageQueue.Enqueue(JsonSerializer.Serialize(new
            {
                type = "instruction",
                message = "Send tool calls to POST /mcp/sse/{sessionId}/tool"
            }));

            // Keep connection alive and process messages
            try
            {
                while (!session.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Send heartbeat every 30 seconds
                    await SendSseEvent("heartbeat", new { timestamp = DateTime.UtcNow });

                    // Check for messages in queue
                    if (session.MessageQueue.TryDequeue(out var message))
                    {
                        await SendSseEvent("message", JsonSerializer.Deserialize<object>(message));
                    }

                    await Response.Body.FlushAsync();
                    await Task.Delay(1000, session.CancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Connection closed normally
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SSE stream error for session {SessionId}", sessionId);
            }
            finally
            {
                _sessionService.RemoveSession(sessionId);
            }
        }

        [HttpPost("{sessionId}/tool")]
        public async Task<IActionResult> ExecuteTool(string sessionId, [FromBody] ToolCallRequest request)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Session not found" });
            }

            if (!session.IsHandshakeCompleted)
            {
                return BadRequest(new { error = "Handshake not completed" });
            }

            try
            {
                if (request.Params.Name == "check_api_status")
                {
                    string? url = null;
                    if (request.Params.Arguments.TryGetValue("url", out var urlObj))
                    {
                        url = urlObj.ToString();
                    }
                    else
                    {
                        return BadRequest(new { error = "Missing or invalid 'url' parameter" });
                    }

                    var result = await _healthCheckService.CheckApiStatusAsync(url, session.CancellationTokenSource.Token);

                    var response = new ToolCallResponse
                    {
                        Id = request.Id,
                        Result = new ToolCallResult
                        {
                            Content = new List<ToolContent>
                            {
                                new ToolContent
                                {
                                    Type = "text",
                                    Text = JsonSerializer.Serialize(new
                                    {
                                        url = result.Url,
                                        status = result.Status,
                                        http_status = result.HttpStatus,
                                        latency_ms = result.LatencyMs,
                                        checked_at = result.CheckedAt.ToString("o"),
                                        error = result.Error
                                    })
                                }
                            }
                        }
                    };

                    // Queue the response for SSE stream
                    session.MessageQueue.Enqueue(JsonSerializer.Serialize(new
                    {
                        type = "tool_response",
                        response = response
                    }));

                    return Accepted(new
                    {
                        message = "Tool execution started",
                        result_available_via_sse = true
                    });
                }

                return NotFound(new { error = $"Tool '{request.Params.Name}' not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool {ToolName} for session {SessionId}",
                    request.Params.Name, sessionId);

                return StatusCode(500, new { error = "Internal server error during tool execution" });
            }
        }

        private async Task SendSseEvent(string eventName, object data)
        {
            var jsonData = JsonSerializer.Serialize(data);
            var eventMessage = $"event: {eventName}\ndata: {jsonData}\n\n";

            await Response.WriteAsync(eventMessage, Encoding.UTF8);
            await Response.Body.FlushAsync();
        }
    }
}