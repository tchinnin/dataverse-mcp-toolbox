# DataverseMCPToolBox.WhoAmI

[![NuGet](https://img.shields.io/nuget/v/DataverseMCPToolBox.WhoAmI.svg)](https://www.nuget.org/packages/DataverseMCPToolBox.WhoAmI/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Sample plugin demonstrating the **DataverseMCPToolBox.Extensibility** framework by implementing a "Who Am I" tool for Dataverse.

## Overview

This plugin provides a production-ready MCP tool (`who-am-i`) that retrieves comprehensive information about the currently authenticated Dataverse user:

- ✅ **User Display Name** - Full name of the authenticated user
- ✅ **User ID** - Unique identifier (GUID) of the user
- ✅ **Business Unit ID** - User's business unit GUID
- ✅ **Organization ID** - Dataverse organization GUID
- ✅ **Environment URL** - Full Dataverse environment URL
- ✅ **Confirmation Message** - Human-readable success message

## Purpose

This sample serves as a **reference implementation** demonstrating best practices for plugin development:

### Architecture Patterns
- ✅ Plugin class structure with `[McpPlugin]` attribute
- ✅ Strongly-typed tool implementation using `McpToolBase<TInput, TOutput>`
- ✅ Separation of concerns (plugin vs. tool classes)
- ✅ Proper lifecycle management with `InitializeAsync()` and `Dispose()`

### Dataverse Integration
- ✅ `IDataverseContext` usage for accessing service client
- ✅ WhoAmIRequest execution (SDK operation)
- ✅ Entity retrieval with QueryExpression
- ✅ Safe attribute value extraction with fallback

### MCP Compliance
- ✅ Kebab-case tool naming (`who-am-i`)
- ✅ JSON Schema generation for parameters (none required in this case)
- ✅ PascalCase C# properties → camelCase JSON output
- ✅ Structured error handling with `ToolExecutionException`

### Best Practices
- ✅ Comprehensive error handling with fallback logic
- ✅ Async-only operations with `CancellationToken` support
- ✅ Logging to stderr exclusively (never stdout)
- ✅ Human-readable success messages
- ✅ Detailed XML documentation for IntelliSense

## Installation

### Option 1: From NuGet (Recommended)

Install directly from NuGet.org:

```bash
# .NET CLI
dotnet add package DataverseMCPToolBox.WhoAmI

# Package Manager Console (Visual Studio)
Install-Package DataverseMCPToolBox.WhoAmI
```

### Option 2: Via VS Code Extension

1. Open **Dataverse MCP Toolbox** panel in VS Code
2. Navigate to **Plugins** section
3. Click **Install Plugin**
4. Enter package ID: `DataverseMCPToolBox.WhoAmI`
5. Click **Install**

The plugin will be automatically downloaded and loaded.

### Option 3: Build from Source

1. **Clone the repository:**
   ```bash
   git clone https://github.com/tchinnin/dataverse-mcp-toolbox.git
   cd dataverse-mcp-toolbox/SampleWhoAmIPlugin
   ```

2. **Build the project:**
   ```bash
   dotnet build --configuration Release
   ```

3. **Package as NuGet:**
   ```bash
   dotnet pack --configuration Release -o ./nupkg
   ```

4. **Locate the package:**
   ```
   nupkg/DataverseMCPToolBox.WhoAmI.1.1.0-alpha.nupkg
   ```

5. **Install locally:**
   - Copy `.nupkg` file to your plugin directory
   - Or install via: `dotnet nuget add source ./nupkg -n LocalSource`

## Usage

### Prerequisites

1. **Dataverse MCP Toolbox** Core Server must be running
2. An **active Dataverse connection** must be established
3. The **plugin must be loaded** by the Core Server

### Tool: `who-am-i`

**Description:** Retrieves information about the currently connected Dataverse user

**Tool Name:** `who-am-i` (kebab-case, MCP-compliant)

**Parameters:** None required (empty object `{}` or `null`)

**Returns:** `WhoAmIOutput` object with the following properties:

```typescript
interface WhoAmIOutput {
  userDisplayName: string;   // Full name of the user
  userId: string;            // User GUID
  businessUnitId: string;    // Business unit GUID
  organizationId: string;    // Organization GUID
  environmentUrl: string;    // Dataverse environment URL
  message: string;           // Confirmation message
}
```

**Example Response:**
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

### Invocation Examples

#### Via MCP Protocol (JSON-RPC)

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

#### Via GitHub Copilot (Natural Language)

In VS Code with GitHub Copilot:
```
@workspace Use the who-am-i tool to get the current user information
```

#### Via VS Code Extension UI

1. Open **Dataverse MCP Toolbox** panel
2. Navigate to **Tools** section
3. Find `who-am-i` tool
4. Click **Execute**
5. View results in output panel

## Code Structure

```
SampleWhoAmIPlugin/
├── WhoAmI.csproj               # Project file with package metadata
├── WhoAmIPlugin.cs             # Main plugin class and tool implementation
├── Models/
│   └── WhoAmIOutput.cs         # Output DTO with PascalCase properties
├── README.md                   # This documentation (included in NuGet)
└── nupkg/                      # Generated NuGet packages (not in repo)
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

### Dataverse SDK Operations

The plugin uses two primary Dataverse operations:

#### 1. WhoAmIRequest
```csharp
var whoAmIRequest = new WhoAmIRequest();
var whoAmIResponse = (WhoAmIResponse)await context.ServiceClient
    .ExecuteAsync(whoAmIRequest, cancellationToken);
```
Retrieves:
- `UserId` - Unique identifier of the authenticated user
- `BusinessUnitId` - User's business unit
- `OrganizationId` - Dataverse organization

#### 2. RetrieveAsync
```csharp
var user = await context.ServiceClient.RetrieveAsync(
    "systemuser",
    whoAmIResponse.UserId,
    new ColumnSet("fullname"),
    cancellationToken);
```
Fetches:
- `fullname` attribute from `systemuser` table
- Fallback to "Unknown User" if retrieval fails

### Error Handling Strategy

1. **Structured Exceptions**: All errors thrown as `ToolExecutionException` with:
   - Error code (e.g., `DATAVERSE_ERROR`)
   - User-friendly message
   - Additional context (details object)

2. **Graceful Degradation**: If display name retrieval fails:
   - Operation continues
   - Returns "Unknown User" as fallback
   - Logs warning to stderr

3. **Cancellation Support**: All async operations respect `CancellationToken`

4. **Logging**: All errors and warnings logged to stderr only

### Conventions Followed

✅ **Naming:**
- Tool name: `who-am-i` (kebab-case, enforced)
- C# properties: PascalCase (`UserDisplayName`)
- JSON output: camelCase (`userDisplayName`)

✅ **Async/Await:**
- All operations async with `Task<T>`
- CancellationToken passed to all SDK calls
- No blocking `.Result` or `.Wait()` calls

✅ **Logging:**
- Console.Error.WriteLine() for all logs
- Never write to stdout (reserved for data)

✅ **Resource Management:**
- Proper disposal patterns
- Lifecycle managed by Core Server

## Testing

### Manual Testing

1. **Install the plugin:**
   ```bash
   # From source
   cd SampleWhoAmIPlugin
   dotnet pack -c Release
   # Install via VS Code Extension UI
   ```

2. **Create a Dataverse connection:**
   - Open VS Code Extension
   - Add new connection
   - Authenticate with OAuth device flow

3. **Execute the tool:**
   - Via Extension UI: Click "Execute" on `who-am-i` tool
   - Via Copilot: Ask "Use who-am-i tool"
   - Via direct JSON-RPC call

4. **Verify output:**
   - Check user display name matches your account
   - Verify all GUIDs are valid
   - Confirm environment URL is correct

### Expected Results

**Success Case:**
```json
{
  "userDisplayName": "Your Name",
  "userId": "valid-guid",
  "businessUnitId": "valid-guid",
  "organizationId": "valid-guid",
  "environmentUrl": "https://your-org.crm.dynamics.com",
  "message": "Successfully retrieved information for user 'Your Name'"
}
```

**Error Case (No Connection):**
```json
{
  "error": {
    "code": "DATAVERSE_ERROR",
    "message": "Failed to retrieve user information",
    "details": { /* error context */ }
  }
}
```

## Dependencies

This plugin automatically includes:

- **DataverseMCPToolBox.Extensibility** (v0.1.0-alpha)
  - Extensibility framework
  - Base classes and interfaces
  - JSON Schema generation
- **.NET 8.0** - Target framework
- **Microsoft.PowerPlatform.Dataverse.Client** (v1.1.32)
  - Included transitively via Extensibility package
  - Dataverse SDK operations
- **NJsonSchema** (v11.0.2)
  - JSON Schema generation and validation
- **Newtonsoft.Json** (v13.0.3)
  - JSON serialization

## Version History

### 1.1.0-alpha (Current)
- Enhanced error messages with more context
- Improved fallback handling for display name retrieval
- Updated documentation with comprehensive examples
- Code cleanup and optimization

### 1.0.0-alpha
- Initial release
- Basic WhoAmI functionality
- Strongly-typed implementation with McpToolBase
- Complete Dataverse user information retrieval

## Project Metadata

- **Package ID**: DataverseMCPToolBox.WhoAmI
- **Version**: 1.1.0-alpha
- **Author**: Théophile CHIN-NIN
- **License**: MIT
- **Target Framework**: .NET 8.0
- **Language**: C# 12

## Use Cases

This plugin is useful for:

1. **Identity Verification**: Confirm which user account is currently authenticated
2. **Debugging**: Verify connection context during development
3. **Auditing**: Track which user performed operations
4. **Reference Implementation**: Learn plugin development patterns
5. **Testing**: Validate Dataverse connection and permissions

## License

MIT License - See [LICENSE](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/LICENSE) file for details.

## Documentation

Comprehensive guides:
- [Creating Plugins](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/14-Creating-Plugins.md)
- [Plugin Architecture](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/15-Plugin-Architecture.md)
- [Using Tools](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/12-Using-Tools.md)

## Repository

**GitHub**: [https://github.com/tchinnin/dataverse-mcp-toolbox](https://github.com/tchinnin/dataverse-mcp-toolbox)

## Support

For issues, questions, or feature requests:
- **Issues**: [GitHub Issues](https://github.com/tchinnin/dataverse-mcp-toolbox/issues)
- **Discussions**: [GitHub Discussions](https://github.com/tchinnin/dataverse-mcp-toolbox/discussions)

## Contributing

This is a sample plugin for demonstration purposes. Feel free to:
- Use it as a template for your own plugins
- Submit improvements via pull requests
- Report issues or suggest enhancements

See [CONTRIBUTING.md](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/CONTRIBUTING.md) for guidelines.

## Related Resources

### Official Documentation
- [Dataverse SDK Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [WhoAmI Message](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/reference/whoami)

### Related Packages
- **[DataverseMCPToolBox.Runtime](https://www.nuget.org/packages/DataverseMCPToolBox.Runtime/)** - Core Server and Bridge binaries
- **[DataverseMCPToolBox.Extensibility](https://www.nuget.org/packages/DataverseMCPToolBox.Extensibility/)** - Plugin development SDK

### Development Guides
- [Plugin Development Instructions](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/.github/instructions/DataverseMCPToolBoxPlugin.instructions.md)
- [Architecture Documentation](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/04-Architecture.md)

## Author

**Théophile CHIN-NIN**
- GitHub: [@tchinnin](https://github.com/tchinnin)

---

**Note**: This is an alpha release. APIs and behavior may change in future versions. Use in production environments at your own discretion.
