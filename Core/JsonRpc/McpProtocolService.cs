using System.Text.Json;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Models.Mcp;
using DataverseMCPToolBox.Services;
using DataverseMCPToolBox.Helpers;
using StreamJsonRpc;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Implementation of the Model Context Protocol for VS Code integration
/// Handles tool discovery and execution via MCP protocol
/// Reads active connection from shared state for cross-instance support
/// </summary>
public class McpProtocolService : IMcpProtocolService
{
    private const string ServiceName = "MCP Protocol";
    private readonly ToolRegistryService _toolRegistry;
    private readonly DataverseConnectionService _connectionService;
    private readonly ToolExecutionService _toolExecutionService;
    private readonly ConnectionStateService _connectionStateService;
    private string? _activeConnectionId;

    public McpProtocolService(
        ToolRegistryService toolRegistry,
        DataverseConnectionService connectionService,
        ToolExecutionService toolExecutionService,
        ConnectionStateService connectionStateService)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _toolExecutionService = toolExecutionService ?? throw new ArgumentNullException(nameof(toolExecutionService));
        _connectionStateService = connectionStateService ?? throw new ArgumentNullException(nameof(connectionStateService));

        Logger.LogInfo(ServiceName, "Initialized with connection state sharing");
    }

    /// <summary>
    /// Set the active connection for tool executions
    /// Called by management layer when active connection changes
    /// </summary>
    public void SetActiveConnection(string? connectionId)
    {
        _activeConnectionId = connectionId;
        Logger.LogInfo(ServiceName, $"Active connection set to: {connectionId ?? "(none)"}");
    }

    /// <summary>
    /// Initialize the MCP connection
    /// </summary>
    [JsonRpcMethod("initialize")]
    public Task<InitializeResult> InitializeAsync(
        string protocolVersion,
        ClientInfo? clientInfo = null,
        ClientCapabilities? capabilities = null)
    {
        var clientName = clientInfo?.Name ?? "unknown client";
        var clientVersion = clientInfo?.Version ?? "unknown version";
        
        Logger.LogSeparator(ServiceName);
        Logger.LogInfo(ServiceName, $"Initialize called by: {clientName} v{clientVersion}");
        Logger.LogInfo(ServiceName, $"Protocol version: {protocolVersion}");
        Logger.LogSeparator(ServiceName);

        return Task.FromResult(new InitializeResult
        {
            ProtocolVersion = McpProtocolConstants.CurrentVersion,
            ServerInfo = new ServerInfo
            {
                Name = McpProtocolConstants.ServerName,
                Version = McpProtocolConstants.ServerVersion
            },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability()
            }
        });
    }

    /// <summary>
    /// List all available tools
    /// </summary>
    [JsonRpcMethod("tools/list")]
    public Task<ListToolsResult> ToolsListAsync(string? cursor = null)
    {
        Logger.LogSeparator(ServiceName);
        Logger.LogInfo(ServiceName, "tools/list called - discovering tools...");
        Logger.LogSeparator(ServiceName);

        var tools = _toolRegistry.GetAllTools();
        
        Logger.LogInfo(ServiceName, $"Found {tools.Count} tool(s):");
        foreach (var tool in tools)
        {
            Logger.LogInfo(ServiceName, $"  - {tool.Name}: {tool.Description}");
        }

        var mcpTools = tools.Select(t => new McpTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();

        Logger.LogInfo(ServiceName, $"Returning {mcpTools.Count} tool(s) to client");

        return Task.FromResult(new ListToolsResult
        {
            Tools = mcpTools
        });
    }

    /// <summary>
    /// Execute a tool by name
    /// </summary>
    [JsonRpcMethod("tools/call")]
    public async Task<CallToolResult> ToolsCallAsync(
        string name, 
        Dictionary<string, object>? arguments = null,
        object? _meta = null)
    {
        Logger.LogSeparator(ServiceName);
        Logger.LogInfo(ServiceName, $"tools/call invoked: {name}");
        
        if (arguments != null && arguments.Count > 0)
        {
            Logger.LogInfo(ServiceName, $"Arguments: {JsonSerializer.Serialize(arguments)}");
        }
        
        if (_meta != null)
        {
            Logger.LogInfo(ServiceName, $"Metadata: {JsonSerializer.Serialize(_meta)}");
        }
        
        Logger.LogSeparator(ServiceName);

        try
        {
            // Check for active connection - first in-memory, then shared state
            string? activeConnectionId = _activeConnectionId;
            
            if (string.IsNullOrEmpty(activeConnectionId))
            {
                Logger.LogInfo(ServiceName, "No active connection in memory, checking shared state...");
                activeConnectionId = await _connectionStateService.GetActiveConnectionIdAsync();
                
                if (!string.IsNullOrEmpty(activeConnectionId))
                {
                    Logger.LogSuccess(ServiceName, $"Found active connection in shared state: {activeConnectionId}");
                    _activeConnectionId = activeConnectionId; // Cache for next call
                }
            }
            
            if (string.IsNullOrEmpty(activeConnectionId))
            {
                Logger.LogError(ServiceName, "No active Dataverse connection in shared state");
                return new CallToolResult
                {
                    IsError = true,
                    Content = new List<Content>
                    {
                        new TextContent
                        {
                            Text = "No active Dataverse connection. Please activate a connection in the Dataverse MCP ToolBox extension first."
                        }
                    }
                };
            }

            Logger.LogInfo(ServiceName, $"Using active connection: {activeConnectionId}");

            // Convert MCP arguments to JSON string for tool execution
            string? parametersJson = null;
            if (arguments != null && arguments.Count > 0)
            {
                parametersJson = JsonSerializer.Serialize(arguments, JsonHelper.CamelCaseOptions);
            }

            // Execute tool using existing infrastructure
            var toolRequest = new ToolCallRequest
            {
                ToolName = name,
                ConnectionId = activeConnectionId,
                ParametersJson = parametersJson
            };

            Logger.LogInfo(ServiceName, "Executing tool via ToolExecutionService...");
            var result = await _toolExecutionService.ExecuteToolAsync(toolRequest);

            if (!result.IsSuccess)
            {
                Logger.LogError(ServiceName, "Tool execution failed");
                Logger.LogInfo(ServiceName, $"  Error code: {result.Error?.Code}");
                Logger.LogInfo(ServiceName, $"  Error message: {result.Error?.Message}");
                
                return new CallToolResult
                {
                    IsError = true,
                    Content = new List<Content>
                    {
                        new TextContent
                        {
                            Text = $"Tool execution failed: {result.Error?.Message ?? "Unknown error"}\n\n" +
                                   (result.Error?.Details != null 
                                       ? $"Details: {JsonSerializer.Serialize(result.Error.Details, JsonHelper.IndentedCamelCaseOptions)}" 
                                       : "")
                        }
                    }
                };
            }

            // Format successful result
            var resultText = result.Content != null
                ? JsonSerializer.Serialize(result.Content, JsonHelper.IndentedCamelCaseOptions)
                : "Tool executed successfully with no output.";

            Logger.LogSuccess(ServiceName, "Tool executed successfully");
            Logger.LogInfo(ServiceName, $"Result preview: {(resultText.Length > 100 ? resultText.Substring(0, 100) + "..." : resultText)}");

            return new CallToolResult
            {
                Content = new List<Content>
                {
                    new TextContent { Text = resultText }
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Exception during tool execution");
            
            return new CallToolResult
            {
                IsError = true,
                Content = new List<Content>
                {
                    new TextContent
                    {
                        Text = $"Error executing tool: {ex.Message}\n\n{ex.GetType().Name}"
                    }
                }
            };
        }
    }
}
