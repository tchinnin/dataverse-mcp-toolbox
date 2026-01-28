/**
 * Model representing the VS Code MCP server configuration
 * This matches the format expected in ~/Library/Application Support/Code/User/mcp.json
 */

/**
 * Configuration for a single MCP server
 */
export interface McpServer {
    /**
     * Type of server connection - 'stdio' for local process communication
     */
    type: 'stdio' | 'sse' | 'http';

    /**
     * Command to execute the server
     */
    command: string;

    /**
     * Command-line arguments
     */
    args?: string[];

    /**
     * Environment variables
     */
    env?: Record<string, string>;
}

/**
 * Root configuration object for MCP servers
 */
export interface McpConfiguration {
    /**
     * MCP servers keyed by server name
     */
    servers: Record<string, McpServer>;
    
    /**
     * Optional input variables for sensitive data like API keys
     */
    inputs?: Array<{
        type: 'promptString';
        id: string;
        description: string;
        password?: boolean;
    }>;
}
