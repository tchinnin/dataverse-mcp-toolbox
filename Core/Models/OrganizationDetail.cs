namespace DataverseMCPToolBox.Models;

/// <summary>
/// Détails d'une organisation Dataverse
/// </summary>
public class OrganizationDetail
{
    public required string OrganizationId { get; set; }
    public required string FriendlyName { get; set; }
    public required string UniqueName { get; set; }
    public required string Version { get; set; }
}
