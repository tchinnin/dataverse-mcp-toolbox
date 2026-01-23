using System.Reflection;
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for loading plugins from assemblies
/// </summary>
public class PluginLoaderService
{
    private readonly string _pluginDirectory;
    private readonly List<IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new();

    public PluginLoaderService(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
    }

    /// <summary>
    /// Load all plugins from the configured directory
    /// </summary>
    public async Task<List<IPlugin>> LoadPluginsAsync()
    {
        _loadedPlugins.Clear();
        _loadedAssemblies.Clear();

        Console.Error.WriteLine($"Loading plugins from: {_pluginDirectory}");

        if (!Directory.Exists(_pluginDirectory))
        {
            Console.Error.WriteLine("Plugin directory does not exist");
            return _loadedPlugins;
        }

        // Get all subdirectories (each plugin in its own folder)
        var pluginDirs = Directory.GetDirectories(_pluginDirectory);

        foreach (var pluginDir in pluginDirs)
        {
            Console.Error.WriteLine($"Scanning plugin directory: {pluginDir}");
            
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
                    Console.Error.WriteLine($"Failed to load plugin from {dllFile}: {ex.Message}");
                }
            }
        }

        Console.Error.WriteLine($"Loaded {_loadedPlugins.Count} plugins");
        return _loadedPlugins;
    }

    /// <summary>
    /// Load plugins from a specific assembly file
    /// </summary>
    private async Task LoadPluginFromAssemblyAsync(string assemblyPath)
    {
        Console.Error.WriteLine($"Loading assembly: {assemblyPath}");

        // Skip if already loaded
        if (_loadedAssemblies.ContainsKey(assemblyPath))
        {
            Console.Error.WriteLine($"Assembly already loaded: {assemblyPath}");
            return;
        }

        // Load assembly
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
            _loadedAssemblies[assemblyPath] = assembly;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load assembly: {ex.Message}");
            return;
        }

// Discover plugins in assembly using PluginManifest
            var pluginManifests = PluginManifest.DiscoverPlugins(assembly);

        if (!pluginManifests.Any())
        {
            Console.Error.WriteLine($"No plugins found in assembly: {assemblyPath}");
            return;
        }

        // Instantiate each plugin
        foreach (var manifest in pluginManifests)
        {
            try
            {
                Console.Error.WriteLine($"Found plugin: {manifest.Name} v{manifest.Version} by {manifest.Author}");
                Console.Error.WriteLine($"  Description: {manifest.Description}");

                // Create instance
                var pluginInstance = Activator.CreateInstance(manifest.PluginType) as IPlugin;
                
                if (pluginInstance == null)
                {
                    Console.Error.WriteLine($"Failed to create instance of plugin: {manifest.Name}");
                    continue;
                }

                // Initialize plugin (without connection context yet)
                await pluginInstance.InitializeAsync(null!);

                _loadedPlugins.Add(pluginInstance);
                Console.Error.WriteLine($"Successfully loaded plugin: {manifest.Name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to instantiate plugin {manifest.Name}: {ex}");
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

    /// <summary>
    /// Clear all loaded plugins
    /// </summary>
    public void ClearPlugins()
    {
        _loadedPlugins.Clear();
        _loadedAssemblies.Clear();
    }
}
