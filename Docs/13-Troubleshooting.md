# Troubleshooting

Comprehensive troubleshooting guide for common issues and their solutions.

## Diagnostic Overview

```mermaid
graph TB
    Issue[Problem Occurs]
    
    Issue --> Category{Category}
    
    Category -->|Installation| Install[Installation Issues]
    Category -->|Connection| Connect[Connection Issues]
    Category -->|Tools| Tools[Tool Execution Issues]
    Category -->|Plugins| Plugins[Plugin Issues]
    Category -->|Performance| Perf[Performance Issues]
    
    subgraph Diagnostics["Diagnostic Steps"]
        Logs[Check Logs]
        Status[Check Status]
        Config[Verify Configuration]
        Test[Run Tests]
    end
    
    Install --> Diagnostics
    Connect --> Diagnostics
    Tools --> Diagnostics
    Plugins --> Diagnostics
    Perf --> Diagnostics
    
    style Diagnostics fill:#e1f5ff
```

## Finding Logs

### Output Panel

```mermaid
graph TB
    VSCode[VS Code]
    
    VSCode --> Menu[View Menu]
    Menu --> Output[Output Panel]
    Output --> Select[Select Channel]
    Select --> DV[Dataverse MCP Toolbox]
    
    subgraph LogTypes["Log Types"]
        Extension[Extension Logs<br/>TypeScript]
        CoreServer[Core Server Logs<br/>stderr]
        Bridge[Bridge Logs<br/>stderr]
    end
    
    DV --> LogTypes
    
    style DV fill:#e1f5ff
    style LogTypes fill:#ffe1e1
```

**Access Logs:**
1. `View` → `Output` (or `Ctrl+Shift+U`)
2. Select `Dataverse MCP Toolbox` from dropdown
3. View real-time logs

### Log Levels

| Level | Prefix | Description |
|-------|--------|-------------|
| **ERROR** | `[ERROR]` | Critical errors requiring attention |
| **WARNING** | `[WARN]` | Warnings about potential issues |
| **INFO** | `[INFO]` | General information |
| **DEBUG** | `[DEBUG]` | Detailed debugging information |

## Installation Issues

### Extension Won't Activate

```mermaid
graph TB
    Problem[Extension Not Activating]
    
    Problem --> Check1{VS Code<br/>Version?}
    Check1 -->|< 1.85.0| Update1[Update VS Code]
    Check1 -->|>= 1.85.0| Check2{Extension<br/>Installed?}
    
    Check2 -->|No| Install[Install Extension]
    Check2 -->|Yes| Check3{Activation<br/>Error?}
    
    Check3 -->|Yes| ViewError[Check Output Panel]
    Check3 -->|No| Check4[Check Extension Host]
    
    ViewError --> Solutions["• Reload Window<br/>• Reinstall Extension<br/>• Check for conflicts"]
    
    style Update1 fill:#ffe1e1
    style Solutions fill:#e1ffe1
```

### Binary Download Fails

**Symptoms:**
- "Downloading runtime..." hangs
- "Failed to download server binaries"
- Extension activates but Core Server won't start

```mermaid
graph TB
    DownloadFail[Binary Download Fails]
    
    DownloadFail --> Check{Root<br/>Cause?}
    
    Check -->|Network| Network["Network Issue:<br/>• Check internet connection<br/>• Verify NuGet.org accessible<br/>• Check proxy settings<br/>• Try VPN/different network"]
    
    Check -->|Storage| Storage["Storage Issue:<br/>• Check disk space<br/>• Verify write permissions<br/>• Check antivirus blocking<br/>• Clear globalStorage"]
    
    Check -->|NuGet| NuGet["NuGet Issue:<br/>• Verify package exists<br/>• Try different version<br/>• Manual download"]
    
    style Network fill:#ffe1e1
    style Storage fill:#ffe1e1
    style NuGet fill:#ffe1e1
```

**Solutions:**

1. **Check Network**
   ```
   # Test NuGet connectivity
   curl https://api.nuget.org/v3/index.json
   ```

2. **Clear Cache and Retry**
   ```
   Command Palette → Dataverse: Clear Server Cache
   Reload Window
   ```

3. **Manual Installation**
   - Download Runtime package from NuGet.org
   - Extract to `globalStorageUri/server/`
   - Set execute permissions (Unix)

### Core Server Won't Start

