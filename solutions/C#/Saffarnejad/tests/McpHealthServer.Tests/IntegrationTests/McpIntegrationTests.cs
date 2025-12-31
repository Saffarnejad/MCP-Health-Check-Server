using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IntegrationTests
{
    public class McpIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public McpIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Initialize_ShouldCreateSession()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = new
            {
                jsonrpc = "2.0",
                method = "initialize",
                @params = new
                {
                    protocolVersion = "1.0",
                    capabilities = new { },
                    clientInfo = new { name = "test-client", version = "1.0" }
                },
                id = 1
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            // Act
            var response = await client.PostAsync("/api/mcp/initialize", content);

            // Assert
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

            Assert.True(responseObj.TryGetProperty("session_id", out var sessionId));
            Assert.True(responseObj.TryGetProperty("sse_stream_url", out var sseUrl));
            Assert.NotEmpty(sessionId.GetString());
            Assert.Contains("sse", sseUrl.GetString());
        }

        [Fact]
        public async Task ListTools_WithValidSession_ShouldReturnTools()
        {
            // Arrange
            var client = _factory.CreateClient();

            // First create a session
            var initRequest = new { /* initialize request */ };
            var initContent = new StringContent(
                JsonSerializer.Serialize(initRequest),
                Encoding.UTF8,
                "application/json");

            var initResponse = await client.PostAsync("/api/mcp/initialize", initContent);
            var initJson = await initResponse.Content.ReadAsStringAsync();
            var initObj = JsonSerializer.Deserialize<JsonElement>(initJson);
            var sessionId = initObj.GetProperty("session_id").GetString();

            // Act
            var toolsResponse = await client.GetAsync($"/api/mcp/tools?session_id={sessionId}");

            // Assert
            toolsResponse.EnsureSuccessStatusCode();

            var toolsJson = await toolsResponse.Content.ReadAsStringAsync();
            var toolsObj = JsonSerializer.Deserialize<JsonElement>(toolsJson);

            Assert.True(toolsObj.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            Assert.True(tools.GetArrayLength() > 0);
        }

        [Fact]
        public async Task SseStream_ShouldEstablishConnection()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Create a session
            var initRequest = new { /* initialize request */ };
            var initContent = new StringContent(
                JsonSerializer.Serialize(initRequest),
                Encoding.UTF8,
                "application/json");

            var initResponse = await client.PostAsync("/api/mcp/initialize", initContent);
            var initJson = await initResponse.Content.ReadAsStringAsync();
            var initObj = JsonSerializer.Deserialize<JsonElement>(initJson);
            var sessionId = initObj.GetProperty("session_id").GetString();

            // Act & Assert
            var sseUrl = $"/mcp/sse/{sessionId}";
            var request = new HttpRequestMessage(HttpMethod.Get, sseUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        }
    }
}