namespace WhoAmI.Models;

/// <summary>
/// Output model for the who-am-i tool containing detailed user and environment information.
/// Properties use PascalCase in C# and are automatically converted to camelCase in JSON.
/// </summary>
public class WhoAmIOutput
{
    /// <summary>
    /// The display name of the connected user.
    /// </summary>
    public string UserDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier (GUID) of the connected user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The unique identifier (GUID) of the user's business unit.
    /// </summary>
    public Guid BusinessUnitId { get; set; }

    /// <summary>
    /// The unique identifier (GUID) of the organization.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The URL of the Dataverse environment.
    /// </summary>
    public string EnvironmentUrl { get; set; } = string.Empty;

    /// <summary>
    /// Additional message or status information.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