```mermaid
sequenceDiagram
    participant Ext as Extension
    participant OS
    participant Core as Core Server
    
    Ext->>OS: spawn(serverPath)
    
    alt Binary Missing
        OS->>Ext: ENOENT
        Note over Ext: "Server binary not found"
    else Permission Denied
        OS->>Ext: EACCES
        Note over Ext: "Permission denied"
    else Binary Corrupted
        OS->>Core: Start Process
        Core->>Core: Fail to Initialize
        Core->>OS: Exit Code != 0
        OS->>Ext: Process Exited
    else Success
        OS->>Core: Process Started
        Core->>Core: Initialize
        Core->>Ext: Ready
    end
```

**Diagnostic Steps:**

1. **Verify Binary Exists**
   ```
   ls -la ~/Library/Application\ Support/Code/User/globalStorage/.../server/
   ```

2. **Check Permissions (Unix)**
   ```bash
   # Make executable
   chmod +x DataverseMCPToolBox
   chmod +x DataverseMCPToolBox.Bridge
   ```

3. **Check Core Server Logs**
   - Output panel → Dataverse MCP Toolbox
   - Look for `[Core]` prefix logs

4. **Try Manual Execution**
   ```bash
   # Test if binary runs
   ./DataverseMCPToolBox --help
   ```

## Connection Issues

### Authentication Failures

```mermaid
graph TB
    AuthFail[Authentication Fails]
    
    AuthFail --> Type{Error<br/>Type?}
    
    Type -->|Browser| BrowserIssue["Browser Not Opening:<br/>• Check default browser<br/>• Try device code flow<br/>• Check pop-up blocker<br/>• Check firewall"]
    
    Type -->|Credentials| CredIssue["Invalid Credentials:<br/>• Verify account<br/>• Check MFA setup<br/>• Verify tenant<br/>• Check account access"]
    
    Type -->|Permissions| PermIssue["Permission Denied:<br/>• Check Dataverse access<br/>• Verify security role<br/>• Check environment status<br/>• Verify license"]
    
    Type -->|Timeout| TimeoutIssue["Timeout:<br/>• Check network<br/>• Check proxy<br/>• Verify AAD reachable<br/>• Try again later"]
    
    style BrowserIssue fill:#ffe1e1
    style CredIssue fill:#ffe1e1
    style PermIssue fill:#ffe1e1
    style TimeoutIssue fill:#ffe1e1
```

**Common Solutions:**

1. **Browser Authentication Issues**
   ```
   # Set default browser
   # macOS
   open -a "Google Chrome" --args --new-window
   
   # Try device code flow as fallback
   # Automatically attempted if browser fails
   ```

2. **Clear MSAL Cache**
   ```
   Delete connection and recreate
   Extension clears cached tokens
   ```

3. **Test URL Manually**
   ```
   # Open in browser
   https://yourorg.crm.dynamics.com
   
   # Verify you can access environment
   ```

### Connection Timeout

```mermaid
graph TB
    Timeout[Connection Timeout]
    
    Timeout --> Check{Check<br/>What?}
    
    Check -->|Network| CheckNetwork["• Ping environment<br/>• Check firewall<br/>• Verify proxy<br/>• Check VPN"]
    
    Check -->|Environment| CheckEnv["• Verify URL correct<br/>• Check environment status<br/>• Check Power Platform admin<br/>• Verify not disabled"]
    
    Check -->|Settings| CheckSettings["• Check timeout settings<br/>• Increase timeout<br/>• Check retry settings"]
    
    style CheckNetwork fill:#ffe1e1
    style CheckEnv fill:#ffe1e1
    style CheckSettings fill:#ffe1e1
```

### Token Refresh Failures

```mermaid
sequenceDiagram
    participant Tool
    participant Core
    participant MSAL
    participant AAD
    
    Tool->>Core: Execute Tool
    Core->>MSAL: Get Token
    MSAL->>MSAL: Token Expired
    MSAL->>AAD: Refresh Token
    
    alt Refresh Success
        AAD->>MSAL: New Token
        MSAL->>Core: Token
        Core->>Tool: Continue
    else Refresh Failure
        AAD->>MSAL: Error
        MSAL->>Core: Error
        Core->>Tool: Re-authentication Required
    end
```

**Solution:**
- Delete connection
- Recreate connection
- Re-authenticate

## Tool Execution Issues

### Tool Not Found

```mermaid
graph TB
    NotFound[Tool Not Found Error]
    
    NotFound --> Verify{Verify}
    
    Verify -->|Name| CheckName["• Check spelling<br/>• Verify kebab-case<br/>• Case sensitive check"]
    
    Verify -->|Plugin| CheckPlugin["• Verify plugin installed<br/>• Check plugin loaded<br/>• Reload plugins"]
    
    Verify -->|Registration| CheckReg["• Check tool registered<br/>• View loaded plugins<br/>• Check manifest"]
    
    CheckName --> List[List Available Tools]
    CheckPlugin --> List
    CheckReg --> List
    
    List --> Compare[Compare with Request]
    
    style NotFound fill:#ffe1e1
```

