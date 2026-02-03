using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Services;
using StreamJsonRpc;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Implémentation du service RPC qui expose les fonctionnalités Dataverse
/// </summary>
public class DataverseMCPToolBoxRpcService : IDataverseMCPToolBoxRpcService
{
    private readonly DataverseConnectionService _connectionService;
    private readonly ConnectionStateService _connectionStateService;
    private IPluginManager _pluginManager;
    private IToolManager _toolManager;
    private McpProtocolService _mcpProtocolService;
    private readonly ToolRegistryService _toolRegistryService;
    private readonly ToolExecutionService _toolExecutionService;
    private string? _activeConnectionId;
    private readonly string _pluginDirectory;

    /// <summary>
    /// Get the MCP Protocol service for registration
    /// </summary>
    public McpProtocolService McpProtocolService => _mcpProtocolService;

    public DataverseMCPToolBoxRpcService(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
        
        // Initialize connection state service (now used only for optional persistence between restarts)
        _connectionStateService = new ConnectionStateService(pluginDirectory);
        _connectionService = new DataverseConnectionService(_connectionStateService);
        
        // Initialize plugin manager immediately
        Console.Error.WriteLine($"[Management RPC] Initializing with plugin directory: {pluginDirectory}");
        _pluginManager = new PluginManager();
        // Note: Blocking is acceptable here during server startup (not in UI context)
#pragma warning disable VSTHRD002
        _pluginManager.InitializeAsync(pluginDirectory).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

        // Get the registry service from plugin manager
        _toolRegistryService = ((PluginManager)_pluginManager).GetRegistryService();
        _toolExecutionService = new ToolExecutionService(_toolRegistryService, _connectionService);
        
        // Initialize tool manager
        _toolManager = new ToolManager(_toolRegistryService, _toolExecutionService);

        // Create MCP protocol service immediately (will be registered when JsonRpc connection is set)
        // UNIFIED INSTANCE: Both Extension and Copilot share the same in-memory state
        _mcpProtocolService = new McpProtocolService(
            _toolRegistryService,
            _connectionService,
            _toolExecutionService,
            _connectionStateService
        );
        Console.Error.WriteLine("[Management RPC] ✓ MCP Protocol service created (unified instance - shared in-memory state)");

        // Load existing plugins
#pragma warning disable VSTHRD002
        _pluginManager.ReloadPluginsAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    // Connection management methods
    public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
    {
        return await _connectionService.CreateConnectionAsync(request);
    }

    public Task<bool> TestConnectionAsync(string connectionId)
    {
        var result = _connectionService.TestConnection(connectionId);
        return Task.FromResult(result);
    }

    public async Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId)
    {
        return await _connectionService.GetOrganizationDetailsAsync(connectionId);
    }

    public async Task<WhoAmIResult> GetWhoAmIAsync(string connectionId)
    {
        return await _connectionService.GetWhoAmIAsync(connectionId);
    }

    public async Task CloseConnectionAsync(string connectionId)
    {
        await _connectionService.CloseConnectionAsync(connectionId);
    }

    public async Task CloseAllConnectionsAsync()
    {
        await _connectionService.CloseAllConnectionsAsync();
    }

    /// <summary>
    /// Set the active connection for MCP tool executions
    /// With unified instance, state is shared in-memory (no file persistence needed)
    /// Optional: persists to file for recovery after restart
    /// </summary>
    public async Task SetActiveConnectionAsync(string connectionId)
    {
        _activeConnectionId = connectionId;
        Console.Error.WriteLine($"[Management RPC] Active connection set to: {connectionId}");
        
        // Optional: Persist active connection for recovery after restart
        await _connectionStateService.SetActiveConnectionAsync(connectionId);
        Console.Error.WriteLine($"[Management RPC] Active connection persisted (optional - for restart recovery)");
        
        // Notify MCP protocol service of active connection change (in-memory, same instance)
        _mcpProtocolService.SetActiveConnection(connectionId);
        Console.Error.WriteLine($"[Management RPC] Active connection shared with MCP protocol service (in-memory)");
    }

    // Plugin management methods
    public async Task SetPluginDirectoryAsync(string directoryPath)
    {
        Console.Error.WriteLine($"[Management RPC] SetPluginDirectoryAsync called with: {directoryPath}");
        Console.Error.WriteLine($"[Management RPC] Note: Plugin directory was already set to: {_pluginDirectory} at startup");
        Console.Error.WriteLine($"[Management RPC] This method is kept for backward compatibility but is now a no-op");
        
        // For backward compatibility, just reload plugins if directory matches
        if (directoryPath == _pluginDirectory)
        {
            await _pluginManager.ReloadPluginsAsync();
        }
        else
        {
            Console.Error.WriteLine($"[Management RPC] WARNING: Requested directory '{directoryPath}' differs from initialized directory '{_pluginDirectory}'");
            Console.Error.WriteLine($"[Management RPC] To change plugin directory, restart the server with DATAVERSE_MCP_PLUGIN_DIR environment variable");
        }
    }

    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request)
    {
        return await _pluginManager.InstallPluginAsync(request);
    }

    public Task<bool> UninstallPluginAsync(string packageId)
    {
        return _pluginManager.UninstallPluginAsync(packageId);
    }

    public Task ReloadPluginsAsync()
    {
        return _pluginManager.ReloadPluginsAsync();
    }

    public Task<List<PluginInfo>> ListPluginsAsync()
    {
        return _pluginManager.GetAllPluginsAsync();
    }

    // Tool management methods
    public Task<List<ToolInfo>> ListToolsAsync()
    {
        return _toolManager.GetAllToolsAsync();
    }

    public Task<ToolCallResult> CallToolAsync(ToolCallRequest request)
    {
        return _toolManager.ExecuteToolAsync(request);
    }

    // Server management methods
    public Task<ServerVersionInfo> GetServerVersionAsync()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "unknown";
        // Use AppContext.BaseDirectory for single-file published apps (Assembly.Location is empty)
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
        var assemblyPath = string.IsNullOrEmpty(assembly.Location) 
            ? Path.Combine(AppContext.BaseDirectory, "DataverseMCPToolBox.dll")
            : assembly.Location;
#pragma warning restore IL3000
        var buildDate = System.IO.File.GetLastWriteTimeUtc(assemblyPath);
        var platform = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

        var versionInfo = new ServerVersionInfo
        {
            Version = version,
            BuildDate = buildDate,
            Platform = platform
        };

        Console.Error.WriteLine($"[Management RPC] GetServerVersion: {version} ({platform})");
        return Task.FromResult(versionInfo);
    }

    public async Task ShutdownServerAsync()
    {
        Console.Error.WriteLine("[Management RPC] Shutdown requested - closing all connections...");
        
        // Close all active connections gracefully
        await _connectionService.CloseAllConnectionsAsync();
        
        Console.Error.WriteLine("[Management RPC] All connections closed - initiating server shutdown");
        
        // Trigger application exit after a brief delay to allow response to be sent
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // Give time for RPC response to be sent
            Console.Error.WriteLine("[Management RPC] Exiting server process...");
            Environment.Exit(0);
        });
    }
}
