# Using Tools

Guide to using tools with GitHub Copilot and understanding tool execution in the Dataverse MCP Toolbox.

## Tool Overview

Tools are discrete operations that can be invoked by AI assistants (like GitHub Copilot) to interact with Dataverse. Each tool has a defined input schema and returns structured output.

```mermaid
graph TB
    User[Developer] -->|Natural Language| Copilot[GitHub Copilot]
    Copilot -->|Discovers| Tools[Available Tools]
    Copilot -->|Selects & Calls| Tool[Specific Tool]
    Tool -->|Uses| Connection[Active Connection]
    Connection -->|SDK| Dataverse[(Dataverse)]
    Dataverse -->|Results| Tool
    Tool -->|Structured Data| Copilot
    Copilot -->|Formatted Response| User
    
    style Tools fill:#e1f5ff
    style Tool fill:#ffe1e1
    style Connection fill:#fff4e1
    style Dataverse fill:#e1ffe1
```

## Tool Discovery

### How Copilot Discovers Tools

```mermaid
sequenceDiagram
    participant Copilot as GitHub Copilot
    participant Bridge as MCP Bridge
    participant Core as Core Server
    participant Registry as Tool Registry
    
    Note over Copilot: User starts chat session
    
    Copilot->>Bridge: tools/list (MCP)
    Bridge->>Core: DiscoverToolsAsync (RPC)
    Core->>Registry: GetAllTools()
    Registry->>Core: List of IMcpTool
    
    loop For each tool
        Core->>Core: Generate Tool Info<br/>(name, description, schema)
    end
    
    Core->>Bridge: ToolInfo[]
    Bridge->>Copilot: MCP tools/list response
    
    Note over Copilot: Copilot now knows<br/>available tools
```

### Tool Information

```mermaid
graph TB
    Tool[Tool Definition]
    
    subgraph Info["Tool Information"]
        Name[Tool Name<br/>kebab-case identifier]
        Description[Description<br/>What the tool does]
        Schema[Input Schema<br/>JSON Schema format]
    end
    
    Tool --> Info
    
    subgraph Schema["Input Schema Structure"]
        Type[Type: object]
        Properties[Properties: parameter definitions]
        Required[Required: mandatory parameters]
        Examples[Examples: sample inputs]
    end
    
    Schema --> Schema
    
    style Tool fill:#e1f5ff
    style Info fill:#ffe1e1
    style Schema fill:#fff4e1
```

**Example Tool Info:**
```json
{
  "name": "get-record",
  "description": "Retrieve a specific record from Dataverse by entity name and ID",
  "inputSchema": {
    "type": "object",
    "properties": {
      "entityName": {
        "type": "string",
        "description": "Logical name of the entity (e.g., 'account', 'contact')"
      },
      "recordId": {
        "type": "string",
        "description": "GUID of the record to retrieve"
      },
      "columns": {
        "type": "array",
        "items": {"type": "string"},
        "description": "Optional: specific columns to retrieve"
      }
    },
    "required": ["entityName", "recordId"]
  }
}
```

## Tool Execution

### Execution Flow

```mermaid
sequenceDiagram
    participant User
    participant Copilot
    participant Bridge
    participant Core
    participant Plugin
    participant DV as Dataverse
    
    User->>Copilot: "Get account record with ID xyz"
    Copilot->>Copilot: Understand Intent
    Copilot->>Copilot: Select 'get-record' tool
    Copilot->>Copilot: Build Parameters
    
    Copilot->>Bridge: tools/call (get-record, params)
    Bridge->>Core: ExecuteToolAsync
    
    Core->>Core: Validate Tool Name
    Core->>Core: Validate Parameters
    Core->>Core: Get Active Connection
    Core->>Core: Create DataverseContext
    
    Core->>Plugin: ExecuteAsync(context, params)
    Plugin->>DV: SDK Call (Retrieve)
    DV->>Plugin: Record Data
    Plugin->>Core: Tool Result
    
    Core->>Bridge: ToolCallResult
    Bridge->>Copilot: MCP Response
    Copilot->>Copilot: Format Response
    Copilot->>User: "Here's the account information..."
```

### Parameter Validation

```mermaid
graph TB
    Request[Tool Call Request]
    
    Request --> Validate{Validation}
    
    Validate -->|Tool Name| ToolCheck{Tool<br/>Exists?}
    Validate -->|Parameters| ParamCheck{Match<br/>Schema?}
    Validate -->|Connection| ConnCheck{Active<br/>Connection?}
    
    ToolCheck -->|No| Error1[Error: Tool not found]
    ToolCheck -->|Yes| Continue1[✓]
    
    ParamCheck -->|No| Error2[Error: Invalid parameters]
    ParamCheck -->|Yes| Continue2[✓]
    
    ConnCheck -->|No| Error3[Error: No active connection]
    ConnCheck -->|Yes| Continue3[✓]
    
    Continue1 --> AllValid
    Continue2 --> AllValid
    Continue3 --> AllValid[All Validations Pass]
    
    AllValid --> Execute[Execute Tool]
    
    Error1 --> ErrorResponse[Error Response]
    Error2 --> ErrorResponse
    Error3 --> ErrorResponse
    
    style Execute fill:#e1ffe1
    style ErrorResponse fill:#ffe1e1
```

