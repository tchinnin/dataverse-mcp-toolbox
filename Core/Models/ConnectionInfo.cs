namespace DataverseMCPToolBox.Models;

/// <summary>
/// Connection information stored in the in-memory state
/// Includes authentication tokens for connection management
/// </summary>
public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string EnvironmentUrl { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsValid { get; set; }
    
    // Authentication tokens for connection recreation
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresOn { get; set; }
}
