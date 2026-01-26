namespace DataverseMCPToolBox.Extensibility.Models;

/// <summary>
/// Represents an error that occurred during tool execution.
/// </summary>
public sealed class ToolError
{
    /// <summary>
    /// Gets or sets the error code identifying the type of error.
    /// Common codes: VALIDATION_ERROR, EXECUTION_ERROR, DATAVERSE_ERROR, AUTHORIZATION_ERROR
    /// </summary>
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
