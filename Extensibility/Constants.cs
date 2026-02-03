namespace DataverseMCPToolBox.Extensibility;

/// <summary>
/// Error codes used in tool execution results
/// Provides consistent error identification across plugins and tools
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    /// Input parameters failed validation (schema, deserialization, or business rules)
    /// </summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>
    /// Tool execution failed due to an error in the tool logic
    /// </summary>
    public const string ExecutionError = "EXECUTION_ERROR";

    /// <summary>
    /// Dataverse operation failed (connection, API call, etc.)
    /// </summary>
    public const string DataverseError = "DATAVERSE_ERROR";

    /// <summary>
    /// User is not authorized to perform the requested operation
    /// </summary>
    public const string AuthorizationError = "AUTHORIZATION_ERROR";

    /// <summary>
    /// Tool execution was cancelled by user or timeout
    /// </summary>
    public const string OperationCancelled = "OPERATION_CANCELLED";

    /// <summary>
    /// Requested resource or entity was not found
    /// </summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>
    /// Invalid configuration or setup
    /// </summary>
    public const string ConfigurationError = "CONFIGURATION_ERROR";
}
