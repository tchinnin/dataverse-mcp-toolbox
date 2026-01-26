namespace DataverseMCPToolBox.Extensibility.Attributes;

/// <summary>
/// Attribute to mark a class as an MCP plugin.
/// Used for plugin discovery and metadata collection.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class McpPluginAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique name of the plugin.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the semantic version of the plugin (e.g., "1.0.0").
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the author of the plugin.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the description of the plugin's functionality.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Initializes a new instance of McpPluginAttribute.
    /// </summary>
    /// <param name="name">The unique name of the plugin</param>
    /// <param name="version">The semantic version of the plugin</param>
    public McpPluginAttribute(string name, string version)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }
}