## Using Tools with Copilot

### Natural Language Examples

```mermaid
graph TB
    subgraph Examples["Example Prompts"]
        Ex1["'List all entities in my environment'"]
        Ex2["'Get the account record with ID abc-123'"]
        Ex3["'Show me metadata for the contact entity'"]
        Ex4["'Create a new account named Contoso'"]
    end
    
    subgraph Mapping["Tool Mapping"]
        Ex1 --> Tool1[list-entities]
        Ex2 --> Tool2[get-record]
        Ex3 --> Tool3[get-entity-metadata]
        Ex4 --> Tool4[create-record]
    end
    
    style Examples fill:#e1f5ff
    style Mapping fill:#ffe1e1
```

### Copilot Chat Examples

**Example 1: List Entities**
```
User: @workspace What entities are available in my Dataverse environment?

Copilot: Let me retrieve the list of entities for you.
[Calls: list-entities]
I found 250 entities in your environment. Here are some common ones:
- account (Accounts)
- contact (Contacts)
- opportunity (Opportunities)
- lead (Leads)
[...]
```

**Example 2: Get Record**
```
User: @workspace Get me the details of account with ID 12345678-...

Copilot: Let me retrieve that account record.
[Calls: get-record with entityName='account', recordId='12345678-...']
Here's the account information:
- Name: Contoso Corporation
- Revenue: $1,000,000
- Primary Contact: John Doe
[...]
```

**Example 3: Entity Metadata**
```
User: @workspace What fields does the opportunity entity have?

Copilot: Let me get the metadata for the opportunity entity.
[Calls: get-entity-metadata with entityName='opportunity']
The opportunity entity has 87 attributes, including:
- name (string): Topic/Name
- estimatedvalue (money): Est. Revenue
- closeprobability (integer): Probability
[...]
```

## Tool Result Handling

### Success Results

```mermaid
graph TB
    Tool[Tool Execution]
    
    Tool --> Success[Success Result]
    
    subgraph SuccessStructure["Success Structure"]
        SuccessFlag[success: true]
        Content[content: { data }]
        NoError[error: null]
    end
    
    Success --> SuccessStructure
    
    subgraph Processing["Copilot Processing"]
        Parse[Parse Content]
        Format[Format for User]
        Present[Present to User]
    end
    
    SuccessStructure --> Processing
    
    style Success fill:#e1ffe1
    style SuccessStructure fill:#e1f5ff
```

**Example Success Response:**
```json
{
  "success": true,
  "content": {
    "accountid": "12345678-...",
    "name": "Contoso Corp",
    "revenue": 1000000,
    "primarycontactid": {
      "id": "87654321-...",
      "name": "John Doe"
    }
  },
  "error": null
}
```

### Error Results

```mermaid
graph TB
    Tool[Tool Execution]
    
    Tool --> Error[Error Result]
    
    subgraph ErrorStructure["Error Structure"]
        SuccessFlag[success: false]
        NoContent[content: null]
        ErrorObj[error: { code, message, details }]
    end
    
    Error --> ErrorStructure
    
    subgraph ErrorCodes["Error Types"]
        NotFound[NOT_FOUND<br/>Resource not found]
        Validation[VALIDATION_ERROR<br/>Invalid input]
        Dataverse[DATAVERSE_ERROR<br/>SDK error]
        Unauthorized[UNAUTHORIZED<br/>No connection]
    end
    
    ErrorStructure --> ErrorCodes
    
    style Error fill:#ffe1e1
    style ErrorStructure fill:#ffe1e1
```

**Example Error Response:**
```json
{
  "success": false,
  "content": null,
  "error": {
    "code": "NOT_FOUND",
    "message": "Record not found",
    "details": "No account record with ID 12345678-... exists in the environment"
  }
}
```

## Active Connection Context

### Connection Requirement

```mermaid
graph TB
    ToolCall[Tool Call Received]
    
    ToolCall --> Check{Active<br/>Connection?}
    
    Check -->|No| Error[Return Error<br/>UNAUTHORIZED]
    Check -->|Yes| GetContext[Get Dataverse Context]
    
    GetContext --> Context[IDataverseContext]
    
    subgraph ContextContents["Context Contents"]
        ServiceClient[ServiceClient<br/>Authenticated SDK client]
        OrgService[IOrganizationService<br/>SDK interface]
        ConnInfo[Connection Information]
    end
    
    Context --> ContextContents
    
    ContextContents --> Execute[Execute Tool]
    
    style Execute fill:#e1ffe1
    style Error fill:#ffe1e1
```

**Important:** All tool executions use the currently active connection. Ensure a connection is active before using tools.

## Tool Naming Convention

### Kebab-Case Format

