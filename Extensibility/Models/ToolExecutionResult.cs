namespace DataverseMCPToolBox.Extensibility.Models;

/// <summary>
/// Represents the result of an MCP tool execution.
/// Contains either successful output content or error details.
/// </summary>
public sealed class ToolExecutionResult
{
    /// <summary>
    /// Gets a value indicating whether the tool execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the output content from a successful tool execution.
    /// Null if the execution failed.
    /// </summary>
    public object? Content { get; init; }

    /// <summary>
    /// Gets the error details if the tool execution failed.
    /// Null if the execution was successful.
    /// </summary>
    public ToolError? Error { get; init; }

    /// <summary>
    /// Creates a successful tool execution result with content.
    /// </summary>
    /// <param name="content">The output content from the tool</param>
    /// <returns>A successful ToolExecutionResult</returns>
    public static ToolExecutionResult Success(object? content = null)
    {
        return new ToolExecutionResult
        {
            IsSuccess = true,
            Content = content
        };
    }

    /// <summary>
    /// Creates a failed tool execution result with error details.
    /// </summary>
    /// <param name="error">The error details</param>
    /// <returns>A failed ToolExecutionResult</returns>
    public static ToolExecutionResult Failure(ToolError error)
    {
        return new ToolExecutionResult
        {
            IsSuccess = false,
            Error = error
        };
    }

    /// <summary>
    /// Creates a failed tool execution result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="code">Optional error code (defaults to "EXECUTION_ERROR")</param>
    /// <returns>A failed ToolExecutionResult</returns>
    public static ToolExecutionResult Failure(Exception exception, string code = "EXECUTION_ERROR")
    {
        return new ToolExecutionResult
        {
            IsSuccess = false,
            Error = new ToolError
            {
                Code = code,
                Message = exception.Message,
                Details = new
                {
                    exceptionType = exception.GetType().Name,
                    stackTrace = exception.StackTrace
                }
            }
        };
    }
}
