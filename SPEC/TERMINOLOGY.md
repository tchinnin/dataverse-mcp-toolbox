# Terminology Clarification

To avoid confusion in the DataverseMCPToolbox project, we use specific terminology consistently throughout the documentation and codebase.

## Key Terms

### DataverseMCPToolbox
The overall project name: **DataverseMCPToolbox**  
- Refers to the complete system including the MCP server, plugin system, and VSCode extension

### Plugin  
A .NET class library that extends the MCP server functionality  
- Plugins are distributed as NuGet packages or GitHub releases
- Plugins contain one or more MCP tools
- Example: `MetadataPlugin`, `DataPlugin`, `SolutionPlugin`
- Interface: `IPlugin`

### MCP Tool (or DataverseMCPTool)
An individual function/operation that can be invoked via the MCP protocol  
- MCP tools are provided by plugins
- Each MCP tool represents a specific capability (e.g., "list tables", "create record")
- MCP tools are what AI assistants actually call
- Interface: `IMCPTool`
- Base class: `MCPToolBase`

## Class and Interface Naming

### Plugins
- **Interface**: `IPlugin`
- **Base Class**: `PluginBase`
- **Example**: `MetadataPlugin : PluginBase`

### MCP Tools
- **Interface**: `IMCPTool`
- **Base Class**: `MCPToolBase`
- **Registry**: `IMCPToolRegistry`
- **Executor**: `MCPToolExecutor`
- **Request**: `MCPToolRequest`
- **Result**: `MCPToolResult`
- **Error**: `MCPToolError`
- **Schema**: `MCPToolInputSchema`
- **Example**: `ListTablesM CPTool : MCPToolBase`

## Hierarchy

```
DataverseMCPToolbox (Project)
│
├── MCP Server (.NET Application)
│   ├── Plugin System
│   │   ├── Plugin 1 (e.g., MetadataPlugin)
│   │   │   ├── MCP Tool: list_tables
│   │   │   ├── MCP Tool: get_table
│   │   │   └── MCP Tool: list_columns
│   │   │
│   │   ├── Plugin 2 (e.g., DataPlugin)
│   │   │   ├── MCP Tool: create_record
│   │   │   ├── MCP Tool: retrieve_record
│   │   │   └── MCP Tool: update_record
│   │   │
│   │   └── Community Plugin (e.g., CustomPlugin)
│   │       ├── MCP Tool: custom_operation_1
│   │       └── MCP Tool: custom_operation_2
│   │
│   └── MCP Protocol Handler
│
└── VSCode Extension (Distribution)
```

## Usage in Documentation

### ✅ Correct Usage

- "The plugin provides three MCP tools for metadata operations"
- "Implement the `IMCPTool` interface to create an MCP tool"
- "The `GetMCPTools()` method returns all MCP tools from the plugin"
- "MCP tools are registered with the `IMCPToolRegistry`"
- "Execute an MCP tool using `MCPToolExecutor`"

### ❌ Avoid

- "The tool provides functions" (use "plugin" instead of "tool")
- "Tool library" when referring to plugin ecosystem (use "plugin library")
- "ITool" or "ToolBase" (always use MCP prefix: `IMCPTool`, `MCPToolBase`)

## In Code Comments

```csharp
/// <summary>
/// MCP tool for listing all tables in Dataverse.
/// </summary>
public class ListTablesMCPTool : MCPToolBase
{
    // Implementation
}

/// <summary>
/// Plugin that provides metadata-related MCP tools.
/// </summary>
public class MetadataPlugin : PluginBase
{
    public override IEnumerable<IMCPTool> GetMCPTools()
    {
        yield return new ListTablesMCPTool(DataverseClient, Logger);
        yield return new GetTableMCPTool(DataverseClient, Logger);
    }
}
```

## Summary

- **Plugin** = Container (NuGet package or DLL)
- **MCP Tool** = Individual capability/function
- **DataverseMCPToolbox** = The entire project ecosystem

This distinction helps developers understand:
- Plugins are distributed and installed
- MCP tools are invoked by AI assistants
- DataverseMCPToolbox is the ecosystem that brings it all together
