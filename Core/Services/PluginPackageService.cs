using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Helpers;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for managing plugin installation from NuGet packages
/// </summary>
public class PluginPackageService
{
    private const string ServiceName = "PluginPackageService";
    private readonly string _pluginDirectory;
    private readonly ILogger _logger;
    private static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromMinutes(5);

    public PluginPackageService(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
        _logger = NullLogger.Instance;

        // Ensure plugin directory exists
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
            Logger.LogInfo(ServiceName, $"Created plugin directory: {_pluginDirectory}");
        }
        else
        {
            Logger.LogInfo(ServiceName, $"Using existing plugin directory: {_pluginDirectory}");
        }
    }

    /// <summary>
    /// Install a plugin from NuGet
    /// </summary>
    /// <param name="packageId">The NuGet package ID to install</param>
    /// <param name="version">Optional specific version (null for latest)</param>
    /// <param name="cancellationToken">Cancellation token (default timeout: 5 minutes)</param>
    public async Task<(bool Success, string? ErrorMessage, string? InstalledPath)> InstallPluginAsync(
        string packageId, 
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        // Apply default timeout if no cancellation requested
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultDownloadTimeout);
        var effectiveCancellationToken = timeoutCts.Token;

        try
        {
            Logger.LogInfo(ServiceName, $"Installing plugin: {packageId} {version ?? "latest"}");

            // Configure NuGet source repository
            var sourceRepository = Repository.Factory.GetCoreV3(PackageSourceConstants.DefaultNuGetSource);
            var findPackageResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(effectiveCancellationToken);

            // Resolve version
            NuGetVersion? packageVersion = null;
            if (string.IsNullOrEmpty(version))
            {
                // Get latest version
                var versions = await findPackageResource.GetAllVersionsAsync(packageId, new SourceCacheContext(), _logger, effectiveCancellationToken);
                packageVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                
                if (packageVersion == null)
                {
                    return (false, $"Package '{packageId}' not found on {PackageSourceConstants.NuGetOrgName}", null);
                }
                Logger.LogInfo(ServiceName, $"Resolved to latest version: {packageVersion}");
            }
            else
            {
                if (!NuGetVersion.TryParse(version, out packageVersion))
                {
                    return (false, $"Invalid version format: {version}", null);
                }
            }

            // Download package
            var packagePath = Path.Combine(Path.GetTempPath(), $"{packageId}.{packageVersion}.nupkg");
            using (var packageStream = File.Create(packagePath))
            {
                var downloaded = await findPackageResource.CopyNupkgToStreamAsync(
                    packageId, 
                    packageVersion, 
                    packageStream, 
                    new SourceCacheContext(), 
                    _logger, 
                    effectiveCancellationToken);

                if (!downloaded)
                {
                    return (false, $"Failed to download package '{packageId}' version {packageVersion}", null);
                }
            }

            Logger.LogInfo(ServiceName, $"Downloaded package to: {packagePath}");

            // Extract package to plugin directory
            var extractPath = Path.Combine(_pluginDirectory, $"{packageId}.{packageVersion}");
            if (Directory.Exists(extractPath))
            {
                Logger.LogInfo(ServiceName, $"Plugin already installed at: {extractPath}");
                // Clean up old installation
                Directory.Delete(extractPath, true);
            }

            Directory.CreateDirectory(extractPath);

            using (var archive = ZipFile.OpenRead(packagePath))
            {
                // Extract lib folder (contains the plugin DLL)
                var libEntries = archive.Entries.Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));
                
                foreach (var entry in libEntries)
                {
                    // Skip directory entries
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var targetPath = Path.Combine(extractPath, entry.Name);
                    entry.ExtractToFile(targetPath, true);
                    Logger.LogInfo(ServiceName, $"Extracted: {entry.Name}");
                }
            }

            // Clean up temp file
            File.Delete(packagePath);

            Logger.LogSuccess(ServiceName, $"Plugin '{packageId}' installed successfully");
            Logger.LogInfo(ServiceName, $"Installation path: {extractPath}");
            return (true, null, extractPath);
        }
        catch (OperationCanceledException) when (effectiveCancellationToken.IsCancellationRequested)
        {
            var message = "Plugin installation timed out after 5 minutes";
            Logger.LogError(ServiceName, message);
            return (false, message, null);
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Error installing plugin");
            return (false, ex.Message, null);
        }
    }

    /// <summary>
    /// Uninstall a plugin
    /// </summary>
    public bool UninstallPlugin(string packageId)
    {
        try
        {
            Logger.LogInfo(ServiceName, $"Uninstalling plugin: {packageId}");

            // Find plugin directory (may have version suffix)
            var pluginDirs = Directory.GetDirectories(_pluginDirectory, $"{packageId}.*");
            
            if (pluginDirs.Length == 0)
            {
                Logger.LogWarning(ServiceName, $"Plugin not found: {packageId}");
                return false;
            }

            foreach (var dir in pluginDirs)
            {
                Directory.Delete(dir, true);
                Logger.LogInfo(ServiceName, $"Deleted: {dir}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Error uninstalling plugin");
            return false;
        }
    }
}
