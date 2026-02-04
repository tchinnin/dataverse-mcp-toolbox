# Managing Plugins

Guide to installing, updating, and managing plugins that extend the toolbox functionality.

## Plugin Overview

Plugins are NuGet packages that extend the Dataverse MCP Toolbox with custom tools. They are dynamically loaded by the Core Server and automatically registered for use with GitHub Copilot.

```mermaid
graph TB
    subgraph Lifecycle["Plugin Lifecycle"]
        Find[Find Plugin on NuGet]
        Install[Install Plugin]
        Load[Load into Core Server]
        Register[Register Tools]
        Use[Use Tools via Copilot]
        Update[Update Plugin]
        Uninstall[Uninstall Plugin]
    end
    
    Find --> Install
    Install --> Load
    Load --> Register
    Register --> Use
    Use --> Update
    Update --> Load
    Use --> Uninstall
    
    style Install fill:#e1f5ff
    style Load fill:#ffe1e1
    style Use fill:#e1ffe1
```

## Finding Plugins

### NuGet Package Search

```mermaid
graph TB
    Search[Search for Plugin]
    
    subgraph Methods["Search Methods"]
        Browse[Browse NuGet.org]
        Extension[Search via Extension UI]
        CLI[Use nuget.exe CLI]
    end
    
    Search --> Methods
    
    subgraph Identify["Plugin Identification"]
        Naming[Package Name Pattern<br/>DataverseMCPToolBox.Plugins.*]
        Tags[NuGet Tags<br/>dataverse-mcp, plugin]
        Description[Package Description]
    end
    
    Methods --> Identify
    
    style Search fill:#e1f5ff
    style Methods fill:#ffe1e1
    style Identify fill:#fff4e1
```

**Common Plugin Package Patterns:**
- `DataverseMCPToolBox.Plugins.{Name}`
- `{Organization}.DataverseMCP.{Feature}`
- Tagged with: `dataverse-mcp`, `mcp-plugin`, `dataverse`

## Installing Plugins

### Installation Flow

```mermaid
sequenceDiagram
    participant User
    participant UI as Extension UI
    participant Input as Input Dialog
    participant Mgr as Plugin Manager (Ext)
    participant NuGet as NuGet.org
    participant FS as Filesystem
    participant RPC as RPC Client
    participant Core as Core Server
    
    User->>UI: Click "Install Plugin"
    UI->>Input: Prompt for Package ID
    User->>Input: Enter "DataverseMCPToolBox.Plugins.Sample"
    Input->>UI: Package ID Entered
    
    UI->>Mgr: installPlugin(packageId)
    Mgr->>NuGet: Query Package Metadata
    NuGet->>Mgr: Package Info + Versions
    
    Mgr->>UI: Show Version Selector
    User->>UI: Select Version (or latest)
    
    UI->>Mgr: Download Plugin
    Mgr->>NuGet: Download .nupkg
    NuGet->>Mgr: Package Binary
    
    Mgr->>FS: Extract to globalStorageUri/plugins/{packageId}
    FS->>Mgr: Extraction Complete
    
    Mgr->>RPC: reloadPlugins()
    RPC->>Core: ReloadPluginsAsync()
    Core->>FS: Scan Plugin Directory
    Core->>Core: Load New Assembly
    Core->>Core: Discover & Register Tools
    Core->>RPC: Plugin Loaded
    
    RPC->>UI: Success
    UI->>UI: Refresh Plugin Tree
    UI->>User: "Plugin Installed Successfully"
```

### UI Workflow

```mermaid
graph TB
    Start[User Clicks Install] --> Input[Enter Package ID]
    Input --> Validate{Valid<br/>Package?}
    
    Validate -->|No| Error[Show Error]
    Error --> Input
    
    Validate -->|Yes| QueryNuGet[Query NuGet]
    QueryNuGet --> Exists{Package<br/>Exists?}
    
    Exists -->|No| NotFound[Package Not Found]
    Exists -->|Yes| ShowVersions[Show Available Versions]
    
    ShowVersions --> SelectVersion[User Selects Version]
    SelectVersion --> Download[Download Package]
    Download --> Extract[Extract to Plugins Dir]
    Extract --> Reload[Reload Plugins in Core]
    Reload --> Success[Installation Complete]
    
    style Success fill:#e1ffe1
    style Error fill:#ffe1e1
    style NotFound fill:#ffe1e1
```

