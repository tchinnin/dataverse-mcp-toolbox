namespace DataverseMCPToolBox.Models;

/// <summary>
/// Représente le résultat d'une tentative de connexion à Dataverse
/// </summary>
public class ConnectionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ConnectionId { get; set; }
    public string? OrganizationUrl { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresOn { get; set; }
}
