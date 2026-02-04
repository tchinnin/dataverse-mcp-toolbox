# Communication Protocol

Detailed specification of the JSON-RPC 2.0 communication protocol used between all components.

## Protocol Overview

All components communicate using **JSON-RPC 2.0** over different transport layers.

```mermaid
graph TB
    subgraph Protocol["JSON-RPC 2.0 Protocol"]
        Request[Request Message]
        Response[Response Message]
        Notification[Notification Message<br/>Future]
        Error[Error Message]
    end
    
    subgraph Transports["Transport Layers"]
        NamedPipe[Named Pipes<br/>Extension ↔ Core<br/>Bridge ↔ Core]
        STDIO[Standard I/O<br/>Copilot ↔ Bridge]
    end
    
    subgraph Serialization["Serialization"]
        JSON[JSON Format]
        CamelCase[camelCase Convention]
        NewlineDelim[Newline-Delimited]
    end
    
    Protocol --> Transports
    Transports --> Serialization
    
    style Protocol fill:#e1f5ff
    style Transports fill:#ffe1e1
    style Serialization fill:#fff4e1
```

## Message Format

### Request Message

```json
{
  "jsonrpc": "2.0",
  "id": "request-unique-id",
  "method": "methodName",
  "params": {
    "param1": "value1",
    "param2": "value2"
  }
}
```

### Success Response

```json
{
  "jsonrpc": "2.0",
  "id": "request-unique-id",
  "result": {
    "data": "response data"
  }
}
```

### Error Response

```json
{
  "jsonrpc": "2.0",
  "id": "request-unique-id",
  "error": {
    "code": -32600,
    "message": "Error description",
    "data": {
      "details": "Additional error information"
    }
  }
}
```

## Message Flow

### Request-Response Pattern

```mermaid
sequenceDiagram
    participant Client
    participant Transport
    participant Server
    
    Client->>Client: Generate Request ID
    Client->>Client: Serialize to JSON
    Client->>Transport: Write JSON + \n
    Transport->>Server: Forward Message
    Server->>Server: Deserialize & Validate
    Server->>Server: Route to Handler
    Server->>Server: Process Request
    Server->>Server: Serialize Response
    Server->>Transport: Write JSON + \n
    Transport->>Client: Forward Response
    Client->>Client: Deserialize
    Client->>Client: Match by Request ID
    Client->>Client: Return Result
```

### Concurrent Requests

```mermaid
sequenceDiagram
    participant Client
    participant Server
    
    par Request 1
        Client->>Server: Request (id: "1")
        Server->>Client: Response (id: "1")
    and Request 2
        Client->>Server: Request (id: "2")
        Server->>Client: Response (id: "2")
    and Request 3
        Client->>Server: Request (id: "3")
        Server->>Client: Response (id: "3")
    end
    
    Note over Client,Server: Responses may arrive out of order<br/>Matched by request ID
```

## Transport Layers

### Named Pipes (Extension ↔ Core, Bridge ↔ Core)

```mermaid
graph TB
    subgraph Windows["Windows Named Pipes"]
        WinPath[Path: \\.\pipe\{name}]
        WinAPI[Win32 Named Pipe API]
    end
    
    subgraph Unix["Unix Domain Sockets"]
        UnixPath[Path: /tmp/{dir}/CoreFxPipe_{name}]
        UnixAPI[POSIX Socket API]
        UnixLimit[⚠️ 104 char path limit]
    end
    
    subgraph Common["Common Behavior"]
        Bidirectional[Bidirectional Communication]
        Buffered[Buffered I/O]
        NewlineDelim[Newline-Delimited Messages]
    end
    
    Platform{Platform?}
    Platform -->|Windows| Windows
    Platform -->|Unix/macOS| Unix
    
    Windows --> Common
    Unix --> Common
    
    style Windows fill:#e1f5ff
    style Unix fill:#ffe1e1
    style Common fill:#fff4e1
```

**Pipe Naming Convention:**

| Platform | Pattern | Example |
|----------|---------|---------|
| **Windows** | `\\.\pipe\dvmcp-{id}` | `\\.\pipe\dvmcp-abc12345` |
| **Unix/macOS** | `/tmp/dvmcptb-sockets/CoreFxPipe_dvmcp-{id}` | `/tmp/dvmcptb-sockets/CoreFxPipe_dvmcp-abc12345` |

