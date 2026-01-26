namespace DataverseMCPToolBox.Models;

/// <summary>
/// Information about an MCP tool exposed by a plugin
/// </summary>
public class ToolInfo
{
    /// <summary>
    /// Unique name of the tool (kebab-case)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the tool does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema object describing the input parameters
    /// </summary>
    public object? InputSchema { get; set; }
}
