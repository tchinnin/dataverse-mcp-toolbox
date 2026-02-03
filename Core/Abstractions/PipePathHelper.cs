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
    /// <exception cref="ArgumentException">Thrown when pipe name is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown when socket path exceeds Unix limit</exception>
    public static string GetUnixSocketPath(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be null or empty", nameof(pipeName));
        }

        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var socketPath = Path.Combine(tempPath, $"{UnixSocketPrefix}{pipeName}");
        
        // Validate socket path length for Unix systems
        if (PlatformHelper.IsUnix && socketPath.Length > Models.NetworkConstants.UnixSocketPathLimit)
        {
            throw new InvalidOperationException(
                $"Unix socket path exceeds maximum length of {Models.NetworkConstants.UnixSocketPathLimit} characters. " +
                $"Path: {socketPath} (length: {socketPath.Length})");
        }
        
        return socketPath;
    }
}
