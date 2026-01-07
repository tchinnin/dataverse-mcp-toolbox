# Technical Architecture

## System Overview

The Dataverse MCP Toolbox consists of three primary components working together to provide Dataverse operations through the Model Context Protocol:

```
┌─────────────────────┐
│   VSCode Extension  │
│   (Distribution)    │
└──────────┬──────────┘
           │ Manages & Configures
           ▼
┌─────────────────────┐
│   MCP Server        │
│   (.NET Core)       │
│   ┌───────────────┐ │
│   │ Plugin System │ │
│   └───────────────┘ │
└──────────┬──────────┘
           │ Uses
           ▼
┌─────────────────────┐
│  Dataverse SDK      │
│  (Microsoft)        │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   Dataverse         │
│   (Cloud/OnPrem)    │
└─────────────────────┘
```

## Component Details

### 1. MCP Server (.NET)

**Technology**: .NET 8.0 (cross-platform)

**Responsibilities**:
- Implement MCP protocol (stdio-based communication)
- Load and manage plugins dynamically
- **Dataverse Connection Management**:
  - OAuth authentication (browser popup for user login)
  - Device code authentication flow
  - Support for common development client ID or custom client ID
  - Secure credential storage via VSCode secret folder
  - Multiple environment connection profiles
- **Plugin Library Management**:
  - Expose installed plugins (the tools of the toolbox)
  - Install, uninstall, and update plugins
  - Track plugin versions and capabilities
  - Provide plugin discovery from NuGet and GitHub releases
- **GitHub Copilot Integration**:
  - Generate instructions for properly using installed MCP tools
  - Provide MCP tool schemas and documentation to Copilot
  - Enable intelligent MCP tool suggestions based on context
- Expose MCP tools registered by plugins
- Manage MCP tool execution lifecycle
- Handle errors and logging

**Key Interfaces**:
- `IMcpServer` - Main server interface
- `IMCPToolRegistry` - MCP tool registration and discovery
- `IDataverseClient` - Wrapper for Dataverse operations
- `IPluginLoader` - Plugin discovery and loading

See [PLUGIN_INTERFACES.md](PLUGIN_INTERFACES.md) for complete interface definitions.

**Configuration**:
- Connection strings
- Authentication settings (OAuth, Service Principal)
- Plugin discovery paths
- Logging configuration

### 2. Plugin System

**Design Pattern**: Plugin Architecture with dynamic loading

**Community MCP tool Library**: The project supports community-contributed plugins, allowing developers to create and share custom MCP tools for Dataverse operations.

**Core Plugin Interfaces**:
- `IPlugin` - Main plugin contract
- `IMCPTool` - Individual MCP tool contract
- `IPluginContext` - Access to server services
- `IDataverseClient` - Dataverse operations
- `IDataverseMetadataCache` - Optional metadata caching

Complete interface specifications: [PLUGIN_INTERFACES.md](PLUGIN_INTERFACES.md)

**Plugin Structure**:
```csharp
public interface IPlugin
{
    string Id { get; }                    // Unique ID (reverse domain notation)
    string Name { get; }                  // Display name
    string Description { get; }           // What the plugin does
    string Version { get; }               // Semantic version
    string Author { get; }                // Author/organization
    string MinimumServerVersion { get; }  // Compatibility version
    
    Task InitializeAsync(IPluginContext context, CancellationToken ct);
    IEnumerable<IMCPTool> GetMCPTools();
    Task ShutdownAsync(CancellationToken ct);
}

public interface IMCPTool
{
    string Name { get; }                  // Namespaced MCP tool name
    string Description { get; }           // MCP tool description
    MCPToolInputSchema InputSchema { get; }  // JSON Schema for parameters
    
    Task<MCPToolResult> ExecuteAsync(MCPToolRequest request, CancellationToken ct);
}
```

**Built-in Plugins** (Initial set):
- **Metadata Plugin**: Schema exploration, table/column information
- **Data Plugin**: CRUD operations on records
- **Solution Plugin**: Solution management operations
- **Environment Plugin**: Environment information and configuration

**Community Plugin Development**:
- Plugins are standard .NET class libraries
- Must reference `DataverseMcpToolbox.PluginBase` NuGet package
- Implement `IPlugin` interface (or extend `PluginBase` abstract class)
- MCP tools implement `IMCPTool` interface (or extend `MCPToolBase` abstract class)
- Include plugin.json metadata file

**Plugin Distribution**:
- **NuGet Packages** (Recommended): Publish to NuGet.org for easy installation
- **GitHub Releases**: Attach compiled assemblies to releases for manual installation
- Users install plugins via package manager or download from releases
- Plugins placed in designated plugin directory (~/.dataversemcptoolbox/plugins/)

