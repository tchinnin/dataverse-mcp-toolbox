# Plugins Directory

This directory is where MCP plugins are automatically installed when using the VSCode extension.

## How it works

1. **Installation**: When you install a plugin from NuGet using the extension, the plugin files are downloaded to this folder
2. **Location**: This folder is located at `<extension-installation-path>/plugins/`
3. **Structure**: Each plugin is installed in its own subfolder named `<PackageId>.<Version>`

## Example Structure

```
plugins/
├── DataverseMCPToolBox.WhoAmI.1.0.0/
│   ├── WhoAmIPlugin.dll
│   └── DataverseMCPToolBox.Extensibility.dll
└── AnotherPlugin.1.2.3/
    └── ...
```

## Managing Plugins

- **Install**: Use the command `Dataverse MCP ToolBox: Install Plugin` or click the + icon in the MCP Plugins view
- **Uninstall**: Right-click on a plugin in the MCP Plugins view and select "Uninstall Plugin"
- **Refresh**: Click the refresh icon in the MCP Plugins view to reload all plugins

## Important Notes

- ⚠️ **Do not manually modify** files in this directory
- ✅ This folder is included in the packaged extension
- ✅ Plugins persist across VSCode sessions
- ✅ Each plugin installation is isolated in its own folder

## Bundled Plugins

On first activation, the extension automatically installs the following bundled plugins:
- **DataverseMCPToolBox.WhoAmI**: Provides WhoAmI functionality for Dataverse connections
