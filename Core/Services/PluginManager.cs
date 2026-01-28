using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Manages plugin lifecycle including installation, loading, and unloading
/// </summary>
public class PluginManager : IPluginManager
{
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
        var (isValid, error) = InputValidator.ValidateDirectoryPath(pluginDirectory);
        if (!isValid)
        {
            Console.Error.WriteLine($"[PluginManager] Validation failed: {error}");
            throw new ArgumentException(error, nameof(pluginDirectory));
        }

        Console.Error.WriteLine($"[PluginManager] Initializing with directory: {pluginDirectory}");
        
        _pluginDirectory = pluginDirectory;
        _packageService = new PluginPackageService(pluginDirectory);
        _loaderService = new PluginLoaderService(pluginDirectory);
        _registryService = new ToolRegistryService();

        Console.Error.WriteLine("[PluginManager] ✓ Initialized successfully");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Install a plugin from NuGet package
    /// </summary>
    public async Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request)
    {
        EnsureInitialized();

        // Validate request
        var (isValid, error) = InputValidator.ValidatePluginInstallRequest(request);
        if (!isValid)
        {
            Console.Error.WriteLine($"[PluginManager] Validation failed: {error}");
            return new PluginInstallResult
            {
                Success = false,
                ErrorMessage = error
            };
        }

        Console.Error.WriteLine($"[PluginManager] Installing plugin: {request.PackageId}");

        var (success, errorMessage, installedPath) = await _packageService!.InstallPluginAsync(
            request.PackageId, 
            request.Version);

        if (!success)
        {
            Console.Error.WriteLine($"[PluginManager] ✗ Installation failed: {errorMessage}");
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

        Console.Error.WriteLine($"[PluginManager] ✓ Plugin '{request.PackageId}' installed successfully");

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
        var (isValid, error) = InputValidator.ValidatePackageId(packageId);
        if (!isValid)
        {
            Console.Error.WriteLine($"[PluginManager] Validation failed: {error}");
            return false;
        }

        Console.Error.WriteLine($"[PluginManager] Uninstalling plugin: {packageId}");
        
        var result = _packageService!.UninstallPlugin(packageId);
        
        if (result)
        {
            Console.Error.WriteLine($"[PluginManager] ✓ Plugin '{packageId}' uninstalled successfully");
            // Reload plugins to update registry
            await ReloadPluginsAsync();
        }
        else
        {
            Console.Error.WriteLine($"[PluginManager] ✗ Failed to uninstall plugin '{packageId}'");
        }

        return result;
    }

    /// <summary>
    /// Reload all plugins from the plugin directory
    /// </summary>
    public async Task ReloadPluginsAsync()
    {
        EnsureInitialized();

        Console.Error.WriteLine("[PluginManager] Reloading plugins...");

        // Load all plugins
        var plugins = await _loaderService!.LoadPluginsAsync();

        // Register tools
        _registryService!.RegisterPlugins(plugins);

        Console.Error.WriteLine($"[PluginManager] ✓ Reloaded {plugins.Count} plugins with {_registryService.GetToolCount()} tools");
    }

    /// <summary>
    /// Get list of all loaded plugins with their information
    /// </summary>
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
