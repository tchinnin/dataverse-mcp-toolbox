# Coding Guidelines for AI Agents

## General Principles

These guidelines are designed for AI-assisted development (GitHub Copilot) to ensure consistent, maintainable, and high-quality code throughout the Dataverse MCP Toolbox project.

### Core Values
1. **Clarity over Cleverness**: Write explicit, understandable code
2. **Consistency**: Follow established patterns throughout the codebase
3. **Cross-Platform**: Always consider Windows and macOS compatibility
4. **Error Handling**: Never silently fail; provide meaningful error messages
5. **Documentation**: Document public APIs, complex logic, and architectural decisions

---

## .NET Code Standards

### Naming Conventions

```csharp
// Interfaces: Prefix with 'I'
public interface IPlugin { }

// Classes: PascalCase
public class MetadataPlugin { }

// Methods: PascalCase
public async Task<Result> ExecuteToolAsync() { }

// Private fields: _camelCase with underscore
private readonly ILogger _logger;

// Properties: PascalCase
public string Name { get; set; }

// Constants: UPPER_CASE
public const int MAX_RETRY_COUNT = 3;

// Parameters: camelCase
public void ProcessRequest(string requestId, object payload) { }
```

### Project Structure

```
DataverseMcpToolbox.Server/
├── Program.cs                  # Entry point
├── McpServer.cs               # Main server implementation
├── Protocol/                   # MCP protocol handling
│   ├── McpProtocolHandler.cs
│   ├── JsonRpcMessage.cs
│   └── McpResponse.cs
├── Tools/                      # MCP tool registry and execution
│   ├── IMCPToolRegistry.cs
│   ├── MCPToolRegistry.cs
│   └── MCPToolExecutor.cs
├── Plugins/                    # Plugin system
│   ├── IPluginLoader.cs
│   ├── PluginLoader.cs
│   └── PluginContext.cs
├── Dataverse/                  # Dataverse integration
│   ├── IDataverseClient.cs
│   ├── DataverseClient.cs
│   └── DataverseAuthenticator.cs
├── Configuration/              # Configuration management
│   ├── McpConfiguration.cs
│   └── ConfigurationLoader.cs
└── Utilities/                  # Shared utilities
    ├── Logger.cs
    └── ExceptionExtensions.cs
```

### Code Patterns

#### Async/Await
```csharp
// Always use async/await for I/O operations
public async Task<Entity> RetrieveRecordAsync(string entityName, Guid id)
{
    try
    {
        var entity = await _serviceClient.RetrieveAsync(
            entityName, 
            id, 
            new ColumnSet(true));
        return entity;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve record {EntityName}:{Id}", entityName, id);
        throw;
    }
}
```

#### Dependency Injection
```csharp
// Use constructor injection
public class MetadataPlugin : IPlugin
{
    private readonly IDataverseClient _dataverseClient;
    private readonly ILogger<MetadataPlugin> _logger;

    public MetadataPlugin(
        IDataverseClient dataverseClient,
        ILogger<MetadataPlugin> logger)
    {
        _dataverseClient = dataverseClient ?? throw new ArgumentNullException(nameof(dataverseClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

#### Error Handling
```csharp
// Wrap external calls with try-catch and provide context
public async Task<McpResponse> ExecuteAsync(MCPToolRequest request)
{
    try
    {
        ValidateRequest(request);
        var result = await PerformOperationAsync(request);
        return McpResponse.Success(result);
    }
    catch (ArgumentException ex)
    {
        _logger.LogWarning(ex, "Invalid request parameters");
        return McpResponse.Error("INVALID_PARAMS", ex.Message);
    }
    catch (DataverseException ex)
    {
        _logger.LogError(ex, "Dataverse operation failed");
        return McpResponse.Error("DATAVERSE_ERROR", ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error in MCP tool execution");
        return McpResponse.Error("INTERNAL_ERROR", "An unexpected error occurred");
    }
}
```

#### Resource Disposal
```csharp
// Use 'using' statements for IDisposable
public async Task<string> ReadConfigurationAsync(string path)
{
    using var fileStream = File.OpenRead(path);
    using var reader = new StreamReader(fileStream);
    return await reader.ReadToEndAsync();
}

// Or using declarations for cleaner code
public async Task ProcessFileAsync(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    
    // Stream and reader disposed at end of method
    await ProcessStreamAsync(reader);
}
```

### Cross-Platform Code

```csharp
// Path handling
using System.IO;

// ✅ CORRECT: Use Path.Combine
var configPath = Path.Combine(basePath, "config", "settings.json");

// ❌ WRONG: Hardcoded separators
var configPath = basePath + "\\config\\settings.json"; // Windows-only

// Environment-specific paths
var appDataPath = Environment.GetFolderPath(
    Environment.SpecialFolder.ApplicationData);

// Platform detection
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows-specific code
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    // macOS-specific code
}
```

### Configuration

```csharp
// Use strongly-typed configuration
public class McpConfiguration
{
    public string DataverseUrl { get; set; }
    public string ClientId { get; set; }
    public string TenantId { get; set; }
    public LogLevel MinimumLogLevel { get; set; }
    public string PluginDirectory { get; set; }
}