**Path Length Considerations:**

```mermaid
graph TB
    PipeName[Pipe Name Generation]
    
    PipeName --> Platform{Platform?}
    
    Platform -->|Windows| NoLimit[No Path Limit<br/>Use Full UUID]
    Platform -->|Unix| Limited[104 Char Limit]
    
    Limited --> Short[Use Short Temp Dir<br/>/tmp/dvmcptb-sockets]
    Short --> ShortID[Shorten ID to 8 chars<br/>dvmcp-abc12345]
    ShortID --> Final[Total: ~45 chars<br/>Safe margin]
    
    style Limited fill:#ffe1e1
    style Final fill:#e1ffe1
```

### STDIO (Copilot ↔ Bridge)

```mermaid
graph LR
    Copilot[GitHub Copilot]
    
    subgraph Bridge[MCP Bridge]
        stdin[stdin Reader<br/>Async Read Loop]
        stdout[stdout Writer<br/>Synchronized Write]
        stderr[stderr<br/>Logging Only]
    end
    
    Copilot -->|Write| stdin
    stdout -->|Read| Copilot
    
    stderr -.->|Never| Copilot
    
    style stdin fill:#e1f5ff
    style stdout fill:#e1ffe1
    style stderr fill:#ffe1e1
```

**Critical Rules:**
- ✅ `stdout`: Only JSON-RPC messages
- ✅ `stderr`: All logs and debug output
- ❌ Never mix logs with stdout
- ✅ Flush after every stdout write
- ✅ Newline-delimited messages

## Serialization

### Naming Convention Mapping

```mermaid
graph TB
    CSharp[C# Code<br/>PascalCase]
    
    subgraph Serialization["JSON Serialization"]
        Serialize[Newtonsoft.Json<br/>CamelCasePropertyNamesContractResolver]
    end
    
    JSON[JSON Wire Format<br/>camelCase]
    
    subgraph Deserialization["JSON Deserialization"]
        Deserialize[Same Resolver<br/>Automatic Mapping]
    end
    
    TypeScript[TypeScript Code<br/>camelCase]
    
    CSharp -->|Serialize| Serialization
    Serialization --> JSON
    JSON -->|Deserialize| Deserialization
    Deserialization --> TypeScript
    
    TypeScript -->|Serialize| Serialization
    Serialization --> JSON
    JSON -->|Deserialize| Deserialization
    Deserialization --> CSharp
    
    style CSharp fill:#e1f5ff
    style JSON fill:#ffe1e1
    style TypeScript fill:#fff4e1
```

**Example Mapping:**

| C# Property | JSON Property | TypeScript Property |
|-------------|---------------|---------------------|
| `ConnectionId` | `connectionId` | `connectionId` |
| `IsActive` | `isActive` | `isActive` |
| `EnvironmentUrl` | `environmentUrl` | `environmentUrl` |
| `UserInfo` | `userInfo` | `userInfo` |

### Data Types

```mermaid
graph TB
    subgraph Primitives["Primitive Types"]
        String[string]
        Number[number<br/>int, long, double]
        Boolean[boolean]
        Null[null]
    end
    
    subgraph Complex["Complex Types"]
        Object[object<br/>Dictionary, Class]
        Array[array<br/>List, Array]
    end
    
    subgraph Special["Special Types"]
        DateTime[DateTime → ISO 8601 string]
        GUID[Guid → string]
        Enum[Enum → string]
    end
    
    JSON[JSON Format]
    
    Primitives --> JSON
    Complex --> JSON
    Special --> JSON
    
    style Primitives fill:#e1f5ff
    style Complex fill:#ffe1e1
    style Special fill:#fff4e1
```

## Core RPC Methods

### Connection Management

```mermaid
graph TB
    subgraph ConnectionMethods["Connection Methods"]
        Create[CreateConnectionAsync<br/>→ ConnectionResult]
        Get[GetConnectionAsync<br/>→ ConnectionInfo]
        List[ListConnectionsAsync<br/>→ ConnectionInfo[]]
        Delete[DeleteConnectionAsync<br/>→ bool]
        SetActive[SetActiveConnectionAsync<br/>→ bool]
        WhoAmI[GetWhoAmIAsync<br/>→ WhoAmIResult]
    end
    
    subgraph Models["Data Models"]
        ConnRequest[ConnectionRequest]
        ConnInfo[ConnectionInfo]
        ConnResult[ConnectionResult]
        WhoAmIRes[WhoAmIResult]
    end
    
    ConnectionMethods --> Models
    
    style ConnectionMethods fill:#e1f5ff
    style Models fill:#ffe1e1
```

