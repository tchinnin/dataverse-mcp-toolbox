# Core Concepts

Understanding the fundamental concepts behind Dataverse MCP Toolbox.

## The Sidecar Architecture Pattern

The toolbox follows a **sidecar pattern** where specialized components run alongside the main application (VS Code) to provide additional functionality without cluttering the main process.

```mermaid
graph TB
    subgraph "VS Code Instance"
        UI[Extension UI Layer<br/>TypeScript]
        Copilot[GitHub Copilot]
    end
    
    subgraph "Sidecar Processes"
        Core[Core Server<br/>Business Logic]
        Bridge[MCP Bridge<br/>Protocol Adapter]
    end
    
    subgraph "External Services"
        DV[(Dataverse)]
        NuGet[NuGet.org]
    end
    
    UI -->|Spawns & Manages| Core
    UI -->|Named Pipe| Core
    UI -->|Registers| Bridge
    Copilot -->|STDIO| Bridge
    Bridge -->|Named Pipe| Core
    Core -->|SDK| DV
    UI -->|Downloads| NuGet
    
    style UI fill:#e1f5ff
    style Core fill:#fff4e1
    style Bridge fill:#ffe1f5
    style DV fill:#e1ffe1
```

### Why Sidecar?

- **Separation of Concerns**: UI, business logic, and protocol adaptation are independent
- **Language Flexibility**: Use .NET for Dataverse SDK, TypeScript for VS Code
- **Isolation**: Each VS Code window has its own isolated server instance
- **Reliability**: Crashes in one component don't affect others
- **Performance**: Native .NET performance for Dataverse operations

## Model Context Protocol (MCP)

MCP is a protocol that enables AI assistants to access external tools and data sources in a standardized way.

```mermaid
sequenceDiagram
    participant AI as AI Assistant
    participant Server as MCP Server
    participant Tool as Tool Implementation
    
    Note over AI,Server: Discovery Phase
    AI->>Server: initialize
    Server->>AI: Server Capabilities
    AI->>Server: tools/list
    Server->>AI: Available Tools + Schemas
    
    Note over AI,Server: Execution Phase
    AI->>Server: tools/call (name, arguments)
    Server->>Tool: Execute Tool
    Tool->>Server: Result
    Server->>AI: Tool Response
```

### Key MCP Concepts

#### Tools
- **Definition**: Callable functions with defined inputs and outputs
- **Schema**: JSON Schema describing parameters
- **Execution**: Invoked by AI with validated arguments
- **Example**: `list-entities`, `get-record`, `create-record`

#### Resources
- **Definition**: Data sources that can be read
- **Access**: Retrieved by URI
- **Example**: Connection metadata, plugin information

#### Prompts
- **Definition**: Predefined prompt templates
- **Purpose**: Guide AI interactions
- **Example**: "Query all accounts with revenue > $1M"

## Components Deep Dive

### Core Server

The **brain** of the system - handles all business logic and Dataverse operations.

```mermaid
graph TB
    subgraph Core[Core Server Process]
        RPC[RPC Service Layer]
        
        subgraph Services
            ConnState[ConnectionStateService<br/>In-Memory State]
            ConnMgr[DataverseConnectionService<br/>SDK Pool]
            Auth[DataverseAuthService<br/>OAuth/MSAL]
            ToolExec[ToolExecutionService<br/>Orchestration]
            PluginMgr[PluginManager<br/>Dynamic Loading]
        end
        
        Pipe[Named Pipe Server]
    end
    
    External[External Clients]
    DV[(Dataverse)]
    
    External -->|JSON-RPC| Pipe
    Pipe --> RPC
    RPC --> Services
    ConnMgr -->|SDK| DV
    Auth -->|MSAL| DV
    
    style Core fill:#fff4e1
    style Services fill:#ffe1e1
```

**Responsibilities:**
- Manage Dataverse SDK connections
- Authenticate users via OAuth
- Load and manage plugins
- Execute tools with proper context
- Maintain in-memory state
- Expose RPC interface

**Lifecycle:**
- Started by Extension on activation
- Runs until VS Code window closes
- One instance per VS Code window
- Communicates via unique named pipe

### MCP Bridge

The **translator** - converts between MCP protocol and Core Server's RPC interface.

```mermaid
graph LR
    Copilot[GitHub Copilot<br/>MCP Client] 
    
    subgraph Bridge[MCP Bridge Process]
        STDIO[STDIO Handler<br/>stdin/stdout]
        Translator[Protocol Translator]
        PipeClient[Named Pipe Client]
    end
    
    Core[Core Server]
    
    Copilot <-->|MCP JSON-RPC| STDIO
    STDIO <--> Translator
    Translator <--> PipeClient
    PipeClient <-->|Core RPC| Core
    
    style Bridge fill:#ffe1f5
```

