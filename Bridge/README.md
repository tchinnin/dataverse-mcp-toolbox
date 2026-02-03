# Dataverse MCP ToolBox Bridge

## Overview
The Bridge executable acts as a proxy between GitHub Copilot (via MCP STDIO) and the main Dataverse MCP ToolBox server (via Named Pipes).

## Architecture
```
GitHub Copilot (MCP)
        ↓ STDIO
    Bridge.exe
        ↓ Named Pipe
Main Server (DataverseMCPToolBox)
        ↓
    Dataverse
```

## Purpose
- **Launched by**: GitHub Copilot via MCP configuration
- **Connects to**: Main server via Named Pipe `DataverseMCPToolBox`
- **Forwards**: STDIO ↔ Named Pipe bidirectionally

## Usage
This executable is automatically launched by the MCP configuration in VS Code. It should not be run directly by users.

### MCP Configuration Example
```json
{
  "mcpServers": {
    "dataverse": {
      "command": "/path/to/DataverseMCPToolBox.Bridge",
      "args": []
    }
  }
}
```

## Requirements
- The main server (`DataverseMCPToolBox`) must be running
- Named Pipe `DataverseMCPToolBox` must be available
- Connection timeout: 10 seconds

## Logging
All logs are written to **stderr** to avoid polluting STDIO JSON-RPC communication.
