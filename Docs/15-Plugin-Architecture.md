# Plugin Architecture

Deep dive into how plugins are loaded, managed, and executed within the Dataverse MCP Toolbox.

## Plugin System Overview

```mermaid
graph TB
    subgraph Sources["Plugin Sources"]
        NuGet[NuGet Packages]
        Local[Local Directories]
        Bundled[Bundled Plugins]
    end
    
    subgraph Core["Core Server"]
        Manager[PluginManager]
        Loader[Assembly Loader]
        Registry[Tool Registry]
    end
    
    subgraph Execution["Execution Layer"]
        Service[ToolExecutionService]
        Context[DataverseContext]
        Validator[Parameter Validator]
    end
    
    subgraph Isolation["Isolation"]
        AppDomain[Shared AppDomain]
        Dependencies[Dependency Management]
        Security[Security Boundaries]
    end
    
    Sources --> Manager
    Manager --> Loader
    Loader --> Registry
    Registry --> Service
    Service --> Context
    Service --> Validator
    
    Loader -.->|Loads in| AppDomain
    AppDomain --> Dependencies
    AppDomain --> Security
    
    style Sources fill:#e1f5ff
    style Core fill:#ffe1e1
    style Execution fill:#fff4e1
    style Isolation fill:#e1ffe1
```

## Plugin Lifecycle

### Discovery and Loading

```mermaid
stateDiagram-v2
    [*] --> Scanning: Core Server Start
    
    Scanning --> Discovery: Scan Plugin Directories
    Discovery --> Validation: Found Plugin DLLs
    
    Validation --> Loading: manifest.json Valid
    Validation --> Skipped: Invalid/Missing Manifest
    
    Loading --> Reflection: Load Assembly
    Reflection --> Registration: Find Plugin Classes
    Registration --> ToolDiscovery: Register Plugin
    
    ToolDiscovery --> Ready: Discover & Register Tools
    Ready --> Execution: Tool Calls
    Execution --> Ready: Continue
    
    Skipped --> [*]: Log Warning
    Ready --> Unloading: Uninstall/Update
    Unloading --> [*]
```

### Startup Sequence

```mermaid
sequenceDiagram
    participant Core as Core Server
    participant PM as PluginManager
    participant FS as FileSystem
    participant AL as AssemblyLoader
    participant Registry
    
    Core->>PM: Initialize()
    PM->>FS: Scan plugin directories
    
    loop For each plugin directory
        FS->>PM: Plugin directory found
        PM->>FS: Read manifest.json
        FS-->>PM: Manifest data
        
        PM->>PM: Validate manifest
        
        alt Valid Manifest
            PM->>AL: LoadAssembly(path)
            AL->>AL: Load DLL + dependencies
            AL-->>PM: Assembly loaded
            
            PM->>PM: Find plugin classes
            PM->>PM: Instantiate plugins
            
            PM->>Registry: Register plugin
            Registry->>Registry: Discover tools
            Registry-->>PM: Tools registered
            
            PM->>Core: Plugin loaded
        else Invalid Manifest
            PM->>Core: Log warning
        end
    end
    
    Core->>Core: Ready for tool calls
```

## Plugin Structure

### Directory Layout

```
plugins/
├── WhoAmIPlugin/
│   ├── manifest.json
│   ├── WhoAmIPlugin.dll
│   ├── WhoAmIPlugin.pdb
│   └── [dependencies]
├── CustomPlugin/
│   ├── manifest.json
│   ├── CustomPlugin.dll
│   └── [dependencies]
└── AnotherPlugin/
    ├── manifest.json
    └── AnotherPlugin.dll
```

### Manifest Format

```json
{
  "name": "whoami-plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Provides WhoAmI functionality",
  "entryPoint": "WhoAmIPlugin.dll",
  "dependencies": [
    {
      "name": "Newtonsoft.Json",
      "version": "13.0.3"
    }
  ],
  "compatibility": {
    "minCoreVersion": "0.1.0",
    "maxCoreVersion": "1.0.0"
  },
  "metadata": {
    "repository": "https://github.com/user/whoami-plugin",
    "license": "MIT",
    "tags": ["whoami", "user", "identity"]
  }
}
```

## Tool Discovery

### Attribute-Based Discovery