## Plugin Storage

### Directory Structure

```mermaid
graph TB
    Root[globalStorageUri/plugins/]
    
    subgraph Plugins["Installed Plugins"]
        Plugin1[package-id-1/<br/>Version 1.0.0]
        Plugin2[package-id-2/<br/>Version 2.1.0]
        Plugin3[package-id-3/<br/>Version 1.5.0]
    end
    
    Root --> Plugins
    
    subgraph PluginContents["Plugin Directory Contents"]
        DLL[{PluginName}.dll<br/>Main Assembly]
        Deps[Dependencies/<br/>Referenced DLLs]
        Manifest[plugin.json<br/>Metadata]
    end
    
    Plugin1 --> PluginContents
    
    style Root fill:#e1f5ff
    style Plugins fill:#ffe1e1
    style PluginContents fill:#fff4e1
```

**Example Path:**
```
globalStorageUri/
└── plugins/
    ├── DataverseMCPToolBox.Plugins.WhoAmI/
    │   ├── WhoAmI.dll
    │   ├── plugin.json
    │   └── lib/
    │       └── Newtonsoft.Json.dll
    └── CustomOrg.Plugins.EntityTools/
        ├── EntityTools.dll
        ├── plugin.json
        └── lib/
            └── [dependencies]
```

## Plugin Loading

### Dynamic Assembly Loading

```mermaid
sequenceDiagram
    participant Core as Core Server
    participant Mgr as Plugin Manager
    participant FS as Filesystem
    participant Loader as Assembly Loader
    participant Reflect as Reflection
    participant Registry as Tool Registry
    
    Core->>Mgr: ReloadPlugins()
    Mgr->>FS: Scan plugins/ directory
    FS->>Mgr: List of plugin directories
    
    loop For each plugin directory
        Mgr->>FS: Read plugin.json
        FS->>Mgr: Plugin Manifest
        
        Mgr->>Mgr: Validate Manifest
        
        alt Valid Manifest
            Mgr->>Loader: LoadFrom(dllPath)
            Loader->>Mgr: Assembly Loaded
            
            Mgr->>Reflect: Scan for [McpPlugin]
            Reflect->>Mgr: Plugin Types Found
            
            loop For each plugin type
                Mgr->>Reflect: Get [McpTool] methods
                Reflect->>Mgr: Tool Methods
                
                loop For each tool
                    Mgr->>Registry: Register Tool
                    Registry->>Mgr: Tool Registered
                end
            end
        else Invalid Manifest
            Mgr->>Mgr: Log Error, Skip Plugin
        end
    end
    
    Mgr->>Core: All Plugins Loaded
```

### Plugin Manifest

```json
{
  "name": "sample-plugin",
  "version": "1.0.0",
  "author": "Your Organization",
  "description": "Sample plugin for Dataverse MCP Toolbox",
  "assembly": "SamplePlugin.dll",
  "tools": [
    {
      "name": "sample-tool",
      "description": "Example tool implementation"
    }
  ],
  "dependencies": []
}
```

## Viewing Installed Plugins

### Plugin Tree View

```mermaid
graph TB
    TreeView[Plugin Tree View]
    
    subgraph Display["Plugin Display"]
        Name[Plugin Name]
        Version[Version Badge]
        ToolCount[Tool Count]
        Status[Load Status]
    end
    
    TreeView --> Display
    
    subgraph Actions["Available Actions"]
        ViewDetails[View Details]
        Update[Check for Updates]
        Uninstall[Uninstall Plugin]
        Reload[Reload All Plugins]
    end
    
    Display --> Actions
    
    style TreeView fill:#e1f5ff
    style Display fill:#ffe1e1
    style Actions fill:#fff4e1
```