// Load with validation
public static McpConfiguration LoadConfiguration()
{
    var config = ConfigurationLoader.Load<McpConfiguration>();
    
    if (string.IsNullOrEmpty(config.DataverseUrl))
    {
        throw new InvalidOperationException("DataverseUrl is required");
    }
    
    return config;
}
```

### Logging

```csharp
// Use structured logging with Serilog/Microsoft.Extensions.Logging
_logger.LogInformation(
    "Executing tool {ToolName} with parameters {Parameters}",
    toolName,
    JsonSerializer.Serialize(parameters));

// Use appropriate log levels
_logger.LogTrace("Detailed trace information");      // Development only
_logger.LogDebug("Debug information");               // Debugging
_logger.LogInformation("General information");       // Normal flow
_logger.LogWarning("Warning condition");             // Recoverable issues
_logger.LogError(exception, "Error occurred");       // Errors
_logger.LogCritical(exception, "Critical failure");  // Fatal errors
```

---

## TypeScript Code Standards (VSCode Extension)

### Naming Conventions

```typescript
// Interfaces: PascalCase with 'I' prefix (optional)
interface ServerConfiguration { }

// Classes: PascalCase
class McpServerManager { }

// Methods/Functions: camelCase
async function startServer(): Promise<void> { }

// Constants: UPPER_CASE
const DEFAULT_TIMEOUT = 5000;

// Variables: camelCase
let isServerRunning = false;