```mermaid
graph TB
    Plugin[Plugin Assembly]
    
    Plugin --> Scan[Scan for Classes]
    Scan --> Filter[Filter by Attributes]
    
    subgraph AttributeCheck["Attribute Check"]
        McpPlugin[Has [McpPlugin]?]
        Inherits[Inherits PluginBase?]
    end
    
    Filter --> AttributeCheck
    
    subgraph MethodScan["Method Scan"]
        Methods[Find Public Methods]
        McpTool[Has [McpTool]?]
        Signature[Valid Signature?]
    end
    
    AttributeCheck -->|Yes| MethodScan
    AttributeCheck -->|No| Skip[Skip]
    
    subgraph Registration["Tool Registration"]
        Name[Extract Tool Name]
        Desc[Extract Description]
        Schema[Generate Schema]
        Register[Register in Registry]
    end
    
    MethodScan -->|Valid| Registration
    MethodScan -->|Invalid| Skip
    
    style Plugin fill:#e1f5ff
    style AttributeCheck fill:#ffe1e1
    style MethodScan fill:#fff4e1
    style Registration fill:#e1ffe1
```

### Strongly-Typed Discovery

```mermaid
graph TB
    Plugin[Plugin Assembly]
    
    Plugin --> Scan[Scan for Classes]
    Scan --> Filter[Filter by Base Class]
    
    subgraph TypeCheck["Type Check"]
        McpToolBase[Inherits McpToolBase&lt;TInput,TOutput&gt;?]
        Generic[Extract Generic Types]
    end
    
    Filter --> TypeCheck
    
    subgraph Analysis["Type Analysis"]
        InputType[Analyze TInput]
        OutputType[Analyze TOutput]
        Properties[Extract Properties]
    end
    
    TypeCheck -->|Yes| Analysis
    TypeCheck -->|No| Skip[Skip]
    
    subgraph SchemaGen["Schema Generation"]
        InputSchema[Generate Input Schema]
        OutputSchema[Generate Output Schema]
        Validation[Generate Validation Rules]
    end
    
    Analysis --> SchemaGen
    
    subgraph Registration["Tool Registration"]
        Register[Register Tool Instance]
        StoreName[Store Tool Name]
        StoreSchema[Store Schema]
    end
    
    SchemaGen --> Registration
    
    style Plugin fill:#e1f5ff
    style TypeCheck fill:#ffe1e1
    style Analysis fill:#fff4e1
    style SchemaGen fill:#e1ffe1
```

## Tool Registry

### Internal Structure

```mermaid
graph TB
    Registry[Tool Registry]
    
    subgraph Storage["Storage"]
        Tools[Dictionary&lt;string, ToolInfo&gt;]
        Plugins[Dictionary&lt;string, PluginInfo&gt;]
    end
    
    Registry --> Storage
    
    subgraph ToolInfo["ToolInfo Structure"]
        Name[Name: string]
        Desc[Description: string]
        Schema[InputSchema: JsonSchema]
        Method[Method: MethodInfo]
        Plugin[Plugin: PluginInfo]
    end
    
    subgraph PluginInfo["PluginInfo Structure"]
        PName[Name: string]
        Version[Version: string]
        Assembly[Assembly: Assembly]
        Instance[Instance: IPlugin]
        ToolList[Tools: List&lt;ToolInfo&gt;]
    end
    
    Tools --> ToolInfo
    Plugins --> PluginInfo
    
    ToolInfo -.->|References| PluginInfo
    
    style Registry fill:#e1f5ff
    style Storage fill:#ffe1e1
    style ToolInfo fill:#fff4e1
    style PluginInfo fill:#e1ffe1
```

### Tool Lookup

```mermaid
sequenceDiagram
    participant Caller
    participant Registry
    participant Cache
    participant Storage
    
    Caller->>Registry: GetTool("get-whoami")
    Registry->>Cache: Check cache
    
    alt Tool in cache
        Cache-->>Registry: ToolInfo
        Registry-->>Caller: ToolInfo
    else Not in cache
        Registry->>Storage: Lookup by name
        Storage-->>Registry: ToolInfo
        Registry->>Cache: Store in cache
        Registry-->>Caller: ToolInfo
    end
```

## Schema Generation

### Process Flow

