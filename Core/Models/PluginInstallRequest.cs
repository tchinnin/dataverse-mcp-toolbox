namespace DataverseMCPToolBox.Models;

/// <summary>
/// Request to install a plugin from NuGet
/// </summary>
public class PluginInstallRequest
{
    /// <summary>
    /// NuGet package ID to install
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Specific version to install (null for latest)
    /// </summary>
    public string? Version { get; set; }
}
