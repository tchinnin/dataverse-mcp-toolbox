using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Models;
using DataverseMCPToolBox.Models;
using Microsoft.PowerPlatform.Dataverse.Client;
using Newtonsoft.Json;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for executing MCP tools with connection context
/// </summary>
public class ToolExecutionService
{
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
        var (isValid, error) = InputValidator.ValidateToolCallRequest(request);
        if (!isValid)
        {
            Console.Error.WriteLine($"[ToolExecutionService] Validation failed: {error}");
            return new ToolCallResult
            {
                IsSuccess = false,
                Error = new ToolErrorInfo
                {
                    Code = "VALIDATION_ERROR",
                    Message = error!
                }
            };
        }

        try
        {
            Console.Error.WriteLine($"Executing tool: {request.ToolName} with connection: {request.ConnectionId}");

            // Lookup tool
            var (plugin, tool) = _toolRegistry.GetTool(request.ToolName);
            if (tool == null || plugin == null)
            {
                return new ToolCallResult
                {
                    IsSuccess = false,
                    Error = new ToolErrorInfo
                    {
                        Code = "TOOL_NOT_FOUND",
                        Message = $"Tool '{request.ToolName}' not found"
                    }
                };
            }

            // Get connection
            var serviceClient = _connectionService.GetConnection(request.ConnectionId);
            if (serviceClient == null)
            {
                return new ToolCallResult
                {
                    IsSuccess = false,
                    Error = new ToolErrorInfo
                    {
                        Code = "CONNECTION_NOT_FOUND",
                        Message = $"Connection '{request.ConnectionId}' not found"
                    }
                };
            }

            // Verify connection is ready
            if (!serviceClient.IsReady)
            {
                return new ToolCallResult
                {
                    IsSuccess = false,
                    Error = new ToolErrorInfo
                    {
                        Code = "CONNECTION_NOT_READY",
                        Message = $"Connection '{request.ConnectionId}' is not ready or has been disconnected"
                    }
                };
            }

            // Create connection context
            var context = new ConnectionContext(
                serviceClient,
                request.ConnectionId,
                serviceClient.ConnectedOrgUriActual?.ToString() ?? string.Empty,
                CancellationToken.None
            );

            // Execute tool
            var executionResult = await tool.ExecuteAsync(request.ParametersJson ?? "{}", context);

            // Convert to ToolCallResult
            if (executionResult.IsSuccess)
            {
                Console.Error.WriteLine($"Tool executed successfully: {request.ToolName}");
                
                return new ToolCallResult
                {
                    IsSuccess = true,
                    Content = executionResult.Content
                };
            }
            else
            {
                Console.Error.WriteLine($"Tool execution failed: {executionResult.Error?.Message}");
                
                return new ToolCallResult
                {
                    IsSuccess = false,
                    Error = new ToolErrorInfo
                    {
                        Code = executionResult.Error?.Code ?? "EXECUTION_ERROR",
                        Message = executionResult.Error?.Message ?? "Unknown error",
                        Details = executionResult.Error?.Details
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing tool: {ex}");
            
            return new ToolCallResult
            {
                IsSuccess = false,
                Error = new ToolErrorInfo
                {
                    Code = "EXECUTION_EXCEPTION",
                    Message = ex.Message,
                    Details = ex.StackTrace
                }
            };
        }
    }
}