**Responsibilities:**
- Listen to GitHub Copilot on stdin/stdout
- Translate MCP protocol to Core RPC calls
- Forward requests and responses bidirectionally
- No business logic - pure protocol adaptation

**Lifecycle:**
- Started by GitHub Copilot on demand
- Managed by VS Code's MCP system
- Connects to Core Server via named pipe
- Terminates when Copilot session ends

### VS Code Extension

The **face** - provides UI and manages the system lifecycle.

```mermaid
graph TB
    subgraph Extension[VS Code Extension]
        Commands[Commands<br/>User Actions]
        TreeView[Tree View Providers<br/>Sidebar UI]
        Panels[WebView Panels<br/>Detailed Views]
        
        subgraph Services
            ServerMgr[ServerManager<br/>Binary Management]
            RpcClient[RPC Client<br/>Core Communication]
            Storage[StorageService<br/>Persistence]
            McpProvider[McpProvider<br/>Copilot Integration]
        end
    end
    
    User[User] --> Commands
    User --> TreeView
    Commands --> Services
    TreeView --> Services
    ServerMgr -->|Spawns| Core[Core Server]
    RpcClient -->|Named Pipe| Core
    McpProvider -->|Registers| Bridge[MCP Bridge]
    
    style Extension fill:#e1f5ff
```

**Responsibilities:**
- Download and manage runtime binaries
- Spawn and monitor Core Server process
- Provide UI for connections and plugins
- Store connection metadata and tokens
- Register MCP Bridge with Copilot
- Handle user commands

**Lifecycle:**
- Activated when VS Code opens workspace
- Downloads binaries if needed
- Starts Core Server
- Registers Bridge configuration
- Deactivates on window close

## Connections

A **connection** represents authenticated access to a Dataverse environment.

```mermaid
stateDiagram-v2
    [*] --> Creating: User Provides URL
    Creating --> Authenticating: Request Created
    Authenticating --> Testing: Token Acquired
    Testing --> Active: WhoAmI Success
    Testing --> Failed: Connection Error
    
    Active --> Inactive: Deactivate
    Inactive --> Active: Activate
    
    Active --> Refreshing: Token Expired
    Refreshing --> Active: Token Refreshed
    Refreshing --> Failed: Refresh Failed
    
    Failed --> [*]: Delete
    Active --> [*]: Delete
    Inactive --> [*]: Delete
```

**Connection Properties:**
- **ID**: Unique identifier (GUID)
- **Name**: User-friendly label
- **URL**: Dataverse environment URL
- **Status**: Active, Inactive, or Error
- **Token**: OAuth access token (encrypted)
- **User Info**: Cached WhoAmI result

**Active Connection:**
- Only one connection can be active at a time
- Active connection is used for all tool executions
- Tools receive `IDataverseContext` with active ServiceClient

## Plugins

Plugins extend the toolbox with new capabilities.

```mermaid
graph TB
    Dev[Plugin Developer] -->|Creates| Plugin[Plugin Project<br/>.NET]
    Plugin -->|References| SDK[Extensibility SDK]
    Plugin -->|Packages| NuGet[NuGet Package]
    
    User[User] -->|Installs| UI[Extension UI]
    UI -->|Downloads| NuGet
    NuGet -->|Extracts| Storage[Plugin Directory]
    
    Core[Core Server] -->|Loads| Storage
    Storage -->|Assembly| Loader[Plugin Loader]
    Loader -->|Discovers| Tools[Tool Instances]
    Tools -->|Registers| Registry[Tool Registry]
    
    Copilot[Copilot] -->|Calls| Registry
    
    style Plugin fill:#e1ffe1
    style SDK fill:#fff4e1
    style Core fill:#ffe1f5
```

**Plugin Structure:**
- **Manifest**: Metadata (name, version, author)
- **Tools**: One or more tool implementations
- **Dependencies**: Managed by NuGet
- **Lifecycle**: Loaded dynamically by Core Server

**Tool Definition:**
```mermaid
classDiagram
    class IMcpTool {
        <<interface>>
        +Name string
        +Description string
        +InputSchema JsonSchema
        +ExecuteAsync() Task
    }
    
    class McpToolBase~TInput,TOutput~ {
        <<abstract>>
        #ExecuteAsync() Task~TOutput~
        +GenerateSchema() JsonSchema
    }
    
    class CustomTool {
        +ExecuteAsync() Task
    }
    
    IMcpTool <|.. McpToolBase
    McpToolBase <|-- CustomTool
```

## Tools

A **tool** is a discrete operation that can be invoked by AI.

**Tool Anatomy:**
- **Name**: Unique identifier in kebab-case (`list-entities`)
- **Description**: What the tool does
- **Input Schema**: JSON Schema for parameters
- **Output**: Structured result or error
- **Context**: Access to authenticated Dataverse ServiceClient

