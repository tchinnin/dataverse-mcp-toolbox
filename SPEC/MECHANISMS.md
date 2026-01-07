# Implemented Mechanisms and Patterns

This document captures architectural decisions, design patterns, and mechanisms implemented in the Dataverse MCP Toolbox. It serves as a living record of "how things work" and "why we did it this way."

> **Note for AI Agents**: Update this document whenever you implement a significant pattern, mechanism, or make an architectural decision. This helps maintain consistency across the codebase and provides context for future development.

---

## Status Key
- ✅ **Implemented**: Completed and tested
- 🚧 **In Progress**: Currently being developed
- 📋 **Planned**: Designed but not yet implemented
- ❌ **Deprecated**: No longer used

---

## Core Mechanisms

### MCP Protocol Communication (Status: 📋 Planned)

**Description**: Stdio-based JSON-RPC communication implementing the Model Context Protocol specification.

**Implementation Details**:
```csharp
// To be implemented
// - Read JSON-RPC messages from stdin
// - Parse and validate messages
// - Route to appropriate handlers
// - Write JSON-RPC responses to stdout
```

**Design Decisions**:
- Use stdio transport for simplicity and VSCode extension compatibility
- Implement async message processing to handle concurrent requests
- Use System.Text.Json for performance over Newtonsoft.Json where possible
- Buffer stdin/stdout to prevent message fragmentation

**Related Files**:
- `Protocol/McpProtocolHandler.cs` (planned)
- `Protocol/JsonRpcMessage.cs` (planned)

**Gotchas**:
- Must flush stdout after each message
- Handle partial messages when reading from stdin
- Ensure thread-safe access to stdio streams

---

### Plugin Discovery and Loading (Status: 📋 Planned)

**Description**: Dynamic plugin discovery and loading mechanism using .NET reflection with support for NuGet packages and local assemblies.

**Implementation Pattern**:
```csharp
// To be implemented
// - Scan plugin directories for assemblies
// - Support NuGet package restoration
// - Reflect for types implementing IPlugin
// - Load assemblies into separate contexts (isolation)
// - Validate plugin compatibility
// - Initialize plugins with dependencies
```

**Design Decisions**:
- Use AssemblyLoadContext for plugin isolation
- Support NuGet packages as primary distribution method
- Implement lazy loading to reduce startup time
- Validate plugin API version compatibility
- Gracefully handle plugin load failures (log and continue)
- Support both NuGet-installed and manually placed plugins

**Plugin Sources**:
1. **NuGet Packages**: Installed via `dotnet MCP tool install` to plugin directory
2. **GitHub Releases**: Manually downloaded DLLs placed in plugin directory
3. **Local Development**: Debug builds during plugin development

**Configuration**:
```json
{
  "pluginDirectories": [
    "./plugins",
    "%APPDATA%/DataverseMcpToolbox/plugins",
    "~/.dataversemcptoolbox/plugins"
  ],
  "enabledPlugins": ["*"], // or specific list like ["com.company.plugin1", "com.company.plugin2"]
  "disabledPlugins": [],
  "nugetSources": [
    "https://api.nuget.org/v3/index.json"
  ]
}
```

**Related Files**:
- `Plugins/IPluginLoader.cs` (planned)
- `Plugins/PluginLoader.cs` (planned)
- `Plugins/PluginContext.cs` (planned)

**Gotchas**:
- Assembly version conflicts between plugins
- Plugins must target same .NET version
- Circular dependencies between plugins
- Plugin unloading on .NET can be problematic

---

### MCP tool registry and Execution (Status: 📋 Planned)

**Description**: Central registry for all MCP tools provided by plugins, with execution pipeline.

**Implementation Pattern**:
```csharp
// MCP tool registration
public interface IMCPTool
{
    string Name { get; }
    string Description { get; }
    JsonSchema InputSchema { get; }
    Task<object> ExecuteAsync(object parameters, CancellationToken ct);
}

// Execution pipeline
// Request → Validation → Authorization → Execution → Logging → Response
```

**Design Decisions**:
- MCP tools identified by unique names (namespace:toolname pattern)
- JSON Schema for parameter validation
- Support for long-running MCP tools with cancellation
- Middleware pipeline for cross-cutting concerns

**Middleware Pipeline**:
1. **Validation**: Validate parameters against schema
2. **Authentication**: Check Dataverse connection
3. **Authorization**: Check permissions (future)
4. **Logging**: Log execution start
5. **Execution**: Run the MCP tool
6. **Error Handling**: Catch and format errors
7. **Logging**: Log execution complete

**Related Files**:
- `MCP tools/IMCPToolRegistry.cs` (planned)
- `MCP tools/MCPToolRegistry.cs` (planned)
- `MCP tools/MCPToolExecutor.cs` (planned)

**Gotchas**:
- MCP tool name collisions between plugins
- Large response payloads (consider streaming)
- Timeout handling for long-running operations

---

### Dataverse Client Wrapper (Status: 📋 Planned)

