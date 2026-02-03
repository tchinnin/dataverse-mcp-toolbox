using System.Reflection;
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Helpers;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for loading plugins from assemblies
/// </summary>
public class PluginLoaderService
{
    private const string ServiceName = "PluginLoaderService";
    private readonly string _pluginDirectory;
    private readonly List<IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();
    private readonly IServiceProvider _serviceProvider;

    public PluginLoaderService(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
        _serviceProvider = new PluginServiceProvider();
    }

    /// <summary>
    /// Load all plugins from the configured directory
    /// </summary>
    public async Task<List<IPlugin>> LoadPluginsAsync()
    {
        _loadedPlugins.Clear();
        _loadedAssemblies.Clear();

        Logger.LogInfo(ServiceName, $"Loading plugins from: {_pluginDirectory}");

        if (!Directory.Exists(_pluginDirectory))
        {
            Logger.LogWarning(ServiceName, "Plugin directory does not exist");
            return _loadedPlugins;
        }

        // Get all subdirectories (each plugin in its own folder)
        var pluginDirs = Directory.GetDirectories(_pluginDirectory);

        foreach (var pluginDir in pluginDirs)
        {
            Logger.LogInfo(ServiceName, $"Scanning plugin directory: {pluginDir}");
            
            // Find all DLL files
            var dllFiles = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    await LoadPluginFromAssemblyAsync(dllFile);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ServiceName, ex, $"Failed to load plugin from {dllFile}");
                }
            }
        }

        Logger.LogSuccess(ServiceName, $"Loaded {_loadedPlugins.Count} plugins");
        return _loadedPlugins;
    }

    /// <summary>
    /// Load plugins from a specific assembly file
    /// </summary>
    private async Task LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        Logger.LogInfo(ServiceName, $"Loading assembly: {assemblyPath}");

        // Skip if already loaded
        if (_loadedAssemblies.ContainsKey(assemblyPath))
        {
            Logger.LogInfo(ServiceName, $"Assembly already loaded: {assemblyPath}");
            return;
        }

        // Load assembly
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
            _loadedAssemblies[assemblyPath] = assembly;
        }
        catch (FileNotFoundException ex)
        {
            Logger.LogError(ServiceName, $"Assembly file not found: {ex.Message}");
            return;
        }
        catch (BadImageFormatException ex)
        {
            Logger.LogError(ServiceName, $"Invalid assembly format (not a valid .NET assembly): {ex.Message}");
            return;
        }
        catch (FileLoadException ex)
        {
            Logger.LogError(ServiceName, $"Assembly could not be loaded: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Unexpected error loading assembly");
            return;
        }

        // Discover plugins in assembly using PluginManifest
        var pluginManifests = PluginManifest.DiscoverPlugins(assembly);

        if (!pluginManifests.Any())
        {
            Logger.LogInfo(ServiceName, $"No plugins found in assembly: {assemblyPath}");
            return;
        }

        // Instantiate each plugin
        foreach (var manifest in pluginManifests)
        {
            try
            {
                Logger.LogInfo(ServiceName, $"Found plugin: {manifest.Name} v{manifest.Version} by {manifest.Author}");
                Logger.LogInfo(ServiceName, $"  Description: {manifest.Description}");

                // Create instance
                var pluginInstance = Activator.CreateInstance(manifest.PluginType) as IPlugin;
                
                if (pluginInstance == null)
                {
                    Logger.LogError(ServiceName, $"Failed to create instance of plugin: {manifest.Name}");
                    continue;
                }

                // Initialize plugin with service provider
                await pluginInstance.InitializeAsync(_serviceProvider);

                _loadedPlugins.Add(pluginInstance);
                Logger.LogSuccess(ServiceName, $"Successfully loaded plugin: {manifest.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ServiceName, ex, $"Failed to instantiate plugin {manifest.Name}");
            }
        }
    }

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public List<IPlugin> GetLoadedPlugins()
    {
        return _loadedPlugins.ToList();
    }
}
