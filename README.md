# Dataverse MCP Toolbox

A comprehensive toolbox for Microsoft Dataverse operations, exposing tools through the Model Context Protocol (MCP) for use with AI assistants like GitHub Copilot.

## Features

- **Local MCP Server**: .NET-based server implementing the MCP specification
- **VS Code Extension**: Easy installation and management of the MCP server
- **Automatic MCP Configuration**: Automatically registers the server in VS Code's MCP configuration
- **Plugin System**: Extensible architecture for custom Dataverse operations
- **Connection Management**: Manage multiple Dataverse connections
- **AI Integration**: Use Dataverse tools directly from GitHub Copilot and other AI assistants

## Installation

1. Install the VS Code extension from the marketplace
2. The extension will automatically:
   - Download and install the MCP server
   - Register it in VS Code's MCP configuration (`~/Library/Application Support/Code/User/mcp.json` on macOS)
   - Make it available for GitHub Copilot and other AI assistants

## Usage

### With GitHub Copilot

Once installed, you can use Dataverse operations directly in your conversations with GitHub Copilot:

```
@copilot who am I in Dataverse?
@copilot list all entities in my Dataverse environment
@copilot create a new contact record
```

### Managing Connections

1. Open the Dataverse Connections view in VS Code
2. Click the "+" icon to add a new connection
3. Enter your Dataverse URL and authenticate
4. Set the connection as active

### Managing Plugins

- Install plugins from NuGet using the plugin manager
- View available tools in the plugins view
- Plugins extend the MCP server with custom Dataverse operations

## MCP Configuration

The extension automatically configures VS Code to use the Dataverse MCP server. The configuration is stored in:

- **macOS**: `~/Library/Application Support/Code/User/mcp.json`
- **Windows**: `%APPDATA%\Code\User\mcp.json`
- **Linux**: `~/.config/Code/User/mcp.json`

### Manual MCP Configuration Commands

- **Open MCP Configuration**: View and edit the mcp.json file
- **Re-register MCP Server**: Update the server path in the configuration
- **Unregister MCP Server**: Remove the server from the MCP configuration

## Architecture

- **Core**: .NET MCP server with JSON-RPC interface
- **Extensibility**: SDK for building custom plugins
- **Extension**: VS Code extension for distribution and management

## Development

See individual project READMEs for development setup:
- [Core Server](./Core/README.md)
- [Extensibility SDK](./Extensibility/README.md)
- [Sample Plugin](./SampleWhoAmIPlugin/README.md)

## License

See LICENSE file for details.