**Example Tree View:**
```
PLUGINS
├── WhoAmI Plugin v1.0.0
│   ├── 📋 get-whoami
│   └── [Uninstall] [Update]
├── Entity Tools v2.1.0
│   ├── 📋 list-entities
│   ├── 📋 get-entity-metadata
│   ├── 📋 create-record
│   └── [Uninstall] [Update]
└── [Install New Plugin]
```

### Plugin Details Panel

```mermaid
graph TB
    Panel[Plugin Details WebView]
    
    subgraph Info["Plugin Information"]
        Name[Plugin Name & Version]
        Author[Author]
        Description[Description]
        InstallDate[Installation Date]
    end
    
    subgraph Tools["Available Tools"]
        ToolList[List of Tools]
        ToolSchemas[Input Schemas]
        Examples[Usage Examples]
    end
    
    subgraph Management["Management"]
        CheckUpdate[Check for Updates]
        Update[Update Button]
        Uninstall[Uninstall Button]
    end
    
    Panel --> Info
    Panel --> Tools
    Panel --> Management
    
    style Panel fill:#e1f5ff
    style Info fill:#ffe1e1
    style Tools fill:#fff4e1
    style Management fill:#e1ffe1
```

## Updating Plugins

### Update Check Flow

```mermaid
sequenceDiagram
    participant User
    participant UI
    participant Mgr as Plugin Manager
    participant FS as Filesystem
    participant NuGet as NuGet.org
    
    User->>UI: Click "Check for Updates"
    UI->>Mgr: checkUpdates()
    
    loop For each installed plugin
        Mgr->>FS: Read Current Version
        FS->>Mgr: Version (e.g., 1.0.0)
        
        Mgr->>NuGet: Query Latest Version
        NuGet->>Mgr: Latest Version (e.g., 1.1.0)
        
        Mgr->>Mgr: Compare Versions
        
        alt Update Available
            Mgr->>UI: Update Available (1.0.0 → 1.1.0)
        else Up to Date
            Mgr->>UI: Up to Date
        end
    end
    
    UI->>User: Display Update Status
```

### Update Installation

```mermaid
sequenceDiagram
    participant User
    participant UI
    participant Mgr
    participant FS
    participant NuGet
    participant Core
    
    User->>UI: Click "Update Plugin"
    UI->>Mgr: updatePlugin(packageId, newVersion)
    
    Mgr->>FS: Backup Current Version
    FS->>Mgr: Backup Complete
    
    Mgr->>NuGet: Download New Version
    NuGet->>Mgr: Package Downloaded
    
    alt Download Success
        Mgr->>FS: Delete Old Version
        Mgr->>FS: Extract New Version
        FS->>Mgr: Extraction Complete
        
        Mgr->>Core: ReloadPlugins()
        Core->>Core: Unload Old Assembly
        Core->>Core: Load New Assembly
        Core->>Mgr: Reload Success
        
        Mgr->>UI: Update Successful
        UI->>User: "Plugin Updated to v1.1.0"
    else Download Failed
        Mgr->>FS: Restore Backup
        Mgr->>UI: Update Failed
        UI->>User: "Update failed, restored previous version"
    end
```

## Uninstalling Plugins

### Uninstall Flow

```mermaid
sequenceDiagram
    participant User
    participant UI
    participant Confirm as Confirmation
    participant Mgr
    participant Core
    participant FS
    
    User->>UI: Click "Uninstall Plugin"
    UI->>Confirm: Show Confirmation Dialog
    Confirm->>User: "Uninstall 'Sample Plugin'?"
    User->>Confirm: Confirm
    
    UI->>Mgr: uninstallPlugin(packageId)
    
    Mgr->>Core: Unregister Tools
    Core->>Core: Remove from Tool Registry
    Core->>Core: Unload Assembly (if possible)
    Core->>Mgr: Tools Unregistered
    
    Mgr->>FS: Delete Plugin Directory
    FS->>Mgr: Deletion Complete
    
    Mgr->>UI: Uninstall Successful
    UI->>UI: Refresh Plugin Tree
    UI->>User: "Plugin Uninstalled"
```

