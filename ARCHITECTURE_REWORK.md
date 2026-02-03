# Architecture Rework - Sidecar Pattern with Named Pipes

**Date**: 2026-02-03  
**Branch**: feature/switch-connection  
**Status**: Planning Phase

## Executive Summary

Complete architectural redesign from the current TCP Master/Proxy pattern to a **Sidecar Architecture** with **Named Pipes** for inter-process communication.

### Key Changes
- **2 .NET Applications**: Core Server + MCP Bridge (replacing single TCP server)
- **Named Pipes**: Cross-platform IPC for Core ↔ Extension and Core ↔ MCP Bridge
- **Environment Variable**: Shared pipe name for same VS Code instance isolation
- **Copilot-First**: MCP Bridge keeps Copilot as master (stdio preserved)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│ VS Code Instance (Process Group)                            │
│                                                              │
│  ┌──────────────────┐                                       │
│  │   Extension      │                                       │
│  │   (TypeScript)   │                                       │
│  └────────┬─────────┘                                       │
│           │ Named Pipe                                      │
│           │ (via ENV VAR)                                   │
│           ▼                                                 │
│  ┌──────────────────┐        ┌──────────────────┐          │
│  │   Core Server    │◄───────┤   MCP Bridge     │          │
│  │   (.NET)         │ Named  │   (.NET)         │          │
│  │                  │  Pipe  │                  │          │
│  │  • Dataverse Ops │        │  • MCP Protocol  │          │
│  │  • Tool Exec     │        │  • Protocol      │          │
│  │  • State Mgmt    │        │    Translation   │          │
│  └──────────────────┘        └────────┬─────────┘          │
│                                       │ stdio              │
│                                       ▼                     │
│                              ┌──────────────────┐           │
│                              │  GitHub Copilot  │           │
│                              │  (Master)        │           │
│                              └──────────────────┘           │
└─────────────────────────────────────────────────────────────┘

Environment Variable: DATAVERSE_MCP_PIPE_NAME = "dataverse-mcp-<vscode-pid>"
```

---

## Components Breakdown

### 1. Core Server (.NET Application)

**Location**: `Core/` (refactored)  
**Executable**: `DataverseMCPToolBox.Core.exe`  
**Responsibilities**:
- Primary application server
- Manages all Dataverse connections
- Executes tools against Dataverse
- Maintains state (in-memory)
- Plugin loading and management
- Serves both Extension and MCP Bridge

**Communication**:
- **Input**: Named Pipe Server (listens on `DATAVERSE_MCP_PIPE_NAME`)
- **Output**: Responses via same Named Pipe
- **Protocol**: JSON-RPC 2.0

**Lifecycle**:
- Spawned by VS Code Extension on activation
- Stays alive until Extension deactivates
- Single instance per VS Code workspace

**Key Services**:
- `ConnectionStateService`: In-memory state management
- `DataverseConnectionService`: Connection handling
- `ToolExecutionService`: Tool execution orchestration
- `PluginManager`: Plugin lifecycle
- `NamedPipeRpcServer`: New service for Named Pipe communication

---

### 2. MCP Bridge (.NET Application)

**Location**: `Bridge/` (new/refactored)  
**Executable**: `DataverseMCPToolBox.Bridge.exe`  
**Responsibilities**:
- MCP Protocol adapter for Copilot
- Translates MCP requests to Core's JSON-RPC format
- Forwards responses back to Copilot
- Maintains Copilot as master (stdio contract)

**Communication**:
- **Input**: stdio (from Copilot) - MCP Protocol
- **Output**: stdio (to Copilot) - MCP Protocol
- **Side Channel**: Named Pipe Client (to Core) - JSON-RPC 2.0

**Lifecycle**:
- Spawned by Copilot (via MCP configuration)
- Reads `DATAVERSE_MCP_PIPE_NAME` from environment
- Connects to existing Core Server
- Stays alive as long as Copilot needs it

**Key Services**:
- `McpProtocolService`: MCP message handling (stdin/stdout)
- `NamedPipeRpcClient`: Client to communicate with Core
- `ProtocolTranslator`: MCP ↔ JSON-RPC translation

---

### 3. VS Code Extension (TypeScript)

**Location**: `Extension/`  
**Responsibilities**:
- User interface (commands, panels, tree views)
- Spawns and manages Core Server process
- Sets `DATAVERSE_MCP_PIPE_NAME` environment variable
- Communicates with Core via Named Pipe

**Communication**:
- **Output**: Named Pipe Client (to Core)
- **Input**: Responses from Core via Named Pipe
- **Protocol**: JSON-RPC 2.0 (same as before)

**Lifecycle**:
1. `activate()`: Start Core Server
2. Generate pipe name: `dataverse-mcp-${process.pid}`
3. Set environment variable
4. Connect to Core via Named Pipe
5. Register VS Code commands
6. `deactivate()`: Disconnect and terminate Core Server

**Key Changes**:
- Replace TCP socket with Named Pipe client
- Add environment variable management
- Add process spawning with env vars

---

## Named Pipe Strategy

### Pipe Naming Convention

```
DATAVERSE_MCP_PIPE_NAME = "dataverse-mcp-<vscode-process-pid>"
```

**Examples**:
- macOS/Linux: `/tmp/dataverse-mcp-12345`
- Windows: `\\.\pipe\dataverse-mcp-12345`

### Cross-Platform Implementation

**Technology**: `System.IO.Pipes.NamedPipeServerStream` and `NamedPipeClientStream`

**Platform-Specific Paths**:
```csharp
public static string GetPipePath(string pipeName)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return pipeName; // Windows uses \\.\pipe\ automatically
    }
    else
    {
        // Unix: explicit path required
        return $"/tmp/{pipeName}";
    }
}
```

### Environment Variable Management

**Extension (TypeScript)**:
```typescript
const pipeName = `dataverse-mcp-${process.pid}`;
process.env.DATAVERSE_MCP_PIPE_NAME = pipeName;