#### CreateConnectionAsync

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "method": "CreateConnectionAsync",
  "params": {
    "name": "Dev Environment",
    "environmentUrl": "https://yourorg.crm.dynamics.com"
  }
}
```

**Success Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "result": {
    "success": true,
    "connectionId": "550e8400-e29b-41d4-a716-446655440000",
    "userInfo": {
      "userId": "...",
      "userName": "John Doe",
      "businessUnitId": "...",
      "organizationId": "..."
    }
  }
}
```

#### ListConnectionsAsync

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "method": "ListConnectionsAsync",
  "params": {}
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "2",
  "result": [
    {
      "id": "550e8400-...",
      "name": "Dev Environment",
      "environmentUrl": "https://dev.crm.dynamics.com",
      "isActive": true,
      "userInfo": {...}
    },
    {
      "id": "660f9511-...",
      "name": "UAT Environment",
      "environmentUrl": "https://uat.crm.dynamics.com",
      "isActive": false,
      "userInfo": {...}
    }
  ]
}
```

### Tool Operations

```mermaid
graph TB
    subgraph ToolMethods["Tool Methods"]
        Discover[DiscoverToolsAsync<br/>→ ToolInfo[]]
        Execute[ExecuteToolAsync<br/>→ ToolCallResult]
    end
    
    subgraph Models["Data Models"]
        ToolInfo[ToolInfo<br/>name, description, schema]
        ToolRequest[ToolCallRequest<br/>toolName, arguments]
        ToolResult[ToolCallResult<br/>success, content, error]
    end
    
    ToolMethods --> Models
    
    style ToolMethods fill:#e1f5ff
    style Models fill:#ffe1e1
```

#### DiscoverToolsAsync

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "method": "DiscoverToolsAsync",
  "params": {}
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "result": [
    {
      "name": "list-entities",
      "description": "List all entities in the Dataverse environment",
      "inputSchema": {
        "type": "object",
        "properties": {},
        "required": []
      }
    },
    {
      "name": "get-record",
      "description": "Retrieve a specific record",
      "inputSchema": {
        "type": "object",
        "properties": {
          "entityName": {"type": "string"},
          "recordId": {"type": "string"}
        },
        "required": ["entityName", "recordId"]
      }
    }
  ]
}
```

#### ExecuteToolAsync

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "method": "ExecuteToolAsync",
  "params": {
    "toolName": "get-record",
    "arguments": {
      "entityName": "account",
      "recordId": "00000000-0000-0000-0000-000000000001"
    }
  }
}
```

**Success Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "success": true,
    "content": {
      "accountid": "00000000-0000-0000-0000-000000000001",
      "name": "Contoso Ltd",
      "revenue": 1000000
    }
  }
}
```

**Error Response:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "success": false,
    "error": {
      "code": "NOT_FOUND",
      "message": "Record not found",
      "details": "No record with ID 00000000-... exists"
    }
  }
}
```

### Plugin Operations

```mermaid
graph TB
    subgraph PluginMethods["Plugin Methods"]
        List[ListPluginsAsync<br/>→ PluginInfo[]]
        Reload[ReloadPluginsAsync<br/>→ bool]
    end
    
    subgraph Models["Data Models"]
        PluginInfo[PluginInfo<br/>name, version, tools[]]
    end
    
    PluginMethods --> Models
    
    style PluginMethods fill:#e1f5ff
    style Models fill:#ffe1e1
```

### Server Information

```mermaid
graph TB
    subgraph ServerMethods["Server Methods"]
        Version[GetVersionAsync<br/>→ ServerVersionInfo]
    end
    
    subgraph Models["Data Models"]
        VersionInfo[ServerVersionInfo<br/>version, buildDate]
    end
    
    ServerMethods --> Models
    
    style ServerMethods fill:#e1f5ff
    style Models fill:#ffe1e1
