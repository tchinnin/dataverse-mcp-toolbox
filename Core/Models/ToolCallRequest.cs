namespace DataverseMCPToolBox.Models;

/// <summary>
/// Request to execute an MCP tool
/// </summary>
public class ToolCallRequest
{
    /// <summary>
    /// Name of the tool to execute (kebab-case)
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Connection ID to use for tool execution
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// JSON string containing tool parameters (camelCase)
    /// Null or empty if tool has no parameters
    /// </summary>
    public string? ParametersJson { get; set; }
}
