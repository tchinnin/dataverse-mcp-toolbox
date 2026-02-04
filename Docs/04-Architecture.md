# Architecture Overview

A comprehensive look at the Dataverse MCP Toolbox architecture, component relationships, and design principles.

## System Architecture

```mermaid
graph TB
    subgraph "User Space"
        User[👤 Developer]
        VSCode[VS Code Application]
    end
    
    subgraph "Extension Layer"
        UI[Extension UI<br/>Commands, TreeViews, Panels]
        ServerMgr[Server Manager<br/>Binary Lifecycle]
        RpcClient[RPC Client<br/>Named Pipe]
        Storage[Storage Services<br/>State & Secrets]
        McpProvider[MCP Provider<br/>Copilot Integration]
    end
    
    subgraph "Sidecar Layer"
        direction LR
        
        subgraph Core["Core Server (.NET)"]
            direction TB
            CoreRpc[RPC Service<br/>Request Handler]
            ConnService[Connection Services]
            AuthService[Auth Services]
            ToolService[Tool Execution]
            PluginService[Plugin Manager]
            CoreState[State Manager]
        end
        
        subgraph Bridge["MCP Bridge (.NET)"]
            direction TB
            StdioHandler[STDIO Handler<br/>MCP Protocol]
            PipeClient[Named Pipe Client]
            Forwarder[Request Forwarder]
        end
    end
    
    subgraph "External Services"
        Copilot[GitHub Copilot]
        Dataverse[(Microsoft Dataverse)]
        NuGet[NuGet.org]
        AAD[Azure AD]
    end
    
    User --> VSCode
    VSCode --> UI
    UI --> ServerMgr
    UI --> RpcClient
    UI --> Storage
    UI --> McpProvider
    
    ServerMgr -->|Spawns| Core
    ServerMgr -->|Downloads| NuGet
    RpcClient -->|Named Pipe| CoreRpc
    McpProvider -->|Registers| Bridge
    
    Copilot -->|STDIO/MCP| StdioHandler
    StdioHandler --> Forwarder
    Forwarder --> PipeClient
    PipeClient -->|Named Pipe| CoreRpc
    
    CoreRpc --> ConnService
    CoreRpc --> ToolService
    CoreRpc --> PluginService
    ConnService --> AuthService
    ToolService --> PluginService
    
    ConnService -->|SDK| Dataverse
    AuthService -->|MSAL| AAD
    PluginService -->|Downloads| NuGet
    
    style "Extension Layer" fill:#e1f5ff
    style Core fill:#fff4e1
    style Bridge fill:#ffe1f5
    style "External Services" fill:#e1ffe1
```

## Design Principles

### 1. Separation of Concerns

Each component has a single, well-defined responsibility:

| Component | Responsibility | Does NOT Handle |
|-----------|----------------|-----------------|
| **Extension** | UI, lifecycle, storage | Dataverse operations, auth |
| **Core Server** | Business logic, Dataverse SDK | UI, MCP protocol |
| **MCP Bridge** | Protocol translation | Business logic, state |

### 2. Transport Agnostic Services

```mermaid
graph TB
    subgraph "Service Layer - Transport Agnostic"
        ConnService[Connection Service]
        AuthService[Auth Service]
        ToolService[Tool Service]
    end
    
    subgraph "Communication Layer - Pluggable"
        NamedPipe[Named Pipe Transport]
        TCP[TCP Transport<br/>Future]
        HTTP[HTTP Transport<br/>Future]
    end
    
    RpcService[RPC Service Layer] --> ConnService
    RpcService --> AuthService
    RpcService --> ToolService
    
    NamedPipe --> RpcService
    TCP -.->|Future| RpcService
    HTTP -.->|Future| RpcService
    
    style "Service Layer - Transport Agnostic" fill:#e1ffe1
    style "Communication Layer - Pluggable" fill:#ffe1e1
```

**Benefits:**
- Services can be tested without transport
- Transport can be swapped without changing business logic
- Services reusable in different contexts

### 3. Instance Isolation

