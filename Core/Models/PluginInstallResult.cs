namespace DataverseMCPToolBox.Models;

/// <summary>
/// Result of a plugin installation operation
/// </summary>
public class PluginInstallResult
{
    /// <summary>
    /// Whether the installation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Installed plugin information (if successful)
    /// </summary>
    public PluginInfo? PluginInfo { get; set; }
}