// Type aliases: PascalCase
type ServerStatus = 'running' | 'stopped' | 'error';
```

### Project Structure

```
dataverse-mcp-extension/
├── src/
│   ├── extension.ts           # Extension entry point
│   ├── mcpServerManager.ts    # Server lifecycle
│   ├── configurationProvider.ts
│   ├── statusBarManager.ts
│   ├── commands/              # Command implementations
│   │   ├── startServer.ts
│   │   ├── stopServer.ts
│   │   └── configureConnection.ts
│   ├── views/                 # Webview providers
│   │   └── configurationView.ts
│   └── utils/                 # Utilities
│       ├── platform.ts
│       └── logger.ts
├── resources/                 # Extension resources
│   └── icons/
├── bin/                      # MCP server binaries
│   ├── win-x64/
│   └── osx-x64/
└── package.json              # Extension manifest
```

### Code Patterns

#### Async/Await
```typescript
// Always use async/await
async function startMcpServer(): Promise<void> {
    try {
        const config = await loadConfiguration();
        await validateConfiguration(config);
        await launchServer(config);
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to start server: ${error.message}`);
        throw error;
    }
}
```

#### VSCode API Usage
```typescript
// Extension activation
export async function activate(context: vscode.ExtensionContext): Promise<void> {
    const serverManager = new McpServerManager(context);
    
    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('dataverseMcpToolbox.start', () => serverManager.start()),
        vscode.commands.registerCommand('dataverseMcpToolbox.stop', () => serverManager.stop())
    );
    
    // Auto-start if configured
    if (vscode.workspace.getConfiguration('dataverseMcpToolbox').get('autoStart')) {
        await serverManager.start();
    }
}
```

#### Error Handling
```typescript
// Provide user-friendly error messages
try {
    await performOperation();
} catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown error';
    logger.error('Operation failed', error);
    
    const action = await vscode.window.showErrorMessage(
        `Operation failed: ${message}`,
        'Retry',
        'View Logs'
    );
    
    if (action === 'Retry') {
        await performOperation();
    } else if (action === 'View Logs') {
        outputChannel.show();
    }
}
```

#### Platform Detection
```typescript
// Cross-platform binary selection
function getServerBinaryPath(): string {
    const platform = process.platform;
    const arch = process.arch;
    
    let binaryPath: string;
    
    if (platform === 'win32') {
        binaryPath = path.join(extensionPath, 'bin', 'win-x64', 'DataverseMcpToolbox.Server.exe');
    } else if (platform === 'darwin') {
        if (arch === 'arm64') {
            binaryPath = path.join(extensionPath, 'bin', 'osx-arm64', 'DataverseMcpToolbox.Server');
        } else {
            binaryPath = path.join(extensionPath, 'bin', 'osx-x64', 'DataverseMcpToolbox.Server');
        }
    } else {
        throw new Error(`Unsupported platform: ${platform}`);
    }
    
    return binaryPath;
}
```

---

## Testing Guidelines

### Unit Tests
```csharp
[Fact]
public async Task ExecuteAsync_WithValidParameters_ReturnsSuccess()
{
    // Arrange
    var mockClient = new Mock<IDataverseClient>();
    mockClient.Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<Guid>()))
              .ReturnsAsync(new Entity("account"));
    
    var plugin = new DataPlugin(mockClient.Object, Mock.Of<ILogger>());
    
    // Act
    var result = await plugin.ExecuteAsync(new MCPToolRequest 
    { 
        Name = "retrieve_record",
        Parameters = new { entityName = "account", id = Guid.NewGuid() }
    });
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Data);
}
```

### Integration Tests
```csharp
[Fact]
public async Task EndToEnd_McpProtocol_WorksCorrectly()
{
    // Arrange
    var server = new McpServer();
    await server.InitializeAsync();
    
    // Act
    var listToolsResponse = await server.HandleRequestAsync(@"
    {
        ""jsonrpc"": ""2.0"",
        ""method"": ""tools/list"",
        ""id"": 1
    }");
    
    // Assert
    Assert.Contains(""metadata_list_tables"", listToolsResponse);
}
```

---

## Documentation Standards

### XML Documentation
```csharp
/// <summary>
/// Executes a Dataverse tool with the specified parameters.
/// </summary>
/// <param name="toolName">The name of the MCP tool to execute.</param>
/// <param name="parameters">The parameters for the MCP tool execution.</param>
/// <returns>A task representing the asynchronous operation, containing the MCP tool result.</returns>
/// <exception cref="ArgumentNullException">Thrown when toolName is null or empty.</exception>
/// <exception cref="ToolNotFoundException">Thrown when the specified tool is not found.</exception>
public async Task<MCPToolResult> ExecuteToolAsync(string toolName, object parameters)
{
    // Implementation
}
```

### README Sections
- Project overview
- Installation instructions
- Configuration guide
- Usage examples
- Troubleshooting
- Contributing guidelines
- License information

---

## Git Commit Messages

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples**:
```
feat(plugins): Add metadata plugin for table discovery

Implements the metadata plugin with tools for:
- Listing all tables
- Getting table definitions
- Retrieving column information

Closes #15
```

```
fix(server): Handle connection timeout gracefully

Previously, connection timeouts would crash the server.
Now we catch the timeout exception and return a proper
error response to the client.

Fixes #42
```

---

## Security Best Practices

1. **Never log sensitive information** (passwords, tokens, connection strings)
2. **Validate all inputs** from MCP protocol
3. **Use parameterized queries** when constructing FetchXML
4. **Store credentials securely** (VSCode secrets API)
5. **Implement rate limiting** to prevent abuse
6. **Keep dependencies updated** for security patches

---

## Performance Considerations

1. **Cache metadata** where appropriate (table schemas, option sets)
2. **Use async/await** for all I/O operations
3. **Implement connection pooling** for Dataverse SDK
4. **Lazy load plugins** only when needed
5. **Batch operations** when making multiple Dataverse calls
6. **Set reasonable timeouts** to prevent hanging operations
7. **Dispose resources properly** using `using` statements

---

## Review Checklist

Before submitting code, ensure:
- [ ] Code follows naming conventions
- [ ] All public APIs have XML documentation
- [ ] Error handling is comprehensive
- [ ] Cross-platform compatibility verified
- [ ] Unit tests added/updated
- [ ] No hardcoded paths or platform-specific code
- [ ] Logging is appropriate and structured
- [ ] No sensitive information logged
- [ ] Dependencies are justified and minimal
- [ ] Performance implications considered
