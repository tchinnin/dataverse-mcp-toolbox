/**
 * Information about an MCP tool exposed by a plugin
 */
export interface ToolInfo {
    /**
     * Unique name of the tool (kebab-case)
     */
    name: string;

    /**
     * Human-readable description of what the tool does
     */
    description: string;

    /**
     * JSON Schema object describing the input parameters
     */
    inputSchema?: any;
}