**Tool Execution Flow:**

```mermaid
sequenceDiagram
    participant AI
    participant Core
    participant Tool
    participant DV as Dataverse
    
    AI->>Core: ExecuteTool(name, args)
    Core->>Core: Validate Arguments
    Core->>Core: Get Active Connection
    Core->>Tool: ExecuteAsync(context)
    Tool->>DV: SDK Operations
    DV->>Tool: Results
    Tool->>Core: Success/Error
    Core->>AI: Formatted Response
```

## State Management

Understanding where state lives and its lifecycle.

```mermaid
graph TB
    subgraph "In-Memory (Core Server)"
        Connections[Connection Pool<br/>ServiceClient Instances]
        ToolReg[Tool Registry<br/>Loaded Plugins]
        ActiveConn[Active Connection ID]
    end
    
    subgraph "Persistent (Extension)"
        ConnMeta[Connection Metadata<br/>Workspace State]
        Tokens[OAuth Tokens<br/>Secret Storage]
        Settings[User Settings<br/>Configuration]
    end
    
    subgraph "Filesystem"
        Binaries[Runtime Binaries<br/>Global Storage]
        Plugins[Plugin Assemblies<br/>Global Storage]
    end
    
    CoreServer[Core Server Process] --> Connections
    CoreServer --> ToolReg
    CoreServer --> ActiveConn
    
    ExtensionUI[Extension] --> ConnMeta
    ExtensionUI --> Tokens
    ExtensionUI --> Settings
    ExtensionUI --> Binaries
    ExtensionUI --> Plugins
    
    style "In-Memory (Core Server)" fill:#ffe1e1
    style "Persistent (Extension)" fill:#e1f5ff
    style "Filesystem" fill:#ffe1f5
```

**State Characteristics:**

| State | Location | Lifecycle | Shared |
|-------|----------|-----------|--------|
| Connection Pool | Core RAM | Server uptime | Extension + Bridge |
| Tool Registry | Core RAM | Server uptime | Extension + Bridge |
| Connection Metadata | Extension storage | Persistent | No |
| OAuth Tokens | VS Code secrets | Persistent | No |
| Binaries | Filesystem | Until deleted | No |
| Plugins | Filesystem | Until deleted | No |

## Communication Patterns

All communication uses **JSON-RPC 2.0** over different transports.

### Named Pipes (Extension ↔ Core, Bridge ↔ Core)

```mermaid
graph LR
    Client[RPC Client] -->|Write| Pipe[Named Pipe<br/>or Unix Socket]
    Pipe -->|Read| Server[RPC Server]
    Server -->|Process| Handler[Request Handler]
    Handler -->|Result| Server
    Server -->|Write| Pipe
    Pipe -->|Read| Client
    
    style Pipe fill:#ffe1e1
```

**Characteristics:**
- Platform-specific (named pipes on Windows, Unix sockets on macOS/Linux)
- Newline-delimited JSON messages
- Unique pipe name per VS Code instance
- Bidirectional communication

### STDIO (Copilot ↔ Bridge)

```mermaid
graph LR
    Copilot[GitHub Copilot] -->|stdin| Bridge[MCP Bridge]
    Bridge -->|stdout| Copilot
    Bridge -.->|stderr| Logs[Log Files]
    
    style Bridge fill:#ffe1f5
```

**Characteristics:**
- Standard input/output streams
- JSON-RPC 2.0 over stdio
- stderr for logging only (never stdout)
- Managed by VS Code's MCP system

## Instance Isolation

Each VS Code window has its own isolated system.

```mermaid
graph TB
    subgraph Window1[VS Code Window 1]
        Ext1[Extension Instance 1]
        Core1[Core Server 1<br/>Pipe: dvmcp-abc123]
        State1[State A]
        Ext1 --> Core1
        Core1 --> State1
    end
    
    subgraph Window2[VS Code Window 2]
        Ext2[Extension Instance 2]
        Core2[Core Server 2<br/>Pipe: dvmcp-xyz789]
        State2[State B]
        Ext2 --> Core2
        Core2 --> State2
    end
    
    DV[(Dataverse)]
    
    Core1 --> DV
    Core2 --> DV
    
    style Window1 fill:#e1f5ff
    style Window2 fill:#ffe1f5
```

**Isolation Benefits:**
- No state conflicts between windows
- Independent connections and plugins
- Separate crash domains
- Parallel operation without interference

## Next Steps

- **[Architecture](04-Architecture.md)**: Detailed architecture documentation
- **[Core Server](05-Core-Server.md)**: Deep dive into Core Server
- **[Communication](08-Communication.md)**: Communication protocol details
