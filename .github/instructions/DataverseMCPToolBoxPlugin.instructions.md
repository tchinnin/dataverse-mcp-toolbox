# DataverseMCPToolBox Plugin Development Instructions

## Overview

These instructions guide AI agents to develop plugins for **DataverseMCPToolBox** using the **DataverseMCPToolBox.Extensibility** NuGet package. Plugins extend the MCP server with custom Dataverse operations exposed as MCP tools that can be discovered and invoked by AI assistants like GitHub Copilot.

## Critical Architecture Principles

### 1. Plugin System Architecture
- **Plugins are NuGet packages** containing classes that implement `IPlugin` interface
- **Tools are discovered automatically** from `[McpTool]` decorated methods or explicitly returned by `GetTools()`
- **Tools expose Dataverse operations** through the MCP protocol to AI assistants
- **Plugins run in the same process** as the DataverseMCPToolBox server (shared AppDomain)
- **All operations are async-only** - synchronous methods are not supported

### 2. Required NuGet Package
```xml
<PackageReference Include="DataverseMCPToolBox.Extensibility" Version="0.1.0-alpha" />
```

This package provides:
- `IPlugin`, `IToolProvider`, `IDataverseContext` interfaces
- `IMcpTool` interface for tool definition
- `PluginBase` abstract class with automatic tool discovery
- `McpToolBase<TInput, TOutput>` for strongly-typed tools
- `[McpPlugin]` and `[McpTool]` attributes
- Helper classes for schema generation and error handling

## Plugin Development Patterns

### Pattern 1: Attribute-Based Plugin (Recommended for Simple Plugins)

Use `PluginBase` with `[McpTool]` decorated methods for automatic tool discovery:

```csharp
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

[McpPlugin("entity-operations", "1.0.0",
    Author = "Your Name",
    Description = "Basic entity CRUD operations for Dataverse")]
public class EntityOperationsPlugin : PluginBase
{
    // Tool name will be "list-entities" (auto-converted from method name)
    [McpTool("Lists all available entity logical names in the organization")]
    public async Task<object> ListEntities(IDataverseContext context, CancellationToken cancellationToken)
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = (RetrieveAllEntitiesResponse)await context.ServiceClient
            .ExecuteAsync(request, cancellationToken);

        return response.EntityMetadata
            .Select(e => new { e.LogicalName, e.DisplayName?.UserLocalizedLabel?.Label })
            .OrderBy(e => e.LogicalName)
            .ToList();
    }

    // Explicit tool name via attribute
    [McpTool("Retrieves a single record by entity name and ID", Name = "get-record")]
    public async Task<object> GetRecord(
        string entityName,
        string recordId,
        IDataverseContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(recordId, out var id))
            throw new ToolExecutionException("INVALID_ID", "Record ID must be a valid GUID");

        var entity = await context.ServiceClient.RetrieveAsync(
            entityName, 
            id, 
            new ColumnSet(true), 
            cancellationToken);

        return new
        {
            id = entity.Id,
            logicalName = entity.LogicalName,
            attributes = entity.Attributes.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString())
        };
    }
}
```

**Key Points for Pattern 1:**
- Inherit from `PluginBase`
- Decorate class with `[McpPlugin(name, version)]`
- Decorate public methods with `[McpTool(description)]`
- Method name converted to kebab-case automatically (or specify `Name` in attribute)
- Method signature must include `IDataverseContext` parameter
- Optional `CancellationToken` parameter for cancellation support
- Return `Task<object>` - result will be serialized to JSON
- Other parameters become tool input parameters (camelCase in JSON)

### Pattern 2: Strongly-Typed Tools (Recommended for Complex Tools)

Use `McpToolBase<TInput, TOutput>` for type-safe parameter handling:

