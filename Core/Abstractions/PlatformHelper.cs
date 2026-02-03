using System.Runtime.InteropServices;

namespace DataverseMCPToolBox.Abstractions;

/// <summary>
/// Helper for platform detection and platform-specific operations
/// Provides consistent platform checks across the application
/// </summary>
public static class PlatformHelper
{
    /// <summary>
    /// Check if the current platform is Windows
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Check if the current platform is macOS
    /// </summary>
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Check if the current platform is Linux
    /// </summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Check if the current platform is Unix-based (macOS or Linux)
    /// </summary>
    public static bool IsUnix => IsMacOS || IsLinux;

    /// <summary>
    /// Get a human-readable platform name for logging and diagnostics
    /// </summary>
    /// <returns>Platform name: "Windows", "macOS", "Linux", or "Unknown"</returns>
    public static string GetPlatformName()
    {
        if (IsWindows) return "Windows";
        if (IsMacOS) return "macOS";
        if (IsLinux) return "Linux";
        return "Unknown";
    }
}