```mermaid
graph TB
    Input[Input Type]
    
    Input --> Analyze[Type Analyzer]
    
    subgraph Analysis["Type Analysis"]
        Props[Extract Properties]
        Types[Determine Types]
        Attrs[Read Data Annotations]
    end
    
    Analyze --> Analysis
    
    subgraph SchemaBuilder["Schema Builder"]
        Root[Create Root Object]
        PropDefs[Define Properties]
        Required[Mark Required]
        Constraints[Add Constraints]
    end
    
    Analysis --> SchemaBuilder
    
    subgraph JsonSchema["JSON Schema Output"]
        Type[type: object]
        Properties[properties: {...}]
        Req[required: [...]]
        Defs[definitions: {...}]
    end
    
    SchemaBuilder --> JsonSchema
    
    Copilot[Sent to Copilot]
    JsonSchema --> Copilot
    
    style Input fill:#e1f5ff
    style Analysis fill:#ffe1e1
    style SchemaBuilder fill:#fff4e1
    style JsonSchema fill:#e1ffe1
```

### Type Mapping

| C# Type | JSON Schema Type | Additional |
|---------|-----------------|------------|
| `string` | `"string"` | |
| `int`, `long` | `"integer"` | |
| `float`, `double`, `decimal` | `"number"` | |
| `bool` | `"boolean"` | |
| `DateTime` | `"string"` | `format: "date-time"` |
| `Guid` | `"string"` | `format: "uuid"` |
| `T[]`, `List<T>` | `"array"` | `items: {...}` |
| `Dictionary<string, T>` | `"object"` | `additionalProperties: {...}` |
| Custom class | `"object"` | Nested schema |
| `T?` (nullable) | Same as T | Not in `required` |

### Data Annotation Mapping

| Attribute | JSON Schema |
|-----------|-------------|
| `[Required]` | Adds to `required` array |
| `[StringLength(max)]` | `maxLength: max` |
| `[Range(min, max)]` | `minimum: min`, `maximum: max` |
| `[EmailAddress]` | `format: "email"` |
| `[Url]` | `format: "uri"` |
| `[RegularExpression(pattern)]` | `pattern: "pattern"` |
| `[MinLength(n)]` | `minLength: n` |
| `[MaxLength(n)]` | `maxLength: n` |

## Tool Execution

### Execution Pipeline

```mermaid
sequenceDiagram
    participant Copilot
    participant Bridge as MCP Bridge
    participant Core as ToolExecutionService
    participant Registry
    participant Validator
    participant Plugin
    participant Dataverse
    
    Copilot->>Bridge: Call tool
    Bridge->>Core: ExecuteToolAsync(request)
    
    Core->>Registry: GetTool(toolName)
    Registry-->>Core: ToolInfo
    
    Core->>Validator: ValidateInput(parameters, schema)
    
    alt Validation Success
        Validator-->>Core: Valid
        
        Core->>Core: Resolve IDataverseContext
        Core->>Plugin: Invoke tool method
        
        Plugin->>Dataverse: Dataverse operations
        Dataverse-->>Plugin: Results
        
        Plugin-->>Core: Tool result
        Core-->>Bridge: Success result
        Bridge-->>Copilot: Response
    else Validation Failure
        Validator-->>Core: Validation errors
        Core-->>Bridge: Error result
        Bridge-->>Copilot: Error response
    end
```

### Context Injection

```mermaid
graph TB
    Execution[Tool Execution]
    
    Execution --> Check[Check Method Signature]
    
    subgraph Parameters["Parameter Analysis"]
        UserParams[User Parameters]
        SystemParams[System Parameters]
    end
    
    Check --> Parameters
    
    subgraph Resolution["Parameter Resolution"]
        Match[Match by Name/Type]
        Inject[Inject IDataverseContext]
        InjectCT[Inject CancellationToken]
    end
    
    Parameters --> Resolution
    
    subgraph Invocation["Method Invocation"]
        Build[Build Argument Array]
        Invoke[MethodInfo.Invoke()]
        Await[Await Task Result]
    end
    
    Resolution --> Invocation
    
    Result[Return Result]
    Invocation --> Result
    
    style Execution fill:#e1f5ff
    style Parameters fill:#ffe1e1
    style Resolution fill:#fff4e1
    style Invocation fill:#e1ffe1
```