**Diagnostic Commands:**
```
Command Palette → Dataverse: List Loaded Plugins
Ask Copilot: "@workspace What tools are available?"
```

### Tool Execution Fails

```mermaid
graph TB
    ExecFail[Tool Execution Fails]
    
    ExecFail --> Error{Error<br/>Type?}
    
    Error -->|Validation| Validation["Parameter Validation:<br/>• Check parameter types<br/>• Verify required params<br/>• Check schema<br/>• Review error message"]
    
    Error -->|Connection| Connection["Connection Issue:<br/>• Verify active connection<br/>• Check connection status<br/>• Test connection<br/>• Reactivate connection"]
    
    Error -->|Dataverse| Dataverse["Dataverse Error:<br/>• Check permissions<br/>• Verify record exists<br/>• Check entity name<br/>• Review Dataverse logs"]
    
    Error -->|Plugin| Plugin["Plugin Error:<br/>• Check plugin logs<br/>• Verify plugin version<br/>• Update plugin<br/>• Report issue to author"]
    
    style Validation fill:#ffe1e1
    style Connection fill:#ffe1e1
    style Dataverse fill:#ffe1e1
    style Plugin fill:#ffe1e1
```

### No Active Connection Error

```mermaid
sequenceDiagram
    participant User
    participant Copilot
    participant Core
    
    User->>Copilot: Execute tool command
    Copilot->>Core: ExecuteTool request
    Core->>Core: Check active connection
    
    alt No Active Connection
        Core->>Copilot: Error: UNAUTHORIZED
        Copilot->>User: "No active connection"
    else Has Active Connection
        Core->>Core: Execute tool
        Core->>Copilot: Result
    end
```

**Solution:**
1. Open Dataverse MCP sidebar
2. Activate a connection (click on it)
3. Verify green checkmark appears
4. Retry tool execution

## Plugin Issues

### Plugin Won't Load

```mermaid
graph TB
    LoadFail[Plugin Won't Load]
    
    LoadFail --> Check{Check}
    
    Check -->|File| FileCheck["• Verify DLL exists<br/>• Check manifest present<br/>• Verify not corrupted"]
    
    Check -->|Compat| CompatCheck["• Check .NET version<br/>• Verify SDK version<br/>• Check dependencies"]
    
    Check -->|Format| FormatCheck["• Verify [McpPlugin] attribute<br/>• Check assembly format<br/>• Validate manifest JSON"]
    
    FileCheck --> Logs[Check Logs]
    CompatCheck --> Logs
    FormatCheck --> Logs
    
    Logs --> Solutions["• Reinstall plugin<br/>• Update plugin<br/>• Check with author<br/>• Reload plugins"]
    
    style LoadFail fill:#ffe1e1
    style Solutions fill:#e1ffe1
```

### Tools Not Appearing

```mermaid
graph TB
    ToolsMissing[Tools Not Appearing]
    
    ToolsMissing --> Step1[Verify Plugin Loaded]
    Step1 --> Step2{Loaded?}
    
    Step2 -->|No| LoadPlugin[Load Plugin First]
    Step2 -->|Yes| Step3[Check Tool Registration]
    
    Step3 --> Step4{Tools<br/>Registered?}
    
    Step4 -->|No| CheckCode["• Verify [McpTool] attributes<br/>• Check method signatures<br/>• Reload plugins"]
    
    Step4 -->|Yes| CheckCopilot["• Restart VS Code<br/>• Reload window<br/>• Check Copilot connection"]
    
    style ToolsMissing fill:#ffe1e1
```

## Performance Issues

### Slow Tool Execution

```mermaid
graph TB
    Slow[Tool Executes Slowly]
    
    Slow --> Measure[Measure Time]
    
    Measure --> Component{Slow<br/>Component?}
    
    Component -->|Network| Network["Network Latency:<br/>• Check connection<br/>• Verify Dataverse location<br/>• Check bandwidth"]
    
    Component -->|Query| Query["Query Performance:<br/>• Simplify query<br/>• Add filters<br/>• Limit columns<br/>• Reduce result size"]
    
    Component -->|Processing| Processing["Processing Time:<br/>• Check plugin efficiency<br/>• Review algorithm<br/>• Optimize code"]
    
    Component -->|Dataverse| DataversePerf["Dataverse Performance:<br/>• Check environment health<br/>• Review Dataverse logs<br/>• Contact support"]
    
    style Network fill:#ffe1e1
    style Query fill:#ffe1e1
    style Processing fill:#ffe1e1
    style DataversePerf fill:#ffe1e1
```

