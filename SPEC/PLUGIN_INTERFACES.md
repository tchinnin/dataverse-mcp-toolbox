# Plugin Development Interfaces

This document defines all interfaces required for developing plugins for the Dataverse MCP Toolbox. These interfaces form the contract between the MCP Server and community-contributed plugins.

## Core Plugin Interfaces

### IPlugin

The main interface that all plugins must implement.

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Base interface that all plugins must implement.
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Unique identifier for the plugin. Should use reverse domain notation.
        /// Example: "com.contoso.metadata"
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the plugin.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Description of what the plugin does.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Plugin version following semantic versioning (e.g., "1.0.0").
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Author or organization name.
        /// </summary>
        string Author { get; }

        /// <summary>
        /// Minimum MCP Server version required (e.g., "1.0.0").
        /// </summary>
        string MinimumServerVersion { get; }

        /// <summary>
        /// Initialize the plugin with required services.
        /// Called once when the plugin is loaded.
        /// </summary>
        /// <param name="context">Context providing access to server services.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all MCP tools provided by this plugin.
        /// Called after initialization to register MCP tools with the MCP server.
        /// </summary>
        IEnumerable<IMCPTool> GetMCPTools();

        /// <summary>
        /// Called when the plugin is being unloaded.
        /// Use this to cleanup resources.
        /// </summary>
        Task ShutdownAsync(CancellationToken cancellationToken = default);
    }
}
```

### IMCPTool

Interface for individual MCP tools provided by plugins.

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Represents a MCP tool that can be invoked via the MCP protocol.
    /// </summary>
    public interface IMCPTool
    {
        /// <summary>
        /// Unique MCP tool name. Should be namespaced with plugin ID.
        /// Example: "com.contoso.metadata:list_tables"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable description of what the MCP tool does.
        /// This is shown to users and AI assistants.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON Schema defining the MCP tool's input parameters.
        /// Used for validation and to help AI assistants understand parameters.
        /// </summary>
        MCPToolInputSchema InputSchema { get; }

        /// <summary>
        /// Execute the MCP tool with provided parameters.
        /// </summary>
        /// <param name="request">MCP tool execution request containing parameters.</param>
        /// <param name="cancellationToken">Cancellation token for long-running operations.</param>
        /// <returns>MCP tool execution result.</returns>
        Task<MCPToolResult> ExecuteAsync(MCPToolRequest request, CancellationToken cancellationToken = default);
    }
}
```

### IPluginContext

