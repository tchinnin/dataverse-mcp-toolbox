# MCP Bridge

The MCP Bridge is a protocol adapter that translates between GitHub Copilot's MCP protocol (over STDIO) and the Core Server's RPC interface (over Named Pipes).

## Purpose

```mermaid
graph LR
    Copilot[GitHub Copilot<br/>MCP Client]
    
    subgraph Bridge[MCP Bridge - Protocol Adapter]
        In[stdin Handler]
        Out[stdout Handler]
        Translator[Protocol Translator]
        PipeClient[Named Pipe Client]
    end
    
    Core[Core Server<br/>RPC Service]
    
    Copilot -->|MCP JSON-RPC<br/>stdin| In
    Out -->|MCP JSON-RPC<br/>stdout| Copilot
    
    In --> Translator
    Translator --> Out
    Translator <--> PipeClient
    PipeClient <-->|Core RPC<br/>Named Pipe| Core
    
    style Bridge fill:#ffe1f5
    style Copilot fill:#e1f5ff
    style Core fill:#fff4e1
```

**Key Principle:** The Bridge has **zero business logic** - it is a pure protocol translator and request forwarder.

## Architecture

### Component Overview

```mermaid
graph TB
    subgraph Bridge[MCP Bridge Process]
        Entry[Program.cs<br/>Entry Point]
        
        subgraph STDIO["STDIO Layer"]
            StdinReader[stdin Reader<br/>Async Read Loop]
            StdoutWriter[stdout Writer<br/>Synchronized Write]
        end
        
        subgraph Translation["Translation Layer"]
            McpParser[MCP Message Parser]
            RpcTranslator[RPC Translator]
            McpFormatter[MCP Response Formatter]
        end
        
        subgraph Client["Core Client"]
            PipeClient[NamedPipeRpcClient]
            Connection[Connection Manager]
        end
        
        Config[Configuration<br/>ENV Variables]
    end
    
    Copilot[GitHub Copilot]
    Core[Core Server]
    
    Entry --> Config
    Config --> STDIO
    Config --> Client
    
    Copilot <-->|stdin/stdout| STDIO
    STDIO <--> Translation
    Translation <--> Client
    Client <-->|Named Pipe| Core
    
    style Bridge fill:#ffe1f5
    style STDIO fill:#e1f5ff
    style Translation fill:#fff4e1
    style Client fill:#ffe1e1
```

## Startup Sequence

```mermaid
sequenceDiagram
    participant OS
    participant Program as Program.cs
    participant Config as Config Reader
    participant Pipe as Pipe Client
    participant Core as Core Server
    participant STDIO as STDIO Handler
    
    OS->>Program: Execute Binary<br/>(Copilot spawned)
    Program->>Config: Read Environment Variables
    
    Note over Config: DATAVERSE_MCP_PIPE_NAME<br/>DATAVERSE_MCP_PLUGIN_DIR<br/>DATAVERSE_MCP_SOCKET_DIR
    
    Config->>Program: Configuration Loaded
    
    Program->>Pipe: Initialize Pipe Client
    Pipe->>Core: Connect to Named Pipe
    
    alt Connection Success
        Core->>Pipe: Connection Established
        Pipe->>Program: Ready
        Program->>STDIO: Start STDIO Loops
        STDIO->>STDIO: Listen on stdin
        Note over STDIO: Bridge Ready<br/>Waiting for MCP requests
    else Connection Failed
        Pipe->>Program: Error
        Program->>OS: Exit with Error Code
    end
```

## Request Flow

### MCP → Core Translation

```mermaid
sequenceDiagram
    participant Copilot as GitHub Copilot
    participant stdin
    participant Bridge as Bridge<br/>Translator
    participant Pipe as Named Pipe
    participant Core as Core Server
    
    Copilot->>stdin: MCP Request<br/>{"method": "tools/call", ...}
    stdin->>Bridge: Read JSON Line
    Bridge->>Bridge: Parse MCP Message
    Bridge->>Bridge: Extract Method & Params
    Bridge->>Bridge: Map to Core RPC Method
    Bridge->>Bridge: Transform Parameters
    Bridge->>Pipe: Core RPC Request<br/>{"method": "ExecuteTool", ...}
    Pipe->>Core: Forward Request
    Core->>Core: Process Request
    Core->>Pipe: Core RPC Response
    Pipe->>Bridge: Receive Response
    Bridge->>Bridge: Map to MCP Format
    Bridge->>Bridge: Format MCP Response
    Bridge->>stdout: Write JSON Line
    stdout->>Copilot: MCP Response<br/>{"result": {...}}
```

