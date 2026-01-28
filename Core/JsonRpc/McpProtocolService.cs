using System.Text.Json;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Models.Mcp;
using DataverseMCPToolBox.Services;
using StreamJsonRpc;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Implementation of the Model Context Protocol for VS Code integration
/// Handles tool discovery and execution via MCP protocol
/// Reads active connection from shared state for cross-instance support
/// </summary>
public class McpProtocolService : IMcpProtocolService
{
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

        Console.Error.WriteLine("[MCP Protocol Service] Initialized with connection state sharing");
    }

    /// <summary>
    /// Set the active connection for tool executions
    /// Called by management layer when active connection changes
    /// </summary>
    public void SetActiveConnection(string? connectionId)
    {
        _activeConnectionId = connectionId;
        Console.Error.WriteLine($"[MCP Protocol] Active connection set to: {connectionId ?? "(none)"}");
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
        
        Console.Error.WriteLine("[MCP Protocol] ========================================");
        Console.Error.WriteLine($"[MCP Protocol] Initialize called by: {clientName} v{clientVersion}");
        Console.Error.WriteLine($"[MCP Protocol] Protocol version: {protocolVersion}");
        Console.Error.WriteLine("[MCP Protocol] ========================================");

        return Task.FromResult(new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = new ServerInfo
            {
                Name = "dataverse-mcp-toolbox",
                Version = "0.1.0"
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
        Console.Error.WriteLine("[MCP Protocol] ========================================");
        Console.Error.WriteLine("[MCP Protocol] tools/list called - discovering tools...");
        Console.Error.WriteLine("[MCP Protocol] ========================================");

        var tools = _toolRegistry.GetAllTools();
        
        Console.Error.WriteLine($"[MCP Protocol] Found {tools.Count} tool(s):");
        foreach (var tool in tools)
        {
            Console.Error.WriteLine($"[MCP Protocol]   - {tool.Name}: {tool.Description}");
        }

        var mcpTools = tools.Select(t => new McpTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();

        Console.Error.WriteLine($"[MCP Protocol] Returning {mcpTools.Count} tool(s) to client");

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
        object? _meta = null) // VS Code MCP peut envoyer des métadonnées
    {
        Console.Error.WriteLine("[MCP Protocol] ========================================");
        Console.Error.WriteLine($"[MCP Protocol] tools/call invoked: {name}");
        
        if (arguments != null && arguments.Count > 0)
        {
            Console.Error.WriteLine($"[MCP Protocol] Arguments: {JsonSerializer.Serialize(arguments)}");
        }
        
        if (_meta != null)
        {
            Console.Error.WriteLine($"[MCP Protocol] Metadata: {JsonSerializer.Serialize(_meta)}");
        }
        
        Console.Error.WriteLine("[MCP Protocol] ========================================");

        try
        {
            // Check for active connection - first in-memory, then shared state
            string? activeConnectionId = _activeConnectionId;
            
            if (string.IsNullOrEmpty(activeConnectionId))
            {
                Console.Error.WriteLine("[MCP Protocol] No active connection in memory, checking shared state...");
                activeConnectionId = await _connectionStateService.GetActiveConnectionIdAsync();
                
                if (!string.IsNullOrEmpty(activeConnectionId))
                {
                    Console.Error.WriteLine($"[MCP Protocol] ✓ Found active connection in shared state: {activeConnectionId}");
                    _activeConnectionId = activeConnectionId; // Cache for next call
                }
            }
            
            if (string.IsNullOrEmpty(activeConnectionId))
            {
                Console.Error.WriteLine("[MCP Protocol] ✗ No active Dataverse connection in shared state");
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

            Console.Error.WriteLine($"[MCP Protocol] Using active connection: {activeConnectionId}");

            // Convert MCP arguments to JSON string for tool execution
            string? parametersJson = null;
            if (arguments != null && arguments.Count > 0)
            {
                parametersJson = JsonSerializer.Serialize(arguments, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            // Execute tool using existing infrastructure
            var toolRequest = new ToolCallRequest
            {
                ToolName = name,
                ConnectionId = activeConnectionId,
                ParametersJson = parametersJson
            };

            Console.Error.WriteLine($"[MCP Protocol] Executing tool via ToolExecutionService...");
            var result = await _toolExecutionService.ExecuteToolAsync(toolRequest);

            if (!result.IsSuccess)
            {
                Console.Error.WriteLine($"[MCP Protocol] ✗ Tool execution failed");
                Console.Error.WriteLine($"[MCP Protocol]   Error code: {result.Error?.Code}");
                Console.Error.WriteLine($"[MCP Protocol]   Error message: {result.Error?.Message}");
                
                return new CallToolResult
                {
                    IsError = true,
                    Content = new List<Content>
                    {
                        new TextContent
                        {
                            Text = $"Tool execution failed: {result.Error?.Message ?? "Unknown error"}\n\n" +
                                   (result.Error?.Details != null 
                                       ? $"Details: {JsonSerializer.Serialize(result.Error.Details, new JsonSerializerOptions { WriteIndented = true })}" 
                                       : "")
                        }
                    }
                };
            }

            // Format successful result
            var resultText = result.Content != null
                ? JsonSerializer.Serialize(result.Content, new JsonSerializerOptions { WriteIndented = true })
                : "Tool executed successfully with no output.";

            Console.Error.WriteLine($"[MCP Protocol] ✓ Tool executed successfully");
            Console.Error.WriteLine($"[MCP Protocol] Result preview: {(resultText.Length > 100 ? resultText.Substring(0, 100) + "..." : resultText)}");

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
            Console.Error.WriteLine($"[MCP Protocol] ✗ Exception during tool execution:");
            Console.Error.WriteLine($"[MCP Protocol]   Type: {ex.GetType().Name}");
            Console.Error.WriteLine($"[MCP Protocol]   Message: {ex.Message}");
            Console.Error.WriteLine($"[MCP Protocol]   Stack trace: {ex.StackTrace}");
            
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