Provides access to server services and configuration.

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Context provided to plugins during initialization.
    /// Gives access to server services and configuration.
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>
        /// Dataverse client for making API calls.
        /// </summary>
        IDataverseClient DataverseClient { get; }

        /// <summary>
        /// Logger for the plugin.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Configuration specific to this plugin.
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        /// Server information (version, capabilities, etc.).
        /// </summary>
        IServerInfo ServerInfo { get; }

        /// <summary>
        /// Service provider for dependency injection.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        /// Get a service of type T from the service provider.
        /// </summary>
        T GetService<T>() where T : class;
    }
}
```

## Dataverse Integration Interfaces

### IDataverseClient

Abstraction over the Microsoft Dataverse SDK.

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Client for interacting with Microsoft Dataverse.
    /// Wraps the Dataverse SDK for easier testing and abstraction.
    /// </summary>
    public interface IDataverseClient
    {
        /// <summary>
        /// Connection string used to connect to Dataverse.
        /// </summary>
        string ConnectionString { get; }

        /// <summary>
        /// Whether the client is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Organization unique name.
        /// </summary>
        string OrganizationName { get; }

        /// <summary>
        /// Organization ID.
        /// </summary>
        Guid OrganizationId { get; }

        // CRUD Operations

        /// <summary>
        /// Create a new record in Dataverse.
        /// </summary>
        Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieve a record from Dataverse.
        /// </summary>
        Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columns, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing record in Dataverse.
        /// </summary>
        Task UpdateAsync(Entity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a record from Dataverse.
        /// </summary>
        Task DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken = default);

        // Query Operations

        /// <summary>
        /// Execute a query and retrieve multiple records.
        /// </summary>
        Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute FetchXML query.
        /// </summary>
        Task<EntityCollection> RetrieveFetchXmlAsync(string fetchXml, CancellationToken cancellationToken = default);

        // Metadata Operations

        /// <summary>
        /// Get entity metadata.
        /// </summary>
        Task<EntityMetadata> GetEntityMetadataAsync(string entityName, EntityFilters filters = EntityFilters.Default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all entity metadata.
        /// </summary>
        Task<IEnumerable<EntityMetadata>> GetAllEntityMetadataAsync(EntityFilters filters = EntityFilters.Entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get attribute metadata.
        /// </summary>
        Task<AttributeMetadata> GetAttributeMetadataAsync(string entityName, string attributeName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get option set metadata.
        /// </summary>
        Task<OptionSetMetadataBase> GetOptionSetMetadataAsync(string optionSetName, CancellationToken cancellationToken = default);

        // Relationship Operations

        /// <summary>
        /// Associate two records.
        /// </summary>
        Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disassociate two records.
        /// </summary>
        Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken = default);

        // Execute Operations

        /// <summary>
        /// Execute an organization request.
        /// </summary>
        Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute multiple requests in a single transaction.
        /// </summary>
        Task<ExecuteMultipleResponse> ExecuteMultipleAsync(ExecuteMultipleRequest request, CancellationToken cancellationToken = default);

        // Connection Management

        /// <summary>
        /// Test the connection to Dataverse.
        /// </summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reconnect to Dataverse if connection is lost.
        /// </summary>
        Task ReconnectAsync(CancellationToken cancellationToken = default);
    }
}
```

### IDataverseMetadataCache

Optional interface for plugins that want to implement metadata caching.

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Cache for Dataverse metadata to reduce API calls.
    /// </summary>
    public interface IDataverseMetadataCache
    {
        /// <summary>
        /// Get cached entity metadata or retrieve from Dataverse if not cached.
        /// </summary>
        Task<EntityMetadata> GetOrAddEntityMetadataAsync(string entityName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get cached attribute metadata or retrieve from Dataverse if not cached.
        /// </summary>
        Task<AttributeMetadata> GetOrAddAttributeMetadataAsync(string entityName, string attributeName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear all cached metadata.
        /// </summary>
        void Clear();

        /// <summary>
        /// Clear cached metadata for a specific entity.
        /// </summary>
        void Clear(string entityName);
    }
}
```

## MCP tool Model Classes

### MCPToolRequest

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Request to execute an MCP tool.
    /// </summary>
    public class MCPToolRequest
    {
        /// <summary>
        /// Unique identifier for this request (for tracing/logging).
        /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Name of the MCP tool to execute.
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// Parameters for the MCP tool as a JSON object.
        /// </summary>
        public JsonElement Parameters { get; set; }

        /// <summary>
        /// Get a typed parameter value.
        /// </summary>
        public T GetParameter<T>(string name, T defaultValue = default);

        /// <summary>
        /// Deserialize parameters to a strongly-typed object.
        /// </summary>
        public T GetParametersAs<T>();
    }
}
```

### MCPToolResult

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Result of a MCP tool execution.
    /// </summary>
    public class MCPToolResult
    {
        /// <summary>
        /// Whether the MCP tool executed successfully.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Result data (will be serialized to JSON).
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Error information if execution failed.
        /// </summary>
        public MCPToolError Error { get; set; }

        /// <summary>
        /// Additional metadata about the execution.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Create a successful result.
        /// </summary>
        public static MCPToolResult Success(object data) => new MCPToolResult
        {
            IsSuccess = true,
            Data = data
        };

        /// <summary>
        /// Create an error result.
        /// </summary>
        public static MCPToolResult Error(string code, string message, object details = null) => new MCPToolResult
        {
            IsSuccess = false,
            Error = new MCPToolError
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
    }
}
```

### MCPToolError

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Error information for failed MCP tool execution.
    /// </summary>
    public class MCPToolError
    {
        /// <summary>
        /// Error code (e.g., "INVALID_PARAMS", "DATAVERSE_ERROR").
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Additional error details.
        /// </summary>
        public object Details { get; set; }
    }
}
```

### MCPToolInputSchema

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// JSON Schema for MCP tool input parameters.
    /// </summary>
    public class MCPToolInputSchema
    {
        /// <summary>
        /// Type of the schema (typically "object").
        /// </summary>
        public string Type { get; set; } = "object";

        /// <summary>
        /// Properties definition (parameter name -> property schema).
        /// </summary>
        public Dictionary<string, SchemaProperty> Properties { get; set; }

        /// <summary>
        /// List of required parameter names.
        /// </summary>
        public List<string> Required { get; set; }

        /// <summary>
        /// Create a schema builder for fluent configuration.
        /// </summary>
        public static MCPToolInputSchemaBuilder Builder() => new MCPToolInputSchemaBuilder();
    }

    /// <summary>
    /// Property definition in a schema.
    /// </summary>
    public class SchemaProperty
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public object Default { get; set; }
        public List<object> Enum { get; set; }
        public SchemaProperty Items { get; set; } // For arrays
        public Dictionary<string, SchemaProperty> Properties { get; set; } // For nested objects
    }
}
```

## Supporting Interfaces

### IServerInfo

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Information about the MCP server.
    /// </summary>
    public interface IServerInfo
    {
        /// <summary>
        /// Server version.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Server name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Server capabilities.
        /// </summary>
        IEnumerable<string> Capabilities { get; }

        /// <summary>
        /// Platform the server is running on.
        /// </summary>
        string Platform { get; }
    }
}
```