```mermaid
graph TB
    subgraph OS[Operating System]
        subgraph Instance1[VS Code Window 1]
            Ext1[Extension<br/>Instance 1]
            Core1[Core Server<br/>PID 1234<br/>Pipe: dvmcp-abc]
            Bridge1[Bridge<br/>PID 1235]
        end
        
        subgraph Instance2[VS Code Window 2]
            Ext2[Extension<br/>Instance 2]
            Core2[Core Server<br/>PID 5678<br/>Pipe: dvmcp-xyz]
            Bridge2[Bridge<br/>PID 5679]
        end
        
        subgraph Instance3[VS Code Window 3]
            Ext3[Extension<br/>Instance 3]
            Core3[Core Server<br/>PID 9012<br/>Pipe: dvmcp-qwe]
            Bridge3[Bridge<br/>PID 9013]
        end
    end
    
    Dataverse[(Dataverse)]
    
    Core1 --> Dataverse
    Core2 --> Dataverse
    Core3 --> Dataverse
    
    style Instance1 fill:#e1f5ff
    style Instance2 fill:#ffe1f5
    style Instance3 fill:#fff4e1
```

**Isolation Mechanisms:**
- Unique named pipe per instance
- Separate process space
- Independent in-memory state
- No shared file locks

### 4. Defense in Depth

```mermaid
graph LR
    Input[User Input] --> Validation1[Extension Validation]
    Validation1 --> Validation2[Core Validation]
    Validation2 --> Validation3[Plugin Validation]
    Validation3 --> Sanitization[Input Sanitization]
    Sanitization --> Execution[Safe Execution]
    
    style Validation1 fill:#ffe1e1
    style Validation2 fill:#ffe1e1
    style Validation3 fill:#ffe1e1
```

**Security Layers:**
- Input validation at every boundary
- Token encryption in VS Code SecretStorage
- Plugin isolation via AppDomain
- Least privilege principle

## Component Communication Flow

### Extension → Core Server (Direct Operations)

```mermaid
sequenceDiagram
    participant UI as Extension UI
    participant RpcClient as RPC Client
    participant Pipe as Named Pipe
    participant Core as Core Server
    participant Service as Service Layer
    
    UI->>RpcClient: CreateConnection(url)
    RpcClient->>RpcClient: Serialize to JSON-RPC
    RpcClient->>Pipe: Write Request
    Pipe->>Core: Named Pipe IPC
    Core->>Core: Deserialize & Route
    Core->>Service: CreateConnectionAsync()
    Service->>Service: Authenticate & Test
    Service->>Core: ConnectionResult
    Core->>Core: Serialize Response
    Core->>Pipe: Write Response
    Pipe->>RpcClient: Read Response
    RpcClient->>RpcClient: Deserialize
    RpcClient->>UI: Result
```

### Copilot → Bridge → Core (Tool Execution)

```mermaid
sequenceDiagram
    participant Copilot as GitHub Copilot
    participant Bridge as MCP Bridge
    participant Core as Core Server
    participant Plugin as Plugin Tool
    participant DV as Dataverse
    
    Note over Copilot,Bridge: MCP Protocol (STDIO)
    Copilot->>Bridge: tools/call (stdin)
    Bridge->>Bridge: Parse MCP Request
    Bridge->>Bridge: Translate to Core RPC
    
    Note over Bridge,Core: Core RPC (Named Pipe)
    Bridge->>Core: ExecuteTool(name, args)
    Core->>Core: Validate & Route
    Core->>Core: Get Active Connection
    Core->>Plugin: ExecuteAsync(context)
    Plugin->>DV: SDK Operation
    DV->>Plugin: Result
    Plugin->>Core: Tool Result
    Core->>Bridge: RPC Response
    
    Bridge->>Bridge: Translate to MCP
    Bridge->>Copilot: MCP Result (stdout)
```

## Data Flow

### Authentication Flow

```mermaid
sequenceDiagram
    participant User
    participant Ext as Extension
    participant Core as Core Server
    participant Browser
    participant AAD as Azure AD
    participant Storage as Secret Storage
    participant DV as Dataverse
    
    User->>Ext: Create Connection
    Ext->>Core: CreateConnection(url)
    Core->>Browser: Open OAuth Flow
    Browser->>AAD: Authenticate
    User->>Browser: Enter Credentials
    AAD->>Browser: Authorization Code
    Browser->>Core: Redirect with Code
    Core->>AAD: Exchange Code for Token
    AAD->>Core: Access Token
    Core->>Ext: Token (encrypted in transit)
    Ext->>Storage: Store Token (encrypted)
    Core->>DV: WhoAmI (test connection)
    DV->>Core: User Info
    Core->>Ext: Connection Success + User Info
    Ext->>User: Connection Ready
```

### Plugin Installation Flow

