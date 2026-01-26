/**
 * Result of an MCP tool execution
 */
export interface ToolCallResult {
    /**
     * Whether the tool executed successfully
     */
    isSuccess: boolean;

    /**
     * Tool output content (if successful)
     */
    content?: any;

    /**
     * Error information (if failed)
     */
    error?: ToolErrorInfo;
}

/**
 * Error information for failed tool execution
 */
export interface ToolErrorInfo {
    /**
     * Error code (e.g., "VALIDATION_ERROR", "EXECUTION_ERROR")
     */
    code: string;

    /**
     * Human-readable error message
     */
    message: string;

    /**
     * Additional error details
     */
    details?: any;
}
