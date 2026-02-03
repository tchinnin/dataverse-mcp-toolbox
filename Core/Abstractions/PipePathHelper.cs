using System.Runtime.InteropServices;

namespace DataverseMCPToolBox.Abstractions;

/// <summary>
/// Helper for constructing Named Pipe paths consistently across platforms
/// On Unix systems, .NET adds "CoreFxPipe_" prefix to socket files
/// </summary>
public static class PipePathHelper
{
    /// <summary>
    /// Prefix automatically added by .NET on Unix systems for Named Pipe sockets
    /// </summary>
    public const string UnixSocketPrefix = "CoreFxPipe_";

    /// <summary>
    /// Get the full Unix socket path for a given pipe name
    /// On Windows, Named Pipes use a different mechanism and don't create socket files
    /// </summary>
    /// <param name="pipeName">The pipe name (without platform-specific prefix)</param>
    /// <returns>Full path to the Unix socket file</returns>
    public static string GetUnixSocketPath(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be null or empty", nameof(pipeName));
        }

        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        return Path.Combine(tempPath, $"{UnixSocketPrefix}{pipeName}");
    }

    /// <summary>
    /// Check if the current platform is Windows
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Check if the current platform is Unix-based (macOS or Linux)
    /// </summary>
    public static bool IsUnix => !IsWindows;
}