```mermaid
sequenceDiagram
    participant User
    participant UI as Extension UI
    participant Mgr as Plugin Manager (Ext)
    participant NuGet as NuGet.org
    participant FS as Filesystem
    participant Core as Core Server
    
    User->>UI: Install Plugin (packageId)
    UI->>Mgr: InstallPlugin(packageId, version)
    Mgr->>NuGet: Download Package
    NuGet->>Mgr: .nupkg File
    Mgr->>FS: Extract to Plugin Directory
    FS->>Mgr: Extraction Complete
    Mgr->>Core: ReloadPlugins()
    Core->>FS: Scan Plugin Directory
    Core->>Core: Load Assemblies
    Core->>Core: Discover Tools
    Core->>Core: Register in Tool Registry
    Core->>Mgr: Plugin Loaded
    Mgr->>UI: Installation Success
    UI->>User: Plugin Available
```

## State Management Architecture

### State Distribution

```mermaid
graph TB
    subgraph "Core Server - In-Memory (Volatile)"
        ConnectionPool[Active ServiceClient Pool<br/>Dictionary&lt;string, IOrganizationService&gt;]
        ToolRegistry[Tool Registry<br/>Dictionary&lt;string, IMcpTool&gt;]
        ActiveConn[Active Connection ID<br/>string?]
        PluginCache[Loaded Plugin Assemblies<br/>List&lt;Assembly&gt;]
    end
    
    subgraph "Extension - Persistent"
        ConnMetadata[Connection Metadata<br/>WorkspaceState<br/>id, name, url]
        Tokens[OAuth Tokens<br/>SecretStorage<br/>Encrypted]
        Settings[User Settings<br/>update channel, version]
    end
    
    subgraph "Filesystem - Persistent"
        Binaries[Runtime Binaries<br/>globalStorageUri/server/]
        Plugins[Plugin Assemblies<br/>globalStorageUri/plugins/]
    end
    
    Restart[Server Restart] -.->|Clears| ConnectionPool
    Restart -.->|Clears| ToolRegistry
    Restart -.->|Clears| ActiveConn
    
    Restart -.->|Reloads from| Plugins
    
    style "Core Server - In-Memory (Volatile)" fill:#ffe1e1
    style "Extension - Persistent" fill:#e1f5ff
    style "Filesystem - Persistent" fill:#fff4e1
```

### State Lifecycle

```mermaid
stateDiagram-v2
    [*] --> ExtensionStart: VS Code Opens
    
    ExtensionStart --> BinaryCheck: Extension Activates
    BinaryCheck --> Download: Binaries Missing
    BinaryCheck --> CoreStart: Binaries Present
    Download --> CoreStart: Download Complete
    
    CoreStart --> ServerRunning: Core Process Spawned
    
    ServerRunning --> LoadPlugins: Initial Load
    LoadPlugins --> Ready: Server Ready
    
    Ready --> ProcessingRequest: Request Received
    ProcessingRequest --> Ready: Request Complete
    
    Ready --> ExtensionClose: VS Code Closes
    ExtensionClose --> CleanupState: Shutdown Signal
    CleanupState --> [*]: Process Terminated
    
    note right of ServerRunning
        In-Memory State:
        - Empty connection pool
        - Empty tool registry
        - No active connection
    end note
    
    note right of Ready
        State Populated:
        - Connections created
        - Plugins loaded
        - Tools registered
    end note
    
    note right of CleanupState
        State Destroyed:
        - Connections disposed
        - Assemblies unloaded
        - Memory freed
    end note
```

## Error Handling Strategy

### Error Propagation

```mermaid
graph TB
    Error[Error Occurs] --> Layer{Which Layer?}
    
    Layer -->|Dataverse SDK| DvError[DataverseException]
    Layer -->|Plugin Code| PluginError[PluginException]
    Layer -->|Validation| ValidationError[ValidationException]
    Layer -->|Auth| AuthError[AuthenticationException]
    
    DvError --> Wrap[Wrap in ToolCallResult]
    PluginError --> Wrap
    ValidationError --> Wrap
    AuthError --> Wrap
    
    Wrap --> RpcResponse[RPC Error Response]
    RpcResponse --> Client{Client Type?}
    
    Client -->|Extension| UIError[VS Code Error Message]
    Client -->|Bridge/Copilot| McpError[MCP Error Response]
    
    UIError --> User[User Sees Friendly Message]
    McpError --> AI[AI Receives Structured Error]
    
    style DvError fill:#ffe1e1
    style PluginError fill:#ffe1e1
    style ValidationError fill:#ffe1e1
    style AuthError fill:#ffe1e1
```

### Error Response Structure

