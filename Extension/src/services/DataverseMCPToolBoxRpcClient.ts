import * as cp from 'child_process';
import * as path from 'path';
import * as vscode from 'vscode';
import { MessageConnection, createMessageConnection } from 'vscode-jsonrpc/node';
import { NewlineDelimitedMessageReader } from '../utils/NewlineDelimitedMessageReader';
import { NewlineDelimitedMessageWriter } from '../utils/NewlineDelimitedMessageWriter';
import { ConnectionRequest, ConnectionResult, OrganizationDetail, WhoAmIResult } from '../models/RpcModels';
import { PluginInfo } from '../models/PluginInfo';
import { ToolInfo } from '../models/ToolInfo';
import { ToolCallRequest } from '../models/ToolCallRequest';
import { ToolCallResult } from '../models/ToolCallResult';
import { PluginInstallRequest, PluginInstallResult } from '../models/PluginInstallRequest';

/**
 * Client JSON-RPC pour communiquer avec le serveur .NET Dataverse
 */
export class DataverseMCPToolBoxRpcClient {
    private connection: MessageConnection | null = null;
    private process: cp.ChildProcess | null = null;
    private isConnected: boolean = false;

    /**
     * Démarre le serveur .NET et établit la connexion JSON-RPC
     */
    async connect(serverPath: string, pluginDirectory?: string): Promise<void> {
        if (this.isConnected) {
            return;
        }

        try {
            console.error(`[RPC Client] Starting .NET RPC server from: ${serverPath}`);
            if (pluginDirectory) {
                console.error(`[RPC Client] Plugin directory: ${pluginDirectory}`);
            }

            // Démarrer le processus .NET SANS flag --mcp pour Extension
            // Pass plugin directory via environment variable
            const env = { ...process.env };
            if (pluginDirectory) {
                env.DATAVERSE_MCP_PLUGIN_DIR = pluginDirectory;
            }

            // Extension uses Content-Length protocol (vscode-jsonrpc default)
            // The MCP instance (launched by VS Code for Copilot) runs separately with --mcp flag
            // Both instances share connection state via persisted connection file
            this.process = cp.spawn(serverPath, [], {
                stdio: ['pipe', 'pipe', 'pipe'],
                env: env
            });

            if (!this.process.stdin || !this.process.stdout || !this.process.stderr) {
                throw new Error('Failed to create stdio streams for .NET process');
            }

            // Logger les erreurs du processus (stderr only - stdout is used for JSON-RPC)
            this.process.stderr.on('data', (data) => {
                console.error(`[.NET Server STDERR] ${data.toString()}`);
            });

            this.process.on('error', (error) => {
                console.error('Failed to start .NET process:', error);
                vscode.window.showErrorMessage(`Failed to start Dataverse RPC server: ${error.message}`);
            });

            this.process.on('exit', (code) => {
                console.log(`.NET process exited with code ${code}`);
                this.isConnected = false;
            });

            // Créer la connexion JSON-RPC avec newline-delimited protocol
            // IMPORTANT: Using newline-delimited JSON-RPC to match .NET's NewLineDelimitedMessageHandler
            // This allows unified communication protocol between Extension and MCP (GitHub Copilot)
            const reader = new NewlineDelimitedMessageReader(this.process.stdout);
            const writer = new NewlineDelimitedMessageWriter(this.process.stdin);
            this.connection = createMessageConnection(reader, writer);

            // Debug: logger les messages envoyés et reçus
            this.connection.trace(2, {
                log: (message: string) => console.log(`[JSON-RPC TRACE] ${message}`)
            });

            // Gérer les erreurs de connexion
            this.connection.onError((error: any) => {
                console.error('JSON-RPC connection error:', error);
            });

            this.connection.onClose(() => {
                console.log('JSON-RPC connection closed');
                this.isConnected = false;
            });

            // Démarrer l'écoute
            this.connection.listen();
            
            // Attendre un peu pour que le serveur soit prêt
            await new Promise(resolve => setTimeout(resolve, 2000));
            
            this.isConnected = true;

            console.log('JSON-RPC connection established');
        } catch (error) {
            console.error('Error connecting to .NET server:', error);
            throw error;
        }
    }

