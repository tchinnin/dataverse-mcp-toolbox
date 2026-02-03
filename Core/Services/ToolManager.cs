using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Helpers;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Manages MCP tool execution and queries
/// </summary>
public class ToolManager : IToolManager
{
    private const string ServiceName = "ToolManager";
    private readonly ToolRegistryService _registryService;
    private readonly ToolExecutionService _executionService;

    public ToolManager(
        ToolRegistryService registryService,
        ToolExecutionService executionService)
    {
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
    }

    /// <summary>
    /// Get list of all available tools
    /// </summary>
    /// <remarks>
    /// This method is async to match the RPC interface contract, even though the underlying
    /// operation is synchronous. This allows for future async implementations without breaking changes.
    /// </remarks>
    public Task<List<ToolInfo>> GetAllToolsAsync()
    {
        var tools = _registryService.GetAllTools();
        Logger.LogInfo(ServiceName, $"Listing {tools.Count} tools");
        return Task.FromResult(tools);
    }

    /// <summary>
    /// Execute a tool by name with the provided parameters
    /// </summary>
    public async Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request)
    {
        Logger.LogInfo(ServiceName, $"Executing tool: {request.ToolName}");
        
        var result = await _executionService.ExecuteToolAsync(request);
        
        if (result.IsSuccess)
        {
            Logger.LogSuccess(ServiceName, $"Tool '{request.ToolName}' executed successfully");
        }
        else
        {
            Logger.LogError(ServiceName, $"Tool '{request.ToolName}' execution failed: {result.Error?.Message}");
        }

        return result;
    }

    /// <summary>
    /// Get count of registered tools
    /// </summary>
    public int GetToolCount()
    {
        return _registryService.GetToolCount();
    }
}
