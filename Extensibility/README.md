# DataverseMCPToolBox.Extensibility

SDK for building MCP (Model Context Protocol) plugins that extend the DataverseMCPToolBox server with custom Dataverse operations.

## Overview

The **DataverseMCPToolBox.Extensibility** package provides interfaces, base classes, attributes, and utilities for developing plugins that expose MCP tools for Dataverse operations. Plugins are distributed as NuGet packages and dynamically loaded by the DataverseMCPToolBox server at runtime.

## Key Features

- **Plugin Architecture**: Implement `IPlugin` interface for lifecycle management
- **Automatic Tool Discovery**: Decorate methods with `[McpTool]` for automatic registration
- **Strongly-Typed Tools**: Use `McpToolBase<TInput, TOutput>` for type-safe tool implementations
- **JSON Schema Generation**: Automatic schema generation from C# types using NJsonSchema
- **Dataverse Integration**: Direct access to authenticated `IOrganizationServiceAsync2` instances
- **MCP Compliance**: Kebab-case naming enforcement and standard error handling

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

## Dependencies

- **.NET 8.0** - Target framework
- **Microsoft.PowerPlatform.Dataverse.Client** - Dataverse SDK
- **NJsonSchema** - JSON Schema generation and validation
- **Newtonsoft.Json** - JSON serialization with camelCase support

## Distribution

1. Build plugin project targeting `net8.0`
2. Package as NuGet: `dotnet pack`
3. Publish to NuGet.org or private feed
4. Users install via: `dotnet add package YourPluginName`
5. DataverseMCPToolBox server auto-discovers plugins in configured directories

## Example Plugin Structure

```
MyDataversePlugin/
├── MyDataversePlugin.csproj
├── MyPlugin.cs (PluginBase implementation)
├── Tools/
│   ├── ListEntitiesT ool.cs
│   ├── CreateRecordTool.cs
│   └── QueryDataTool.cs
├── Models/
│   ├── ListEntitiesInput.cs
│   └── CreateRecordInput.cs
└── README.md
```

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Submit issues and pull requests to the [DataverseMCPToolBox repository](https://github.com/tchinnin/dataverse-mcp-toolbox).

## Support

For questions and support, open an issue on the GitHub repository.
