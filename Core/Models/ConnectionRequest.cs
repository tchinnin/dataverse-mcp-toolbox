namespace DataverseMCPToolBox.Models;

/// <summary>
/// Représente une demande de connexion à un environnement Dataverse
/// </summary>
public class ConnectionRequest
{
    public required string EnvironmentUrl { get; set; }
    public string? ConnectionName { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
