using System.Text.RegularExpressions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for validating request inputs
/// </summary>
public static class InputValidator
{
    private static readonly Regex UrlRegex = new Regex(
        @"^https?://[a-zA-Z0-9\-\.]+\.(?:crm\d*|dynamics)\.com/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validate a connection request
    /// </summary>
    public static (bool IsValid, string? Error) ValidateConnectionRequest(ConnectionRequest request)
    {
        if (request == null)
        {
            return (false, "Connection request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.EnvironmentUrl))
        {
            return (false, "Environment URL is required");
        }

        if (!Uri.TryCreate(request.EnvironmentUrl, UriKind.Absolute, out var uri))
        {
            return (false, "Environment URL is not a valid URI");
        }

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Environment URL must use HTTPS protocol");
        }

        if (!UrlRegex.IsMatch(request.EnvironmentUrl))
        {
            return (false, "Environment URL must be a valid Dataverse URL (*.crm.dynamics.com or *.crm[N].dynamics.com)");
        }

        return (true, null);
    }

    /// <summary>
    /// Validate a connection ID
    /// </summary>
    public static (bool IsValid, string? Error) ValidateConnectionId(string? connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return (false, "Connection ID is required");
        }

        if (!Guid.TryParse(connectionId, out _))
        {
            return (false, "Connection ID must be a valid GUID");
        }

        return (true, null);
    }

    /// <summary>
    /// Validate a plugin install request
    /// </summary>
    public static (bool IsValid, string? Error) ValidatePluginInstallRequest(PluginInstallRequest request)
    {
        if (request == null)
        {
            return (false, "Plugin install request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.PackageId))
        {
            return (false, "Package ID is required");
        }

        // Package ID should follow NuGet naming conventions
        if (!Regex.IsMatch(request.PackageId, @"^[A-Za-z0-9\.\-]+$"))
        {
            return (false, "Package ID contains invalid characters. Only alphanumeric, dots, and hyphens are allowed");
        }

        // Validate version format if provided
        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            if (!Regex.IsMatch(request.Version, @"^\d+\.\d+\.\d+(-[A-Za-z0-9\-\.]+)?$"))
            {
                return (false, "Version must be in format X.Y.Z or X.Y.Z-prerelease (e.g., 1.0.0 or 1.0.0-alpha)");
            }
        }

        return (true, null);
    }

    /// <summary>
    /// Validate a tool call request
    /// </summary>
    public static (bool IsValid, string? Error) ValidateToolCallRequest(ToolCallRequest request)
    {
        if (request == null)
        {
            return (false, "Tool call request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.ToolName))
        {
            return (false, "Tool name is required");
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            return (false, "Connection ID is required");
        }

        var (isValidId, idError) = ValidateConnectionId(request.ConnectionId);
        if (!isValidId)
        {
            return (false, $"Invalid connection ID: {idError}");
        }

        return (true, null);
    }

    /// <summary>
    /// Validate a directory path
    /// </summary>
    public static (bool IsValid, string? Error) ValidateDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "Directory path is required");
        }

        try
        {
            // Check if path is valid
            _ = Path.GetFullPath(path);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Invalid directory path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate a package ID for uninstallation
    /// </summary>
    public static (bool IsValid, string? Error) ValidatePackageId(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return (false, "Package ID is required");
        }

        if (!Regex.IsMatch(packageId, @"^[A-Za-z0-9\.\-]+$"))
        {
            return (false, "Package ID contains invalid characters");
        }

        return (true, null);
    }
}
