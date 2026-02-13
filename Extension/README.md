# Dataverse MCP ToolBox

> Manage Dataverse connections and expose powerful tools for GitHub Copilot through the Model Context Protocol (MCP).

## Overview

The **Dataverse MCP ToolBox** brings Microsoft Dataverse operations directly into GitHub Copilot and other AI assistants. Manage connections, install plugins, and execute Dataverse operations through natural language conversations.

## Features

- 🔌 **Automatic MCP Server Setup** - Downloads and configures the MCP server automatically
- 🔗 **Connection Management** - Manage multiple Dataverse environments with OAuth authentication
- 🧩 **Plugin System** - Install plugins from NuGet to extend functionality
- 🤖 **GitHub Copilot Integration** - Use Dataverse tools directly in Copilot conversations
- 🎯 **Activity Bar Integration** - Visual interface for managing connections and plugins
- 🔄 **Auto-Update** - Automatic updates from stable or prerelease channels

## Installation

1. Install this extension from the VS Code Marketplace
2. The extension will automatically:
   - Download the appropriate MCP server binaries for your platform (macOS, Windows, Linux)
   - Register the server in VS Code's MCP configuration
   - Make Dataverse tools available to GitHub Copilot

## Quick Start

### 1. Add a Dataverse Connection

1. Open the **Dataverse Connections** view in the Activity Bar (left sidebar)
2. Click the **"+"** icon to add a new connection
3. Enter your Dataverse environment details:
   - **Connection Name**: A friendly name (e.g., "Production")
   - **Environment URL**: Your Dataverse URL (e.g., `https://myorg.crm.dynamics.com`)
   - **Client ID**: Your Azure AD app registration client ID
   - **Tenant ID**: Your Azure AD tenant ID
4. Complete OAuth authentication in your browser
5. Set the connection as **Active** (star icon)

### 2. Use with GitHub Copilot

Once you have an active connection, use Dataverse operations in Copilot:

```
@github who am I in Dataverse?
@github list all account entities
@github create a contact with name "John Doe"
@github update the account with ID xxx
```

### 3. Install Plugins

1. Open the **MCP Plugins** view
2. Click **"Install Plugin from NuGet"**
3. Enter a NuGet package ID (e.g., `DataverseMCPToolBox.WhoAmIPlugin`)
4. The plugin tools will appear in the plugins list
5. Use the new tools in Copilot conversations

## Configuration

### Settings

- **Server Channel** (`dataverse.server.channel`):
  - `stable`: Production-ready releases only
  - `prerelease`: Early access to new features

- **Enforced Version** (`dataverse.server.enforcedVersion`):
  - Lock to a specific version (e.g., `0.2.0`)
  - Leave empty to always use the latest version

### MCP Configuration

The extension automatically manages the MCP configuration file:

- **macOS**: `~/Library/Application Support/Code/User/mcp.json`
- **Windows**: `%APPDATA%\Code\User\mcp.json`
- **Linux**: `~/.config/Code/User/mcp.json`

Use the **"Open MCP Configuration"** command to view or manually edit the file.

## Requirements

- **VS Code**: Version 1.102.0 or higher
- **GitHub Copilot**: Required for AI assistant features
- **Dataverse Environment**: Access to a Microsoft Dataverse environment
- **Azure AD App**: Registered app with Dataverse API permissions

### Azure AD App Registration

To connect to Dataverse, you need an Azure AD app registration with:

1. **Redirect URI**: `http://localhost` (Public client/native)
2. **API Permissions**:
   - Dynamics CRM / user_impersonation (Delegated)
3. **Authentication**: Enable public client flows

## Supported Platforms

- macOS (Intel and Apple Silicon)
- Windows 64-bit
- Linux 64-bit

The extension automatically downloads the correct binaries for your platform.

## Troubleshooting

### Server Not Starting

1. Check the **Output** panel → **Dataverse MCP ToolBox** channel
2. Verify the server is installed: Check **MCP Server Info** view
3. Try **"Restart MCP Server"** command

### Connection Issues

1. Verify your Environment URL is correct
2. Check Azure AD app permissions
3. Ensure redirect URI is configured as `http://localhost`
4. Try removing and re-adding the connection

### Plugin Installation Fails

1. Verify the NuGet package ID is correct
2. Check internet connectivity
3. Ensure the package is compatible (targets `DataverseMCPToolBox.Extensibility`)

### GitHub Copilot Not Finding Tools

1. Verify the MCP server is running (check **MCP Server Info**)
2. Ensure you have an **active connection** (star icon)
3. Try reloading VS Code window
4. Check the MCP configuration file is valid

## Commands

All commands are available in the Command Palette (`Cmd+Shift+P` / `Ctrl+Shift+P`):

- **Add Connection** - Create a new Dataverse connection
- **Remove Connection** - Delete a connection
- **Set as Active Connection** - Switch active connection
- **Refresh Connections** - Reload connection list
- **Show WhoAmI Info** - Display current user information
- **Install Plugin from NuGet** - Install a plugin package
- **Uninstall Plugin** - Remove an installed plugin
- **Call Tool** - Execute a tool with custom parameters
- **Start MCP Server** - Start the MCP server
- **Stop MCP Server** - Stop the MCP server
- **Restart MCP Server** - Restart the MCP server
- **Open MCP Configuration** - View mcp.json file
- **Re-register MCP Server** - Update server path in MCP config
- **Unregister MCP Server** - Remove server from MCP config

## Documentation

- [Full Documentation](https://github.com/tchinnin/dataverse-mcp-toolbox/tree/main/Docs)
- [Creating Custom Plugins](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/14-Creating-Plugins.md)
- [GitHub Issues](https://github.com/tchinnin/dataverse-mcp-toolbox/issues)

## License

MIT License - See [LICENSE](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/LICENSE) for details.

## Contributing

Contributions are welcome! Please see the [GitHub repository](https://github.com/tchinnin/dataverse-mcp-toolbox) for contribution guidelines.

---

**Enjoy using Dataverse with AI! 🚀**
