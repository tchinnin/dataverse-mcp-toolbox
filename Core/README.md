# DataverseMCPToolBox.Runtime

[![NuGet](https://img.shields.io/nuget/v/DataverseMCPToolBox.Runtime.svg)](https://www.nuget.org/packages/DataverseMCPToolBox.Runtime/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Runtime package containing Core Server and MCP Bridge binaries for the Dataverse MCP Toolbox. Provides self-contained executables for all supported platforms.

## Overview

The **DataverseMCPToolBox.Runtime** package contains two essential components:

1. **Core Server** (`DataverseMCPToolBox`): Manages Dataverse connections, plugin lifecycle, and tool execution
2. **MCP Bridge** (`DataverseMCPToolBox.Bridge`): Protocol adapter enabling GitHub Copilot integration via Model Context Protocol

Both components communicate via JSON-RPC over named pipes (Windows) or Unix domain sockets (macOS/Linux), following a sidecar architecture pattern.

## Supported Platforms

This package includes self-contained binaries for:
- **macOS ARM64** (Apple Silicon) - `runtimes/osx-arm64/native/`
- **macOS x64** (Intel) - `runtimes/osx-x64/native/`
- **Windows x64** - `runtimes/win-x64/native/`
- **Linux x64** - `runtimes/linux-x64/native/`

All binaries are self-contained and **do not require** .NET runtime to be installed on the target machine.

## Usage

### Automatic Consumption (Recommended)

This package is consumed automatically by the **Dataverse MCP Toolbox VS Code Extension**. The extension:

1. Downloads the appropriate platform binaries from this NuGet package
2. Extracts them to the extension's global storage directory
3. Spawns the Core Server process with a unique instance identifier
4. Optionally spawns the MCP Bridge for GitHub Copilot integration
5. Manages the lifecycle and communication of both processes

### Architecture Overview

```
┌─────────────────────────────────────┐
│ VS Code Instance                    │
│  ┌──────────────┐  ┌─────────────┐ │
│  │  Extension   │──│ Core Server │ │
│  │  (UI Layer)  │  │  (State)    │ │
│  └──────────────┘  └──────▲──────┘ │
│                            │        │
│  ┌──────────────┐          │        │
│  │  MCP Bridge  │──────────┘        │
│  │  (Copilot)   │                   │
│  └──────────────┘                   │
└─────────────────────────────────────┘
```

### Manual Usage (Advanced)

For direct usage or custom integrations:

#### Core Server

```bash
# Make executable (Unix)
chmod +x DataverseMCPToolBox

# Launch with environment variables
export DATAVERSE_MCP_PIPE_NAME="MyInstance-12345"
export DATAVERSE_MCP_PLUGIN_DIR="/path/to/plugins"
./DataverseMCPToolBox
```

#### MCP Bridge (for Copilot integration)

```bash
# Launch Bridge (communicates with Core via pipe/socket)
export DATAVERSE_MCP_PIPE_NAME="MyInstance-12345"
./DataverseMCPToolBox.Bridge
```

**Example JSON-RPC request to Core Server:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "CreateConnection",
  "params": {
    "request": {
      "environmentUrl": "https://org.crm.dynamics.com",
      "connectionName": "Production"
    }
  }
}
```

## Features

### Core Server

#### Connection Management
- OAuth 2.0 device code flow authentication
- Multi-connection support with unique identifiers
- Connection state management (in-memory per instance)
- Organization metadata retrieval
- Connection testing and validation
- Graceful connection lifecycle management

#### Plugin System
- Dynamic plugin discovery from configured directories
- NuGet-based plugin installation and management
- Plugin manifest validation
- Hot-reload capability without server restart
- Isolated plugin execution contexts
- Automatic MCP tool discovery from plugins

#### Tool Execution
- MCP (Model Context Protocol) compliant tool interface
- JSON Schema-based parameter validation
- Structured error handling with standard error codes
- Cancellation token support for long-running operations
- Automatic camelCase/PascalCase conversion

### MCP Bridge

- Protocol translation between MCP and Core Server JSON-RPC
- stdin/stdout communication for GitHub Copilot
- Named pipe/Unix domain socket client for Core Server
- Automatic connection management
- Error propagation and logging

### Available RPC Methods (Core Server)

**Connection Operations:**
- `CreateConnection` - Establish a new Dataverse connection with OAuth
- `TestConnection` - Verify connection validity
- `GetOrganizationDetails` - Retrieve organization metadata
- `GetWhoAmI` - Get current authenticated user information
- `CloseConnection` - Close a specific connection
- `CloseAllConnections` - Terminate all active connections
- `ListConnections` - List all active connections

**Plugin Operations:**
- `SetPluginDirectory` - Configure plugin installation directory
- `InstallPlugin` - Install plugin from NuGet source
- `UninstallPlugin` - Remove an installed plugin
- `ReloadPlugins` - Reload all plugins from directory
- `ListPlugins` - Enumerate installed plugins with metadata

**Tool Operations:**
- `ListTools` - Get all available MCP tools from loaded plugins
- `CallTool` - Execute an MCP tool with parameters
- `GetToolSchema` - Retrieve JSON schema for tool parameters

## Communication Protocol

### Core Server

The Core Server uses **JSON-RPC 2.0** over named pipes (Windows) or Unix domain sockets (macOS/Linux):

- **Transport**: Named pipes / Unix domain sockets
- **Protocol**: JSON-RPC 2.0 with StreamJsonRpc
- **Message Format**: Content-Length headers (Language Server Protocol style)
- **Serialization**: JSON with camelCase property names
- **Logging**: All logs written to stderr only

**Named Pipe/Socket Naming:**
- Windows: `\\.\pipe\DataverseMCP-{InstanceId}`
- Unix: `/tmp/dvmcptb-sockets/DataverseMCP-{InstanceId}.sock`

### MCP Bridge

The MCP Bridge uses **stdin/stdout** for GitHub Copilot communication:

- **stdin**: Receives MCP protocol messages from Copilot
- **stdout**: Sends MCP protocol responses (MCP JSON format)
- **stderr**: Bridge logs and debug information
- **Backend**: Communicates with Core Server via named pipe/socket

### Message Format

Messages use the Language Server Protocol header format:
```
Content-Length: <length>\r\n\r\n
<JSON-RPC message>
```

**Example:**
```
Content-Length: 123\r\n\r\n
{"jsonrpc":"2.0","id":1,"method":"ListTools","params":{}}
```

## Requirements

- **No external dependencies** (self-contained executables)
- **Operating System**: Windows 10/11, macOS 10.15+, or Linux (64-bit)
- **Dataverse Access**: Valid Microsoft 365/Dataverse credentials
- **Network**: Internet connectivity for OAuth authentication and Dataverse API calls

## Environment Variables

The Core Server and MCP Bridge use environment variables for configuration:

### Core Server
- `DATAVERSE_MCP_PIPE_NAME` - Unique pipe/socket name for instance isolation (required)
- `DATAVERSE_MCP_PLUGIN_DIR` - Directory for plugin storage (optional, defaults to user data)

### MCP Bridge
- `DATAVERSE_MCP_PIPE_NAME` - Must match Core Server instance (required)

**Instance Isolation:**
Each VS Code instance uses a unique `DATAVERSE_MCP_PIPE_NAME` to ensure isolated state and prevent conflicts between multiple instances.

## Installation

### Via NuGet Package Manager
```bash
dotnet add package DataverseMCPToolBox.Runtime
```

### Via .NET CLI
```bash
dotnet nuget install DataverseMCPToolBox.Runtime
```

### From VS Code Extension
The Dataverse MCP Toolbox VS Code extension automatically downloads and manages this package.

## Version History

### 0.2.0-alpha (Current)
- Sidecar architecture with Core Server + MCP Bridge separation
- Named pipe/Unix domain socket communication
- Improved instance isolation with environment variables
- Enhanced error handling and logging
- Plugin manifest validation
- Tool schema introspection

### 0.1.0-alpha
- Initial release
- Dataverse connection management
- Plugin system with NuGet integration
- MCP tool execution framework
- OAuth device flow authentication
- Multi-platform support (macOS ARM64/x64, Windows x64, Linux x64)

## License

MIT License - See [LICENSE](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/LICENSE) file for details.

## Repository

**GitHub:** [https://github.com/tchinnin/dataverse-mcp-toolbox](https://github.com/tchinnin/dataverse-mcp-toolbox)

## Documentation

Full documentation available at: [Docs](https://github.com/tchinnin/dataverse-mcp-toolbox/tree/main/Docs)

- [Architecture Overview](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/04-Architecture.md)
- [Core Server Details](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/05-Core-Server.md)
- [MCP Bridge Details](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/06-MCP-Bridge.md)
- [Server Lifecycle](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/09-Server-Lifecycle.md)

## Support

For issues, feature requests, or contributions:
- **Issues:** [GitHub Issues](https://github.com/tchinnin/dataverse-mcp-toolbox/issues)
- **Discussions:** [GitHub Discussions](https://github.com/tchinnin/dataverse-mcp-toolbox/discussions)

## Related Packages

- **[DataverseMCPToolBox.Extensibility](https://www.nuget.org/packages/DataverseMCPToolBox.Extensibility/)** - SDK for building custom plugins
- **[DataverseMCPToolBox.WhoAmI](https://www.nuget.org/packages/DataverseMCPToolBox.WhoAmI/)** - Sample WhoAmI plugin demonstrating extensibility

## Author

**Théophile CHIN-NIN**
- GitHub: [@tchinnin](https://github.com/tchinnin)

---

**Note:** This package is primarily designed for use with the Dataverse MCP Toolbox VS Code Extension. Manual usage requires understanding of JSON-RPC 2.0 protocol, named pipes/Unix domain sockets, and Model Context Protocol concepts.
```