### High Memory Usage

```mermaid
graph TB
    Memory[High Memory Usage]
    
    Memory --> Check{Check}
    
    Check -->|Core| CoreMem["Core Server:<br/>• Check connection pool<br/>• Review loaded plugins<br/>• Restart server"]
    
    Check -->|Plugin| PluginMem["Plugin Issue:<br/>• Check for memory leaks<br/>• Review plugin code<br/>• Update plugin<br/>• Unload unused plugins"]
    
    Check -->|Results| ResultMem["Large Results:<br/>• Reduce query size<br/>• Paginate results<br/>• Limit columns"]
    
    style CoreMem fill:#ffe1e1
    style PluginMem fill:#ffe1e1
    style ResultMem fill:#ffe1e1
```

## Common Error Messages

### Error Reference

| Error Code | Meaning | Solution |
|------------|---------|----------|
| `VALIDATION_ERROR` | Invalid parameters | Check parameter types and values |
| `NOT_FOUND` | Resource not found | Verify entity/record exists |
| `DATAVERSE_ERROR` | Dataverse SDK error | Check permissions, review details |
| `UNAUTHORIZED` | No active connection | Activate a connection |
| `PLUGIN_ERROR` | Plugin execution error | Check plugin logs, update plugin |
| `TIMEOUT` | Operation timeout | Check network, simplify query |
| `AUTHENTICATION_ERROR` | Auth failed | Re-authenticate, check credentials |

## Diagnostic Commands

### Extension Commands

```mermaid
graph TB
    Commands[Diagnostic Commands]
    
    subgraph Available["Available Commands"]
        Restart[Restart Server<br/>Restart Core Server]
        Reload[Reload Plugins<br/>Reload all plugins]
        Clear[Clear Cache<br/>Clear server binaries]
        Logs[View Logs<br/>Open Output panel]
        Status[Server Status<br/>Check server health]
    end
    
    Commands --> Available
    
    Access["Access via:<br/>Ctrl+Shift+P (Command Palette)<br/>Type 'Dataverse:'"]
    
    Available -.-> Access
    
    style Commands fill:#e1f5ff
    style Available fill:#ffe1e1
```

**Quick Commands:**
```
Ctrl+Shift+P → Dataverse: Restart Server
Ctrl+Shift+P → Dataverse: Reload Plugins
Ctrl+Shift+P → Dataverse: View Logs
Ctrl+Shift+P → Dataverse: Check Server Status
```

## Getting Help

### Support Resources

```mermaid
graph TB
    Help[Need Help?]
    
    Help --> Resources{Resource<br/>Type}
    
    Resources -->|Docs| Documentation[📚 Documentation<br/>Read complete docs]
    Resources -->|Issues| GitHub[🐛 GitHub Issues<br/>Report bugs, request features]
    Resources -->|Community| Discussions[💬 Discussions<br/>Ask questions, share tips]
    Resources -->|Logs| Diagnostics[🔍 Diagnostic Logs<br/>Collect and share]
    
    style Documentation fill:#e1f5ff
    style GitHub fill:#ffe1e1
    style Discussions fill:#fff4e1
    style Diagnostics fill:#e1ffe1
```

### Reporting Issues

**Information to Include:**

1. **Environment**
   - OS and version
   - VS Code version
   - Extension version
   - Runtime version

2. **Reproduction Steps**
   - Step-by-step instructions
   - Expected behavior
   - Actual behavior

3. **Logs**
   - Output panel logs
   - Error messages
   - Screenshots

4. **Configuration**
   - Update channel
   - Installed plugins
   - Connection details (no tokens!)

**Template:**
```markdown
## Environment
- OS: macOS 14.0
- VS Code: 1.85.0
- Extension: 1.2.3
- Runtime: 1.2.3

## Issue Description
[Describe the issue]

## Reproduction Steps
1. [Step 1]
2. [Step 2]
3. [Step 3]

## Expected Behavior
[What should happen]

## Actual Behavior
[What actually happens]

## Logs
```
[Paste relevant logs]
```

## Screenshots
[Attach screenshots if applicable]
```

## Next Steps

- **[Configuration Reference](16-Configuration-Reference.md)**: Detailed configuration options
- **[Build and Deployment](17-Build-And-Deployment.md)**: Advanced deployment topics
- **[Security](18-Security.md)**: Security considerations
