using System.Text.Json.Serialization;

namespace McpProtocol.Models
{
    public abstract class McpMessage
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
    }

    public class InitializeRequest : McpMessage
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = "initialize";

        [JsonPropertyName("params")]
        public InitializeParams Params { get; set; } = new();

        [JsonPropertyName("id")]
        public int? Id { get; set; }
    }

    public class InitializeParams
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        [JsonPropertyName("capabilities")]
        public Dictionary<string, object> Capabilities { get; set; } = new();

        [JsonPropertyName("clientInfo")]
        public ClientInfo? ClientInfo { get; set; }
    }

    public class ClientInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class InitializeResponse : McpMessage
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("result")]
        public InitializeResult Result { get; set; } = new();
    }

    public class InitializeResult
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "1.0";

        [JsonPropertyName("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new();

        [JsonPropertyName("serverInfo")]
        public ServerInfo ServerInfo { get; set; } = new();
    }

    public class ServerCapabilities
    {
        [JsonPropertyName("tools")]
        public ToolCapabilities Tools { get; set; } = new();
    }

    public class ToolCapabilities
    {
        [JsonPropertyName("listChanged")]
        public bool ListChanged { get; set; } = false;
    }

    public class ServerInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "MCP Health Check Server";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";
    }

    public class Tool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("inputSchema")]
        public ToolInputSchema InputSchema { get; set; } = new();
    }

    public class ToolInputSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, ToolProperty> Properties { get; set; } = new();

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new();
    }

    public class ToolProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class ToolsListResponse : McpMessage
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("result")]
        public ToolsListResult Result { get; set; } = new();
    }

    public class ToolsListResult
    {
        [JsonPropertyName("tools")]
        public List<Tool> Tools { get; set; } = new();
    }

    public class ToolCallRequest : McpMessage
    {
        [JsonPropertyName("method")]
        public string Method { get; set; } = "tools/call";

        [JsonPropertyName("params")]
        public ToolCallParams Params { get; set; } = new();

        [JsonPropertyName("id")]
        public int? Id { get; set; }
    }

    public class ToolCallParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    public class ToolCallResponse : McpMessage
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("result")]
        public ToolCallResult Result { get; set; } = new();
    }

    public class ToolCallResult
    {
        [JsonPropertyName("content")]
        public List<ToolContent> Content { get; set; } = new();
    }

    public class ToolContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class SseEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public object Data { get; set; } = new();
    }
}