```

## Error Codes

### JSON-RPC Standard Errors

| Code | Message | Meaning |
|------|---------|---------|
| `-32700` | Parse error | Invalid JSON |
| `-32600` | Invalid Request | Missing required fields |
| `-32601` | Method not found | Unknown method name |
| `-32602` | Invalid params | Parameter validation failed |
| `-32603` | Internal error | Server-side error |

### Application-Specific Errors

```mermaid
graph TB
    subgraph ErrorCodes["Custom Error Codes"]
        Validation[VALIDATION_ERROR<br/>Input validation failed]
        NotFound[NOT_FOUND<br/>Resource not found]
        Dataverse[DATAVERSE_ERROR<br/>Dataverse SDK error]
        Auth[AUTHENTICATION_ERROR<br/>Auth failed]
        Plugin[PLUGIN_ERROR<br/>Plugin execution error]
        Timeout[TIMEOUT<br/>Operation timeout]
        Unauthorized[UNAUTHORIZED<br/>No active connection]
    end
    
    subgraph Response["Error Response Structure"]
        Result[ToolCallResult.success = false]
        Error[ToolCallResult.error]
        Code[error.code]
        Message[error.message]
        Details[error.details]
    end
    
    ErrorCodes --> Response
    
    style ErrorCodes fill:#ffe1e1
    style Response fill:#e1f5ff
```

## Protocol Guarantees

### Message Ordering

```mermaid
graph LR
    Client[Client]
    
    subgraph Processing["Server Processing"]
        Q[Request Queue]
        Handler[Handlers Process<br/>Concurrently]
        Response[Responses Return<br/>Out of Order]
    end
    
    Client -->|Req 1| Q
    Client -->|Req 2| Q
    Client -->|Req 3| Q
    
    Q --> Handler
    
    Handler -->|Resp 2| Client
    Handler -->|Resp 1| Client
    Handler -->|Resp 3| Client
    
    style Processing fill:#ffe1e1
```

**Guarantees:**
- Requests processed concurrently
- No ordering guarantee on responses
- Client matches responses by request ID
- Requests on same connection are thread-safe

### Error Recovery

```mermaid
stateDiagram-v2
    [*] --> Connected: Connection Established
    Connected --> Sending: Send Request
    Sending --> Waiting: Awaiting Response
    
    Waiting --> Received: Response Received
    Received --> Connected: Process Response
    
    Waiting --> Timeout: No Response
    Timeout --> Retry: Retry Request
    Retry --> Sending
    Retry --> Failed: Max Retries
    
    Connected --> Disconnected: Connection Lost
    Sending --> Disconnected: Write Failed
    Waiting --> Disconnected: Read Failed
    
    Disconnected --> Reconnecting: Attempt Reconnect
    Reconnecting --> Connected: Reconnect Success
    Reconnecting --> Failed: Reconnect Failed
    
    Failed --> [*]
```

## Performance Characteristics

### Latency Breakdown

```mermaid
graph LR
    Start[Request Start] -->|~1ms| Serialize[Serialization]
    Serialize -->|<1ms| Write[Write to Pipe]
    Write -->|~1-5ms| Transport[Transport]
    Transport -->|<1ms| Read[Read from Pipe]
    Read -->|~1ms| Deserialize[Deserialization]
    Deserialize -->|Variable| Process[Processing]
    Process -->|~1ms| SerializeResp[Serialize Response]
    SerializeResp -->|~1-5ms| TransportResp[Transport]
    TransportResp -->|~1ms| DeserializeResp[Deserialize Response]
    DeserializeResp --> End[Response Ready]
    
    style Serialize fill:#e1ffe1
    style Deserialize fill:#e1ffe1
    style Process fill:#ffe1e1
```

**Typical Latencies:**
- Local RPC overhead: ~5-10ms
- Dataverse SDK call: 100-500ms
- Total request: 110-520ms

### Message Size Limits

| Component | Typical Size | Max Recommended |
|-----------|--------------|-----------------|
| **Request** | < 10 KB | 1 MB |
| **Response** | < 100 KB | 10 MB |
| **Tool Result** | Variable | 5 MB |

**Large Result Handling:**
- Paginate large datasets
- Return references instead of full data
- Stream large responses (future enhancement)

## Next Steps

- **[Server Lifecycle](09-Server-Lifecycle.md)**: Binary download and updates
- **[Managing Connections](10-Managing-Connections.md)**: Connection workflows
- **[Using Tools](12-Using-Tools.md)**: Tool execution details
