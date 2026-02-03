using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Models;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Helpers;
using Microsoft.PowerPlatform.Dataverse.Client;
using Newtonsoft.Json;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for executing MCP tools with connection context
/// </summary>
public class ToolExecutionService
{
    private const string ServiceName = "ToolExecutionService";
    private readonly ToolRegistryService _toolRegistry;
    private readonly DataverseConnectionService _connectionService;

    public ToolExecutionService(ToolRegistryService toolRegistry, DataverseConnectionService connectionService)
    {
        _toolRegistry = toolRegistry;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Execute a tool by name with the specified connection
    /// </summary>
    public async Task<ToolCallResult> ExecuteToolAsync(ToolCallRequest request)
    {
        // Validate request
        var validation = InputValidator.ValidateToolCallRequest(request);
        if (!validation.IsValid)
        {
            Logger.LogError(ServiceName, $"Validation failed: {validation.Error}");
            return ToolCallResult.ValidationError(validation.Error!);
        }

        try
        {
            Logger.LogInfo(ServiceName, $"Executing tool: {request.ToolName} with connection: {request.ConnectionId}");

            // Lookup tool
            var (plugin, tool) = _toolRegistry.GetTool(request.ToolName);
            if (tool == null || plugin == null)
            {
                return ToolCallResult.ToolNotFound(request.ToolName);
            }

            // Get connection
            var serviceClient = _connectionService.GetConnection(request.ConnectionId);
            
            // Validate connection
            var connectionError = ConnectionHelper.ValidateConnection(serviceClient, request.ConnectionId);
            if (connectionError != null)
            {
                return connectionError;
            }

            // Create connection context
            var context = new ConnectionContext(
                serviceClient!,
                request.ConnectionId,
                serviceClient.ConnectedOrgUriActual?.ToString() ?? string.Empty,
                CancellationToken.None
            );

            // Execute tool
            var executionResult = await tool.ExecuteAsync(request.ParametersJson ?? "{}", context);

            // Convert to ToolCallResult
            if (executionResult.IsSuccess)
            {
                Logger.LogSuccess(ServiceName, $"Tool executed: {request.ToolName}");
                return ToolCallResult.Success(executionResult.Content);
            }
            else
            {
                Logger.LogError(ServiceName, $"Tool execution failed: {executionResult.Error?.Message}");
                return ToolCallResult.Failure(
                    executionResult.Error?.Code ?? ErrorCodes.ExecutionError,
                    executionResult.Error?.Message ?? "Unknown error",
                    executionResult.Error?.Details
                );
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Error executing tool");
            return ToolCallResult.ExecutionException(ex);
        }
    }
}
