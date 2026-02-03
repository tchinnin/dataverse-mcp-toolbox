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

    // Internal storage uses Uri
    private bridgeExecutableUri: vscode.Uri | undefined;
    private pluginDirectoryUri: vscode.Uri | undefined;
    private pipeName: string | undefined;
    private socketDir: string | undefined; // Socket path, NOT a filesystem Uri

    constructor(private readonly context: vscode.ExtensionContext) {}

    /**
     * Update the server configuration
     * Triggers onDidChangeMcpServerDefinitions to notify VS Code
     * @param bridgeUri - Uri to the Bridge executable
     * @param pluginDirUri - Uri to the plugin directory
     * @param pipe - Named pipe identifier
     * @param socketDirectory - Socket directory path (NOT a Uri - Unix domain socket path)
     */
    updateConfiguration(bridgeUri: vscode.Uri, pluginDirUri: vscode.Uri, pipe: string, socketDirectory: string): void {
        this.bridgeExecutableUri = bridgeUri;
        this.pluginDirectoryUri = pluginDirUri;
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
        if (!this.bridgeExecutableUri || !this.pluginDirectoryUri || !this.pipeName || !this.socketDir) {
            console.error('[MCP Provider] Configuration not ready yet');
            return [];
        }

        // BOUNDARY: Uri → fsPath for Node.js fs existence check
        const bridgePathString = this.bridgeExecutableUri.fsPath;
        if (!fs.existsSync(bridgePathString)) {
            console.error(`[MCP Provider] Bridge executable not found: ${bridgePathString}`);
            return [];
        }

        console.error(`[MCP Provider] Providing server definition:`);
        console.error(`  - Bridge: ${bridgePathString}`);
        console.error(`  - Plugin Dir: ${this.pluginDirectoryUri.fsPath}`);
        console.error(`  - Pipe Name: ${this.pipeName}`);
        console.error(`  - Socket Dir: ${this.socketDir}`);

        // BOUNDARY: Uri → fsPath for MCP API (VS Code MCP expects string paths)
        const serverDefinition = new vscode.McpStdioServerDefinition(
            'Dataverse MCP Toolbox',  // label
            bridgePathString,          // command (requires string path)
            [],                        // args
            {                          // env
                DATAVERSE_MCP_PLUGIN_DIR: this.pluginDirectoryUri.fsPath, // BOUNDARY: Uri → fsPath for env var
                DATAVERSE_MCP_PIPE_NAME: this.pipeName,
                TMPDIR: this.socketDir  // Socket path stays as string
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