### Core → MCP Translation

```mermaid
sequenceDiagram
    participant Core as Core Server
    participant Bridge as Bridge<br/>Translator
    participant Copilot as GitHub Copilot
    
    Note over Core,Copilot: Example: Server Notification
    
    Core->>Bridge: Server Event (if supported)
    Bridge->>Bridge: Convert to MCP Notification
    Bridge->>Copilot: MCP Notification
```

## Protocol Translation

### MCP Methods to Core RPC

```mermaid
graph TB
    subgraph MCP["MCP Protocol Methods"]
        Init[initialize]
        ListTools[tools/list]
        CallTool[tools/call]
        ListResources[resources/list<br/>Future]
    end
    
    subgraph Translation["Translation Logic"]
        Mapper[Method Mapper]
        ParamTransform[Parameter Transformer]
        ResultTransform[Result Transformer]
    end
    
    subgraph Core["Core RPC Methods"]
        GetVersion[GetVersionAsync]
        DiscoverTools[DiscoverToolsAsync]
        ExecuteTool[ExecuteToolAsync]
        GetResource[GetResourceAsync<br/>Future]
    end
    
    Init -->|Maps to| Mapper
    ListTools -->|Maps to| Mapper
    CallTool -->|Maps to| Mapper
    
    Mapper --> ParamTransform
    ParamTransform --> GetVersion
    ParamTransform --> DiscoverTools
    ParamTransform --> ExecuteTool
    
    GetVersion --> ResultTransform
    DiscoverTools --> ResultTransform
    ExecuteTool --> ResultTransform
    
    ResultTransform -.-> MCP
    
    style MCP fill:#e1f5ff
    style Translation fill:#ffe1e1
    style Core fill:#fff4e1
```

### Method Mapping Table

| MCP Method | Core RPC Method | Transform |
|------------|-----------------|-----------|
| `initialize` | `GetVersionAsync` | Add server capabilities |
| `tools/list` | `DiscoverToolsAsync` | Map tool list to MCP format |
| `tools/call` | `ExecuteToolAsync` | Extract tool name & arguments |
| `resources/list` | N/A (future) | To be implemented |

### Parameter Transformation

```mermaid
graph TB
    McpRequest[MCP Request<br/>tools/call]
    
    subgraph Extract["Parameter Extraction"]
        ToolName[Extract: params.name]
        Arguments[Extract: params.arguments]
    end
    
    subgraph Build["Core Request Building"]
        BuildReq[Build ToolCallRequest]
        SetName[toolName = name]
        SetArgs[arguments = arguments]
    end
    
    CoreRequest[Core RPC Request<br/>ExecuteToolAsync]
    
    McpRequest --> Extract
    Extract --> Build
    Build --> CoreRequest
    
    style McpRequest fill:#e1f5ff
    style Extract fill:#ffe1e1
    style Build fill:#fff4e1
    style CoreRequest fill:#e1ffe1
```

### Response Transformation

```mermaid
graph TB
    CoreResponse[Core RPC Response<br/>ToolCallResult]
    
    subgraph Transform["Response Transformation"]
        Check{Success?}
        BuildSuccess[Build MCP Success Response]
        BuildError[Build MCP Error Response]
    end
    
    McpResponse[MCP Response]
    
    CoreResponse --> Check
    Check -->|Yes| BuildSuccess
    Check -->|No| BuildError
    BuildSuccess --> McpResponse
    BuildError --> McpResponse
    
    style CoreResponse fill:#e1ffe1
    style Transform fill:#ffe1e1
    style McpResponse fill:#e1f5ff
```

## STDIO Communication

### Input Stream (stdin)

```mermaid
sequenceDiagram
    participant Copilot
    participant stdin
    participant Reader as Async Reader
    participant Parser as JSON Parser
    participant Handler as Request Handler
    
    loop Continuous Read
        Copilot->>stdin: Write JSON-RPC Message + \n
        stdin->>Reader: ReadLineAsync()
        Reader->>Parser: Parse JSON
        
        alt Valid JSON
            Parser->>Handler: Parsed Message
            Handler->>Handler: Process Request
        else Parse Error
            Parser->>stderr: Log Error
            Parser->>stdout: Write Error Response
        end
    end
```

### Output Stream (stdout)

