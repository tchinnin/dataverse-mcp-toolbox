using System.Text.RegularExpressions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for validating request inputs
/// All validation methods return ValidationResult for consistent error handling
/// </summary>
public static partial class InputValidator
{
    [GeneratedRegex(@"^https?://[a-zA-Z0-9\-\.]+\.(?:crm\d*|dynamics)\.com/?$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    /// <summary>
    /// Validate a connection request
    /// </summary>
    public static ValidationResult ValidateConnectionRequest(ConnectionRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Connection request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.EnvironmentUrl))
        {
            return ValidationResult.Failure("Environment URL is required");
        }

        if (!Uri.TryCreate(request.EnvironmentUrl, UriKind.Absolute, out var uri))
        {
            return ValidationResult.Failure("Environment URL is not a valid URI");
        }

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Failure("Environment URL must use HTTPS protocol");
        }

        if (!UrlRegex().IsMatch(request.EnvironmentUrl))
        {
            return ValidationResult.Failure("Environment URL must be a valid Dataverse URL (*.crm.dynamics.com or *.crm[N].dynamics.com)");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate a connection ID
    /// </summary>
    public static ValidationResult ValidateConnectionId(string? connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return ValidationResult.Failure("Connection ID is required");
        }

        if (!Guid.TryParse(connectionId, out _))
        {
            return ValidationResult.Failure("Connection ID must be a valid GUID");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate a plugin install request
    /// </summary>
    public static ValidationResult ValidatePluginInstallRequest(PluginInstallRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Plugin install request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.PackageId))
        {
            return ValidationResult.Failure("Package ID is required");
        }

        // Package ID should follow NuGet naming conventions
        if (!Regex.IsMatch(request.PackageId, @"^[A-Za-z0-9\.\-]+$"))
        {
            return ValidationResult.Failure("Package ID contains invalid characters. Only alphanumeric, dots, and hyphens are allowed");
        }

        // Validate version format if provided
        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            if (!Regex.IsMatch(request.Version, @"^\d+\.\d+\.\d+(-[A-Za-z0-9\-\.]+)?$"))
            {
                return ValidationResult.Failure("Version must be in format X.Y.Z or X.Y.Z-prerelease (e.g., 1.0.0 or 1.0.0-alpha)");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate a tool call request
    /// </summary>
    public static ValidationResult ValidateToolCallRequest(ToolCallRequest request)
    {
        if (request == null)
        {
            return ValidationResult.Failure("Tool call request cannot be null");
        }

        if (string.IsNullOrWhiteSpace(request.ToolName))
        {
            return ValidationResult.Failure("Tool name is required");
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            return ValidationResult.Failure("Connection ID is required");
        }

        var connectionIdValidation = ValidateConnectionId(request.ConnectionId);
        if (!connectionIdValidation.IsValid)
        {
            return ValidationResult.Failure($"Invalid connection ID: {connectionIdValidation.Error}");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validate a directory path
    /// </summary>
    public static ValidationResult ValidateDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Failure("Directory path is required");
        }

        try
        {
            // Check if path is valid
            _ = Path.GetFullPath(path);
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Invalid directory path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate a package ID for uninstallation
    /// </summary>
    public static ValidationResult ValidatePackageId(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return ValidationResult.Failure("Package ID is required");
        }

        if (!Regex.IsMatch(packageId, @"^[A-Za-z0-9\.\-]+$"))
        {
            return ValidationResult.Failure("Package ID contains invalid characters");
        }

        return ValidationResult.Success();
    }
}