// Spawn Core Server with inherited env
const coreProcess = spawn(coreServerPath, [], {
    env: { ...process.env }
});
```

**MCP Bridge (.NET)**:
```csharp
string? pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME");
if (string.IsNullOrEmpty(pipeName))
{
    throw new InvalidOperationException("DATAVERSE_MCP_PIPE_NAME not set");
}
```

### Multi-Instance Support

Each VS Code window (instance) has:
- Unique process PID
- Unique pipe name
- Isolated Core Server
- Isolated MCP Bridge(s)

No cross-contamination between VS Code instances.

---

## Communication Protocols

### Protocol Matrix

| Source | Destination | Transport | Protocol | Direction |
|--------|-------------|-----------|----------|-----------|
| Extension | Core | Named Pipe | JSON-RPC 2.0 | Bidirectional |
| MCP Bridge | Core | Named Pipe | JSON-RPC 2.0 | Bidirectional |
| Copilot | MCP Bridge | stdio | MCP Protocol | Bidirectional |

### Message Flow Examples

#### Example 1: Extension Creates Connection
```
Extension → Core (Named Pipe):
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "createConnection",
  "params": { "organizationUri": "https://org.crm.dynamics.com", ... }
}

Core → Extension (Named Pipe):
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": { "success": true, "connectionId": "conn-123" }
}
```

#### Example 2: Copilot Calls Tool via MCP Bridge
```
Copilot → MCP Bridge (stdio - MCP Protocol):
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": { "name": "whoami", "arguments": {} }
}

MCP Bridge → Core (Named Pipe - JSON-RPC):
{
  "jsonrpc": "2.0",
  "id": "bridge-2",
  "method": "executeTool",
  "params": { "toolName": "whoami", "parameters": {} }
}

Core → MCP Bridge (Named Pipe):
{
  "jsonrpc": "2.0",
  "id": "bridge-2",
  "result": { "content": [ { "type": "text", "text": "UserId: ..." } ] }
}

