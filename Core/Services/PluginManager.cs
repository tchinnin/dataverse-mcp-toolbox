using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Helpers;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Manages plugin lifecycle including installation, loading, and unloading
/// </summary>
public class PluginManager : IPluginManager
{
    private const string ServiceName = "PluginManager";
    private PluginPackageService? _packageService;
    private PluginLoaderService? _loaderService;
    private ToolRegistryService? _registryService;
    private string? _pluginDirectory;

    /// <summary>
    /// Initialize plugin management with the specified directory
    /// </summary>
    public Task InitializeAsync(string pluginDirectory)
    {
        // Validate directory path
        var validation = InputValidator.ValidateDirectoryPath(pluginDirectory);
        if (!validation.IsValid)
        {
            Logger.LogError(ServiceName, $"Validation failed: {validation.Error}");
            throw new ArgumentException(validation.Error, nameof(pluginDirectory));
        }

        Logger.LogInfo(ServiceName, $"Initializing with directory: {pluginDirectory}");
        
        _pluginDirectory = pluginDirectory;
        _packageService = new PluginPackageService(pluginDirectory);
        _loaderService = new PluginLoaderService(pluginDirectory);
        _registryService = new ToolRegistryService();

        Logger.LogSuccess(ServiceName, "Initialized successfully");
        
        // Return completed task - method is async for future extensibility
        return Task.CompletedTask;
    }

    /// <summary>
    /// Install a plugin from NuGet package
    /// </summary>
    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request)
    {
        EnsureInitialized();

        // Validate request
        var validation = InputValidator.ValidatePluginInstallRequest(request);
        if (!validation.IsValid)
        {
            Logger.LogError(ServiceName, $"Validation failed: {validation.Error}");
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = validation.Error
            };
        }

        Logger.LogInfo(ServiceName, $"Installing plugin: {request.PackageId}");

        var (success, errorMessage, installedPath) = await _packageService!.InstallPluginAsync(
            request.PackageId, 
            request.Version);

        if (!success)
        {
            Logger.LogError(ServiceName, $"Installation failed: {errorMessage}");
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        // Reload plugins to include the newly installed one
        await ReloadPluginsAsync();

        // Find the installed plugin info
        var plugins = await GetAllPluginsAsync();
        var installedPlugin = plugins.FirstOrDefault(p => 
            p.Name.Contains(request.PackageId, StringComparison.OrdinalIgnoreCase));

        Logger.LogSuccess(ServiceName, $"Plugin '{request.PackageId}' installed successfully");

        return new PluginInstallResult
        {
            Success = true,
            PluginInfo = installedPlugin
        };
    }

    /// <summary>
    /// Uninstall a plugin
    /// </summary>
    public async Task<bool> UninstallPluginAsync(string packageId)
    {
        EnsureInitialized();

        // Validate package ID
        var validation = InputValidator.ValidatePackageId(packageId);
        if (!validation.IsValid)
        {
            Logger.LogError(ServiceName, $"Validation failed: {validation.Error}");
            return false;
        }

        Logger.LogInfo(ServiceName, $"Uninstalling plugin: {packageId}");
        
        var result = _packageService!.UninstallPlugin(packageId);
        
        if (result)
        {
            Logger.LogSuccess(ServiceName, $"Plugin '{packageId}' uninstalled successfully");
            // Reload plugins to update registry
            await ReloadPluginsAsync();
        }
        else
        {
            Logger.LogError(ServiceName, $"Failed to uninstall plugin '{packageId}'");
        }

        return result;
    }

    /// <summary>
    /// Reload all plugins from the plugin directory
    /// </summary>
    public async Task ReloadPluginsAsync()
    {
        EnsureInitialized();

        Logger.LogInfo(ServiceName, "Reloading plugins...");

        // Load all plugins
        var plugins = await _loaderService!.LoadPluginsAsync();

        // Register tools
        _registryService!.RegisterPlugins(plugins);

        Logger.LogSuccess(ServiceName, $"Reloaded {plugins.Count} plugins with {_registryService.GetToolCount()} tools");
    }

    /// <summary>
    /// Get list of all loaded plugins with their information
    /// </summary>
    /// <remarks>
    /// This method is async to match the RPC interface contract.
    /// </remarks>
    public Task<List<PluginInfo>> GetAllPluginsAsync()
    {
        EnsureInitialized();

        var plugins = _registryService!.GetAllPlugins();
        return Task.FromResult(plugins);
    }

    /// <summary>
    /// Get the tool registry service (internal use)
    /// </summary>
    internal ToolRegistryService GetRegistryService()
    {
        EnsureInitialized();
        return _registryService!;
    }

    private void EnsureInitialized()
    {
        if (_packageService == null || _loaderService == null || _registryService == null)
        {
            throw new InvalidOperationException("PluginManager not initialized. Call InitializeAsync first.");
        }
    }
}
