using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Services;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Implémentation du service RPC qui expose les fonctionnalités Dataverse
/// </summary>
public class DataverseMCPToolBoxRpcService : IDataverseMCPToolBoxRpcService
{
    private readonly DataverseConnectionService _connectionService;
    private PluginPackageService? _pluginPackageService;
    private PluginLoaderService? _pluginLoaderService;
    private ToolRegistryService? _toolRegistryService;
    private ToolExecutionService? _toolExecutionService;
    private string? _pluginDirectory;

    public DataverseMCPToolBoxRpcService()
    {
        _connectionService = new DataverseConnectionService();
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

    public Task CloseConnectionAsync(string connectionId)
    {
        _connectionService.CloseConnection(connectionId);
        return Task.CompletedTask;
    }

    public Task CloseAllConnectionsAsync()
    {
        _connectionService.CloseAllConnections();
        return Task.CompletedTask;
    }

    // Plugin management methods
    public async Task SetPluginDirectoryAsync(string directoryPath)
    {
        Console.Error.WriteLine($"Setting plugin directory: {directoryPath}");
        
        _pluginDirectory = directoryPath;
        _pluginPackageService = new PluginPackageService(directoryPath);
        _pluginLoaderService = new PluginLoaderService(directoryPath);
        _toolRegistryService = new ToolRegistryService();
        _toolExecutionService = new ToolExecutionService(_toolRegistryService, _connectionService);

        // Load existing plugins
        await ReloadPluginsAsync();
    }

    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request)
    {
        if (_pluginPackageService == null)
        {
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = "Plugin directory not set. Call SetPluginDirectoryAsync first."
            };
        }

        Console.Error.WriteLine($"Installing plugin: {request.PackageId}");

        var (success, errorMessage, installedPath) = await _pluginPackageService.InstallPluginAsync(
            request.PackageId, 
            request.Version);

        if (!success)
        {
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        // Reload plugins to include the newly installed one
        await ReloadPluginsAsync();

        // Find the installed plugin info
        var plugins = _toolRegistryService?.GetAllPlugins() ?? new List<PluginInfo>();
        var installedPlugin = plugins.FirstOrDefault(p => 
            p.Name.Contains(request.PackageId, StringComparison.OrdinalIgnoreCase));

        return new PluginInstallResult
        {
            Success = true,
            PluginInfo = installedPlugin
        };
    }

    public Task<bool> UninstallPluginAsync(string packageId)
    {
        if (_pluginPackageService == null)
        {
            Console.Error.WriteLine("Plugin directory not set");
            return Task.FromResult(false);
        }

        Console.Error.WriteLine($"Uninstalling plugin: {packageId}");
        
        var result = _pluginPackageService.UninstallPlugin(packageId);
        
        if (result)
        {
            // Reload plugins to update registry
            _ = ReloadPluginsAsync();
        }

        return Task.FromResult(result);
    }

    public async Task ReloadPluginsAsync()
    {
        if (_pluginLoaderService == null || _toolRegistryService == null)
        {
            Console.Error.WriteLine("Plugin services not initialized");
            return;
        }

        Console.Error.WriteLine("Reloading plugins...");

        // Load all plugins
        var plugins = await _pluginLoaderService.LoadPluginsAsync();

        // Register tools
        _toolRegistryService.RegisterPlugins(plugins);

        Console.Error.WriteLine($"Reloaded {plugins.Count} plugins with {_toolRegistryService.GetToolCount()} tools");
    }

    public Task<List<PluginInfo>> ListPluginsAsync()
    {
        if (_toolRegistryService == null)
        {
            Console.Error.WriteLine("Plugin services not initialized");
            return Task.FromResult(new List<PluginInfo>());
        }

        var plugins = _toolRegistryService.GetAllPlugins();
        Console.Error.WriteLine($"Listing {plugins.Count} plugins");
        
        return Task.FromResult(plugins);
    }

    // Tool management methods
    public Task<List<ToolInfo>> ListToolsAsync()
    {
        if (_toolRegistryService == null)
        {
            Console.Error.WriteLine("Plugin services not initialized");
            return Task.FromResult(new List<ToolInfo>());
        }

        var tools = _toolRegistryService.GetAllTools();
        Console.Error.WriteLine($"Listing {tools.Count} tools");
        
        return Task.FromResult(tools);
    }

    public async Task<ToolCallResult> CallToolAsync(ToolCallRequest request)
    {
        if (_toolExecutionService == null)
        {
            return new ToolCallResult
            {
                IsSuccess = false,
                Error = new ToolErrorInfo
                {
                    Code = "SERVICE_NOT_INITIALIZED",
                    Message = "Plugin services not initialized. Call SetPluginDirectoryAsync first."
                }
            };
        }

        return await _toolExecutionService.ExecuteToolAsync(request);
    }
}
