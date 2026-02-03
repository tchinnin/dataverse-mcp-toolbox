namespace DataverseMCPToolBox.Models;

/// <summary>
/// Server version information
/// </summary>
public class ServerVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public DateTime BuildDate { get; set; }
    public string Platform { get; set; } = string.Empty;
}
