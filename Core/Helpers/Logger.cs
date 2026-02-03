namespace DataverseMCPToolBox.Helpers;

/// <summary>
/// Centralized logging helper for consistent log formatting across services
/// All logs are written to stderr to avoid polluting stdout (used for data communication)
/// </summary>
public static class Logger
{
    /// <summary>
    /// Log an informational message
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <param name="message">Message to log</param>
    public static void LogInfo(string serviceName, string message)
    {
        Console.Error.WriteLine($"[{serviceName}] {message}");
    }

    /// <summary>
    /// Log a success message with checkmark
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <param name="message">Success message</param>
    public static void LogSuccess(string serviceName, string message)
    {
        Console.Error.WriteLine($"[{serviceName}] ✓ {message}");
    }

    /// <summary>
    /// Log an error message with cross mark
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <param name="message">Error message</param>
    public static void LogError(string serviceName, string message)
    {
        Console.Error.WriteLine($"[{serviceName}] ✗ {message}");
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <param name="message">Warning message</param>
    public static void LogWarning(string serviceName, string message)
    {
        Console.Error.WriteLine($"[{serviceName}] ⚠️  {message}");
    }

    /// <summary>
    /// Log a separator line for visual grouping
    /// </summary>
    /// <param name="serviceName">Optional service name to include</param>
    public static void LogSeparator(string? serviceName = null)
    {
        var prefix = serviceName != null ? $"[{serviceName}] " : "";
        Console.Error.WriteLine($"{prefix}========================================");
    }

    /// <summary>
    /// Log an exception with full details
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <param name="ex">Exception to log</param>
    /// <param name="context">Optional context message</param>
    public static void LogException(string serviceName, Exception ex, string? context = null)
    {
        if (!string.IsNullOrEmpty(context))
        {
            Console.Error.WriteLine($"[{serviceName}] ✗ {context}");
        }
        Console.Error.WriteLine($"[{serviceName}]   Type: {ex.GetType().Name}");
        Console.Error.WriteLine($"[{serviceName}]   Message: {ex.Message}");
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            Console.Error.WriteLine($"[{serviceName}]   Stack trace: {ex.StackTrace}");
        }
    }
}