**Description**: Abstraction layer over Microsoft Dataverse SDK for dependency injection and testing.

**Implementation Pattern**:
```csharp
public interface IDataverseClient
{
    Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columns);
    Task<Guid> CreateAsync(Entity entity);
    Task UpdateAsync(Entity entity);
    Task DeleteAsync(string entityName, Guid id);
    Task<EntityCollection> RetrieveMultipleAsync(QueryBase query);
    Task<EntityMetadata> GetEntityMetadataAsync(string entityName);
    // ... other SDK operations
}
```

**Design Decisions**:
- Wrap ServiceClient from Dataverse SDK
- Implement retry logic for transient failures
- Add connection health monitoring
- Cache metadata to reduce API calls
- Support multiple authentication methods

**Authentication Methods**:
1. OAuth (interactive user login)
2. Service Principal (client credentials)
3. Connection String (various formats)

**Related Files**:
- `Dataverse/IDataverseClient.cs` (planned)
- `Dataverse/DataverseClient.cs` (planned)
- `Dataverse/DataverseAuthenticator.cs` (planned)

**Gotchas**:
- SDK connection needs keep-alive for long-running processes
- Metadata cache invalidation strategy
- Rate limiting from Dataverse service
- Connection string format variations

---

### Configuration Management (Status: 📋 Planned)

**Description**: Layered configuration system with validation and environment variable support.

**Configuration Sources** (in priority order):
1. Command-line arguments
2. Environment variables
3. User configuration file (~/.dataversemcptoolbox/config.json)
4. Extension-provided configuration (via VSCode)
5. Default values

**Implementation Pattern**:
```csharp
public class McpConfiguration
{
    [Required]
    public string DataverseUrl { get; set; }
    
    public string ClientId { get; set; }
    public string TenantId { get; set; }
    public string ClientSecret { get; set; }
    
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public string PluginDirectory { get; set; } = "./plugins";
    public int CommandTimeout { get; set; } = 300; // seconds
}
```

**Design Decisions**:
- Strongly-typed configuration classes
- Validation on load with clear error messages
- Sensitive data never logged
- Support for environment-specific configs (dev/prod)

**Related Files**:
- `Configuration/McpConfiguration.cs` (planned)
- `Configuration/ConfigurationLoader.cs` (planned)

---

### Error Handling and Response Format (Status: 📋 Planned)

**Description**: Standardized error handling and MCP-compliant response formatting.

**Error Categories**:
```csharp
public enum ErrorCode
{
    // Protocol errors
    INVALID_REQUEST,
    METHOD_NOT_FOUND,
    INVALID_PARAMS,
    
    // Application errors
    DATAVERSE_ERROR,
    PLUGIN_ERROR,
    TOOL_NOT_FOUND,
    
    // System errors
    INTERNAL_ERROR,
    TIMEOUT,
    UNAUTHORIZED
}
```

**Response Format**:
```csharp
public class McpResponse
{
    public bool IsSuccess { get; set; }
    public object Data { get; set; }
    public ErrorInfo Error { get; set; }
}

public class ErrorInfo
{
    public string Code { get; set; }
    public string Message { get; set; }
    public object Details { get; set; }
}
```

**Design Decisions**:
- Never expose internal implementation details in errors
- Provide actionable error messages
- Include request IDs for tracing
- Log detailed errors, return user-friendly messages

**Related Files**:
- `Protocol/McpResponse.cs` (planned)
- `Utilities/ExceptionExtensions.cs` (planned)

---

### Logging Strategy (Status: 📋 Planned)

**Description**: Structured logging with Serilog to file and console.

**Log Structure**:
```csharp
_logger.LogInformation(
    "MCP tool executed: {ToolName} by {User} in {Duration}ms",
    toolName,
    userId,
    durationMs);
```

**Log Levels**:
- **Trace**: Detailed protocol messages (development only)
- **Debug**: Plugin loading, MCP tool registration
- **Information**: MCP tool execution, configuration loaded
- **Warning**: Retries, fallback operations
- **Error**: MCP tool failures, plugin errors
- **Critical**: Server crashes, unrecoverable errors

**Log Outputs**:
1. Console (stderr) - structured format for VSCode extension
2. File (~/.dataversemcptoolbox/logs/) - rolling files
3. Application Insights (optional, for telemetry)

**Design Decisions**:
- No sensitive data in logs (PII, credentials)
- Include correlation IDs for request tracing
- Separate log file per day, auto-cleanup after 30 days
- Configurable log levels per category

**Related Files**:
- `Program.cs` - Serilog configuration
- `Utilities/Logger.cs` (planned)

---

### Cross-Platform Binary Distribution (Status: 📋 Planned)

**Description**: Platform-specific binary packaging within VSCode extension.

**Binary Structure**:
```
extension/bin/
├── win-x64/
│   ├── DataverseMcpToolbox.Server.exe
│   └── [dependencies]
├── osx-x64/
│   ├── DataverseMcpToolbox.Server
│   └── [dependencies]
└── osx-arm64/
    ├── DataverseMcpToolbox.Server
    └── [dependencies]
```

