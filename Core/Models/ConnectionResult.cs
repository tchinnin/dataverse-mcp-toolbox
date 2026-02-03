namespace DataverseMCPToolBox.Models;

/// <summary>
/// Result of a Dataverse connection attempt
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

    /// <summary>
    /// Create a successful connection result
    /// </summary>
    public static ConnectionResult SuccessResult(
        string connectionId,
        string organizationUrl,
        string userId,
        string? userName = null,
        string? accessToken = null,
        string? refreshToken = null,
        string? expiresOn = null) => new()
    {
        Success = true,
        ConnectionId = connectionId,
        OrganizationUrl = organizationUrl,
        UserId = userId,
        UserName = userName,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresOn = expiresOn
    };

    /// <summary>
    /// Create a failed connection result
    /// </summary>
    /// <param name="errorMessage">Error description</param>
    public static ConnectionResult Failure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// Create an authentication failure result
    /// </summary>
    /// <param name="reason">Reason for authentication failure</param>
    public static ConnectionResult AuthenticationFailure(string reason) =>
        Failure($"Authentication failed: {reason}");

    /// <summary>
    /// Create a connection failure result
    /// </summary>
    /// <param name="reason">Reason why connection could not be established</param>
    public static ConnectionResult ConnectionFailure(string reason) =>
        Failure($"Unable to connect: {reason}");
}