```csharp
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;
using DataverseMCPToolBox.Extensibility.Exceptions;
using Microsoft.Xrm.Sdk;

// Define input/output models with PascalCase properties
public class CreateRecordInput
{
    public string EntityName { get; set; }
    public Dictionary<string, object> Attributes { get; set; }
}

public class CreateRecordOutput
{
    public Guid RecordId { get; set; }
    public string EntityName { get; set; }
    public string Message { get; set; }
}

// Strongly-typed tool implementation
public class CreateRecordTool : McpToolBase<CreateRecordInput, CreateRecordOutput>
{
    public CreateRecordTool()
        : base("create-record", "Creates a new record in Dataverse with the specified attributes")
    {
    }

    protected override async Task<CreateRecordOutput> ExecuteAsync(
        CreateRecordInput parameters,
        IDataverseContext context,
        CancellationToken cancellationToken)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(parameters.EntityName))
            throw new ToolExecutionException("VALIDATION_ERROR", "EntityName is required");

        if (parameters.Attributes == null || parameters.Attributes.Count == 0)
            throw new ToolExecutionException("VALIDATION_ERROR", "At least one attribute must be provided");

        // Build entity
        var entity = new Entity(parameters.EntityName);
        foreach (var attr in parameters.Attributes)
        {
            entity[attr.Key] = attr.Value;
        }

        // Create record
        try
        {
            var recordId = await context.ServiceClient.CreateAsync(entity, cancellationToken);

            return new CreateRecordOutput
            {
                RecordId = recordId,
                EntityName = parameters.EntityName,
                Message = $"Record created successfully with ID {recordId}"
            };
        }
        catch (Exception ex)
        {
            throw new ToolExecutionException(
                "DATAVERSE_ERROR",
                $"Failed to create {parameters.EntityName} record: {ex.Message}",
                new { entityName = parameters.EntityName },
                ex);
        }
    }
}

// Plugin that registers the tool
[McpPlugin("record-management", "1.0.0",
    Author = "Your Name",
    Description = "Advanced record management operations")]
public class RecordManagementPlugin : PluginBase
{
    public override IEnumerable<IMcpTool> GetTools()
    {
        yield return new CreateRecordTool();
        // Add more tools here
    }
}
```

**Key Points for Pattern 2:**
- Create input/output model classes with `PascalCase` properties
- JSON will use `camelCase` (auto-converted)
- Inherit from `McpToolBase<TInput, TOutput>`
- Constructor enforces kebab-case tool name
- Override `ExecuteAsync(TInput, IDataverseContext, CancellationToken)`
- JSON Schema auto-generated from `TInput` type
- Type-safe parameter access
- Automatic validation against schema

### Pattern 3: Hybrid Approach

Combine both patterns in the same plugin:

```csharp
[McpPlugin("advanced-dataverse", "1.0.0")]
public class AdvancedDataversePlugin : PluginBase
{
    // Simple tool via attribute
    [McpTool("Gets the current user's information")]
    public async Task<object> WhoAmI(IDataverseContext context, CancellationToken cancellationToken)
    {
        var response = (WhoAmIResponse)await context.ServiceClient
            .ExecuteAsync(new WhoAmIRequest(), cancellationToken);

        return new
        {
            userId = response.UserId,
            businessUnitId = response.BusinessUnitId,
            organizationId = response.OrganizationId
        };
    }

    // Complex tools added explicitly
    public override IEnumerable<IMcpTool> GetTools()
    {
        // Get tools from [McpTool] methods
        var attributeTools = base.GetTools();

        // Add strongly-typed tools
        var customTools = new IMcpTool[]
        {
            new CreateRecordTool(),
            new UpdateRecordTool(),
            new DeleteRecordTool()
        };

        return attributeTools.Concat(customTools);
    }
}
```

## Mandatory Conventions

### 1. Tool Naming: kebab-case ONLY
- **REQUIRED**: All tool names MUST be in kebab-case format
- **Valid**: `list-entities`, `create-record`, `whoami`, `get-user-by-id`
- **Invalid**: `listEntities`, `CreateRecord`, `WHOAMI`, `Get_User`
- **Enforced**: `McpToolBase` constructor validates and throws `ArgumentException`
- **Auto-conversion**: `PluginBase` converts method names to kebab-case

### 2. Property Naming: PascalCase → camelCase
- **C# Models**: Use `PascalCase` for all properties
  ```csharp
  public class MyInput
  {
      public string EntityName { get; set; }  // PascalCase in C#
      public int MaxResults { get; set; }
  }
  ```
- **JSON Parameters**: Automatically converted to `camelCase`
  ```json
  {
      "entityName": "account",    // camelCase in JSON
      "maxResults": 100
  }
  ```
- **Consistent with server**: Matches DataverseMCPToolBox JSON-RPC conventions

