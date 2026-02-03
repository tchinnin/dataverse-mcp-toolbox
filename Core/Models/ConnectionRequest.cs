namespace DataverseMCPToolBox.Models;

/// <summary>
/// Représente une demande de connexion à un environnement Dataverse
/// </summary>
public class ConnectionRequest
{
    /// <summary>
    /// Optional existing connection ID to reuse (for re-authentication)
    /// If not provided, a new GUID will be generated
    /// </summary>
    public string? ConnectionId { get; set; }
    
    public required string EnvironmentUrl { get; set; }
    public string? ConnectionName { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