```mermaid
graph TB
    Name[Tool Name]
    
    subgraph Rules["Naming Rules"]
        Format[Format: kebab-case]
        Lowercase[All lowercase]
        Hyphens[Words separated by hyphens]
        NoSpaces[No spaces or underscores]
    end
    
    Name --> Rules
    
    subgraph Examples["Valid Examples"]
        Ex1[list-entities]
        Ex2[get-record]
        Ex3[create-record]
        Ex4[get-entity-metadata]
        Ex5[execute-workflow]
    end
    
    Rules --> Examples
    
    subgraph Invalid["Invalid Examples"]
        Inv1[listEntities ❌]
        Inv2[list_entities ❌]
        Inv3[ListEntities ❌]
        Inv4["list entities ❌"]
    end
    
    Rules -.->|Avoid| Invalid
    
    style Examples fill:#e1ffe1
    style Invalid fill:#ffe1e1
```

## Common Tools

### Built-in Tool Categories

```mermaid
graph TB
    Tools[Common Tools]
    
    subgraph Entity["Entity Operations"]
        ListEntities[list-entities<br/>List all entities]
        GetMetadata[get-entity-metadata<br/>Get entity definition]
    end
    
    subgraph Record["Record Operations"]
        GetRecord[get-record<br/>Retrieve a record]
        CreateRecord[create-record<br/>Create a record]
        UpdateRecord[update-record<br/>Update a record]
        DeleteRecord[delete-record<br/>Delete a record]
    end
    
    subgraph Query["Query Operations"]
        RetrieveMultiple[retrieve-multiple<br/>Query records]
        FetchXml[execute-fetchxml<br/>Execute FetchXML]
    end
    
    subgraph Info["Environment Info"]
        WhoAmI[get-whoami<br/>Current user info]
        OrgInfo[get-organization<br/>Org details]
    end
    
    Tools --> Entity
    Tools --> Record
    Tools --> Query
    Tools --> Info
    
    style Entity fill:#e1f5ff
    style Record fill:#ffe1e1
    style Query fill:#fff4e1
    style Info fill:#e1ffe1
```

### Tool Usage Patterns

**Pattern 1: Information Retrieval**
```
User Intent → Copilot selects tool → Tool queries Dataverse → Returns data
```

**Pattern 2: Data Modification**
```
User Intent → Copilot selects tool → Tool validates → Modifies Dataverse → Confirms
```

**Pattern 3: Multi-Step Operations**
```
Query entities → Select entity → Get metadata → Create record → Verify
```

## Performance Considerations

### Execution Time

```mermaid
graph LR
    Start[Tool Call] -->|~5ms| Bridge[Bridge Forward]
    Bridge -->|~5ms| Core[Core Processing]
    Core -->|~10ms| Validation[Validation]
    Validation -->|Variable| SDK[Dataverse SDK Call]
    SDK -->|~100-500ms| DataverseAPI[Dataverse API]
    DataverseAPI -->|~100-500ms| Response[Response]
    Response -->|~10ms| Format[Format Result]
    Format -->|~5ms| Return[Return to Copilot]
    
    style SDK fill:#ffe1e1
    style DataverseAPI fill:#ffe1e1
```

**Typical Timings:**
- Simple queries: 200-500ms
- Complex queries: 500-2000ms
- Create/Update: 300-1000ms
- Large datasets: 1-5 seconds

### Optimization Tips

1. **Use Specific Queries**
   - Request only needed columns
   - Use filters to reduce results
   - Avoid retrieving all records

2. **Batch Operations**
   - Use tools that support batch operations
   - Reduce number of round trips

3. **Cache Results**
   - Copilot may cache responses
   - Reuse data within conversation

## Troubleshooting Tool Usage

### Common Issues

```mermaid
graph TB
    Issue{Tool Issue}
    
    Issue -->|Not Found| NotFound[Tool Not Available]
    Issue -->|Fails| Fails[Execution Failure]
    Issue -->|Timeout| Timeout[Operation Timeout]
    Issue -->|Wrong Result| WrongResult[Unexpected Output]
    
    NotFound --> NotFoundFix["• Verify plugin installed<br/>• Reload plugins<br/>• Check tool name spelling"]
    
    Fails --> FailsFix["• Check active connection<br/>• Verify parameters<br/>• Check Dataverse permissions<br/>• Review error message"]
    
    Timeout --> TimeoutFix["• Check network<br/>• Simplify query<br/>• Check Dataverse performance<br/>• Reduce result size"]
    
    WrongResult --> WrongFix["• Verify parameters<br/>• Check active connection<br/>• Review tool documentation<br/>• Check Dataverse data"]
    
    style NotFound fill:#ffe1e1
    style Fails fill:#ffe1e1
    style Timeout fill:#ffe1e1
    style WrongResult fill:#ffe1e1
```

### Debugging Tools

**View Available Tools:**
```
Ask Copilot: @workspace What tools are available for Dataverse?
```

**Check Active Connection:**
```
Sidebar → Connections → Look for ✓ (green checkmark)
```

**View Execution Logs:**
```
Output Panel → Dataverse MCP Toolbox
Filter for tool execution logs
```

## Next Steps

- **[Creating Plugins](14-Creating-Plugins.md)**: Build custom tools
- **[Plugin Architecture](15-Plugin-Architecture.md)**: Understanding plugin structure
- **[Troubleshooting](13-Troubleshooting.md)**: Detailed troubleshooting
