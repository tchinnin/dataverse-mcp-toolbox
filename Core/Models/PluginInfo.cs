namespace DataverseMCPToolBox.Models;

/// <summary>
/// Information about an installed plugin
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Plugin name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Plugin author
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Plugin description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// NuGet package ID (if installed from NuGet)
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// List of tools exposed by this plugin
    /// </summary>
    public List<ToolInfo> Tools { get; set; } = new();
}
