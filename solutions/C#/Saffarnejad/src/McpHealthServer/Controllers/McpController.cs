using Microsoft.AspNetCore.Mvc;
using McpProtocol.Models;
using McpHealthServer.Services;

namespace McpHealthServer.Controllers
{
    [ApiController]
    [Route("api/mcp")]
    public class McpController : ControllerBase
    {
        private readonly ISessionService _sessionService;
        private readonly ILogger<McpController> _logger;

        public McpController(ISessionService sessionService, ILogger<McpController> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        [HttpPost("initialize")]
        public IActionResult Initialize([FromBody] InitializeRequest request)
        {
            try
            {
                var session = _sessionService.CreateSession();

                var response = new InitializeResponse
                {
                    Id = request.Id,
                    Result = new InitializeResult
                    {
                        ProtocolVersion = "1.0",
                        Capabilities = new ServerCapabilities
                        {
                            Tools = new ToolCapabilities { ListChanged = false }
                        },
                        ServerInfo = new ServerInfo
                        {
                            Name = "MCP Health Check Server",
                            Version = "1.0.0"
                        }
                    }
                };

                _logger.LogInformation("Initialized session {SessionId}", session.SessionId);

                return Ok(new
                {
                    session_id = session.SessionId,
                    sse_stream_url = session.SseStreamUrl,
                    response = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initialization");
                return StatusCode(500, new { error = "Internal server error during initialization" });
            }
        }

        [HttpGet("tools")]
        public IActionResult ListTools([FromQuery] string session_id)
        {
            try
            {
                var session = _sessionService.GetSession(session_id);
                if (session == null)
                {
                    return NotFound(new { error = "Session not found" });
                }

                var tools = new List<Tool>
                {
                    new Tool
                    {
                        Name = "check_api_status",
                        Description = "Check the health/availability status of an API endpoint",
                        InputSchema = new ToolInputSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, ToolProperty>
                            {
                                ["url"] = new ToolProperty
                                {
                                    Type = "string",
                                    Description = "The URL to check (must be http:// or https://)"
                                }
                            },
                            Required = new List<string> { "url" }
                        }
                    }
                };

                var response = new ToolsListResponse
                {
                    Id = 1, // Mock ID for tools list request
                    Result = new ToolsListResult { Tools = tools }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing tools for session {SessionId}", session_id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}