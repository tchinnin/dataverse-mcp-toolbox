namespace DataverseMCPToolBox.Extensibility.Abstractions;

/// <summary>
/// Defines the contract for plugins that provide MCP tools.
/// Plugins implementing this interface will have their tools discovered and exposed by the MCP server.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Gets the collection of MCP tools provided by this plugin.
    /// Called after plugin initialization to register tools with the MCP server.
    /// </summary>
    /// <returns>Enumerable collection of MCP tools</returns>
    IEnumerable<IMcpTool> GetTools();
}
