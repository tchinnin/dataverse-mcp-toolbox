using System.Reflection;
using DataverseMCPToolBox.Extensibility.Attributes;
using DataverseMCPToolBox.Extensibility.Abstractions;

namespace DataverseMCPToolBox.Extensibility;

/// <summary>
/// Represents metadata about a discovered plugin.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public string Version { get; init; }

    /// <summary>
    /// Gets the plugin author.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the plugin description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the plugin type.
    /// </summary>
    public Type PluginType { get; init; }

    /// <summary>
    /// Gets the assembly containing the plugin.
    /// </summary>
    public Assembly Assembly { get; init; }

    /// <summary>
    /// Initializes a new instance of PluginManifest.
    /// </summary>
    public PluginManifest(string name, string version, Type pluginType, Assembly assembly)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
        PluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    /// <summary>
    /// Discovers all plugins in the specified assemblies.
    /// Scans for classes decorated with [McpPlugin] attribute that implement IPlugin.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for plugins</param>
    /// <returns>Collection of discovered plugin manifests</returns>
    public static IEnumerable<PluginManifest> DiscoverPlugins(params Assembly[] assemblies)
    {
        var manifests = new List<PluginManifest>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes();
                
                foreach (var type in types)
                {
                    // Check if type has McpPluginAttribute and implements IPlugin
                    var pluginAttribute = type.GetCustomAttribute<McpPluginAttribute>();
                    if (pluginAttribute == null)
                        continue;

                    if (!typeof(IPlugin).IsAssignableFrom(type))
                    {
                        Console.Error.WriteLine($"[PluginManifest] Type {type.FullName} has [McpPlugin] but does not implement IPlugin. Skipping.");
                        continue;
                    }

                    if (type.IsAbstract || type.IsInterface)
                    {
                        Console.Error.WriteLine($"[PluginManifest] Type {type.FullName} is abstract or interface. Skipping.");
                        continue;
                    }

                    var manifest = new PluginManifest(
                        pluginAttribute.Name,
                        pluginAttribute.Version,
                        type,
                        assembly)
                    {
                        Author = pluginAttribute.Author,
                        Description = pluginAttribute.Description
                    };

                    manifests.Add(manifest);
                    Console.Error.WriteLine($"[PluginManifest] Discovered plugin: {manifest.Name} v{manifest.Version} from {assembly.GetName().Name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[PluginManifest] Error scanning assembly {assembly.FullName}: {ex.Message}");
            }
        }

        return manifests;
    }
}
