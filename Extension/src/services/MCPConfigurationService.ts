import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { McpConfiguration, McpServer } from '../models/McpServerConfig';

/**
 * Service for managing VS Code MCP server configuration.
 * 
 * This service handles registration of the Dataverse MCP server using the standard
 * Model Context Protocol (MCP) configuration format via mcp.json file.
 * 
 * IMPORTANT: This is NOT the VS Code LanguageModelTools API. This service uses
 * the standard MCP protocol which allows dynamic discovery of tools from .NET plugins.
 * The mcp.json configuration format is used by MCP-compatible clients like:
 * - Claude Desktop
 * - GitHub Copilot (with MCP support)
 * - Other MCP-compatible AI assistants
 * 
 * The MCP protocol allows tools to be discovered dynamically at runtime, which is
 * compatible with the plugin architecture where .NET assemblies are loaded dynamically.
 */
export class MCPConfigurationService {
    private static readonly MCP_SERVER_NAME = 'dataverse-mcp-toolbox';
    private readonly mcpConfigPath: string;

    constructor() {
        // Determine VS Code User directory based on platform
        const userDataDir = this.getVSCodeUserDirectory();
        this.mcpConfigPath = path.join(userDataDir, 'mcp.json');
        
        console.error(`[MCPConfig] MCP configuration path: ${this.mcpConfigPath}`);
    }

    /**
     * Get the MCP configuration file path
     */
    public getMcpConfigPath(): string {
        return this.mcpConfigPath;
    }

    /**
     * Get VS Code User directory path based on platform
     */
    private getVSCodeUserDirectory(): string {
        const platform = os.platform();
        const homeDir = os.homedir();

        switch (platform) {
            case 'darwin': // macOS
                return path.join(homeDir, 'Library', 'Application Support', 'Code', 'User');
            case 'win32': // Windows
                return path.join(process.env.APPDATA || '', 'Code', 'User');
            case 'linux': // Linux
                return path.join(homeDir, '.config', 'Code', 'User');
            default:
                throw new Error(`Unsupported platform: ${platform}`);
        }
    }

    /**
     * Load existing MCP configuration from file
     */
    private loadMcpConfiguration(): McpConfiguration {
        try {
            if (fs.existsSync(this.mcpConfigPath)) {
                const content = fs.readFileSync(this.mcpConfigPath, 'utf-8');
                const config = JSON.parse(content) as McpConfiguration;
                
                // Ensure servers property exists
                if (!config.servers) {
                    config.servers = {};
                }
                
                return config;
            }
        } catch (error) {
            console.error('[MCPConfig] Failed to load MCP configuration:', error);
        }

        // Return default empty configuration
        return { servers: {} };
    }

    /**
     * Save MCP configuration to file
     */
    private saveMcpConfiguration(config: McpConfiguration): void {
        try {
            // Ensure directory exists
            const dir = path.dirname(this.mcpConfigPath);
            if (!fs.existsSync(dir)) {
                fs.mkdirSync(dir, { recursive: true });
            }

            // Write configuration with pretty formatting
            const content = JSON.stringify(config, null, 2);
            fs.writeFileSync(this.mcpConfigPath, content, 'utf-8');
            
            console.error('[MCPConfig] MCP configuration saved successfully');
        } catch (error) {
            console.error('[MCPConfig] Failed to save MCP configuration:', error);
            throw error;
        }
    }