```mermaid
classDiagram
    class ToolCallResult {
        +bool Success
        +object? Content
        +ToolCallError? Error
        +List~string~? IsError
    }
    
    class ToolCallError {
        +string Code
        +string Message
        +string? Details
        +Dictionary~string,object~? Data
    }
    
    class ErrorCodes {
        <<enumeration>>
        VALIDATION_ERROR
        DATAVERSE_ERROR
        PLUGIN_ERROR
        NOT_FOUND
        UNAUTHORIZED
        TIMEOUT
    }
    
    ToolCallResult --> ToolCallError
    ToolCallError --> ErrorCodes
```

## Platform-Specific Adaptations

### Named Pipe Implementation

```mermaid
graph TB
    Decision{Operating<br/>System?}
    
    Decision -->|Windows| WinPipe[Named Pipe<br/>\\.\pipe\dvmcp-{id}]
    Decision -->|macOS/Linux| UnixSocket[Unix Domain Socket<br/>/tmp/dvmcp-sockets/CoreFxPipe_dvmcp-{id}]
    
    WinPipe --> WinAPI[Windows Named Pipe API<br/>CreateNamedPipe]
    UnixSocket --> PosixAPI[POSIX Socket API<br/>Unix Domain Socket]
    
    WinAPI --> Communicate[JSON-RPC Communication]
    PosixAPI --> Communicate
    
    Communicate --> LengthLimit{Path Length}
    LengthLimit -->|Windows| NoLimit[No practical limit]
    LengthLimit -->|Unix| Limit104[104 chars maximum]
    
    Limit104 --> Shorten[Use short IDs + temp dirs]
    
    style WinPipe fill:#e1f5ff
    style UnixSocket fill:#ffe1f5
```

### Binary Selection

```mermaid
graph TB
    Start[Extension Starts] --> Detect[Detect Platform & Arch]
    Detect --> Platform{Platform?}
    
    Platform -->|process.platform=darwin| Mac[macOS]
    Platform -->|process.platform=win32| Windows[Windows]
    Platform -->|process.platform=linux| Linux[Linux]
    
    Mac --> MacArch{process.arch?}
    MacArch -->|arm64| ARM[osx-arm64<br/>Apple Silicon]
    MacArch -->|x64| IntelMac[osx-x64<br/>Intel Mac]
    
    Windows --> Win64[win-x64<br/>Windows 64-bit]
    Linux --> Linux64[linux-x64<br/>Linux 64-bit]
    
    ARM --> ExtractBinary[Extract Binary from NuGet]
    IntelMac --> ExtractBinary
    Win64 --> ExtractBinary
    Linux64 --> ExtractBinary
    
    ExtractBinary --> Unix{Unix-like?}
    Unix -->|Yes| Chmod[chmod +x<br/>Make Executable]
    Unix -->|No| Spawn[Spawn Process]
    Chmod --> Spawn
    
    style ARM fill:#e1ffe1
    style IntelMac fill:#e1ffe1
    style Win64 fill:#ffe1e1
    style Linux64 fill:#fff4e1
```

## Scalability Considerations

### Multi-Instance Behavior

```mermaid
graph TB
    subgraph Machine[Developer Machine]
        subgraph Window1[VS Code Window 1]
            E1[Extension 1]
            C1[Core 1]
            E1 -->|Pipe A| C1
        end
        
        subgraph Window2[VS Code Window 2]
            E2[Extension 2]
            C2[Core 2]
            E2 -->|Pipe B| C2
        end
        
        subgraph Window3[VS Code Window 3]
            E3[Extension 3]
            C3[Core 3]
            E3 -->|Pipe C| C3
        end
    end
    
    AAD[Azure AD<br/>Token Endpoint]
    DV[(Dataverse)]
    
    C1 -->|Independent Auth| AAD
    C2 -->|Independent Auth| AAD
    C3 -->|Independent Auth| AAD
    
    C1 -->|Connection A| DV
    C2 -->|Connection B| DV
    C3 -->|Connection C| DV
    
    style Window1 fill:#e1f5ff
    style Window2 fill:#ffe1f5
    style Window3 fill:#fff4e1
```

**No Central Coordination:**
- Each instance operates independently
- No shared memory or state
- No inter-instance communication
- No resource contention

## Next Steps

- **[Core Server](05-Core-Server.md)**: Detailed Core Server architecture
- **[MCP Bridge](06-MCP-Bridge.md)**: MCP Bridge implementation
- **[VS Code Extension](07-VS-Code-Extension.md)**: Extension architecture
- **[Communication Protocol](08-Communication.md)**: RPC protocol details
