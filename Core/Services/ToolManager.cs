using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Manages MCP tool execution and queries
/// </summary>
public class ToolManager : IToolManager
{
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
    public Task<List<ToolInfo>> GetAllToolsAsync()
    {
        var tools = _registryService.GetAllTools();
        Console.Error.WriteLine($"[ToolManager] Listing {tools.Count} tools");
        return Task.FromResult(tools);
    }

    /// <summary>
    /// Execute a tool by name with the provided parameters
    /// </summary>
    public async Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request)
    {
        Console.Error.WriteLine($"[ToolManager] Executing tool: {request.ToolName}");
        
        var result = await _executionService.ExecuteToolAsync(request);
        
        if (result.IsSuccess)
        {
            Console.Error.WriteLine($"[ToolManager] ✓ Tool '{request.ToolName}' executed successfully");
        }
        else
        {
            Console.Error.WriteLine($"[ToolManager] ✗ Tool '{request.ToolName}' execution failed: {result.Error?.Message}");
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