```mermaid
sequenceDiagram
    participant Handler as Response Handler
    participant Formatter as JSON Formatter
    participant Writer as Synchronized Writer
    participant stdout
    participant Copilot
    
    Handler->>Formatter: Format Response
    Formatter->>Formatter: Serialize to JSON
    Formatter->>Writer: Write(json)
    
    critical Synchronized Write
        Writer->>Writer: Lock(stdout)
        Writer->>stdout: WriteLine(json)
        Writer->>stdout: Flush()
    end
    
    stdout->>Copilot: JSON-RPC Response
```

**Critical Rules:**
- ✅ Only JSON-RPC messages on stdout
- ✅ One message per line (newline-delimited)
- ✅ Flush after every write
- ❌ Never write logs to stdout
- ✅ All logs go to stderr

## Connection Management

### Named Pipe Connection

```mermaid
stateDiagram-v2
    [*] --> Initializing: Bridge Starts
    Initializing --> Connecting: Read Config
    Connecting --> Connected: Pipe Connected
    Connecting --> Failed: Connection Error
    
    Connected --> Ready: Test Connection
    Ready --> Processing: Request Received
    Processing --> Ready: Response Sent
    
    Failed --> Retry: Wait & Retry
    Retry --> Connecting
    
    Ready --> Disconnecting: Core Server Down
    Processing --> Disconnecting: Pipe Error
    
    Disconnecting --> Retry: Attempt Reconnect
    Disconnecting --> [*]: Max Retries
    
    note right of Connected
        Verify pipe is writable
        Send test message
    end note
    
    note right of Retry
        Exponential backoff
        Max 3 retries
    end note
```

### Connection Configuration

```mermaid
graph TB
    Config[Configuration Source]
    
    subgraph ENV["Environment Variables"]
        PipeName[DATAVERSE_MCP_PIPE_NAME<br/>e.g., 'dvmcp-abc123']
        PluginDir[DATAVERSE_MCP_PLUGIN_DIR<br/>Not used by Bridge]
        SocketDir[DATAVERSE_MCP_SOCKET_DIR<br/>Unix socket directory]
    end
    
    subgraph Build["Pipe Path Builder"]
        Platform{Platform?}
        WinPath[Windows:<br/>\\.\pipe\dvmcp-abc123]
        UnixPath[Unix:<br/>/tmp/.../CoreFxPipe_dvmcp-abc123]
    end
    
    subgraph Client["Pipe Client"]
        Connect[Connect to Pipe]
        Stream[Named Pipe Stream]
    end
    
    Config --> ENV
    ENV --> Build
    Build --> Platform
    Platform -->|Windows| WinPath
    Platform -->|Unix/macOS| UnixPath
    WinPath --> Connect
    UnixPath --> Connect
    Connect --> Stream
    
    style ENV fill:#e1f5ff
    style Build fill:#ffe1e1
    style Client fill:#fff4e1
```

## Error Handling

### Error Categories

```mermaid
graph TB
    Error[Error Occurs]
    
    Error --> Type{Error Type?}
    
    Type -->|Connection| ConnError[Pipe Connection Error]
    Type -->|Protocol| ProtocolError[Invalid MCP Message]
    Type -->|Core| CoreError[Core RPC Error]
    Type -->|Unexpected| UnexpectedError[Unexpected Exception]
    
    ConnError --> Retry[Retry Connection]
    ProtocolError --> McpError[Return MCP Error]
    CoreError --> ForwardError[Forward Core Error as MCP]
    UnexpectedError --> LogAndError[Log to stderr + MCP Error]
    
    Retry --> Limit{Max<br/>Retries?}
    Limit -->|No| Wait[Wait with Backoff]
    Limit -->|Yes| Exit[Exit Process]
    Wait --> Retry
    
    McpError --> Copilot[Send to Copilot]
    ForwardError --> Copilot
    LogAndError --> Copilot
    
    style ConnError fill:#ffe1e1
    style ProtocolError fill:#ffe1e1
    style CoreError fill:#ffe1e1
    style UnexpectedError fill:#ffe1e1
```

### Error Response Format

```mermaid
graph TB
    CoreError[Core Error Response]
    
    subgraph Extract["Error Extraction"]
        Code[Extract Error Code]
        Message[Extract Error Message]
        Details[Extract Details]
    end
    
    subgraph Build["MCP Error Building"]
        McpCode[Map to MCP Error Code]
        McpMessage[Format Error Message]
        McpData[Add Error Data]
    end
    
    McpError[MCP Error Response]
    
    CoreError --> Extract
    Extract --> Build
    Build --> McpError
    
    style CoreError fill:#e1ffe1
    style Extract fill:#ffe1e1
    style Build fill:#fff4e1
    style McpError fill:#e1f5ff
```