MCP Bridge → Copilot (stdio - MCP Protocol):
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": { "content": [ { "type": "text", "text": "UserId: ..." } ] }
}
```

---

## Implementation Phases

### Phase 0: Legacy Cleanup 🧹
**Goal**: Remove all TCP and STDIO artifacts from Core and Extension

**Duration**: 1-2 hours

#### Core Server Cleanup
- [ ] **Remove TCP code from `Core/Program.cs`**
  - Remove `TcpListener` initialization
  - Remove TCP port detection logic (port 9000)
  - Remove TCP Master/Proxy mode detection
  - Clean up TCP-specific error handling
- [ ] **Verify no stdio usage in Core**
  - Ensure all logs use `Console.Error.WriteLine()` only
  - Remove any `Console.WriteLine()` or `Console.Out` references
  - Verify `Trace.Listeners` redirects to stderr only
- [ ] **Audit all services for communication layer coupling**
  - Review each service in `Core/Services/` for TCP/socket dependencies
  - Flag any service directly using network streams or TCP sockets

#### Extension Cleanup
- [ ] **Remove TCP client code from Extension**
  - Delete TCP socket connection logic in `DataverseMCPToolBoxRpcClient.ts`
  - Remove port 9000 hardcoded references
  - Remove TCP retry/reconnection logic
- [ ] **Remove stdio handling in Extension**
  - Verify no spawned processes use stdio communication
  - Remove any stdio stream readers/writers for Core server
- [ ] **Clean up connection state management**
  - Remove TCP-specific connection state tracking
  - Prepare for Named Pipe connection state

**Files to Review/Modify**:
- `Core/Program.cs` (major cleanup)
- `Extension/src/services/DataverseMCPToolBoxRpcClient.ts` (major cleanup)
- `Extension/src/extension.ts` (connection initialization)

**Success Criteria**:
- ✅ No references to `TcpListener`, `TcpClient`, `NetworkStream` in Core
- ✅ No references to `net.Socket`, TCP ports in Extension
- ✅ Core only logs to stderr, never stdout
- ✅ Code compiles (will not run yet)

---

### Phase 1: Core Services Refactoring 🏗️
**Goal**: Decouple business logic from communication layer with proper separation of concerns

**Duration**: 3-4 hours

#### Service Architecture Principles
1. **Communication Layer Independence**: Services should NOT know about Named Pipes, TCP, or stdio
2. **Interface Segregation**: Each service exposes a clean interface
3. **Single Responsibility**: One concern per service
4. **Dependency Injection Ready**: Services receive dependencies via constructor

#### Service Refactoring Tasks

##### 1.1 Create Communication Abstraction Layer
- [ ] **Create `Core/Abstractions/IRpcServer.cs`**
  ```csharp
  public interface IRpcServer
  {
      Task StartAsync(CancellationToken cancellationToken);
      Task StopAsync();
      void RegisterService(object service);
  }
  ```
- [ ] **Create `Core/Abstractions/IRpcClient.cs`** (for Bridge)
  ```csharp
  public interface IRpcClient
  {
      Task ConnectAsync(CancellationToken cancellationToken);
      Task DisconnectAsync();
      Task<TResult> InvokeAsync<TResult>(string method, object? args);
  }
  ```

##### 1.2 Audit and Refactor Existing Services

**Services to Review** (ensure no communication coupling):

- [ ] **`ConnectionStateService.cs`**
  - ✅ Already communication-agnostic (in-memory dictionary)
  - No changes needed, just verify

- [ ] **`DataverseConnectionService.cs`**
  - Verify no socket/stream dependencies
  - Should only depend on `IOrganizationService` and connection strings
  - Document: Manages Dataverse SDK connections

- [ ] **`DataverseAuthService.cs`**
  - Verify no communication layer coupling
  - Should only handle MSAL authentication
  - Document: Handles OAuth token acquisition

- [ ] **`ToolExecutionService.cs`**
  - Verify no communication dependencies
  - Should only orchestrate tool calls
  - Document: Executes registered tools

- [ ] **`PluginManager.cs` / `PluginLoaderService.cs`**
  - Verify plugin loading is filesystem-based only
  - No socket or communication dependencies
  - Document: Loads and manages plugin assemblies

- [ ] **`ToolManager.cs` / `ToolRegistryService.cs`**
  - Verify tool registry is memory-based only
  - Document: Registers and retrieves tool definitions

- [ ] **`InputValidator.cs`**
  - ✅ Pure validation logic, no coupling
  - No changes needed

##### 1.3 Document Service Dependencies
- [ ] **Create `Core/Services/SERVICE_ARCHITECTURE.md`**
  - Document each service's responsibility
  - Document service dependency graph
  - Document how services interact without knowing transport layer

**Files to Create**:
- `Core/Abstractions/IRpcServer.cs`
- `Core/Abstractions/IRpcClient.cs`
- `Core/Services/SERVICE_ARCHITECTURE.md`

**Files to Audit** (no breaking changes, just verification):
- All files in `Core/Services/`

**Success Criteria**:
- ✅ Each service has a clear, documented responsibility
- ✅ No service directly references Named Pipes, TCP, or stdio
- ✅ Services can be instantiated and tested without network layer
- ✅ SERVICE_ARCHITECTURE.md provides clear service interaction diagram

---

### Phase 2: Named Pipe Implementation - Core Server 🔌
**Goal**: Implement Named Pipe server in Core following clean architecture

**Duration**: 4-5 hours

#### 2.1 Named Pipe Server Implementation

- [ ] **Create `Core/Services/NamedPipeRpcServer.cs`**
  - Implements `IRpcServer` interface
  - Cross-platform pipe name resolution:
    ```csharp
    private static string GetPipePath(string pipeName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return pipeName; // Windows: \\.\pipe\<name>
        else
            return $"/tmp/{pipeName}"; // Unix: /tmp/<name>
    }
    ```
  - Accept multiple concurrent connections (Extension + Bridge(s))
  - Use `NamedPipeServerStream` with `MaxAllowedServerInstances`
  - Create new `JsonRpc` instance per connection
  - Register shared service instances (singleton `DataverseMCPToolBoxRpcService`)

- [ ] **Implement connection lifecycle management**
  - Track active connections (List<NamedPipeServerStream>)
  - Clean up disconnected pipes
  - Handle client disconnection gracefully
  - Support graceful shutdown (dispose all connections)

- [ ] **Error handling for Named Pipes**
  - Handle `IOException` when pipe already exists
  - Handle `UnauthorizedAccessException` for permissions
  - Handle `TimeoutException` for connection attempts
  - All errors logged to stderr

#### 2.2 Environment Variable Integration

- [ ] **Read pipe name from environment variable**
  ```csharp
  string pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME") 
                    ?? "DataverseMCPToolBox"; // Fallback for testing
  ```
- [ ] **Validate pipe name**
  - Check for invalid characters
  - Ensure length limits
  - Log pipe name being used

#### 2.3 Update Core Program.cs

- [ ] **Refactor `Core/Program.cs`**
  - Remove all TCP code (already done in Phase 0)
  - Instantiate `NamedPipeRpcServer` instead
  - Pass `IRpcServer` to startup logic
  - Keep same JSON formatter configuration (CamelCase)
  - Maintain same service instantiation (singleton `DataverseMCPToolBoxRpcService`)
  - Handle `CancellationToken` for graceful shutdown

- [ ] **Startup sequence**:
  1. Read `DATAVERSE_MCP_PIPE_NAME` from environment
  2. Create plugin directory
  3. Instantiate `DataverseMCPToolBoxRpcService`
  4. Create `NamedPipeRpcServer`
  5. Register service with server
  6. Start server async
  7. Wait for cancellation signal

**New Files**:
- `Core/Services/NamedPipeRpcServer.cs`

**Files to Modify**:
- `Core/Program.cs` (major refactor)

**Success Criteria**:
- ✅ Core server starts and listens on Named Pipe
- ✅ Multiple clients can connect simultaneously
- ✅ JSON-RPC messages processed correctly
- ✅ Graceful shutdown closes all connections
- ✅ All logs go to stderr only

---

### Phase 3: Named Pipe Implementation - MCP Bridge 🌉
**Goal**: Create MCP Bridge as Named Pipe client with stdio forwarding

**Duration**: 3-4 hours

#### 3.1 Bridge Project Setup

- [ ] **Verify `Bridge/DataverseMCPToolBox.Bridge.csproj`**
  - Target .NET 8.0
  - Self-contained publish settings
  - Dependencies: StreamJsonRpc, Newtonsoft.Json, System.IO.Pipes

- [ ] **Update `Bridge/Program.cs`**
  - Read `DATAVERSE_MCP_PIPE_NAME` from environment
  - Fail fast if environment variable missing
  - Connect to Named Pipe (not create server)
  - Forward stdin → Named Pipe and Named Pipe → stdout
  - All logs to stderr only

#### 3.2 Named Pipe Client Implementation

- [ ] **Create `Bridge/Services/NamedPipeRpcClient.cs`**
  - Implements `IRpcClient`
  - Connects to existing Named Pipe server (Core)
  - Uses `NamedPipeClientStream`
  - Cross-platform pipe path resolution (same as server)
  - Connection timeout handling (10 seconds default)
  - Reconnection logic (optional, depends on requirements)

#### 3.3 MCP Protocol Handler

- [ ] **Create `Bridge/Services/McpProtocolService.cs`**
  - Reads JSON-RPC messages from stdin (MCP format)
  - Writes JSON-RPC responses to stdout (MCP format)
  - Newline-delimited JSON messages
  - UTF-8 encoding
  - Non-blocking async reads/writes

#### 3.4 Protocol Translation (if needed)

- [ ] **Analyze MCP vs Core JSON-RPC differences**
  - Determine if direct forwarding works
  - If translation needed, create `Bridge/Services/ProtocolTranslator.cs`
  - Handle method name mapping (e.g., `tools/call` → `executeTool`)
  - Handle parameter structure differences

- [ ] **Implement request/response forwarding**
  - stdin JSON → Parse → Forward to Core via Named Pipe
  - Core response via Named Pipe → Format MCP → stdout
  - Preserve request IDs for correlation
  - Handle errors and propagate to Copilot

#### 3.5 Error Handling

- [ ] **Bridge-specific error scenarios**
  - Core server not running (pipe not found)
  - Core server crashed (pipe closed mid-operation)
  - Malformed stdin messages
  - All errors logged to stderr, graceful error responses to stdout

#### 3.6 Bridge README

- [ ] **Create `Bridge/README.md`**
  - Explain Bridge purpose and architecture
  - Document how Copilot spawns Bridge
  - Document environment variable requirements
  - Provide troubleshooting guide

**New Files**:
- `Bridge/Services/NamedPipeRpcClient.cs`
- `Bridge/Services/McpProtocolService.cs`
- `Bridge/Services/ProtocolTranslator.cs` (if needed)
- `Bridge/README.md`

**Files to Modify**:
- `Bridge/Program.cs` (major refactor)

**Success Criteria**:
- ✅ Bridge connects to Core via Named Pipe
- ✅ stdin messages forwarded to Core
- ✅ Core responses forwarded to stdout
- ✅ MCP protocol compliance verified
- ✅ Bridge fails gracefully if Core unavailable

---

### Phase 4: Extension Named Pipe Integration 🎨
**Goal**: Update VS Code Extension to use Named Pipe instead of TCP

**Duration**: 3-4 hours

#### 4.1 Named Pipe Client for TypeScript

- [ ] **Create `Extension/src/services/NamedPipeClient.ts`**
  - Use Node.js `net` module for Named Pipe connection
  - Cross-platform pipe path resolution:
    ```typescript
    function getPipePath(pipeName: string): string {
        if (process.platform === 'win32') {
            return `\\\\.\\pipe\\${pipeName}`;
        } else {
            return `/tmp/${pipeName}`;
        }
    }
    ```
  - Implement connection retry logic
  - Handle disconnection events
  - Wrap JSON-RPC communication

#### 4.2 Environment Variable Management

- [ ] **Update `Extension/src/extension.ts` activation**
  - Generate pipe name from VS Code process PID:
    ```typescript
    const pipeName = `dataverse-mcp-${process.pid}`;
    process.env.DATAVERSE_MCP_PIPE_NAME = pipeName;
    ```
  - Pass environment to spawned Core Server process
  - Store pipe name in Extension context for Bridge config

#### 4.3 Core Server Process Management

- [ ] **Update Core Server spawn logic**
  - Select correct binary for platform (osx-arm64, osx-x64, win-x64, linux-x64)
  - Spawn with inherited environment (includes `DATAVERSE_MCP_PIPE_NAME`)
  - Capture stderr for logging
  - Ensure stdout is never captured (not used in sidecar)
  - Handle process exit and restart logic

- [ ] **Process lifecycle**
  - Start Core on Extension activation
  - Wait for pipe availability (retry with timeout)
  - Connect Extension to Core via Named Pipe
  - Stop Core on Extension deactivation
  - Clean up pipe file on Unix systems (optional)

#### 4.4 Update RPC Client

- [ ] **Refactor `Extension/src/services/DataverseMCPToolBoxRpcClient.ts`**
  - Remove all TCP socket code
  - Use `NamedPipeClient` instead
  - Keep same JSON-RPC method signatures
  - Update error handling for pipe-specific errors
  - Connection state management (connected, disconnected, error)

#### 4.5 Copilot MCP Configuration

- [ ] **Auto-configure Copilot to use Bridge**
  - Extension writes Copilot MCP settings on activation
  - Path to Bridge binary for user's platform
  - Set `DATAVERSE_MCP_PIPE_NAME` in Bridge's environment
  - Handle user's existing MCP settings (merge, not overwrite)

- [ ] **MCP configuration structure** (write to workspace or user settings):
  ```json
  {
    "github.copilot.chat.codeGeneration.instructions": [],
    "github.copilot.chat.mcp.servers": {
      "dataverse-mcp-toolbox": {
        "command": "/path/to/DataverseMCPToolBox.Bridge",
        "env": {
          "DATAVERSE_MCP_PIPE_NAME": "dataverse-mcp-<pid>"
        }
      }
    }
  }
  ```

#### 4.6 UI Updates

- [ ] **Update status bar item**
  - Show "Core Server: Running (Pipe: dataverse-mcp-12345)"
  - Show connection status to Core
  - Click to show logs or restart server

- [ ] **Update connection tree view**
  - Show active Named Pipe connection
  - Show Bridge status (if detectable)

**Files to Create**:
- `Extension/src/services/NamedPipeClient.ts`

**Files to Modify**:
- `Extension/src/extension.ts` (activation/deactivation)
- `Extension/src/services/DataverseMCPToolBoxRpcClient.ts` (major refactor)
- Extension UI components (status bar, tree views)

**Success Criteria**:
- ✅ Extension spawns Core Server with correct environment
- ✅ Extension connects to Core via Named Pipe
- ✅ All RPC commands work (create connection, list tools, etc.)
- ✅ Copilot MCP config automatically written
- ✅ Multiple VS Code windows have isolated Core instances

---

### Phase 5: Build System Overhaul 📦
**Goal**: Single NuGet package with both Core and Bridge executables for all platforms

**Duration**: 2-3 hours

#### 5.1 Update Build Scripts

- [ ] **Refactor `scripts/build-all.sh`**
  - Build Core for all platforms (osx-arm64, osx-x64, win-x64, linux-x64)
  - Build Bridge for all platforms (same platforms)
  - Self-contained, single-file executables
  - Output structure:
    ```
    /Core/publish/
      /osx-arm64/DataverseMCPToolBox
      /osx-x64/DataverseMCPToolBox
      /win-x64/DataverseMCPToolBox.exe
      /linux-x64/DataverseMCPToolBox
    /Bridge/publish/
      /osx-arm64/DataverseMCPToolBox.Bridge
      /osx-x64/DataverseMCPToolBox.Bridge
      /win-x64/DataverseMCPToolBox.Bridge.exe
      /linux-x64/DataverseMCPToolBox.Bridge
    ```

- [ ] **Refactor `scripts/build-all.ps1`** (Windows equivalent)
  - Same logic as shell script
  - PowerShell syntax

#### 5.2 NuGet Package Structure

- [ ] **Create unified NuGet package**
  - Package ID: `DataverseMCPToolBox.Runtime`
  - Contains both Core and Bridge binaries
  - Structure:
    ```
    runtimes/
      osx-arm64/native/
        DataverseMCPToolBox
        DataverseMCPToolBox.Bridge
      osx-x64/native/
        DataverseMCPToolBox
        DataverseMCPToolBox.Bridge
      win-x64/native/
        DataverseMCPToolBox.exe
        DataverseMCPToolBox.Bridge.exe
      linux-x64/native/
        DataverseMCPToolBox
        DataverseMCPToolBox.Bridge
    ```

- [ ] **Update `Core/scripts/pack-nuget.sh`**
  - Call `build-all.sh` first
  - Copy Bridge binaries alongside Core binaries
  - Single `dotnet pack` command with custom nuspec or csproj targets
  - Output single `.nupkg` file

- [ ] **Update `Core/scripts/pack-nuget.ps1`** (Windows)

#### 5.3 Local Development Workflow

- [ ] **Create `scripts/install-local.sh`**
  - Build both Core and Bridge
  - Create NuGet package
  - Copy package to Extension's `server/` directory (or extract binaries)
  - Make executable on Unix systems (`chmod +x`)
  - Purpose: Fast local testing without publishing to NuGet

- [ ] **Create `scripts/install-local.ps1`** (Windows)

- [ ] **Update Extension to use local binaries during development**
  - Check for `Extension/server/` directory first
  - Fall back to NuGet package if production

#### 5.4 CI/CD Integration (Optional)

- [ ] **GitHub Actions workflow** (if using GitHub)
  - Build on push to main
  - Run on all platforms (macos, windows, ubuntu)
  - Create NuGet package
  - Publish to NuGet.org or GitHub Packages
  - Create VSIX with embedded binaries
  - Publish VSIX to VS Code Marketplace

#### 5.5 Documentation Updates

- [ ] **Update `Core/scripts/README.md`**
  - Document new build process
  - Document NuGet package structure
  - Document local development workflow

- [ ] **Update root `README.md`**
  - Update architecture section
  - Update build instructions
  - Update development setup

**Files to Create**:
- `scripts/install-local.sh`
- `scripts/install-local.ps1`

**Files to Modify**:
- `scripts/build-all.sh`
- `scripts/build-all.ps1`
- `Core/scripts/pack-nuget.sh`
- `Core/scripts/pack-nuget.ps1`
- `Core/scripts/README.md`
- `README.md`

**Success Criteria**:
- ✅ Single command builds both Core and Bridge for all platforms
- ✅ Single NuGet package contains all binaries
- ✅ Local install script deploys to Extension for testing
- ✅ Binaries are self-contained (no .NET runtime needed)
- ✅ All platforms tested and verified

---

### Phase 6: Test Project Creation & Validation 🧪
**Goal**: Create comprehensive .NET test project to validate the complete sidecar architecture

**Duration**: 4-5 hours

#### 6.1 Test Project Setup

- [ ] **Create test project `Tests/DataverseMCPToolBox.Tests.csproj`**
  - Target .NET 8.0
  - Use xUnit test framework
  - Dependencies:
    - `xUnit` + `xUnit.runner.visualstudio`
    - `Microsoft.NET.Test.Sdk`
    - `Moq` (for mocking)
    - `FluentAssertions` (for readable assertions)
  - Reference Core and Bridge projects

- [ ] **Project structure**:
  ```
  Tests/
    ├── DataverseMCPToolBox.Tests.csproj
    ├── Unit/
    │   ├── Services/
    │   │   ├── ConnectionStateServiceTests.cs
    │   │   ├── DataverseConnectionServiceTests.cs
    │   │   ├── ToolExecutionServiceTests.cs
    │   │   └── PluginManagerTests.cs
    │   └── Communication/
    │       ├── NamedPipeRpcServerTests.cs
    │       └── NamedPipeRpcClientTests.cs
    ├── Integration/
    │   ├── CoreServerIntegrationTests.cs
    │   ├── BridgeIntegrationTests.cs
    │   └── EndToEndTests.cs
    └── Helpers/
        ├── TestPipeHelper.cs
        ├── MockDataverseService.cs
        └── TestConstants.cs
  ```

#### 6.2 Unit Tests Implementation

##### 6.2.1 Service Layer Tests
- [ ] **`ConnectionStateServiceTests.cs`**
  - Test connection state CRUD operations
  - Test thread-safety of concurrent operations
  - Test connection retrieval and enumeration

- [ ] **`DataverseConnectionServiceTests.cs`**
  - Mock `IOrganizationService`
  - Test connection creation and validation
  - Test error handling for invalid credentials

- [ ] **`ToolExecutionServiceTests.cs`**
  - Mock tool providers and Dataverse context
  - Test tool discovery and registration
  - Test tool execution with various parameters
  - Test error propagation

- [ ] **`PluginManagerTests.cs`**
  - Test plugin loading from filesystem
  - Test plugin validation (attributes, manifest)
  - Test plugin isolation and unloading

##### 6.2.2 Communication Layer Tests
- [ ] **`NamedPipeRpcServerTests.cs`**
  - Test pipe creation with valid/invalid names
  - Test multiple concurrent client connections
  - Test graceful shutdown
  - Test error handling (pipe already exists, permissions)
  - Mock JSON-RPC service calls

- [ ] **`NamedPipeRpcClientTests.cs`**
  - Test connection to existing pipe
  - Test connection timeout handling
  - Test reconnection logic
  - Test error handling (pipe not found, server crash)

#### 6.3 Integration Tests Implementation

- [ ] **`CoreServerIntegrationTests.cs`**
  - Start Core Server in test mode
  - Connect via Named Pipe client
  - Test complete JSON-RPC workflow:
    - List tools
    - Create connection (mock Dataverse)
    - Execute tool
    - Disconnect
  - Verify state management across requests
  - Test multiple simultaneous clients

- [ ] **`BridgeIntegrationTests.cs`**
  - Start Core Server
  - Start Bridge process
  - Simulate stdin input (MCP protocol)
  - Verify stdout output
  - Test request forwarding to Core
  - Test response translation

- [ ] **`EndToEndTests.cs`**
  - Simulate complete workflow:
    1. Start Core Server with env var
    2. Connect Extension (simulated)
    3. Create Dataverse connection
    4. Start Bridge
    5. Execute tool via Bridge
    6. Verify results in both clients
  - Test multi-instance isolation (different pipe names)
  - Test error scenarios (server crash, malformed messages)

#### 6.4 Test Helpers

- [ ] **`TestPipeHelper.cs`**
  - Generate unique test pipe names
  - Clean up test pipes after each test
  - Provide async pipe client for testing

- [ ] **`MockDataverseService.cs`**
  - Mock `IOrganizationService` implementation
  - Return predictable test data
  - Track method calls for assertions

- [ ] **`TestConstants.cs`**
  - Test connection strings
  - Test pipe names
  - Test timeouts
  - Sample tool definitions

#### 6.5 Platform-Specific Tests

- [ ] **Create `PlatformTests.cs`**
  - Test pipe path resolution on Windows vs Unix
  - Test executable permissions (Unix)
  - Test pipe naming restrictions
  - Use conditional compilation for platform-specific tests:
    ```csharp
    [Fact]
    [Trait("Category", "Windows")]
    public void WindowsPipePathTest() { ... }
    
    [Fact]
    [Trait("Category", "Unix")]
    public void UnixPipePathTest() { ... }
    ```

#### 6.6 Performance & Load Tests

- [ ] **Create `PerformanceTests.cs`**
  - Measure RPC call latency (target: < 50ms)
  - Test throughput (messages per second)
  - Test memory usage over time
  - Test with 10+ concurrent clients
  - Compare with baseline (TCP architecture if available)

#### 6.7 Test Execution & CI Integration

- [ ] **Update build scripts to run tests**
  - Add test execution to `scripts/build-all.sh`
  - Add test execution to `scripts/build-all.ps1`
  - Generate test coverage reports (optional)

- [ ] **Test execution command**:
  ```bash
  dotnet test Tests/DataverseMCPToolBox.Tests.csproj \
    --configuration Release \
    --logger "trx;LogFileName=test-results.trx" \
    --collect:"XPlat Code Coverage"
  ```

#### 6.8 Documentation Updates

- [ ] **Create `Tests/README.md`**
  - Document test project structure
  - Explain how to run tests locally
  - Explain test categories and filtering
  - Document mocking strategy

- [ ] **Update root `README.md`**
  - Add "Running Tests" section
  - Document test coverage goals
  - Link to test documentation

- [ ] **Update `.github/copilot-instructions.md`**
  - Replace TCP architecture documentation
  - Document Named Pipe architecture
  - Update error handling guide
  - Document testing approach

- [ ] **Create `MIGRATION_GUIDE.md`**
  - Document breaking changes from v1.x
  - Provide migration steps for users
  - FAQ for common issues

**New Files**:
- `Tests/DataverseMCPToolBox.Tests.csproj`
- All test files in `Tests/` directory
- `Tests/README.md`
- `MIGRATION_GUIDE.md`

**Files to Modify**:
- `scripts/build-all.sh`
- `scripts/build-all.ps1`
- `README.md`
- `.github/copilot-instructions.md`
- `dataverse-mcp-toolbox.sln` (add test project)

**Success Criteria**:
- ✅ Test project builds successfully
- ✅ All unit tests pass (services are transport-agnostic)
- ✅ All integration tests pass (complete workflows)
- ✅ Platform-specific tests pass on respective platforms
- ✅ Performance tests meet latency targets
- ✅ Test coverage > 70% (target: 80%+)
- ✅ Tests run automatically in build pipeline
- ✅ Documentation complete and clear

---

## Migration Guide

### From Current (TCP) → New (Named Pipes)

| Aspect | Old (TCP) | New (Named Pipes) |
|--------|-----------|-------------------|
| **Architecture** | Single .NET server (Master/Proxy modes) | 2 .NET apps (Core + Bridge) |
| **Extension → Server** | TCP Socket (port 9000) | Named Pipe (via env var) |
| **Copilot → Server** | STDIO Proxy → TCP | STDIO → Bridge → Named Pipe → Core |
| **Instance Isolation** | Port-based (single master) | Pipe-based (per VS Code instance) |
| **State Sharing** | In-memory in TCP master | In-memory in Core (per instance) |
| **Copilot Role** | Proxy forwards to master | Master (MCP Bridge is adapter) |

### Breaking Changes
- ⚠️ Port 9000 no longer used
- ⚠️ Copilot config must point to Bridge, not Core
- ⚠️ Multiple VS Code instances now have isolated states (feature, not bug)

---

## Testing Strategy

### Unit Tests
- [ ] Core: Named Pipe server creation and message handling
- [ ] Bridge: MCP protocol parsing and translation
- [ ] Extension: Pipe name generation and environment setup

### Integration Tests
- [ ] Extension → Core communication
- [ ] MCP Bridge → Core communication
- [ ] Full flow: Copilot → Bridge → Core → Dataverse

### Platform Tests
- [ ] macOS (arm64 and x64)
- [ ] Windows (x64)
- [ ] Linux (x64)

### Multi-Instance Tests
- [ ] 2+ VS Code windows with separate Core instances
- [ ] Verify no cross-talk between instances

---

## Error Handling

### Named Pipe Specific Errors

**Pipe Already Exists**:
```csharp
try {
    pipeServer = new NamedPipeServerStream(pipePath, PipeDirection.InOut, maxClients);
} catch (IOException ex) when (ex.Message.Contains("already in use")) {
    // Cleanup stale pipe or generate new name
}
```

**Pipe Not Found (Client)**:
```csharp
try {
    pipeClient.Connect(timeout);
} catch (TimeoutException) {
    Console.Error.WriteLine("Core Server not responding. Ensure it's running.");
}
```

**Extension Error Handling**:
```typescript
try {
    const pipe = net.connect(pipePath);
} catch (error) {
    vscode.window.showErrorMessage('Cannot connect to Core Server');
}
```

---

## Performance Considerations

### Named Pipe vs TCP
- **Latency**: Named Pipes ~10-50% faster than TCP loopback
- **Throughput**: Similar for small messages, better for large payloads
- **Security**: Named Pipes stay in kernel, TCP goes through network stack

### Concurrency
- Core Server must handle concurrent clients (Extension + potentially multiple Bridges)
- Use `maxNumberOfServerInstances` parameter in NamedPipeServerStream

---

## Security

### Access Control
- **Unix**: Pipe file permissions (`chmod 600 /tmp/dataverse-mcp-*`)
- **Windows**: Named Pipe ACLs (default: current user only)

### Recommendations
- Set restrictive permissions on pipe creation
- Validate all incoming messages (already done via JSON-RPC)
- Log access attempts for auditing

---

## Open Questions & Decisions

### Q1: Multiple MCP Bridges per Core?
**Decision**: YES - Core should accept multiple pipe clients (1 Extension + N Bridges)

### Q2: Pipe Cleanup on Crash?
**Decision**: Extension responsible for cleanup on deactivate. Implement stale pipe detection.

### Q3: Backwards Compatibility?
**Decision**: NO - This is a breaking change. Version bump to 2.0.0.

### Q4: Shared State Between Copilot and Extension?
**Decision**: YES - Both talk to same Core, so they share connections/state naturally.

---

## Rollout Plan

1. **Development**: Implement phases 1-3 on feature branch
2. **Internal Testing**: Test all scenarios on all platforms
3. **Beta Release**: Limited users (opt-in)
4. **Documentation**: Update all READs and guides
5. **Full Release**: Version 2.0.0

---

## Success Criteria

- [ ] Extension can connect to Core via Named Pipe
- [ ] Copilot can call tools via MCP Bridge → Core
- [ ] Multiple VS Code instances work independently
- [ ] All platforms supported (macOS, Windows, Linux)
- [ ] No stdout pollution (logs only to stderr)
- [ ] Performance equal or better than TCP
- [ ] Clean shutdown and process cleanup

---

## References

### Technical Documentation
- [.NET Named Pipes](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-use-named-pipes-for-network-interprocess-communication)
- [MCP Protocol Specification](https://modelcontextprotocol.io/docs/specification)
- [JSON-RPC 2.0 Spec](https://www.jsonrpc.org/specification)
- [Node.js Named Pipes](https://nodejs.org/api/net.html#ipc-support)

### Related Files
- `PROJECT_CONTEXT.md`: Original architecture documentation
- `.github/copilot-instructions.md`: Current TCP architecture (to be updated)
- `Core/README.md`: Core server documentation
- `Bridge/README.md`: MCP Bridge documentation (to be created)

---

## Changelog

### 2026-02-03
- Initial architecture rework planning document created
- Defined sidecar pattern with Named Pipes
- Outlined 5 implementation phases
- Identified breaking changes and migration path

---

**Status**: Ready for implementation  
**Next Steps**: Begin Phase 1 - Core Server Refactoring