## Dependency Management

### Assembly Loading

```mermaid
graph TB
    Plugin[Plugin DLL]
    
    Plugin --> Load[Assembly.LoadFrom()]
    
    subgraph Resolution["Dependency Resolution"]
        Search[Search Plugin Directory]
        Check[Check GAC]
        CheckRuntime[Check Runtime Directory]
    end
    
    Load --> Resolution
    
    subgraph Conflicts["Conflict Handling"]
        Version[Version Check]
        Resolve[Resolve to Best Match]
        Load2[Load Assembly]
    end
    
    Resolution --> Conflicts
    
    subgraph Shared["Shared Dependencies"]
        SDK[Extensibility SDK]
        Dataverse[Dataverse SDK]
        System[System Libraries]
    end
    
    Conflicts --> Shared
    
    Ready[Plugin Ready]
    Shared --> Ready
    
    style Plugin fill:#e1f5ff
    style Resolution fill:#ffe1e1
    style Conflicts fill:#fff4e1
    style Shared fill:#e1ffe1
```

### Version Compatibility

```mermaid
graph TB
    Plugin[Plugin Manifest]
    
    Plugin --> Check[Read Compatibility]
    
    subgraph Requirements["Requirements"]
        MinVersion[minCoreVersion]
        MaxVersion[maxCoreVersion]
        Current[Current Core Version]
    end
    
    Check --> Requirements
    
    subgraph Validation["Version Check"]
        Compare[Compare Versions]
        Decision{Compatible?}
    end
    
    Requirements --> Validation
    
    Decision -->|Yes| Load[Load Plugin]
    Decision -->|No| Reject[Reject Plugin]
    
    Load --> Success[Plugin Active]
    Reject --> Warning[Log Warning]
    
    style Plugin fill:#e1f5ff
    style Requirements fill:#ffe1e1
    style Validation fill:#fff4e1
```

## Isolation and Security

### Plugin Isolation

```mermaid
graph TB
    subgraph CoreProcess["Core Server Process"]
        AppDomain[Shared AppDomain]
        
        subgraph PluginA["Plugin A"]
            A1[Tools]
            A2[Dependencies]
        end
        
        subgraph PluginB["Plugin B"]
            B1[Tools]
            B2[Dependencies]
        end
        
        subgraph Shared["Shared"]
            SDK[Extensibility SDK]
            Runtime[.NET Runtime]
            CoreServices[Core Services]
        end
        
        AppDomain --> PluginA
        AppDomain --> PluginB
        AppDomain --> Shared
    end
    
    subgraph Boundaries["Security Boundaries"]
        Context[Scoped IDataverseContext]
        Connection[Connection Isolation]
        Permissions[Dataverse Permissions]
    end
    
    CoreProcess -.->|Enforced by| Boundaries
    
    style AppDomain fill:#e1f5ff
    style PluginA fill:#ffe1e1
    style PluginB fill:#fff4e1
    style Shared fill:#e1ffe1
    style Boundaries fill:#ffe1e1
```

### Security Model

```mermaid
graph TB
    User[User Request]
    
    User --> Bridge[MCP Bridge]
    Bridge --> Validate[Input Validation]
    
    Validate --> Context[Context Creation]
    
    subgraph ContextScope["Context Scope"]
        Connection[Dataverse Connection]
        Permissions[User Permissions]
        Timeout[Execution Timeout]
    end
    
    Context --> ContextScope
    
    subgraph PluginExecution["Plugin Execution"]
        Execute[Execute Tool]
        Operations[Dataverse Operations]
    end
    
    ContextScope --> PluginExecution
    
    subgraph DataverseLayer["Dataverse Security"]
        EntityPerms[Entity Permissions]
        FieldPerms[Field Security]
        BusinessRules[Business Rules]
    end
    
    PluginExecution --> DataverseLayer
    
    Result[Return Result]
    DataverseLayer --> Result
    
    style User fill:#e1f5ff
    style ContextScope fill:#ffe1e1
    style PluginExecution fill:#fff4e1
    style DataverseLayer fill:#e1ffe1
```

## Plugin Updates

### Update Flow