**Note:** Assembly unloading may require Core Server restart on some platforms due to .NET limitations.

## Plugin Compatibility

### Version Compatibility

```mermaid
graph TB
    Plugin[Plugin Package]
    
    subgraph Versions["Version Requirements"]
        SDKVersion[Extensibility SDK Version]
        TargetFramework[.NET Target Framework]
        RuntimeVersion[Runtime Version]
    end
    
    Plugin --> Versions
    
    subgraph Check["Compatibility Check"]
        Compare[Compare Versions]
        Compatible{Compatible?}
    end
    
    Versions --> Check
    
    Compatible -->|Yes| Install[Install Plugin]
    Compatible -->|No| Error[Show Error Message]
    
    style Install fill:#e1ffe1
    style Error fill:#ffe1e1
```

**Compatibility Requirements:**
- Plugin built against compatible SDK version
- .NET Standard 2.0 or .NET 6+ target framework
- No conflicting dependencies

### Dependency Resolution

```mermaid
graph TB
    Install[Install Plugin Request]
    
    Install --> CheckDeps{Has<br/>Dependencies?}
    
    CheckDeps -->|No| Direct[Install Directly]
    CheckDeps -->|Yes| ResolveDeps[Resolve Dependencies]
    
    ResolveDeps --> Conflict{Conflicts?}
    
    Conflict -->|No| InstallAll[Install Plugin + Dependencies]
    Conflict -->|Yes| ResolveConflict[Attempt Resolution]
    
    ResolveConflict --> CanResolve{Resolvable?}
    
    CanResolve -->|Yes| InstallAll
    CanResolve -->|No| Error[Installation Error]
    
    Direct --> Success[Plugin Installed]
    InstallAll --> Success
    
    style Success fill:#e1ffe1
    style Error fill:#ffe1e1
```

## Troubleshooting Plugins

### Common Issues

```mermaid
graph TB
    Issue{Plugin<br/>Issue}
    
    Issue -->|Not Loading| LoadIssue[Loading Problem]
    Issue -->|Tools Not Appearing| RegistrationIssue[Registration Problem]
    Issue -->|Execution Error| RuntimeIssue[Runtime Problem]
    Issue -->|Update Failed| UpdateIssue[Update Problem]
    
    LoadIssue --> LoadSolutions["• Check plugin.json format<br/>• Verify assembly exists<br/>• Check Core Server logs<br/>• Verify .NET compatibility"]
    
    RegistrationIssue --> RegSolutions["• Check [McpPlugin] attribute<br/>• Verify [McpTool] methods<br/>• Check tool name format<br/>• Reload plugins manually"]
    
    RuntimeIssue --> RuntimeSolutions["• Check tool parameters<br/>• Verify Dataverse connection<br/>• Check dependency versions<br/>• Review error logs"]
    
    UpdateIssue --> UpdateSolutions["• Check network connection<br/>• Verify NuGet accessibility<br/>• Try manual uninstall/install<br/>• Check disk space"]
    
    style LoadIssue fill:#ffe1e1
    style RegistrationIssue fill:#ffe1e1
    style RuntimeIssue fill:#ffe1e1
    style UpdateIssue fill:#ffe1e1
```

### Diagnostic Commands

**View Loaded Plugins:**
```
Command Palette → Dataverse: List Loaded Plugins
```

**Reload Plugins:**
```
Command Palette → Dataverse: Reload All Plugins
```

**View Plugin Logs:**
```
Output Panel → Dataverse MCP Toolbox
Filter: [Plugin]
```

## Next Steps

- **[Using Tools](12-Using-Tools.md)**: Execute plugin tools
- **[Creating Plugins](14-Creating-Plugins.md)**: Build your own plugins
- **[Troubleshooting](13-Troubleshooting.md)**: Detailed troubleshooting