    /**
     * Register or update the Dataverse MCP server in the global configuration
     * @param serverPath Absolute path to the MCP server executable (Bridge)
     * @param pluginDirectory Absolute path to the plugins directory
     * @param pipeName Named pipe name for this VS Code instance
     * @param showNotification Whether to show a notification to the user (default: true)
     * @returns true if VS Code needs to be reloaded (first registration), false otherwise
     */
    public async registerMcpServer(
        serverPath: string, 
        pluginDirectory: string,
        pipeName: string,
        showNotification: boolean = true
    ): Promise<boolean> {
        try {
            console.error(`[MCPConfig] Registering Dataverse MCP Bridge: ${serverPath}`);
            console.error(`[MCPConfig] Plugin directory: ${pluginDirectory}`);
            console.error(`[MCPConfig] Pipe name: ${pipeName}`);

            // Verify server path exists
            if (!fs.existsSync(serverPath)) {
                throw new Error(`Bridge executable not found at: ${serverPath}`);
            }

            // Load existing configuration
            const config = this.loadMcpConfiguration();

            // Create Bridge configuration with pipe name environment variable
            // Bridge forwards stdio to Named Pipe (Core Server)
            const serverConfig: McpServer = {
                type: 'stdio',
                command: serverPath,
                args: [], // No arguments needed - Bridge auto-forwards
                env: {
                    DATAVERSE_MCP_PLUGIN_DIR: pluginDirectory,
                    DATAVERSE_MCP_PIPE_NAME: pipeName  // Critical: tells Bridge which pipe to connect to
                }
            };

            // Check if this is a new registration
            const previousConfig = config.servers[MCPConfigurationService.MCP_SERVER_NAME];
            const isNewRegistration = !previousConfig;
            
            // Register or update the server
            config.servers[MCPConfigurationService.MCP_SERVER_NAME] = serverConfig;

            // Save configuration
            this.saveMcpConfiguration(config);

            if (previousConfig) {
                console.error(`[MCPConfig] Updated existing Dataverse MCP Bridge configuration`);
            } else {
                console.error(`[MCPConfig] Registered new Dataverse MCP Bridge configuration - reload required`);
            }

            // Notify user with option to reload VS Code (if requested)
            if (showNotification) {
                const action = previousConfig ? 'updated' : 'registered';
                vscode.window.showInformationMessage(
                    `Dataverse MCP Bridge ${action} in mcp.json. Tools are available via MCP protocol for Copilot.`,
                    'Open MCP Configuration'
                ).then(selection => {
                    if (selection === 'Open MCP Configuration') {
                        this.openMcpConfiguration();
                    }
                });
            }

            return isNewRegistration;

        } catch (error) {
            console.error('[MCPConfig] Failed to register MCP server:', error);
            vscode.window.showErrorMessage(`Failed to register MCP server: ${error}`);
            throw error;
        }
    }

    /**
     * Unregister the Dataverse MCP server from the global configuration
     */
    public async unregisterMcpServer(): Promise<void> {
        try {
            console.error('[MCPConfig] Unregistering Dataverse MCP server');

            // Load existing configuration
            const config = this.loadMcpConfiguration();

            // Check if server is registered
            if (!config.servers[MCPConfigurationService.MCP_SERVER_NAME]) {
                console.error('[MCPConfig] Server not registered, nothing to unregister');
                return;
            }

            // Remove server configuration
            delete config.servers[MCPConfigurationService.MCP_SERVER_NAME];

            // Save configuration
            this.saveMcpConfiguration(config);

            console.error('[MCPConfig] Dataverse MCP server unregistered successfully');
            
            vscode.window.showInformationMessage(
                'Dataverse MCP server unregistered from VS Code.'
            );

        } catch (error) {
            console.error('[MCPConfig] Failed to unregister MCP server:', error);
            vscode.window.showErrorMessage(`Failed to unregister MCP server: ${error}`);
            throw error;
        }
    }

    /**
     * Check if the Dataverse MCP server is currently registered
     */
    public isServerRegistered(): boolean {
        try {
            const config = this.loadMcpConfiguration();
            return !!config.servers[MCPConfigurationService.MCP_SERVER_NAME];
        } catch (error) {
            console.error('[MCPConfig] Failed to check server registration:', error);
            return false;
        }
    }

    /**
     * Get the currently registered server path
     */
    public getRegisteredServerPath(): string | null {
        try {
            const config = this.loadMcpConfiguration();
            const serverConfig = config.servers[MCPConfigurationService.MCP_SERVER_NAME];
            return serverConfig?.command || null;
        } catch (error) {
            console.error('[MCPConfig] Failed to get registered server path:', error);
            return null;
        }
    }

    /**
     * Open the MCP configuration file in VS Code
     */
    public openMcpConfiguration(): void {
        try {
            const uri = vscode.Uri.file(this.mcpConfigPath);
            vscode.workspace.openTextDocument(uri).then(doc => {
                vscode.window.showTextDocument(doc);
            });
        } catch (error) {
            console.error('[MCPConfig] Failed to open MCP configuration:', error);
            vscode.window.showErrorMessage(`Failed to open MCP configuration: ${error}`);
        }
    }

    /**
     * Update the server path if already registered
     * @param serverPath New server path (Bridge)
     * @param pluginDirectory Plugin directory path
     * @param pipeName Named pipe name
     */
    public async updateServerPath(serverPath: string, pluginDirectory: string, pipeName: string): Promise<void> {
        if (this.isServerRegistered()) {
            await this.registerMcpServer(serverPath, pluginDirectory, pipeName, false);
        }
    }
}