```mermaid
stateDiagram-v2
    [*] --> Installed: Plugin Installed
    
    Installed --> CheckUpdate: Update Available
    CheckUpdate --> Downloading: User Initiates Update
    
    Downloading --> Downloaded: Download Complete
    Downloaded --> Unloading: Prepare Update
    
    Unloading --> OldRemoved: Unload Old Version
    OldRemoved --> Installing: Install New Version
    
    Installing --> Loading: Extract & Copy
    Loading --> Validating: Load Assembly
    
    Validating --> Active: Validation Success
    Validating --> Rollback: Validation Failure
    
    Rollback --> OldRestored: Restore Old Version
    OldRestored --> Installed
    
    Active --> [*]: Plugin Updated
```

### Hot Reload Considerations

- **Current Limitation**: Hot reload not supported
- **Workaround**: Restart Core Server to load updated plugins
- **Future Enhancement**: AppDomain unloading for hot reload

## Performance Optimization

### Caching Strategy

```mermaid
graph TB
    subgraph Startup["Startup Cache"]
        Assemblies[Assembly Cache]
        Schemas[Schema Cache]
        Methods[Method Info Cache]
    end
    
    subgraph Runtime["Runtime Cache"]
        Results[Result Cache]
        Metadata[Metadata Cache]
    end
    
    subgraph Strategy["Cache Strategy"]
        LoadTime[Load-time: Assemblies, Schemas]
        FirstUse[First-use: Method Info]
        TTL[TTL-based: Results, Metadata]
    end
    
    Startup --> Strategy
    Runtime --> Strategy
    
    Performance[Better Performance]
    Strategy --> Performance
    
    style Startup fill:#e1f5ff
    style Runtime fill:#ffe1e1
    style Strategy fill:#fff4e1
```

### Lazy Loading

- **Plugin Discovery**: At startup
- **Assembly Loading**: On first tool call (if not pre-loaded)
- **Schema Generation**: Cached after first generation
- **Context Creation**: Per tool execution

## Debugging Plugins

### Debug Workflow

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant IDE
    participant Core as Core Server
    participant Plugin
    
    Dev->>IDE: Set breakpoints in plugin
    Dev->>IDE: Attach to Core process
    IDE->>Core: Debugger attached
    
    Dev->>Core: Call tool via Copilot
    Core->>Plugin: Execute tool
    
    Note over Plugin: Breakpoint hit
    
    Plugin-->>IDE: Pause execution
    Dev->>IDE: Inspect variables
    Dev->>IDE: Step through code
    
    IDE->>Plugin: Resume
    Plugin->>Core: Return result
    Core->>Dev: Display result
```

### Debug Configuration

**Visual Studio / VS Code:**

```json
{
  "name": "Attach to Core Server",
  "type": "coreclr",
  "request": "attach",
  "processName": "DataverseMCPToolBox"
}
```

**Logging in Plugins:**

```csharp
[McpTool("Debug tool")]
public async Task<object> DebugTool(
    IDataverseContext context,
    CancellationToken ct)
{
    // Logs visible in Core Server stderr
    Console.Error.WriteLine($"[Plugin] Executing debug tool");
    
    // Your logic
    var result = await DoSomethingAsync();
    
    Console.Error.WriteLine($"[Plugin] Result: {result}");
    return result;
}
```

## Best Practices

### Plugin Design

1. **Stateless**: Plugins should be stateless (no instance variables)
2. **Async**: All operations must be async
3. **Dependencies**: Minimize external dependencies
4. **Compatibility**: Test with multiple Core versions
5. **Documentation**: Document each tool clearly

### Performance

1. **Lazy Loading**: Only load assemblies when needed
2. **Connection Reuse**: Don't create new connections
3. **Caching**: Cache expensive operations
4. **Timeouts**: Implement reasonable timeouts
5. **Cleanup**: Dispose resources properly

### Error Handling

1. **Specific Exceptions**: Use ToolExecutionException
2. **Error Codes**: Use meaningful error codes
3. **User Messages**: Provide helpful error messages
4. **Logging**: Log errors to stderr
5. **Graceful Degradation**: Handle failures gracefully

## Next Steps

- **[Creating Plugins](14-Creating-Plugins.md)**: Step-by-step plugin development guide
- **[Configuration Reference](16-Configuration-Reference.md)**: Plugin configuration options
- **Sample Code**: Explore SampleWhoAmIPlugin/ for complete example