### 3. Async-Only Operations
- All tool execution methods MUST return `Task` or `Task<T>`
- Synchronous methods are NOT supported
- Always include `CancellationToken cancellationToken` parameter
- Respect cancellation for long-running operations:
  ```csharp
  // Check cancellation periodically
  cancellationToken.ThrowIfCancellationRequested();
  
  // Pass to Dataverse SDK methods
  await context.ServiceClient.CreateAsync(entity, cancellationToken);
  ```

## Error Handling Best Practices

### 1. Use ToolExecutionException for Structured Errors

```csharp
throw new ToolExecutionException(
    "ERROR_CODE",           // Machine-readable code
    "Human readable message", // User-friendly message
    new { detail1 = value }  // Optional additional context
);
```

**Standard Error Codes:**
- `VALIDATION_ERROR` - Invalid input parameters
- `DATAVERSE_ERROR` - Dataverse SDK operation failed
- `AUTHORIZATION_ERROR` - Permission denied
- `ENTITY_NOT_FOUND` - Record doesn't exist
- `OPERATION_CANCELLED` - Cancelled by user/timeout
- `EXECUTION_ERROR` - General execution failure

### 2. Return Errors, Don't Crash

For `McpToolBase<TInput, TOutput>`, exceptions are caught and returned as `ToolExecutionResult.Failure()`. For `PluginBase` methods, wrap risky operations:

```csharp
[McpTool("Deletes a record safely")]
public async Task<object> DeleteRecord(
    string entityName, 
    string recordId,
    IDataverseContext context,
    CancellationToken cancellationToken)
{
    try
    {
        if (!Guid.TryParse(recordId, out var id))
            throw new ToolExecutionException("VALIDATION_ERROR", "Invalid GUID format");

        await context.ServiceClient.DeleteAsync(entityName, id, cancellationToken);
        
        return new { success = true, message = "Record deleted" };
    }
    catch (ToolExecutionException)
    {
        throw; // Re-throw structured errors
    }
    catch (Exception ex)
    {
        throw new ToolExecutionException(
            "DATAVERSE_ERROR",
            $"Failed to delete record: {ex.Message}",
            new { entityName, recordId },
            ex);
    }
}
```

### 3. Log Errors to stderr

```csharp
Console.Error.WriteLine($"[{GetType().Name}] Error in tool 'create-record': {ex.Message}");
```

**NEVER** log to stdout - it's reserved for JSON-RPC messages.

## Accessing Dataverse Services

### IDataverseContext Provides

```csharp
public interface IDataverseContext
{
    IOrganizationServiceAsync2 ServiceClient { get; }  // Authenticated Dataverse client
    string ConnectionId { get; }                       // Connection identifier
    string OrganizationUrl { get; }                    // Org URL (e.g., https://org.crm.dynamics.com)
    CancellationToken CancellationToken { get; }       // Execution cancellation token
}
```

### Common Dataverse Operations

```csharp
// Retrieve single record
var entity = await context.ServiceClient.RetrieveAsync(
    "account", 
    accountId, 
    new ColumnSet("name", "revenue"), 
    cancellationToken);

// Query multiple records
var query = new QueryExpression("contact")
{
    ColumnSet = new ColumnSet(true),
    Criteria = new FilterExpression
    {
        Conditions =
        {
            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
        }
    },
    TopCount = 100
};
var results = await context.ServiceClient.RetrieveMultipleAsync(query, cancellationToken);

// Create record
var newEntity = new Entity("account");
newEntity["name"] = "Contoso";
var id = await context.ServiceClient.CreateAsync(newEntity, cancellationToken);

// Update record
var updateEntity = new Entity("account", accountId);
updateEntity["revenue"] = new Money(1000000);
await context.ServiceClient.UpdateAsync(updateEntity, cancellationToken);

// Delete record
await context.ServiceClient.DeleteAsync("account", accountId, cancellationToken);

// Execute request
var whoami = new WhoAmIRequest();
var response = (WhoAmIResponse)await context.ServiceClient.ExecuteAsync(whoami, cancellationToken);
```

## Plugin Lifecycle

