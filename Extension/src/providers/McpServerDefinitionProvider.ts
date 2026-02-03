import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

/**
 * MCP Server Definition Provider for Dataverse MCP Toolbox
 * Implements the official VS Code API for registering MCP servers
 * 
 * This provider replaces the file-based MCP registration approach with
 * VS Code's native vscode.lm.registerMcpServerDefinitionProvider API.
 */
export class McpServerDefinitionProvider implements vscode.McpServerDefinitionProvider {
    private readonly _onDidChangeMcpServerDefinitions = new vscode.EventEmitter<void>();
    readonly onDidChangeMcpServerDefinitions = this._onDidChangeMcpServerDefinitions.event;

    private bridgeExecutablePath: string | undefined;
    private pluginDirectory: string | undefined;
    private pipeName: string | undefined;
    private socketDir: string | undefined;

    constructor(private readonly context: vscode.ExtensionContext) {}

    /**
     * Update the server configuration
     * Triggers onDidChangeMcpServerDefinitions to notify VS Code
     */
    updateConfiguration(bridgePath: string, pluginDir: string, pipe: string, socketDirectory: string): void {
        this.bridgeExecutablePath = bridgePath;
        this.pluginDirectory = pluginDir;
        this.pipeName = pipe;
        this.socketDir = socketDirectory;
        
        // Notify VS Code that server definitions have changed
        this._onDidChangeMcpServerDefinitions.fire();
    }

    /**
     * Provide MCP server definitions to VS Code
     * Called by VS Code to discover available MCP servers
     */
    provideMcpServerDefinitions(): vscode.ProviderResult<vscode.McpServerDefinition[]> {
        // If configuration not set yet, return empty array
        if (!this.bridgeExecutablePath || !this.pluginDirectory || !this.pipeName || !this.socketDir) {
            console.error('[MCP Provider] Configuration not ready yet');
            return [];
        }

        // Verify bridge executable exists
        if (!fs.existsSync(this.bridgeExecutablePath)) {
            console.error(`[MCP Provider] Bridge executable not found: ${this.bridgeExecutablePath}`);
            return [];
        }

        console.error(`[MCP Provider] Providing server definition:`);
        console.error(`  - Bridge: ${this.bridgeExecutablePath}`);
        console.error(`  - Plugin Dir: ${this.pluginDirectory}`);
        console.error(`  - Pipe Name: ${this.pipeName}`);
        console.error(`  - Socket Dir: ${this.socketDir}`);

        // Create stdio MCP server definition using constructor
        const serverDefinition = new vscode.McpStdioServerDefinition(
            'Dataverse MCP Toolbox',  // label
            this.bridgeExecutablePath, // command
            [],                        // args
            {                          // env
                DATAVERSE_MCP_PLUGIN_DIR: this.pluginDirectory,
                DATAVERSE_MCP_PIPE_NAME: this.pipeName,
                TMPDIR: this.socketDir  // CRITICAL: Bridge must use same socket directory as Core Server
            }
        );

        return [serverDefinition];
    }

    /**
     * Resolve MCP server definition before starting
     * Called by VS Code when the MCP server needs to be started
     * 
     * This method performs final validation and can handle authentication if needed
     */
    resolveMcpServerDefinition(
        definition: vscode.McpServerDefinition
    ): vscode.ProviderResult<vscode.McpServerDefinition> {
        // Cast to stdio definition to access properties
        const stdioDefinition = definition as vscode.McpStdioServerDefinition;

        // Verify command path exists
        if (!fs.existsSync(stdioDefinition.command)) {
            const error = `Bridge executable not found: ${stdioDefinition.command}`;
            console.error(`[MCP Provider] ${error}`);
            throw new Error(error);
        }

        // Verify plugin directory exists
        const pluginDir = stdioDefinition.env?.DATAVERSE_MCP_PLUGIN_DIR;
        if (!pluginDir || typeof pluginDir !== 'string' || !fs.existsSync(pluginDir)) {
            const error = `Plugin directory not found: ${pluginDir}`;
            console.error(`[MCP Provider] ${error}`);
            throw new Error(error);
        }

        // Ensure executable has proper permissions (Unix systems)
        if (process.platform !== 'win32') {
            try {
                fs.chmodSync(stdioDefinition.command, 0o755);
            } catch (error) {
                console.error(`[MCP Provider] Failed to set executable permissions: ${error}`);
            }
        }

        console.error('[MCP Provider] Server definition resolved successfully');
        return definition;
    }

    /**
     * Dispose of resources
     */
    dispose(): void {
        this._onDidChangeMcpServerDefinitions.dispose();
    }
}
