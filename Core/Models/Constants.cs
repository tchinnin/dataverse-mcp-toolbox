namespace DataverseMCPToolBox.Models;

/// <summary>
/// Environment variable names used throughout the application
/// Centralizes environment variable configuration for consistency
/// </summary>
public static class EnvironmentVariables
{
    /// <summary>
    /// Pipe name for Named Pipe communication between Extension, Core, and Bridge
    /// Format: DataverseMCPToolBox-{pid}
    /// </summary>
    public const string PipeName = "DATAVERSE_MCP_PIPE_NAME";

    /// <summary>
    /// Temporary directory path (Unix systems)
    /// Used to locate Named Pipe socket files
    /// </summary>
    public const string TmpDir = "TMPDIR";

    /// <summary>
    /// Directory path for plugin installations
    /// Defaults to ~/.dataverse-mcp-toolbox/plugins if not set
    /// </summary>
    public const string PluginDirectory = "DATAVERSE_MCP_PLUGIN_DIR";
}

/// <summary>
/// Network and communication constants
/// Centralizes timeout and buffer size configuration
/// </summary>
public static class NetworkConstants
{
    /// <summary>
    /// Default timeout for Named Pipe connection attempts
    /// </summary>
    public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Buffer size for stream forwarding operations (8KB)
    /// Optimal size for most network/pipe operations
    /// </summary>
    public const int StreamBufferSize = 8192;

    /// <summary>
    /// Retry delay for connection attempts (milliseconds)
    /// Used when retrying failed Named Pipe connections
    /// </summary>
    public const int RetryDelayMs = 1000;

    /// <summary>
    /// Maximum length for Unix socket paths
    /// Unix systems have a 104-108 character limit for socket paths
    /// </summary>
    public const int UnixSocketPathLimit = 104;
}

/// <summary>
/// Error codes used in RPC responses
/// Provides consistent error identification across the application
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string ToolNotFound = "TOOL_NOT_FOUND";
    public const string ConnectionNotFound = "CONNECTION_NOT_FOUND";
    public const string ConnectionNotReady = "CONNECTION_NOT_READY";
    public const string ExecutionError = "EXECUTION_ERROR";
    public const string ExecutionException = "EXECUTION_EXCEPTION";
    public const string PluginNotFound = "PLUGIN_NOT_FOUND";
    public const string PluginLoadError = "PLUGIN_LOAD_ERROR";
    public const string AuthenticationError = "AUTHENTICATION_ERROR";
}
