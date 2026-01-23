using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for managing plugin installation from NuGet packages
/// </summary>
public class PluginPackageService
{
    private readonly string _pluginDirectory;
    private readonly ILogger _logger;

    public PluginPackageService(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
        _logger = NullLogger.Instance;

        // Ensure plugin directory exists
        if (!Directory.Exists(_pluginDirectory))
        {
            Directory.CreateDirectory(_pluginDirectory);
            Console.Error.WriteLine($"Created plugin directory: {_pluginDirectory}");
        }
    }

    /// <summary>
    /// Install a plugin from NuGet
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage, string? InstalledPath)> InstallPluginAsync(
        string packageId, 
        string? version = null)
    {
        try
        {
            Console.Error.WriteLine($"Installing plugin: {packageId} {version ?? "latest"}");

            // Configure NuGet source repository
            var sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var findPackageResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            // Resolve version
            NuGetVersion? packageVersion = null;
            if (string.IsNullOrEmpty(version))
            {
                // Get latest version
                var versions = await findPackageResource.GetAllVersionsAsync(packageId, new SourceCacheContext(), _logger, CancellationToken.None);
                packageVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                
                if (packageVersion == null)
                {
                    return (false, $"Package '{packageId}' not found on NuGet.org", null);
                }
                Console.Error.WriteLine($"Resolved to latest version: {packageVersion}");
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
                    CancellationToken.None);

                if (!downloaded)
                {
                    return (false, $"Failed to download package '{packageId}' version {packageVersion}", null);
                }
            }

            Console.Error.WriteLine($"Downloaded package to: {packagePath}");

            // Extract package to plugin directory
            var extractPath = Path.Combine(_pluginDirectory, $"{packageId}.{packageVersion}");
            if (Directory.Exists(extractPath))
            {
                Console.Error.WriteLine($"Plugin already installed at: {extractPath}");
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
                    Console.Error.WriteLine($"Extracted: {entry.Name}");
                }
            }

            // Clean up temp file
            File.Delete(packagePath);

            Console.Error.WriteLine($"Plugin installed successfully to: {extractPath}");
            return (true, null, extractPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error installing plugin: {ex}");
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
            Console.Error.WriteLine($"Uninstalling plugin: {packageId}");

            // Find plugin directory (may have version suffix)
            var pluginDirs = Directory.GetDirectories(_pluginDirectory, $"{packageId}.*");
            
            if (pluginDirs.Length == 0)
            {
                Console.Error.WriteLine($"Plugin not found: {packageId}");
                return false;
            }

            foreach (var dir in pluginDirs)
            {
                Directory.Delete(dir, true);
                Console.Error.WriteLine($"Deleted: {dir}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error uninstalling plugin: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Get list of installed plugin directories
    /// </summary>
    public List<string> GetInstalledPluginDirectories()
    {
        if (!Directory.Exists(_pluginDirectory))
            return new List<string>();

        return Directory.GetDirectories(_pluginDirectory).ToList();
    }
}
