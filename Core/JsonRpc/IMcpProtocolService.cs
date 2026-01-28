using DataverseMCPToolBox.Models.Mcp;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Service implementing the Model Context Protocol specification
/// Handles tool discovery and invocation for AI assistants via MCP protocol
/// </summary>
public interface IMcpProtocolService
{
    /// <summary>
    /// Initialize the MCP connection
    /// Called when MCP client connects to the server
    /// Parameters sent as individual arguments by VS Code MCP
    /// </summary>
    Task<InitializeResult> InitializeAsync(string protocolVersion, ClientInfo? clientInfo = null, ClientCapabilities? capabilities = null);

    /// <summary>
    /// List all available tools
    /// Called by MCP client to discover available tools
    /// </summary>
    Task<ListToolsResult> ToolsListAsync(string? cursor = null);

    /// <summary>
    /// Execute a tool by name with provided arguments
    /// Called by MCP client to invoke a specific tool
    /// Parameters sent as individual arguments by VS Code MCP
    /// </summary>
    Task<CallToolResult> ToolsCallAsync(string name, Dictionary<string, object>? arguments = null, object? _meta = null);
}