### 1. Initialization
```csharp
public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
{
    await base.InitializeAsync(serviceProvider, cancellationToken);
    
    // Custom initialization
    // - Resolve dependencies from serviceProvider
    // - Load configuration
    // - Initialize caches
    
    Console.Error.WriteLine($"[{GetType().Name}] Plugin initialized");
}
```

### 2. Tool Discovery
- Called once after initialization
- `GetTools()` returns all tools to register with MCP server
- Tools are cached - don't create new instances on each call

### 3. Tool Execution
- Tools invoked via MCP `tools/call` messages
- Each execution receives fresh `IDataverseContext` with authenticated client
- Multiple tools can execute concurrently

### 4. Disposal
```csharp
public override void Dispose()
{
    // Clean up resources
    // - Close connections
    // - Dispose caches
    // - Release unmanaged resources
    
    base.Dispose();
}
```

## Project Structure Template

```
MyDataversePlugin/
├── MyDataversePlugin.csproj
│   └── PackageReference: DataverseMCPToolBox.Extensibility
├── MyPlugin.cs                    # Main plugin class
├── Tools/                          # Strongly-typed tool implementations
│   ├── CreateRecordTool.cs
│   ├── UpdateRecordTool.cs
│   └── QueryDataTool.cs
├── Models/                         # Input/Output DTOs
│   ├── CreateRecordInput.cs
│   ├── CreateRecordOutput.cs
│   └── QueryParameters.cs
├── Helpers/                        # Shared utilities
│   └── DataverseHelpers.cs
└── README.md                       # Plugin documentation
```

## Publishing Plugin as NuGet Package

### 1. Update .csproj Metadata
```xml
<PropertyGroup>
    <PackageId>MyCompany.DataverseMCPToolBox.Plugins.EntityOperations</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Entity CRUD operations plugin for DataverseMCPToolBox</Description>
    <PackageTags>dataverse;mcp;plugin;entity;crud</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/yourorg/your-plugin</RepositoryUrl>
</PropertyGroup>
```

### 2. Build and Pack
```bash
dotnet build --configuration Release
dotnet pack --configuration Release
```

### 3. Publish to NuGet
```bash
dotnet nuget push bin/Release/MyCompany.DataverseMCPToolBox.Plugins.EntityOperations.1.0.0.nupkg \
    --api-key YOUR_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

## Common Pitfalls to Avoid

❌ **DON'T: Use stdout for logging**
```csharp
Console.WriteLine("Log message"); // Corrupts JSON-RPC!
```
✅ **DO: Use stderr**
```csharp
Console.Error.WriteLine("[MyPlugin] Log message");
```

❌ **DON'T: Use camelCase or PascalCase for tool names**
```csharp
public CreateRecordTool() : base("CreateRecord", "...") // Invalid!
```
✅ **DO: Use kebab-case**
```csharp
public CreateRecordTool() : base("create-record", "...") // Valid
```

❌ **DON'T: Ignore cancellation tokens**
```csharp
await context.ServiceClient.RetrieveMultipleAsync(query); // Missing cancellation!
```
✅ **DO: Pass cancellation tokens**
```csharp
await context.ServiceClient.RetrieveMultipleAsync(query, cancellationToken);
```

❌ **DON'T: Return synchronous methods**
```csharp
public object GetData(IDataverseContext context) // Synchronous!
```
✅ **DO: Return Task**
```csharp
public async Task<object> GetData(IDataverseContext context, CancellationToken cancellationToken)
```

❌ **DON'T: Let exceptions crash the tool**
```csharp
var entity = await context.ServiceClient.RetrieveAsync(...); // Can throw!
return entity;
```
✅ **DO: Catch and wrap in ToolExecutionException**
```csharp
try
{
    var entity = await context.ServiceClient.RetrieveAsync(...);
    return entity;
}
catch (Exception ex)
{
    throw new ToolExecutionException("DATAVERSE_ERROR", $"Retrieve failed: {ex.Message}", ex);
}
```

## Example: Complete Plugin with Multiple Tools

```csharp
using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;
using DataverseMCPToolBox.Extensibility.Exceptions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

[McpPlugin("dataverse-essentials", "1.0.0",
    Author = "Your Organization",
    Description = "Essential Dataverse operations for AI assistants")]
