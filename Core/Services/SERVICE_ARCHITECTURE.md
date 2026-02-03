# Core Services Architecture

## Overview

The Core Server follows **clean architecture principles** with strict separation of concerns. Business logic services are **completely independent** from the communication layer (Named Pipes, TCP, etc.).

## Architecture Principles

### 1. Transport Agnosticism
**Services MUST NOT depend on communication protocols**
- ✅ Services work with pure .NET types and interfaces
- ✅ No references to `NamedPipeServerStream`, `TcpClient`, `Socket`, or `Stream`
- ✅ Services can be tested without network layers
- ✅ Services can be reused in any hosting scenario

### 2. Dependency Injection
**Services receive dependencies via constructor**
- Clear dependency graph
- Easy to mock for testing
- No hidden dependencies or service locators

### 3. Single Responsibility
**Each service has one well-defined purpose**
- Easy to understand and maintain
- Changes are localized
- Clear interfaces

---

## Service Catalog

### Business Logic Services (Transport-Agnostic)

#### 1. ConnectionStateService
**Purpose**: In-memory state management for connections  
**Responsibility**: CRUD operations for connection metadata  
**Dependencies**: None  
**State**: Thread-safe in-memory dictionary  
**Location**: `Core/Services/ConnectionStateService.cs`

**Key Methods**:
- `SaveConnectionAsync()` - Store connection info
- `GetConnectionAsync()` - Retrieve connection by ID
- `SetActiveConnectionAsync()` - Mark connection as active
- `GetAllConnectionsAsync()` - List all connections

**Transport Independence**: ✅ Complete
- Pure in-memory operations
- No I/O dependencies
- Thread-safe with locks

---

#### 2. DataverseConnectionService
**Purpose**: Manage Dataverse SDK connections  
**Responsibility**: Create, test, and maintain `ServiceClient` instances  
**Dependencies**: `ConnectionStateService`, `DataverseAuthService`  
**State**: Active connection pool (Dictionary)  
**Location**: `Core/Services/DataverseConnectionService.cs`

**Key Methods**:
- `CreateConnectionAsync()` - Authenticate and create connection
- `TestConnectionAsync()` - Verify connection health
- `GetConnection()` - Retrieve active `ServiceClient`
- `CloseConnection()` - Dispose connection

**Transport Independence**: ✅ Complete
- Only interacts with Dataverse SDK
- Uses dependency-injected services
- No network protocol knowledge

---

#### 3. DataverseAuthService
**Purpose**: OAuth authentication via MSAL  
**Responsibility**: Acquire and refresh access tokens  
**Dependencies**: Microsoft.Identity.Client (MSAL)  
**State**: Token cache managed by MSAL  
**Location**: `Core/Services/DataverseAuthService.cs`

**Key Methods**:
- `AuthenticateInteractiveAsync()` - Browser-based OAuth flow
- `AuthenticateWithTokenAsync()` - Silent token refresh

**Transport Independence**: ✅ Complete
- Pure authentication logic
- No transport layer coupling
- Reusable in any context

---

#### 4. ToolExecutionService
**Purpose**: Execute MCP tools against Dataverse  
**Responsibility**: Orchestrate tool calls with connection context  
**Dependencies**: `ToolRegistryService`, `DataverseConnectionService`  
**State**: Stateless (uses injected services)  
**Location**: `Core/Services/ToolExecutionService.cs`

**Key Methods**:
- `ExecuteToolAsync()` - Run a tool by name with connection

**Transport Independence**: ✅ Complete
- Works with abstract tool interfaces
- No knowledge of RPC layer
- Pure orchestration logic

---

#### 5. PluginManager
**Purpose**: Plugin lifecycle management  
**Responsibility**: Install, load, unload plugins  
**Dependencies**: `PluginPackageService`, `PluginLoaderService`, `ToolRegistryService`  
**State**: Plugin registry (in-memory)  
**Location**: `Core/Services/PluginManager.cs`

**Key Methods**:
- `InstallPluginAsync()` - Download and install NuGet package
- `ReloadPluginsAsync()` - Scan and load plugins from disk
- `UnloadPluginAsync()` - Remove plugin from registry
- `GetAllPluginsAsync()` - List loaded plugins

**Transport Independence**: ✅ Complete
- File system operations only
- No transport dependencies
- Assembly loading logic

---

#### 6. ToolRegistryService
**Purpose**: Index and lookup MCP tools  
**Responsibility**: Maintain tool catalog from loaded plugins  
**Dependencies**: None (receives plugin list)  
**State**: Tool registry (Dictionary)  
**Location**: `Core/Services/ToolRegistryService.cs`

**Key Methods**:
- `RegisterPlugins()` - Index tools from plugin list
- `GetTool()` - Retrieve tool by name
- `GetAllTools()` - List all registered tools

**Transport Independence**: ✅ Complete
- Pure in-memory indexing
- No external dependencies
- Data structure operations only

---

#### 7. InputValidator
**Purpose**: Request validation  
**Responsibility**: Validate input data before processing  
**Dependencies**: None  
**State**: Stateless  
**Location**: `Core/Services/InputValidator.cs`

**Key Methods**:
- `ValidateConnectionRequest()` - Validate connection parameters
- `ValidateToolCallRequest()` - Validate tool execution parameters
- `ValidateDirectoryPath()` - Validate file paths

**Transport Independence**: ✅ Complete
- Pure validation logic
- No side effects
- Reusable across contexts

---

### Communication Layer (Transport-Specific)

These are the ONLY components that should know about transport protocols.

#### 8. NamedPipeRpcServer (Future - Phase 2)
**Purpose**: Named Pipe server implementation  
**Responsibility**: Listen for connections, handle JSON-RPC over Named Pipes  
**Dependencies**: `IRpcServer` interface  
**Location**: `Core/Services/NamedPipeRpcServer.cs` (to be created)

