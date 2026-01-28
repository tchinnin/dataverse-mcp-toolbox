using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Interface for managing plugin lifecycle (installation, loading, unloading)
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Initialize plugin management with the specified directory
    /// </summary>
    Task InitializeAsync(string pluginDirectory);

    /// <summary>
    /// Install a plugin from NuGet package
    /// </summary>
    Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request);

    /// <summary>
    /// Uninstall a plugin
    /// </summary>
    Task<bool> UninstallPluginAsync(string packageId);

    /// <summary>
    /// Reload all plugins from the plugin directory
    /// </summary>
    Task ReloadPluginsAsync();

    /// <summary>
    /// Get list of all loaded plugins with their information
    /// </summary>
    Task<List<PluginInfo>> GetAllPluginsAsync();
}