    /**
     * Ferme la connexion et arrête le serveur .NET
     */
    async disconnect(): Promise<void> {
        if (this.connection) {
            this.connection.dispose();
            this.connection = null;
        }

        if (this.process) {
            this.process.kill();
            this.process = null;
        }

        this.isConnected = false;
    }

    /**
     * Crée une nouvelle connexion Dataverse
     */
    async createConnection(request: ConnectionRequest): Promise<ConnectionResult> {
        this.ensureConnected();
        return await this.connection!.sendRequest('CreateConnection', { request });
    }

    /**
     * Teste si une connexion est valide
     */
    async testConnection(connectionId: string): Promise<boolean> {
        this.ensureConnected();
        return await this.connection!.sendRequest('TestConnection', { connectionId });
    }

    /**
     * Récupère les détails de l'organisation
     */
    async getOrganizationDetails(connectionId: string): Promise<OrganizationDetail | null> {
        this.ensureConnected();
        return await this.connection!.sendRequest('GetOrganizationDetails', { connectionId });
    }

    /**
     * Récupère les informations WhoAmI pour une connexion
     */
    async getWhoAmI(connectionId: string): Promise<WhoAmIResult> {
        this.ensureConnected();
        return await this.connection!.sendRequest('GetWhoAmI', { connectionId });
    }

    /**
     * Ferme une connexion
     */
    async closeConnection(connectionId: string): Promise<void> {
        this.ensureConnected();
        await this.connection!.sendRequest('CloseConnection', { connectionId });
    }

    /**
     * Ferme toutes les connexions
     */
    async closeAllConnections(): Promise<void> {
        this.ensureConnected();
        await this.connection!.sendRequest('CloseAllConnections');
    }

    /**
     * Set the active connection for MCP tool executions
     */
    async setActiveConnection(connectionId: string): Promise<void> {
        this.ensureConnected();
        console.error(`[RPC Client] Setting active connection for MCP: ${connectionId}`);
        await this.connection!.sendRequest('SetActiveConnection', { connectionId });
    }

    /**
     * Set the plugin directory path (must be called before plugin operations)
     */
    async setPluginDirectory(directoryPath: string): Promise<void> {
        this.ensureConnected();
        await this.connection!.sendRequest('SetPluginDirectory', { directoryPath });
    }

    /**
     * Install a plugin from NuGet
     */
    async installPlugin(request: PluginInstallRequest): Promise<PluginInstallResult> {
        this.ensureConnected();
        return await this.connection!.sendRequest('InstallPlugin', { request });
    }

    /**
     * Uninstall a plugin
     */
    async uninstallPlugin(packageId: string): Promise<boolean> {
        this.ensureConnected();
        return await this.connection!.sendRequest('UninstallPlugin', { packageId });
    }

    /**
     * Reload all plugins
     */
    async reloadPlugins(): Promise<void> {
        this.ensureConnected();
        await this.connection!.sendRequest('ReloadPlugins');
    }

    /**
     * List all installed plugins
     */
    async listPlugins(): Promise<PluginInfo[]> {
        this.ensureConnected();
        return await this.connection!.sendRequest('ListPlugins');
    }

    /**
     * List all available MCP tools
     */
    async listTools(): Promise<ToolInfo[]> {
        this.ensureConnected();
        return await this.connection!.sendRequest('ListTools');
    }

    /**
     * Execute an MCP tool
     */
    async callTool(request: ToolCallRequest): Promise<ToolCallResult> {
        this.ensureConnected();
        return await this.connection!.sendRequest('CallTool', { request });
    }

    private ensureConnected(): void {
        if (!this.isConnected || !this.connection) {
            throw new Error('RPC server is not ready yet. Please wait a moment and try again.');
        }
    }
}