**Will Implement**: `IRpcServer` interface

---

#### 9. DataverseMCPToolBoxRpcService
**Purpose**: JSON-RPC service facade  
**Responsibility**: Expose business services via RPC methods  
**Dependencies**: All business logic services  
**Location**: `Core/JsonRpc/DataverseMCPToolBoxRpcService.cs`

**Role**: Adapter between RPC layer and business services
- Receives RPC calls from clients
- Delegates to appropriate business service
- Returns results in RPC format

---

## Dependency Graph

```
┌─────────────────────────────────────────────────┐
│         Communication Layer                     │
│  (NamedPipeRpcServer, RPC Service)              │
└────────────────┬────────────────────────────────┘
                 │ depends on
                 ▼
┌─────────────────────────────────────────────────┐
│         Business Logic Services                 │
│  (ConnectionService, ToolExecution, etc.)       │
└────────────────┬────────────────────────────────┘
                 │ depends on
                 ▼
┌─────────────────────────────────────────────────┐
│         External Dependencies                   │
│  (Dataverse SDK, MSAL, File System)             │
└─────────────────────────────────────────────────┘
```

### Key Principle
**Communication Layer → Business Logic → External Dependencies**
- Communication layer can be replaced without touching business logic
- Business logic can be tested without communication layer
- Clean separation enables maintainability

---

## Service Interactions

### Example: Tool Execution Flow

```
1. RPC Client (Extension/Bridge)
   ↓ (Named Pipe)
2. NamedPipeRpcServer
   ↓ (JSON-RPC)
3. DataverseMCPToolBoxRpcService
   ↓ (method call)
4. ToolExecutionService
   ├─→ ToolRegistryService.GetTool()
   └─→ DataverseConnectionService.GetConnection()
       ↓
   Execute Tool with Connection Context
```

**Transport Independence**: Steps 4+ have ZERO knowledge of Named Pipes

---

## Testing Strategy

### Unit Tests (Transport-Free)
```csharp
// Example: Test ToolExecutionService without any transport
var mockRegistry = new Mock<ToolRegistryService>();
var mockConnectionService = new Mock<DataverseConnectionService>();
var service = new ToolExecutionService(mockRegistry.Object, mockConnectionService.Object);

var result = await service.ExecuteToolAsync(request);
// Assert result
```

### Integration Tests (With Transport)
```csharp
// Start real Named Pipe server
var rpcServer = new NamedPipeRpcServer(pipeName);
rpcServer.RegisterService(realBusinessService);
await rpcServer.StartAsync();

// Connect client and test
var client = new NamedPipeRpcClient(pipeName);
await client.ConnectAsync();
var result = await client.InvokeAsync<Result>("ExecuteTool", args);
```

---

## Adding New Services

### Checklist for New Services

- [ ] **No transport dependencies** - Service doesn't reference Named Pipes, TCP, etc.
- [ ] **Constructor injection** - Dependencies passed via constructor
- [ ] **Interface-based** - Consider creating an interface for the service
- [ ] **Async methods** - Use `Task`/`Task<T>` for I/O operations
- [ ] **Error handling** - Catch and log errors, return structured error objects
- [ ] **Logging** - Use `Console.Error.WriteLine()` for all logs
- [ ] **Validation** - Validate inputs using `InputValidator` or similar
- [ ] **Thread-safe** - Use locks for shared state if needed
- [ ] **Testable** - Can be instantiated and tested without network

### Anti-Patterns to Avoid

❌ **Direct transport coupling**
```csharp
public class BadService
{
    private NamedPipeServerStream _pipe; // ❌ WRONG!
    
    public BadService(NamedPipeServerStream pipe)
    {
        _pipe = pipe;
    }
}
```

✅ **Transport abstraction**
```csharp
public class GoodService
{
    private readonly IConnectionService _connectionService; // ✅ GOOD
    
    public GoodService(IConnectionService connectionService)
    {
        _connectionService = connectionService;
    }
}
```

---

## Service Lifecycle

### Singleton Services (Per Core Instance)
These services live for the entire Core Server lifetime:
- `ConnectionStateService` - State must persist across RPC calls
- `DataverseConnectionService` - Connection pool must be shared
- `PluginManager` - Plugin registry must be shared
- `ToolRegistryService` - Tool catalog must be shared
- `DataverseMCPToolBoxRpcService` - Single RPC facade

### Transient Services (Per Request)
These services can be created per-request if needed:
- `InputValidator` - Stateless
- `DataverseAuthService` - MSAL manages state internally

---

## Configuration

Services read configuration from:
1. **Environment Variables** - `DATAVERSE_MCP_PIPE_NAME`, `DATAVERSE_MCP_PLUGIN_DIR`
2. **Constructor Parameters** - Plugin directory path, etc.
3. **Constants** - Client IDs, URLs (DataverseAuthService)

---

## Future Enhancements

### Potential Improvements
1. **Service Interfaces** - Create `IDataverseConnectionService`, etc. for better testability
2. **Logging Abstraction** - Replace `Console.Error.WriteLine()` with `ILogger`
3. **Configuration Service** - Centralized configuration management
4. **Health Checks** - Service health monitoring
5. **Metrics** - Performance tracking and telemetry

---

## Summary

✅ **All business services are transport-agnostic**  
✅ **Clean separation between communication and business logic**  
✅ **Services are testable without network layers**  
✅ **Architecture supports future transport changes**  

**The only components that know about Named Pipes are:**
- `NamedPipeRpcServer` (to be created in Phase 2)
- `Program.cs` (startup/orchestration)

**Everything else is pure business logic.** 🎯
