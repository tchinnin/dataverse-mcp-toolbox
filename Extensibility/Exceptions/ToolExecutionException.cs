namespace DataverseMCPToolBox.Extensibility.Exceptions;

/// <summary>
/// Exception thrown when a tool execution fails.
/// Provides structured error information that can be serialized and returned to the MCP client.
/// </summary>
/// <remarks>
/// Use constants from <see cref="DataverseMCPToolBox.Extensibility.ErrorCodes"/> for the error code parameter.
/// This exception is caught by the framework and converted to a ToolExecutionResult automatically.
/// </remarks>
public class ToolExecutionException : Exception
{
    /// <summary>
    /// Gets the error code identifying the type of failure.
    /// Should use constants from <see cref="DataverseMCPToolBox.Extensibility.ErrorCodes"/>.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets additional details about the error.
    /// </summary>
    public object? ErrorDetails { get; }

    /// <summary>
    /// Initializes a new instance of ToolExecutionException with an error code and message.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    public ToolExecutionException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of ToolExecutionException with an error code, message, and details.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    public ToolExecutionException(string errorCode, string message, object? details)
        : base(message)
    {
        ErrorCode = errorCode;
        ErrorDetails = details;
    }

    /// <summary>
    /// Initializes a new instance of ToolExecutionException with an error code, message, and inner exception.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public ToolExecutionException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of ToolExecutionException with an error code, message, details, and inner exception.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">The error message</param>
    /// <param name="details">Additional error details</param>
    /// <param name="innerException">The inner exception</param>
    public ToolExecutionException(string errorCode, string message, object? details, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        ErrorDetails = details;
    }
}
