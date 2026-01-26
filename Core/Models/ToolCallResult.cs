namespace DataverseMCPToolBox.Models;

/// <summary>
/// Result of an MCP tool execution
/// </summary>
public class ToolCallResult
{
    /// <summary>
    /// Whether the tool executed successfully
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Tool output content (if successful)
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    /// Error information (if failed)
    /// </summary>
    public ToolErrorInfo? Error { get; set; }
}

/// <summary>
/// Error information for failed tool execution
/// </summary>
public class ToolErrorInfo
{
    /// <summary>
    /// Error code (e.g., "VALIDATION_ERROR", "EXECUTION_ERROR")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error details
    /// </summary>
    public object? Details { get; set; }
}
