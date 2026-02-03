namespace DataverseMCPToolBox.Extensibility.Helpers;

/// <summary>
/// Centralized logging helper for consistent log formatting across plugins and tools
/// All logs are written to stderr to avoid polluting stdout (used for data communication)
/// </summary>
public static class Logger
{
    /// <summary>
    /// Log an informational message
    /// </summary>
    /// <param name="componentName">Name of the plugin, tool, or component</param>
    /// <param name="message">Message to log</param>
    public static void LogInfo(string componentName, string message)
    {
        Console.Error.WriteLine($"[{componentName}] {message}");
    }

    /// <summary>
    /// Log a success message with checkmark
    /// </summary>
    /// <param name="componentName">Name of the plugin, tool, or component</param>
    /// <param name="message">Success message</param>
    public static void LogSuccess(string componentName, string message)
    {
        Console.Error.WriteLine($"[{componentName}] ✓ {message}");
    }

    /// <summary>
    /// Log an error message with cross mark
    /// </summary>
    /// <param name="componentName">Name of the plugin, tool, or component</param>
    /// <param name="message">Error message</param>
    public static void LogError(string componentName, string message)
    {
        Console.Error.WriteLine($"[{componentName}] ✗ {message}");
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    /// <param name="componentName">Name of the plugin, tool, or component</param>
    /// <param name="message">Warning message</param>
    public static void LogWarning(string componentName, string message)
    {
        Console.Error.WriteLine($"[{componentName}] ⚠️  {message}");
    }

    /// <summary>
    /// Log an exception with full details
    /// </summary>
    /// <param name="componentName">Name of the plugin, tool, or component</param>
    /// <param name="ex">Exception to log</param>
    /// <param name="context">Optional context message</param>
    public static void LogException(string componentName, Exception ex, string? context = null)
    {
        if (!string.IsNullOrEmpty(context))
        {
            Console.Error.WriteLine($"[{componentName}] ✗ {context}");
        }
        Console.Error.WriteLine($"[{componentName}]   Type: {ex.GetType().Name}");
        Console.Error.WriteLine($"[{componentName}]   Message: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            Console.Error.WriteLine($"[{componentName}]   Stack trace: {ex.StackTrace}");
        }
    }
}
