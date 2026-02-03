namespace DataverseMCPToolBox.Models.Mcp;

/// <summary>
/// MCP Initialize Request
/// </summary>
public class InitializeRequest
{
    public string ProtocolVersion { get; set; } = McpProtocolConstants.CurrentVersion;
    public ClientInfo ClientInfo { get; set; } = new();
    public ClientCapabilities? Capabilities { get; set; }
}

/// <summary>
/// MCP Initialize Result
/// </summary>
public class InitializeResult
{
    public string ProtocolVersion { get; set; } = McpProtocolConstants.CurrentVersion;
    public ServerInfo ServerInfo { get; set; } = new();
    public ServerCapabilities Capabilities { get; set; } = new();
}

/// <summary>
/// Information about the MCP client
/// </summary>
public class ClientInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Information about the MCP server
/// </summary>
public class ServerInfo
{
    public string Name { get; set; } = McpProtocolConstants.ServerName;
    public string Version { get; set; } = McpProtocolConstants.ServerVersion;
}

/// <summary>
/// Client capabilities
/// </summary>
public class ClientCapabilities
{
    public SamplingCapability? Sampling { get; set; }
}

/// <summary>
/// Server capabilities
/// </summary>
public class ServerCapabilities
{
    public ToolsCapability? Tools { get; set; }
    public PromptsCapability? Prompts { get; set; }
}

/// <summary>
/// Tools capability marker
/// </summary>
public class ToolsCapability
{
    // Empty object for basic tools support
}

/// <summary>
/// Prompts capability marker
/// </summary>
public class PromptsCapability
{
    // Empty object for prompts support (optional)
}

/// <summary>
/// Sampling capability marker
/// </summary>
public class SamplingCapability
{
    // Empty object
}

/// <summary>
/// MCP Tools List Request
/// </summary>
public class ListToolsRequest
{
    public string? Cursor { get; set; }
}

/// <summary>
/// MCP Tools List Result
/// </summary>
public class ListToolsResult
{
    public List<McpTool> Tools { get; set; } = new();
    public string? NextCursor { get; set; }
}

/// <summary>
/// MCP Tool definition
/// </summary>
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? InputSchema { get; set; } // JSON Schema object
}

/// <summary>
/// MCP Tool Call Request
/// </summary>
public class CallToolRequest
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object>? Arguments { get; set; }
}

/// <summary>
/// MCP Tool Call Result
/// </summary>
public class CallToolResult
{
    public List<Content> Content { get; set; } = new();
    public bool IsError { get; set; }
}

/// <summary>
/// Base class for MCP content
/// </summary>
public abstract class Content
{
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Text content
/// </summary>
public class TextContent : Content
{
    public TextContent() { Type = "text"; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Image content (base64 encoded)
/// </summary>
public class ImageContent : Content
{
    public ImageContent() { Type = "image"; }
    public string Data { get; set; } = string.Empty; // base64
    public string MimeType { get; set; } = string.Empty;
}

/// <summary>
/// MCP Error
/// </summary>
public class McpError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