**Plugin Discovery & Loading**:
- Scan designated folder(s) for plugin assemblies
- Support NuGet package restoration on first run
- Reflect for types implementing IPlugin
- Validate plugin API version compatibility
- Load and initialize plugins on server start
- Gracefully handle plugin load failures

### 3. VSCode Extension

**Technology**: TypeScript, VSCode Extension API

**Responsibilities**:
- Package and distribute MCP server binaries (Windows/macOS)
- Configure MCP server connection for AI assistants (GitHub Copilot)
- Handle platform-specific paths and binaries
- **User Interface Components**:
  - **Connection Management Tree View**:
    - View all configured Dataverse connections
    - Create new connections (OAuth or device code flow)
    - Delete existing connections
    - Reauthenticate expired connections
    - Select active connection for current session
  - **Installed Plugins Tree View**:
    - Display all installed plugins
    - Show plugin version and status
    - View plugin capabilities (MCP tools provided)
    - Access plugin details in webview panel
  - **Plugin Library Webview**:
    - Browse available plugins from NuGet and community
    - Search and filter plugins
    - Install new plugins with one click
    - Update existing plugins
    - View plugin documentation and examples
- Display server status and logs in output channel
- Manage MCP server lifecycle (start, stop, restart)

**Extension Structure**:
```
dataverse-mcp-toolbox/
├── package.json (extension manifest)
├── src/
│   ├── extension.ts (activation)
│   ├── mcpServerManager.ts (server lifecycle)
│   ├── configurationProvider.ts (settings)
│   ├── views/
│   │   ├── connectionTreeView.ts (connection management UI)
│   │   ├── pluginTreeView.ts (installed plugins UI)
│   │   └── pluginLibraryWebview.ts (plugin marketplace UI)
│   ├── authentication/
│   │   ├── oauthHandler.ts (OAuth flow with browser)
│   │   └── deviceCodeHandler.ts (device code flow)
│   └── pluginManager/
│       ├── pluginInstaller.ts (install/uninstall)
│       └── pluginRegistry.ts (track installed plugins)
├── bin/
│   ├── win-x64/ (Windows binaries)
│   └── osx-x64/ (macOS binaries)
├── webview/
│   └── pluginLibrary.html (plugin marketplace UI)
└── README.md
```

## Data Flow

### MCP tool Invocation Flow

1. AI Assistant → MCP Protocol Request → MCP Server
2. MCP Server → Parse Request → Identify Target MCP tool
3. MCP Server → Route to Plugin → Execute MCP tool
4. Plugin → Dataverse SDK → Dataverse API
5. Dataverse → Response → Plugin
6. Plugin → Format Response → MCP Server
7. MCP Server → MCP Protocol Response → AI Assistant

### Authentication Flow

1. User configures connection in VSCode extension
2. Extension writes configuration to MCP settings
3. MCP Server reads configuration on startup
4. Server establishes Dataverse connection using SDK
5. Connection context shared with all plugins
6. Plugins use authenticated client for operations

## Cross-Platform Considerations

### .NET Runtime
- Use .NET 8.0 self-contained deployments
- Platform-specific binaries (win-x64, osx-x64, osx-arm64)
- Handle path separators and environment variables

### VSCode Extension
- Detect platform on activation
- Select appropriate binary path
- Handle different executable extensions (.exe on Windows)
- Platform-specific configuration paths

### File System
- Use Path.Combine for cross-platform paths
- Handle case-sensitive filesystems (macOS/Linux)
- Use Environment.SpecialFolder for system paths

## Security Considerations

- Store credentials securely (VSCode secret storage)
- Never log sensitive information
- Validate all inputs from MCP protocol
- Sandbox plugin execution
- Implement rate limiting for Dataverse calls
- Use secure connection strings

## Performance Considerations

- Lazy load plugins
- Cache metadata where appropriate
- Connection pooling for Dataverse SDK
- Async/await throughout for non-blocking operations
- Batch operations where possible
- Implement timeouts for long-running operations

## Error Handling

- Graceful degradation when plugins fail to load
- Detailed error messages following MCP error format
- Structured logging with severity levels
- Capture and report Dataverse SDK exceptions
- Retry logic for transient failures

## Extensibility Points

1. **Custom Plugins**: Load additional plugins from user-specified directories
2. **MCP tool Decorators**: Middleware for authentication, validation, logging
3. **Configuration Providers**: Support multiple authentication methods
4. **Output Formatters**: Customize response formats
5. **Event Hooks**: Plugin lifecycle events (load, unload, MCP tool execution)

## Future Architecture Considerations

- Support for remote MCP servers (not just stdio)
- Plugin marketplace/registry
- Hot reload of plugins
- Multi-environment support (dev/test/prod)
- Telemetry and usage analytics
- Plugin dependencies and versioning
