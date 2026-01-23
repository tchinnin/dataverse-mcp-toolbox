namespace DataverseMCPToolBox.Models;

/// <summary>
/// Représente les informations WhoAmI d'un utilisateur Dataverse
/// </summary>
public class WhoAmIResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? EnvironmentUrl { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? BusinessUnitName { get; set; }
    public string? OrganizationId { get; set; }
}