**Build Configuration**:
```xml
<PropertyGroup>
  <RuntimeIdentifiers>win-x64;osx-x64;osx-arm64</RuntimeIdentifiers>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishSingleFile>true</PublishSingleFile>
</PropertyGroup>
```

**Design Decisions**:
- Self-contained deployments (no .NET runtime required)
- Trimmed binaries to reduce size
- Single-file publish for simpler distribution
- Platform detection in VSCode extension

**Build Process**:
```bash
# Build for all platforms
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r osx-x64
dotnet publish -c Release -r osx-arm64

# Copy to extension bin directory
# Package extension
vsce package
```

**Related Files**:
- Build scripts (planned)
- VSCode extension `package.json` (planned)

---

### VSCode Extension Server Management (Status: 📋 Planned)

**Description**: Extension manages MCP server lifecycle and configuration.

**Server Lifecycle**:
1. **Activation**: Extension loads, detects platform
2. **Configuration**: Load/validate connection settings
3. **Start**: Spawn server process with stdio transport
4. **Monitor**: Health checks, restart on crash
5. **Stop**: Graceful shutdown on deactivation

**Implementation Pattern**:
```typescript
class McpServerManager {
    private serverProcess: child_process.ChildProcess | null;
    
    async start(): Promise<void> {
        const binaryPath = this.getBinaryPath();
        const config = await this.loadConfiguration();
        
        this.serverProcess = spawn(binaryPath, [], {
            stdio: ['pipe', 'pipe', 'pipe'],
            env: this.buildEnvironment(config)
        });
        
        this.setupProcessHandlers();
        await this.waitForReady();
    }
    
    async stop(): Promise<void> {
        if (this.serverProcess) {
            this.serverProcess.kill('SIGTERM');
            await this.waitForExit();
        }
    }
}
```

**Design Decisions**:
- Auto-start server on extension activation (configurable)
- Auto-restart on crash (max 3 attempts)
- Status bar indicator for server status
- Output channel for server logs

**Related Files**:
- `src/mcpServerManager.ts` (planned)
- `src/statusBarManager.ts` (planned)

---

## Plugin Implementations

### Metadata Plugin (Status: 📋 Planned)

**MCP tools Provided**:
- `metadata:list_tables` - List all tables/entities
- `metadata:get_table` - Get table definition
- `metadata:list_columns` - List columns for a table
- `metadata:get_column` - Get column definition
- `metadata:list_relationships` - Get table relationships
- `metadata:get_optionset` - Get choice/optionset values

**Caching Strategy**:
- Cache entity metadata for 1 hour
- Invalidate on solution import
- Lazy load and cache on first access

**Related Files**:
- `Plugins/MetadataPlugin/` (planned)

---

### Data Plugin (Status: 📋 Planned)

**MCP tools Provided**:
- `data:create` - Create a record
- `data:retrieve` - Retrieve a record
- `data:update` - Update a record
- `data:delete` - Delete a record
- `data:query` - Query records with FetchXML
- `data:associate` - Associate records
- `data:disassociate` - Disassociate records

**Design Decisions**:
- Validate entity names against metadata
- Support early-bound and late-bound entities
- Return full entity for create/update operations
- Support for alternate keys

**Related Files**:
- `Plugins/DataPlugin/` (planned)

---

### Solution Plugin (Status: 📋 Planned)

**MCP tools Provided**:
- `solution:list` - List solutions
- `solution:export` - Export solution as zip
- `solution:import` - Import solution from zip
- `solution:get_components` - List solution components
- `solution:publish` - Publish customizations

**Async Operations**:
- Export and import are async operations
- Poll for completion status
- Return progress updates

**Related Files**:
- `Plugins/SolutionPlugin/` (planned)

---

## Future Enhancements

### Remote MCP Server (Status: 📋 Planned)

**Description**: Support for MCP server over HTTP/WebSocket for remote scenarios.

**Use Cases**:
- Shared team server
- Cloud-hosted MCP server
- CI/CD integration

---

### Plugin Marketplace (Status: 📋 Planned)

**Description**: Centralized registry for discovering and installing community plugins.

**Features**:
- Browse available plugins
- Install/update plugins from registry
- Version management
- Plugin ratings and reviews

---

### Telemetry and Analytics (Status: 📋 Planned)

**Description**: Optional telemetry for understanding usage patterns.

**Metrics**:
- Most used MCP tools
- Error rates
- Performance metrics
- Plugin adoption

**Privacy**:
- Opt-in only
- No PII collected
- Anonymized data only

---

## Change Log

| Date | Change | Author | Status |
|------|--------|--------|--------|
| 2026-01-07 | Initial MECHANISMS.md created | AI Agent | 📋 Planned |

---

## Notes for Agents

When implementing a new mechanism:
1. Add a section above with full details
2. Mark status as 🚧 In Progress
3. Update status to ✅ Implemented when complete
4. Document any deviations from the original design
5. Add gotchas and lessons learned
6. Update change log