### ILogger

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Logger interface (matches Microsoft.Extensions.Logging.ILogger).
    /// </summary>
    public interface ILogger
    {
        void LogTrace(string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
        void LogCritical(Exception exception, string message, params object[] args);
    }
}
```

## Base Classes for Plugin Development

### PluginBase

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Abstract base class for plugins providing common functionality.
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        protected IPluginContext Context { get; private set; }
        protected IDataverseClient DataverseClient => Context.DataverseClient;
        protected ILogger Logger => Context.Logger;
        protected IConfiguration Configuration => Context.Configuration;

        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string Version { get; }
        public abstract string Author { get; }
        public virtual string MinimumServerVersion => "1.0.0";

        public virtual async Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Logger.LogInformation("Initializing plugin {PluginName} v{Version}", Name, Version);
            await OnInitializeAsync(cancellationToken);
        }

        public abstract IEnumerable<IMCPTool> GetMCPTools();

        public virtual async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Shutting down plugin {PluginName}", Name);
            await OnShutdownAsync(cancellationToken);
        }

        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
```

### MCPToolBase

```csharp
namespace DataverseMcpToolbox.PluginBase
{
    /// <summary>
    /// Abstract base class for MCP tools providing common functionality.
    /// </summary>
    public abstract class MCPToolBase : IMCPTool
    {
        protected IDataverseClient DataverseClient { get; }
        protected ILogger Logger { get; }

        protected MCPToolBase(IDataverseClient dataverseClient, ILogger logger)
        {
            DataverseClient = dataverseClient ?? throw new ArgumentNullException(nameof(dataverseClient));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract MCPToolInputSchema InputSchema { get; }

        public async Task<MCPToolResult> ExecuteAsync(MCPToolRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Executing MCP tool {ToolName} with request {RequestId}", Name, request.RequestId);
                
                // Validate parameters against schema
                ValidateParameters(request);
                
                // Execute the MCP tool
                var result = await OnExecuteAsync(request, cancellationToken);
                
                Logger.LogInformation("MCP tool {ToolName} completed successfully", Name);
                return result;
            }
            catch (ArgumentException ex)
            {
                Logger.LogWarning(ex, "Invalid parameters for MCP tool {ToolName}", Name);
                return MCPToolResult.Error("INVALID_PARAMS", ex.Message);
            }
            catch (DataverseException ex)
            {
                Logger.LogError(ex, "Dataverse error in MCP tool {ToolName}", Name);
                return MCPToolResult.Error("DATAVERSE_ERROR", ex.Message, ex.ErrorCode);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected error in MCP tool {ToolName}", Name);
                return MCPToolResult.Error("INTERNAL_ERROR", "An unexpected error occurred");
            }
        }

        protected abstract Task<MCPToolResult> OnExecuteAsync(MCPToolRequest request, CancellationToken cancellationToken);

        protected virtual void ValidateParameters(MCPToolRequest request)
        {
            // Override in derived class for custom validation
        }
    }
}
```