## Process Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Starting: Copilot Spawns
    Starting --> ConfigLoading: Binary Executed
    ConfigLoading --> Connecting: ENV Vars Read
    Connecting --> Ready: Core Connected
    Connecting --> Failed: Connection Failed
    
    Ready --> Listening: STDIO Loops Started
    Listening --> Processing: MCP Request
    Processing --> Listening: Response Sent
    
    Failed --> [*]: Exit with Error
    
    Listening --> Shutdown: stdin Closed
    Processing --> Shutdown: Core Disconnected
    
    Shutdown --> Cleanup: Stop Processing
    Cleanup --> [*]: Exit
    
    note right of ConfigLoading
        DATAVERSE_MCP_PIPE_NAME
        DATAVERSE_MCP_SOCKET_DIR
    end note
    
    note right of Listening
        Read stdin continuously
        Write responses to stdout
        Log errors to stderr
    end note
```

## Logging

### Log Output Strategy

```mermaid
graph LR
    Events[Bridge Events]
    
    Events --> Level{Log Level?}
    
    Level -->|Info| Info[Connection status, requests]
    Level -->|Warning| Warning[Retries, recoverable errors]
    Level -->|Error| Error[Failures, exceptions]
    
    Info --> Stderr[Console.Error.WriteLine]
    Warning --> Stderr
    Error --> Stderr
    
    Stderr -->|Output| LogStream[stderr Stream]
    
    LogStream -->|Captured by| VSCode[VS Code Output Panel]
    
    Forbidden[❌ Never stdout]
    Stderr -.->|Never| Forbidden
    
    style Stderr fill:#e1ffe1
    style Forbidden fill:#ffe1e1
```

**Log Examples:**
```
[Bridge] Starting MCP Bridge
[Bridge] Connecting to Core Server via pipe: dvmcp-abc123
[Bridge] Connected to Core Server
[Bridge] MCP request received: tools/list
[Bridge] MCP request completed in 45ms
[Bridge] Connection to Core Server lost, retrying...
[Bridge] Shutting down
```

## Performance Considerations

### Minimal Overhead

```mermaid
graph LR
    Request[MCP Request] -->|~1ms| Parse[Parse JSON]
    Parse -->|<1ms| Map[Map Method]
    Map -->|<1ms| Transform[Transform Params]
    Transform -->|Network| Pipe[Send to Core]
    Pipe -->|Variable| Wait[Wait for Core]
    Wait -->|Network| Receive[Receive Response]
    Receive -->|<1ms| Format[Format MCP]
    Format -->|~1ms| Serialize[Serialize JSON]
    Serialize -->|IO| Output[Write stdout]
    
    style Parse fill:#e1ffe1
    style Map fill:#e1ffe1
    style Transform fill:#e1ffe1
    style Format fill:#e1ffe1
    style Serialize fill:#e1ffe1
```

**Bridge Overhead:** Typically < 5ms per request (excluding Core processing time)

### Memory Footprint

```mermaid
graph TB
    subgraph Memory["Bridge Memory Usage"]
        Base[Base Process<br/>~10-15 MB]
        Buffers[Stream Buffers<br/>~1-2 MB]
        Objects[Transient Objects<br/>~1 MB]
    end
    
    Total[Total: ~12-18 MB]
    
    Memory --> Total
    
    style Memory fill:#e1f5ff
```

**Characteristics:**
- Minimal memory usage
- No state accumulation
- Transient objects only
- Garbage collected frequently

## Configuration Reference

### Required Environment Variables

| Variable | Type | Example | Purpose |
|----------|------|---------|---------|
| `DATAVERSE_MCP_PIPE_NAME` | string | `dvmcp-abc123` | Named pipe identifier |
| `DATAVERSE_MCP_SOCKET_DIR` | string | `/tmp/dvmcptb-sockets` | Unix socket directory (macOS/Linux only) |

### Optional Environment Variables

| Variable | Type | Default | Purpose |
|----------|------|---------|---------|
| `DATAVERSE_MCP_LOG_LEVEL` | string | `Info` | Logging verbosity |
| `DATAVERSE_MCP_RETRY_COUNT` | int | `3` | Connection retry attempts |

## Next Steps

- **[VS Code Extension](07-VS-Code-Extension.md)**: Extension architecture
- **[Communication Protocol](08-Communication.md)**: Detailed protocol spec
- **[Core Server](05-Core-Server.md)**: Core Server details
