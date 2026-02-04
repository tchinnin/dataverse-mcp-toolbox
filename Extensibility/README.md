# DataverseMCPToolBox.Extensibility

[![NuGet](https://img.shields.io/nuget/v/DataverseMCPToolBox.Extensibility.svg)](https://www.nuget.org/packages/DataverseMCPToolBox.Extensibility/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Extensibility SDK for building MCP (Model Context Protocol) plugins that extend the DataverseMCPToolBox with custom Dataverse operations.

## Overview

The **DataverseMCPToolBox.Extensibility** package provides a comprehensive framework for developing plugins that expose MCP-compliant tools for Dataverse operations. Plugins are:

- Distributed as **NuGet packages** for easy installation and versioning
- **Dynamically loaded** by the Core Server at runtime
- **Isolated** with proper lifecycle management
- **Type-safe** with automatic JSON schema generation
- **MCP-compliant** with standardized tool interfaces

## Key Features

- **Plugin Architecture**: Implement `IPlugin` interface for complete lifecycle management (initialization and disposal)
- **Automatic Tool Discovery**: Two approaches:
  - Attribute-based: Decorate methods with `[McpTool]` for automatic registration
  - Manual registration: Override `GetTools()` for explicit tool registration
- **Strongly-Typed Tools**: Use `McpToolBase<TInput, TOutput>` for type-safe implementations with:
  - Automatic parameter deserialization (camelCase JSON → PascalCase C#)
  - Input validation against JSON Schema
  - Exception handling and error formatting
- **JSON Schema Generation**: Automatic schema generation from C# types using NJsonSchema
- **Dataverse Integration**: Direct access to:
  - Authenticated `IOrganizationServiceAsync2` service client
  - Connection metadata (URL, connection ID)
  - Cancellation tokens for async operations
- **MCP Compliance**: 
  - Kebab-case naming enforcement (e.g., `create-record`, `list-entities`)
  - Standardized error codes and messages
  - Structured error details
- **Development Experience**:
  - IntelliSense support with XML documentation
  - Helper utilities (SchemaGenerator)
  - Comprehensive examples and samples

## Quick Start

### 1. Create a Plugin Class

```csharp
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;

[McpPlugin("my-dataverse-plugin", "1.0.0", 
    Author = "Your Name",
    Description = "Custom Dataverse operations plugin")]
public class MyDataversePlugin : PluginBase
{
    [McpTool("Lists all user accounts in the Dataverse organization")]
    public async Task<object> ListUsers(IDataverseContext context, CancellationToken cancellationToken)
    {
        var query = new QueryExpression("systemuser")
        {
            ColumnSet = new ColumnSet("fullname", "internalemailaddress")
        };

        var results = await context.ServiceClient.RetrieveMultipleAsync(query, cancellationToken);
        
        return results.Entities.Select(e => new 
        {
            Name = e.GetAttributeValue<string>("fullname"),
            Email = e.GetAttributeValue<string>("internalemailaddress")
        }).ToList();
    }
}
```

### 2. Using Strongly-Typed Tools

```csharp
public class CreateAccountInput
{
    public string Name { get; set; }
    public string? Email { get; set; }
}

public class CreateAccountOutput
{
    public Guid AccountId { get; set; }
    public string Name { get; set; }
}

[McpPlugin("account-plugin", "1.0.0")]
public class AccountPlugin : PluginBase
{
    public class CreateAccountTool : McpToolBase<CreateAccountInput, CreateAccountOutput>
    {
        public CreateAccountTool() 
            : base("create-account", "Creates a new account record in Dataverse")
        {
        }

        protected override async Task<CreateAccountOutput> ExecuteAsync(
            CreateAccountInput parameters,
            IDataverseContext context,
            CancellationToken cancellationToken)
        {
            var account = new Entity("account");
            account["name"] = parameters.Name;
            
            if (!string.IsNullOrEmpty(parameters.Email))
                account["emailaddress1"] = parameters.Email;

            var accountId = await context.ServiceClient.CreateAsync(account, cancellationToken);

            return new CreateAccountOutput
            {
                AccountId = accountId,
                Name = parameters.Name
            };
        }
    }

    public override IEnumerable<IMcpTool> GetTools()
    {
        yield return new CreateAccountTool();
    }
}
```

## Key Interfaces

### IPlugin
Defines plugin lifecycle: `InitializeAsync(IServiceProvider)` and `Dispose()`

### IToolProvider
Exposes tools via `GetTools()` method

### IDataverseContext
Provides:
- `IOrganizationServiceAsync2 ServiceClient` - Authenticated Dataverse client
- `string ConnectionId` - Connection identifier
- `string OrganizationUrl` - Organization URL
- `CancellationToken CancellationToken` - Cancellation support

### IMcpTool
Defines MCP tool contract:
- `string Name` - Kebab-case tool name (e.g., "list-entities")
- `string Description` - Human-readable description
- `JsonSchema InputSchema` - Parameter validation schema
- `Task<ToolExecutionResult> ExecuteAsync(...)` - Execution method

## Attributes

### [McpPlugin]
Marks a class as a plugin with metadata:
```csharp
[McpPlugin("plugin-name", "1.0.0", 
    Author = "Author Name",
    Description = "Plugin description")]
```

### [McpTool]
Marks a method in `PluginBase` as a tool:
```csharp
[McpTool("Description of what the tool does")]
public async Task<object> ToolMethod(IDataverseContext context) { ... }
```

## Base Classes

### PluginBase
Abstract base class with automatic tool discovery from `[McpTool]` decorated methods.

### McpToolBase<TInput, TOutput>
Generic base class for strongly-typed tools with automatic:
- Parameter deserialization (camelCase JSON)
- Schema generation from `TInput` type
- Schema validation
- Error handling
- Kebab-case name enforcement

## Naming Conventions

### Tool Names
- **MUST** be in kebab-case format: `list-entities`, `create-record`, `whoami`
- Enforced at construction time in `McpToolBase`
- Invalid names throw `ArgumentException`

### Parameter Serialization
- C# properties use `PascalCase`
- JSON parameters use `camelCase` (auto-converted)
- Example: `public string UserName { get; set; }` → `"userName"` in JSON

## Error Handling

### ToolExecutionException
Throw this exception to return structured errors:
```csharp
throw new ToolExecutionException(
    "ENTITY_NOT_FOUND",
    "The specified entity does not exist",
    new { entityId = id });
```

### Error Codes
Standard error codes:
- `VALIDATION_ERROR` - Invalid input parameters
- `EXECUTION_ERROR` - General execution failure
- `DATAVERSE_ERROR` - Dataverse SDK error
- `AUTHORIZATION_ERROR` - Permission denied
- `OPERATION_CANCELLED` - Cancelled by user/timeout

## Plugin Discovery

Plugins are discovered via assembly scanning:
```csharp
var assemblies = LoadPluginAssemblies();
var manifests = PluginManifest.DiscoverPlugins(assemblies);
```

## Installation

### Via NuGet Package Manager
```bash
dotnet add package DataverseMCPToolBox.Extensibility
```

### Via .NET CLI
```bash
dotnet nuget install DataverseMCPToolBox.Extensibility
```

### Package Manager Console (Visual Studio)
```powershell
Install-Package DataverseMCPToolBox.Extensibility
```

## Requirements

- **.NET 8.0** or later
- **C# 12** or later (for latest language features)
- **NuGet Package Manager**

## Dependencies

This SDK automatically includes:
- **Microsoft.PowerPlatform.Dataverse.Client** (v1.1.32) - Official Dataverse SDK
- **NJsonSchema** (v11.0.2) - JSON Schema generation and validation
- **Newtonsoft.Json** (v13.0.3) - JSON serialization with camelCase support

## Plugin Distribution

### Building and Packaging

1. **Create your plugin project** targeting `net8.0`:
   ```bash
   dotnet new classlib -n MyDataversePlugin -f net8.0
   cd MyDataversePlugin
   dotnet add package DataverseMCPToolBox.Extensibility
   ```

2. **Implement your plugin** (see Quick Start examples below)

3. **Build in Release mode**:
   ```bash
   dotnet build --configuration Release
   ```

4. **Package as NuGet**:
   ```bash
   dotnet pack --configuration Release
   ```

5. **Publish your plugin**:
   - **Public**: Publish to [NuGet.org](https://www.nuget.org/)
   - **Private**: Use Azure Artifacts, GitHub Packages, or private NuGet feed
   - **Local**: Place `.nupkg` file in plugin directory

### Plugin Installation (End Users)

Users install your plugin via the VS Code Extension:
1. Open Dataverse MCP Toolbox panel
2. Navigate to Plugins section
3. Click "Install Plugin"
4. Enter package ID: `YourPluginName`
5. Plugin is automatically downloaded, extracted, and loaded

## Example Plugin Structure

```
MyDataversePlugin/
├── MyDataversePlugin.csproj      # Project file with package metadata
├── README.md                      # Plugin documentation (included in NuGet)
├── MyPlugin.cs                    # Main plugin class (PluginBase implementation)
├── Tools/
│   ├── ListEntitiesTool.cs       # Individual tool implementations
│   ├── CreateRecordTool.cs
│   └── QueryDataTool.cs
├── Models/
│   ├── ListEntitiesInput.cs      # Input DTOs (PascalCase properties)
│   ├── ListEntitiesOutput.cs     # Output DTOs
│   ├── CreateRecordInput.cs
│   └── CreateRecordOutput.cs
└── Helpers/                       # Optional utility classes
    └── DataverseHelper.cs
```

## Best Practices

- ✅ **Use kebab-case** for all tool names (enforced by `McpToolBase`)
- ✅ **PascalCase** for C# properties, auto-converted to **camelCase** in JSON
- ✅ **Implement IDisposable** properly if managing resources
- ✅ **Use CancellationToken** for all async operations
- ✅ **Throw ToolExecutionException** for structured errors
- ✅ **Log to stderr** only, never to stdout
- ✅ **Include README.md** in your NuGet package
- ✅ **Version your plugin** semantically (e.g., 1.0.0, 1.1.0, 2.0.0)
- ✅ **Document parameters** with XML comments for IntelliSense

## Troubleshooting

### Common Issues

**"Tool name must be in kebab-case format"**
- Ensure tool names use hyphens: `list-entities`, not `ListEntities` or `list_entities`

**"Cannot load plugin assembly"**
- Verify your plugin targets `net8.0`
- Check all dependencies are compatible

**"JSON Schema validation failed"**
- Ensure input models have public getters/setters
- Check property types are JSON-serializable

**"Dataverse operation failed"**
- Verify connection is active before executing operations
- Check entity logical names and attribute names
- Review Dataverse SDK error messages

## Version History

### 0.1.0-alpha (Current)
- Initial release of Extensibility SDK
- Core interfaces: `IPlugin`, `IMcpTool`, `IDataverseContext`
- Base classes: `PluginBase`, `McpToolBase<TInput, TOutput>`
- Attributes: `[McpPlugin]`, `[McpTool]`
- JSON Schema generation with NJsonSchema
- Automatic camelCase/PascalCase conversion
- Standard error handling with `ToolExecutionException`

## License

MIT License - See [LICENSE](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/LICENSE) file for details.

## Documentation

Comprehensive guides available:
- [Creating Plugins](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/14-Creating-Plugins.md)
- [Plugin Architecture](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/Docs/15-Plugin-Architecture.md)
- [API Reference](https://github.com/tchinnin/dataverse-mcp-toolbox/tree/main/Docs)

## Repository

**GitHub:** [https://github.com/tchinnin/dataverse-mcp-toolbox](https://github.com/tchinnin/dataverse-mcp-toolbox)

## Support

For questions, issues, or feature requests:
- **Issues:** [GitHub Issues](https://github.com/tchinnin/dataverse-mcp-toolbox/issues)
- **Discussions:** [GitHub Discussions](https://github.com/tchinnin/dataverse-mcp-toolbox/discussions)

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request with clear description

See [CONTRIBUTING.md](https://github.com/tchinnin/dataverse-mcp-toolbox/blob/main/CONTRIBUTING.md) for guidelines.

## Related Packages

- **[DataverseMCPToolBox.Runtime](https://www.nuget.org/packages/DataverseMCPToolBox.Runtime/)** - Core Server and MCP Bridge binaries
- **[DataverseMCPToolBox.WhoAmI](https://www.nuget.org/packages/DataverseMCPToolBox.WhoAmI/)** - Sample plugin implementation

## Author

**Théophile CHIN-NIN**
- GitHub: [@tchinnin](https://github.com/tchinnin)
