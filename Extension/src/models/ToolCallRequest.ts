/**
 * Request to execute an MCP tool
 */
export interface ToolCallRequest {
    /**
     * Name of the tool to execute (kebab-case)
     */
    toolName: string;

    /**
     * Connection ID to use for tool execution
     */
    connectionId: string;

    /**
     * JSON string containing tool parameters (camelCase)
     * Null or empty if tool has no parameters
     */
    parametersJson?: string;
}
