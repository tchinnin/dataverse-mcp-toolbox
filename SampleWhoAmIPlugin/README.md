# Sample WhoAmI Plugin for DataverseMCPToolBox

A sample plugin demonstrating the **DataverseMCPToolBox.Extensibility** framework by implementing a simple "Who Am I" tool for Dataverse.

## Overview

This plugin provides a single MCP tool (`who-am-i`) that retrieves information about the currently connected Dataverse user, including:
- User display name
- User ID (GUID)
- Business Unit ID (GUID)
- Organization ID (GUID)
- Environment URL

## Purpose

This sample demonstrates:
- ✅ How to create a plugin using the Extensibility framework
- ✅ Strongly-typed tool implementation with `McpToolBase<TInput, TOutput>`
- ✅ Proper use of the `[McpPlugin]` attribute
- ✅ Error handling with `ToolExecutionException`
- ✅ Logging to stderr (not stdout)
- ✅ Kebab-case tool naming convention
- ✅ Async-only operations with cancellation support
- ✅ Dataverse SDK integration via `IDataverseContext`

## Installation

### From Source

1. Build the project:
   ```bash
   cd SampleWhoAmIPlugin
   dotnet build --configuration Release
   ```

2. Pack as NuGet:
   ```bash
   dotnet pack --configuration Release
   ```

3. The package will be created at:
   ```
   bin/Release/TCH.DataverseMCPToolBox.Plugins.SampleWhoAmI.1.0.0.nupkg
   ```

### From NuGet (if published)

```bash
dotnet add package TCH.DataverseMCPToolBox.Plugins.SampleWhoAmI
```

## Usage

Once the plugin is loaded by the DataverseMCPToolBox server, it exposes the following tool:

### Tool: `who-am-i`

**Description:** Retrieves information about the currently connected Dataverse user

**Parameters:** None required

**Returns:**
```json
{
  "userDisplayName": "John Doe",
  "userId": "12345678-1234-1234-1234-123456789abc",
  "businessUnitId": "87654321-4321-4321-4321-cba987654321",
  "organizationId": "abcdef12-ab12-ab12-ab12-abcdef123456",
  "environmentUrl": "https://org.crm.dynamics.com",
  "message": "Successfully retrieved information for user 'John Doe'"
}
```

**Example MCP Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "who-am-i",
    "arguments": {}
  }
}
```

## Code Structure

```
SampleWhoAmIPlugin/
├── SampleWhoAmIPlugin.csproj    # Project file with NuGet dependencies
├── WhoAmIPlugin.cs              # Main plugin class and tool implementation
├── Models/
│   └── WhoAmIOutput.cs          # Output model for the tool
└── README.md                     # This file
```

## Key Components

### 1. Plugin Class: `WhoAmIPlugin`

The main plugin class decorated with `[McpPlugin]` attribute:

```csharp
[McpPlugin("sample-whoami", "1.0.0",
    Author = "TCH",
    Description = "Sample plugin demonstrating Dataverse user information retrieval")]
public class WhoAmIPlugin : PluginBase
{
    // Plugin lifecycle and tool registration
}
```

### 2. Tool Class: `WhoAmITool`

Strongly-typed tool implementation:

```csharp
public class WhoAmITool : McpToolBase<object, WhoAmIOutput>
{
    public WhoAmITool()
        : base("who-am-i", "Retrieves information about the currently connected Dataverse user...")
    {
    }

    protected override async Task<WhoAmIOutput> ExecuteAsync(
        object? parameters,
        IDataverseContext context,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### 3. Output Model: `WhoAmIOutput`

Data transfer object with PascalCase properties (converted to camelCase in JSON):

```csharp
public class WhoAmIOutput
{
    public string UserDisplayName { get; set; }
    public Guid UserId { get; set; }
    public Guid BusinessUnitId { get; set; }
    public Guid OrganizationId { get; set; }
    public string EnvironmentUrl { get; set; }
    public string Message { get; set; }
}
```

## Technical Details

### Dataverse Operations Used

1. **WhoAmIRequest**: Retrieves user ID, business unit ID, and organization ID
2. **RetrieveAsync**: Fetches the user's display name from the `systemuser` table

### Error Handling

- Structured errors using `ToolExecutionException`
- Graceful fallback if display name retrieval fails
- Proper cancellation support
- All errors logged to stderr

### Conventions Followed

- ✅ Tool name: `who-am-i` (kebab-case)
- ✅ Async operations with `CancellationToken`
- ✅ Logging to stderr only (`Console.Error.WriteLine`)
- ✅ PascalCase properties in C# → camelCase in JSON
- ✅ Proper disposal and lifecycle management

## Testing

To test this plugin:

1. Build and pack the plugin
2. Configure DataverseMCPToolBox to load the plugin
3. Connect to a Dataverse environment
4. Invoke the `who-am-i` tool via MCP

Expected result: Detailed information about the connected user

## Dependencies

- **DataverseMCPToolBox.Extensibility** (v0.1.0-alpha): Core extensibility framework
- **.NET 8.0**: Target framework
- **Microsoft.PowerPlatform.Dataverse.Client**: Included via Extensibility package

## License

MIT License - See LICENSE file in the root of the repository

## Contributing

This is a sample plugin for demonstration purposes. Feel free to use it as a template for your own plugins.

## Related Resources

- [DataverseMCPToolBox Repository](https://github.com/tchinnin/dataverse-mcp-toolbox)
- [Plugin Development Instructions](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/.github/instructions/DataverseMCPToolBoxPlugin.instructions.md)
- [Dataverse SDK Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
- [Model Context Protocol](https://modelcontextprotocol.io/)

## Support

For issues or questions:
1. Check the DataverseMCPToolBox main repository
2. Review the plugin development instructions
3. Open an issue on GitHub