## Plugin Package Structure

Community plugins should follow this structure:

```
MyCompany.DataverseMcpToolbox.CustomPlugin/
├── MyCompany.DataverseMcpToolbox.CustomPlugin.csproj
├── Plugin.cs (implements IPlugin)
├── MCP tools/
│   ├── MyCustomMCPTool.cs (implements IMCPTool)
│   └── AnotherTool.cs
├── Models/
│   └── MyCustomModels.cs
├── README.md
└── plugin.json (metadata file)
```

### plugin.json Example

```json
{
  "id": "com.mycompany.customplugin",
  "name": "My Custom Plugin",
  "description": "Provides custom MCP tools for specific scenarios",
  "version": "1.0.0",
  "author": "My Company",
  "minimumServerVersion": "1.0.0",
  "repository": "https://github.com/mycompany/dataverse-mcp-plugin",
  "license": "MIT",
  "tags": ["dataverse", "custom", "integration"]
}
```

## Example Plugin Implementation

```csharp
using DataverseMcpToolbox.PluginBase;

namespace MyCompany.DataverseMcpToolbox.CustomPlugin
{
    public class CustomPlugin : PluginBase
    {
        public override string Id => "com.mycompany.customplugin";
        public override string Name => "My Custom Plugin";
        public override string Description => "Provides custom MCP tools";
        public override string Version => "1.0.0";
        public override string Author => "My Company";

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            // Custom initialization logic
            Logger.LogInformation("Custom plugin initialized");
            await Task.CompletedTask;
        }

        public override IEnumerable<IMCPTool> GetMCPTools()
        {
            yield return new MyCustomMCPTool(DataverseClient, Logger);
            yield return new AnotherCustomTool(DataverseClient, Logger);
        }
    }

    public class MyCustomMCPTool : MCPToolBase
    {
        public MyCustomMCPTool(IDataverseClient dataverseClient, ILogger logger)
            : base(dataverseClient, logger) { }

        public override string Name => "com.mycompany.customplugin:my_custom_tool";
        public override string Description => "Does something custom";

        public override MCPToolInputSchema InputSchema => MCPToolInputSchema.Builder()
            .AddProperty("entityName", "string", "Name of the entity", required: true)
            .AddProperty("filter", "string", "Filter criteria", required: false)
            .Build();

        protected override async Task<MCPToolResult> OnExecuteAsync(
            MCPToolRequest request, 
            CancellationToken cancellationToken)
        {
            var entityName = request.GetParameter<string>("entityName");
            var filter = request.GetParameter<string>("filter", null);

            // Custom logic here
            var results = await DataverseClient.RetrieveMultipleAsync(
                new QueryExpression(entityName),
                cancellationToken);

            return MCPToolResult.Success(new
            {
                count = results.Entities.Count,
                entities = results.Entities
            });
        }
    }
}
```

## Plugin Development Best Practices

1. **Naming**: Use reverse domain notation for plugin and MCP tool IDs
2. **Versioning**: Follow semantic versioning
3. **Error Handling**: Always catch and return appropriate error codes
4. **Logging**: Use the provided logger, don't use Console.WriteLine
5. **Async**: Make all I/O operations async
6. **Dependencies**: Minimize external dependencies
7. **Testing**: Include unit tests with your plugin
8. **Documentation**: Provide clear README with usage examples
9. **Configuration**: Use IConfiguration for plugin-specific settings
10. **Thread Safety**: Ensure thread-safe implementations

