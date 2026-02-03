namespace DataverseMCPToolBox.Extensibility.Models;

/// <summary>
/// Represents an error that occurred during tool execution.
/// This model is part of the Extensibility SDK public API for plugin authors.
/// </summary>
/// <remarks>
/// Error codes should use constants from <see cref="ErrorCodes"/> class for consistency.
/// Note: This is intentionally separate from Core's ToolErrorInfo to maintain SDK/implementation separation.
/// </remarks>
public sealed class ToolError
{
    /// <summary>
    /// Gets or sets the error code identifying the type of error.
    /// Use constants from <see cref="ErrorCodes"/> class.
    /// </summary>
    /// <example>ErrorCodes.ValidationError, ErrorCodes.DataverseError</example>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets additional details about the error.
    /// Can be any serializable object containing context-specific information.
    /// </summary>
    public object? Details { get; init; }
}
