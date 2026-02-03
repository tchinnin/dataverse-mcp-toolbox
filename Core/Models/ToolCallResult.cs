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

    /// <summary>
    /// Create a successful tool call result
    /// </summary>
    /// <param name="content">Tool execution output</param>
    public static ToolCallResult Success(object? content) => new()
    {
        IsSuccess = true,
        Content = content
    };

    /// <summary>
    /// Create a failed tool call result with error code and message
    /// </summary>
    /// <param name="code">Error code (use ErrorCodes constants)</param>
    /// <param name="message">Error message</param>
    /// <param name="details">Optional additional error details</param>
    public static ToolCallResult Failure(string code, string message, object? details = null) => new()
    {
        IsSuccess = false,
        Error = new ToolErrorInfo
        {
            Code = code,
            Message = message,
            Details = details
        }
    };

    /// <summary>
    /// Create a validation error result
    /// </summary>
    /// <param name="message">Validation error message</param>
    public static ToolCallResult ValidationError(string message) =>
        Failure(ErrorCodes.ValidationError, message);

    /// <summary>
    /// Create a tool not found error result
    /// </summary>
    /// <param name="toolName">Name of the tool that was not found</param>
    public static ToolCallResult ToolNotFound(string toolName) =>
        Failure(ErrorCodes.ToolNotFound, $"Tool '{toolName}' not found");

    /// <summary>
    /// Create a connection not found error result
    /// </summary>
    /// <param name="connectionId">ID of the connection that was not found</param>
    public static ToolCallResult ConnectionNotFound(string connectionId) =>
        Failure(ErrorCodes.ConnectionNotFound, $"Connection '{connectionId}' not found");

    /// <summary>
    /// Create a connection not ready error result
    /// </summary>
    /// <param name="connectionId">ID of the connection that is not ready</param>
    public static ToolCallResult ConnectionNotReady(string connectionId) =>
        Failure(ErrorCodes.ConnectionNotReady, $"Connection '{connectionId}' is not ready or has been disconnected");

    /// <summary>
    /// Create an execution exception error result
    /// </summary>
    /// <param name="exception">Exception that occurred during execution</param>
    public static ToolCallResult ExecutionException(Exception exception) =>
        Failure(ErrorCodes.ExecutionException, exception.Message, exception.StackTrace);
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
