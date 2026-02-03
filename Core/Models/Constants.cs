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

/// <summary>
/// OAuth authentication constants for Microsoft Dataverse
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// Microsoft Business Applications client ID for OAuth authentication
    /// This is a well-known public client ID for Power Platform applications
    /// </summary>
    public const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    /// <summary>
    /// Microsoft identity platform authority URL for multi-tenant authentication
    /// </summary>
    public const string Authority = "https://login.microsoftonline.com/organizations";

    /// <summary>
    /// Default OAuth scope for Dataverse API access
    /// This is the global scope that works across all Dataverse environments
    /// </summary>
    public const string DefaultScope = "https://dynamics.crm.dynamics.com/.default";
}

/// <summary>
/// Model Context Protocol (MCP) constants
/// </summary>
public static class McpProtocolConstants
{
    /// <summary>
    /// Current MCP protocol version supported by this server
    /// Format: YYYY-MM-DD
    /// </summary>
    public const string CurrentVersion = "2024-11-05";

    /// <summary>
    /// Server name identifier for MCP protocol
    /// </summary>
    public const string ServerName = "dataverse-mcp-toolbox";

    /// <summary>
    /// Server version for MCP protocol
    /// </summary>
    public const string ServerVersion = "0.1.0";
}

/// <summary>
/// Package source constants for plugin management
/// </summary>
public static class PackageSourceConstants
{
    /// <summary>
    /// Default NuGet package source URL
    /// </summary>
    public const string DefaultNuGetSource = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// NuGet.org display name for error messages
    /// </summary>
    public const string NuGetOrgName = "NuGet.org";
}