## Plugin Distribution

### Option 1: NuGet Package (Recommended)

**Advantages**: Easy installation, version management, dependency resolution

**Steps**:
1. Create a NuGet package (.nupkg) from your plugin project
2. Publish to NuGet.org (or private feed)
3. Users install via: `dotnet MCP tool install` or package manager
4. Package automatically placed in plugins directory

**NuGet Package Configuration**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>MyCompany.DataverseMcpToolbox.CustomPlugin</PackageId>
    <Version>1.0.0</Version>
    <Authors>My Company</Authors>
    <Description>Custom plugin for Dataverse MCP Toolbox</Description>
    <PackageTags>dataverse;mcp;plugin</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/mycompany/plugin-repo</RepositoryUrl>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="DataverseMcpToolbox.PluginBase" Version="1.0.0" />
  </ItemGroup>
</Project>
```

**Build and Publish**:
```bash
# Build the package
dotnet pack -c Release

# Publish to NuGet.org
dotnet nuget push ./bin/Release/MyCompany.DataverseMcpToolbox.CustomPlugin.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### Option 2: GitHub Releases

**Advantages**: Full control, no NuGet account required, includes source code

**Steps**:
1. Create a GitHub repository for your plugin
2. Build your plugin in Release mode
3. Create a GitHub release with version tag (e.g., v1.0.0)
4. Attach compiled DLL and dependencies as release assets
5. Users download and place in plugins directory manually

**Release Assets to Include**:
- `MyCompany.DataverseMcpToolbox.CustomPlugin.dll`
- `plugin.json` (metadata file)
- `README.md` (installation and usage instructions)
- `LICENSE` file
- Any additional dependencies not in PluginBase

**Installation Instructions for Users**:
```bash
# Download from GitHub release
# Extract to plugins directory
~/.dataversemcptoolbox/plugins/MyCompany.DataverseMcpToolbox.CustomPlugin/
```

### Community Plugin Registry

The main Dataverse MCP Toolbox repository maintains a curated list of community plugins:

**Submission Process**:
1. Create your plugin with comprehensive documentation
2. Publish to NuGet.org or GitHub Releases
3. Open an issue in the main repository with:
   - Plugin name and description
   - NuGet package link or GitHub release URL
   - Author/maintainer information
   - MCP tools provided by the plugin
4. Maintainers review and add to the registry
5. Plugin listed in official documentation

**Quality Guidelines**:
- ✅ Comprehensive README with examples
- ✅ Unit tests with >80% code coverage
- ✅ Follows naming and coding conventions
- ✅ Clear semantic versioning
- ✅ Open source license (MIT, Apache 2.0, etc.)
- ✅ Active maintenance commitment

## Plugin Installation (End Users)

### Installing from NuGet

**Via VSCode Extension** (Future):
1. Open Command Palette
2. Run "Dataverse MCP: Install Plugin"
3. Search for plugin name
4. Click Install

**Via Command Line**:
```bash
# Install plugin to user's plugin directory
dotnet MCP tool install MyCompany.DataverseMcpToolbox.CustomPlugin --MCP tool-path ~/.dataversemcptoolbox/plugins
```

### Installing from GitHub Release

1. Download the release assets (DLL files)
2. Create plugin directory: `~/.dataversemcptoolbox/plugins/PluginName/`
3. Extract all files to the directory
4. Restart MCP Server

### Plugin Configuration

Create `~/.dataversemcptoolbox/config.json`:
```json
{
  "pluginDirectories": [
    "~/.dataversemcptoolbox/plugins"
  ],
  "enabledPlugins": [
    "com.mycompany.customplugin"
  ],
  "plugins": {
    "com.mycompany.customplugin": {
      "setting1": "value1",
      "setting2": "value2"
    }
  }
}
```

---

**Note**: All interfaces are subject to minor changes during the alpha/beta phase. Breaking changes will be announced with migration guides.