public class DataverseEssentialsPlugin : PluginBase
{
    // Simple tools via attributes
    [McpTool("Gets current user information and permissions")]
    public async Task<object> WhoAmI(IDataverseContext context, CancellationToken cancellationToken)
    {
        var request = new WhoAmIRequest();
        var response = (WhoAmIResponse)await context.ServiceClient
            .ExecuteAsync(request, cancellationToken);

        return new
        {
            userId = response.UserId,
            businessUnitId = response.BusinessUnitId,
            organizationId = response.OrganizationId
        };
    }

    [McpTool("Lists all entity metadata with display names")]
    public async Task<object> ListEntities(IDataverseContext context, CancellationToken cancellationToken)
    {
        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity
        };

        var response = (RetrieveAllEntitiesResponse)await context.ServiceClient
            .ExecuteAsync(request, cancellationToken);

        return response.EntityMetadata
            .Select(e => new
            {
                logicalName = e.LogicalName,
                displayName = e.DisplayName?.UserLocalizedLabel?.Label,
                primaryIdAttribute = e.PrimaryIdAttribute,
                primaryNameAttribute = e.PrimaryNameAttribute
            })
            .OrderBy(e => e.logicalName)
            .ToList();
    }

    // Complex tools added explicitly
    public override IEnumerable<IMcpTool> GetTools()
    {
        var baseTools = base.GetTools();
        var customTools = new IMcpTool[]
        {
            new RetrieveRecordTool(),
            new QueryRecordsTool()
        };
        return baseTools.Concat(customTools);
    }
}

// Strongly-typed tools
public class RetrieveRecordInput
{
    public string EntityName { get; set; }
    public string RecordId { get; set; }
    public List<string>? Columns { get; set; }
}

public class RetrieveRecordTool : McpToolBase<RetrieveRecordInput, object>
{
    public RetrieveRecordTool()
        : base("retrieve-record", "Retrieves a single record by entity name and ID")
    {
    }

    protected override async Task<object> ExecuteAsync(
        RetrieveRecordInput parameters,
        IDataverseContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(parameters.RecordId, out var id))
            throw new ToolExecutionException("VALIDATION_ERROR", "RecordId must be a valid GUID");

        var columnSet = parameters.Columns?.Any() == true
            ? new ColumnSet(parameters.Columns.ToArray())
            : new ColumnSet(true);

        try
        {
            var entity = await context.ServiceClient.RetrieveAsync(
                parameters.EntityName,
                id,
                columnSet,
                cancellationToken);

            return new
            {
                id = entity.Id,
                logicalName = entity.LogicalName,
                attributes = entity.Attributes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => FormatAttributeValue(kvp.Value))
            };
        }
        catch (Exception ex)
        {
            throw new ToolExecutionException(
                "DATAVERSE_ERROR",
                $"Failed to retrieve {parameters.EntityName} record: {ex.Message}",
                new { entityName = parameters.EntityName, recordId = parameters.RecordId },
                ex);
        }
    }

    private static object? FormatAttributeValue(object? value)
    {
        return value switch
        {
            EntityReference entityRef => new { id = entityRef.Id, logicalName = entityRef.LogicalName, name = entityRef.Name },
            OptionSetValue optionSet => optionSet.Value,
            Money money => money.Value,
            _ => value?.ToString()
        };
    }
}
```

## Summary Checklist

When developing a DataverseMCPToolBox plugin, ensure:

- [ ] Reference `DataverseMCPToolBox.Extensibility` NuGet package
- [ ] Decorate plugin class with `[McpPlugin(name, version)]`
- [ ] Implement `IPlugin` (or inherit `PluginBase`)
- [ ] All tool names are in kebab-case format
- [ ] All tool execution methods are async (`Task` or `Task<T>`)
- [ ] Include `CancellationToken` parameter in tool methods
- [ ] C# models use PascalCase, JSON uses camelCase
- [ ] Use `ToolExecutionException` for structured errors
- [ ] Log only to stderr, never stdout
- [ ] Document tools with clear descriptions
- [ ] Package and publish to NuGet

## Additional Resources

- [Microsoft Dataverse SDK Documentation](https://learn.microsoft.com/power-apps/developer/data-platform/)
- [MCP Protocol Specification](https://modelcontextprotocol.io/)
- [DataverseMCPToolBox Repository](https://github.com/tchinnin/dataverse-mcp-toolbox)
