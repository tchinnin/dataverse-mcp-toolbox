using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Interface for managing and executing MCP tools
/// </summary>
public interface IToolManager
{
    /// <summary>
    /// Get list of all available tools
    /// </summary>
    Task<List<ToolInfo>> GetAllToolsAsync();

    /// <summary>
    /// Execute a tool by name with the provided parameters
    /// </summary>
    Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request);

    /// <summary>
    /// Get count of registered tools
    /// </summary>
    int GetToolCount();
}
