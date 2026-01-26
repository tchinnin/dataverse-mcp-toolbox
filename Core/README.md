# DataverseMCPToolBox.Server

MCP (Model Context Protocol) server for Dataverse operations. Provides a JSON-RPC interface for managing Dataverse connections and executing plugins.

## Overview

This package contains platform-specific self-contained executables of the Dataverse MCP Server. The server communicates via stdin/stdout using JSON-RPC 2.0 protocol and is designed to be spawned as a child process by the VS Code extension or other clients.

## Supported Platforms

This package includes binaries for:
- **macOS ARM64** (Apple Silicon) - `runtimes/osx-arm64/native/`
- **macOS x64** (Intel) - `runtimes/osx-x64/native/`
- **Windows x64** - `runtimes/win-x64/native/`
- **Linux x64** - `runtimes/linux-x64/native/`

All binaries are self-contained and do not require .NET runtime to be installed on the target machine.

## Usage

This package is typically consumed automatically by the **Dataverse MCP ToolBox VS Code Extension**. The extension will:

1. Download the appropriate platform binary from this NuGet package
2. Extract it to the extension's global storage
3. Spawn the server process and communicate via JSON-RPC

### Manual Usage (Advanced)

If you want to use the server directly:

1. Extract the appropriate platform binary from the package
2. Make it executable (Unix systems): `chmod +x DataverseMCPToolBox`
3. Run the executable - it will listen on stdin/stdout for JSON-RPC messages

**Example JSON-RPC request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "CreateConnection",
  "params": {
    "request": {
      "environmentUrl": "https://org.crm.dynamics.com",
      "connectionName": "MyConnection"
    }
  }
}
```

## Features

### Connection Management
- Create Dataverse connections with OAuth device flow authentication
- Test connection validity
- Retrieve organization details
- Close connections

### Plugin System
- Load plugins from NuGet packages
- Execute MCP tools provided by plugins
- Hot-reload plugins without restarting the server

### Available RPC Methods

- `CreateConnection` - Establish a new Dataverse connection
- `TestConnection` - Verify connection is valid
- `GetOrganizationDetails` - Get organization information
- `GetWhoAmI` - Get current user information
- `CloseConnection` - Close a specific connection
- `CloseAllConnections` - Close all active connections
- `SetPluginDirectory` - Configure plugin installation directory
- `InstallPlugin` - Install a plugin from NuGet
- `UninstallPlugin` - Remove an installed plugin
- `ReloadPlugins` - Reload all plugins
- `ListPlugins` - Get list of installed plugins
- `ListTools` - Get list of available MCP tools
- `CallTool` - Execute an MCP tool

## Communication Protocol

The server uses **JSON-RPC 2.0** over stdin/stdout:

- **stdin**: Receives JSON-RPC requests
- **stdout**: Sends JSON-RPC responses (formatted with Content-Length headers)
- **stderr**: Server logs and debug information

### Message Format

Messages use the Language Server Protocol header format:
```
Content-Length: <length>\r\n\r\n
<JSON-RPC message>
```

## Requirements

- No external dependencies required (self-contained executables)
- Windows, macOS, or Linux operating system
- For Dataverse connections: Valid Microsoft 365/Dataverse credentials

## Version History

### 0.1.0-alpha
- Initial release
- Support for Dataverse connection management
- Plugin system with NuGet integration
- MCP tool execution framework
- OAuth device flow authentication
- Multi-platform support (macOS ARM64/x64, Windows x64, Linux x64)

## License

MIT License - See LICENSE file for details

## Repository

GitHub: [https://github.com/tchinnin/dataverse-mcp-toolbox](https://github.com/tchinnin/dataverse-mcp-toolbox)

## Support

For issues, feature requests, or contributions, please visit the GitHub repository.

## Related Packages

- **DataverseMCPToolBox.Extensibility** - SDK for building plugins
- **DataverseMCPToolBox.WhoAmI** - Sample WhoAmI plugin

## Author

Théophile CHIN-NIN

---

**Note**: This package is intended for use with the Dataverse MCP ToolBox VS Code Extension. Manual usage requires understanding of JSON-RPC protocol and Model Context Protocol concepts.
bool isValid = connectionService.TestConnection(result.ConnectionId);
```

