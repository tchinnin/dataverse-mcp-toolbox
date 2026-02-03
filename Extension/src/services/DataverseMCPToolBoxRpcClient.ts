import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import * as net from 'net';
import { MessageConnection, createMessageConnection } from 'vscode-jsonrpc/node';
import { NewlineDelimitedMessageReader } from '../utils/NewlineDelimitedMessageReader';
import { NewlineDelimitedMessageWriter } from '../utils/NewlineDelimitedMessageWriter';
import { ConnectionRequest, ConnectionResult, OrganizationDetail, WhoAmIResult } from '../models/RpcModels';
import { PluginInfo } from '../models/PluginInfo';
import { ToolInfo } from '../models/ToolInfo';
import { ToolCallRequest } from '../models/ToolCallRequest';
import { ToolCallResult } from '../models/ToolCallResult';
import { PluginInstallRequest, PluginInstallResult } from '../models/PluginInstallRequest';
import { ServerVersionInfo } from '../models/ServerVersionInfo';

// Platform-specific Named Pipe configuration
const CONNECTION_RETRY_DELAY = 500;
const MAX_CONNECTION_RETRIES = 15;

// Helper to get platform-specific pipe path using environment variable
function getPipePath(): string {
    // Read pipe name from environment variable (set during server startup)
    const pipeName = process.env.DATAVERSE_MCP_PIPE_NAME || 'DataverseMCPToolBox';
    
    if (process.platform === 'win32') {
        return `\\\\.\\pipe\\${pipeName}`;
    } else {
        // Unix-like systems use domain sockets for named pipes
        return path.join('/tmp', `CoreFxPipe_${pipeName}`);
    }
}

/**
 * Client JSON-RPC pour communiquer avec le serveur .NET Dataverse via Named Pipes
 * Architecture Sidecar: 
 * - Le serveur principal écoute sur Named Pipe
 * - L'extension se connecte via Named Pipe Client
 * - GitHub Copilot utilise le Bridge (STDIO → Named Pipe)
 */
export class DataverseMCPToolBoxRpcClient {
    private connection: MessageConnection | null = null;
    private serverProcess: cp.ChildProcess | null = null;
    private pipeStream: net.Socket | null = null;
    private isConnected: boolean = false;
    private _pipeName: string = '';
    private readonly _onConnectionStatusChanged = new vscode.EventEmitter<boolean>();
    public readonly onConnectionStatusChanged = this._onConnectionStatusChanged.event;

    /**
     * Get the current Named Pipe name
     * This is used by the MCP Bridge to connect to the same Core Server
     */
    public getPipeName(): string {
        return this._pipeName || process.env.DATAVERSE_MCP_PIPE_NAME || `DataverseMCPToolBox-${process.pid}`;
    }

    /**
     * Check if server is currently connected
     */
    public isServerConnected(): boolean {
        return this.isConnected && this.connection !== null;
    }

    /**
     * Connecte au serveur .NET via Named Pipe
     * Si le serveur n'est pas démarré, le démarre automatiquement
     */
    async connect(serverPath: string, pluginDirectory?: string): Promise<void> {
        if (this.isConnected) {
            return;
        }

        try {
            console.error('[RPC Client] Attempting Named Pipe connection to server...');

            // Try to connect to existing server
            let connected = false;
            
            for (let i = 0; i < MAX_CONNECTION_RETRIES; i++) {
                connected = await this.tryConnect();
                if (connected) {
                    break;
                }
                
                // If first attempt failed, try to start the server
                if (i === 0) {
                    console.error('[RPC Client] Server not found, starting main server...');
                    await this.startServer(serverPath, pluginDirectory);
                }
                
                console.error(`[RPC Client] Connection attempt ${i + 1}/${MAX_CONNECTION_RETRIES}...`);
                await new Promise(resolve => setTimeout(resolve, CONNECTION_RETRY_DELAY));
            }

            if (!connected) {
                throw new Error(
                    'Could not connect to Dataverse MCP server via Named Pipe. ' +
                    'Please check the server logs in VS Code Output panel.'
                );
            }

            console.error('[RPC Client] Connected to Named Pipe server successfully');
            this.isConnected = true;
            this._onConnectionStatusChanged.fire(true);
        } catch (error) {
            console.error('Error connecting to .NET server:', error);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
            throw error;
        }
    }

    /**
     * Start the main server process
     */
    private async startServer(serverPath: string, pluginDirectory?: string): Promise<void> {
        console.error('[RPC Client] Starting main server process...');

        // Generate unique pipe name based on VS Code process PID for multi-instance isolation
        const pipeName = `DataverseMCPToolBox-${process.pid}`;
        this._pipeName = pipeName;
        process.env.DATAVERSE_MCP_PIPE_NAME = pipeName;
        console.error(`[RPC Client] Using Named Pipe: ${pipeName}`);

        const env: NodeJS.ProcessEnv = { ...process.env };
        if (pluginDirectory) {
            env['DATAVERSE_MCP_PLUGIN_DIR'] = pluginDirectory;
        }
        // Ensure pipe name is passed to server
        env['DATAVERSE_MCP_PIPE_NAME'] = pipeName;

        this.serverProcess = cp.spawn(serverPath, [], {
            stdio: ['ignore', 'ignore', 'pipe'], // stderr only for logs
            env,
            detached: false
        });

        // Capture server logs
        this.serverProcess.stderr?.on('data', (data: Buffer) => {
            const message = data.toString();
            console.error(`[Server] ${message.trim()}`);
        });

        this.serverProcess.on('exit', (code, signal) => {
            console.error(`[Server] Process exited with code ${code}, signal ${signal}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        this.serverProcess.on('error', (error) => {
            console.error('[Server] Process error:', error);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        // Wait a bit for server to start
        await new Promise(resolve => setTimeout(resolve, 1000));
    }

    /**
     * Tente de se connecter au serveur Named Pipe existant
     */
    private async tryConnect(): Promise<boolean> {
        return new Promise<boolean>((resolve) => {
            const pipePath = getPipePath();
            
            console.error(`[RPC Client] Trying to connect to Named Pipe: ${pipePath}`);
            
            const timeout = setTimeout(() => {
                if (this.pipeStream) {
                    this.pipeStream.destroy();
                    this.pipeStream = null;
                }
                resolve(false);
            }, 1000);

            this.pipeStream = net.connect(pipePath);

            this.pipeStream.once('connect', () => {
                clearTimeout(timeout);
                this.setupConnection();
                resolve(true);
            });

            this.pipeStream.once('error', () => {
                clearTimeout(timeout);
                if (this.pipeStream) {
                    this.pipeStream.destroy();
                    this.pipeStream = null;
                }
                resolve(false);
            });
        });
    }

    /**
     * Configure la connexion JSON-RPC sur le Named Pipe
     */
    private setupConnection(): void {
        if (!this.pipeStream) {
            return;
        }

        console.error('[RPC Client] Setting up JSON-RPC over Named Pipe');

        // Handle pipe errors and closure
        this.pipeStream.on('error', (error) => {
            console.error('[RPC Client] Named Pipe error:', error);
            vscode.window.showErrorMessage(`Connection to Dataverse RPC server lost: ${error.message}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        this.pipeStream.on('close', () => {
            console.error('[RPC Client] Named Pipe closed');
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        // Create JSON-RPC connection using newline-delimited protocol
        const reader = new NewlineDelimitedMessageReader(this.pipeStream);
        const writer = new NewlineDelimitedMessageWriter(this.pipeStream);
        this.connection = createMessageConnection(reader, writer);

        // Enable trace for debugging
        this.connection.trace(2, {
            log: (message: string) => console.log(`[JSON-RPC TRACE] ${message}`)
        });

        // Handle connection errors
        this.connection.onError((error: any) => {
            console.error('[RPC Client] JSON-RPC error:', error);
        });

        this.connection.onClose(() => {
            console.error('[RPC Client] JSON-RPC connection closed');
            this.isConnected = false;
        });

        // Start listening
        this.connection.listen();
    }

    /**
     * Ferme la connexion Named Pipe (peut tuer le serveur si nécessaire)
     */
    async disconnect(): Promise<void> {
        console.error('[RPC Client] Disconnecting from Named Pipe server...');

        if (this.connection) {
            this.connection.dispose();
            this.connection = null;
        }

        if (this.pipeStream) {
            this.pipeStream.destroy();
            this.pipeStream = null;
        }

        // Kill the server process if we started it
        if (this.serverProcess && !this.serverProcess.killed) {
            console.error('[RPC Client] Terminating server process...');
            try {
                // Try graceful shutdown first
                this.serverProcess.kill('SIGTERM');
                
                // Force kill if still running after 2 seconds
                setTimeout(() => {
                    if (this.serverProcess && !this.serverProcess.killed) {
                        console.error('[RPC Client] Force killing server process...');
                        this.serverProcess.kill('SIGKILL');
                    }
                }, 2000);
            } catch (error) {
                console.error('[RPC Client] Error killing server process:', error);
            }
            this.serverProcess = null;
        }

        this.isConnected = false;
        this._onConnectionStatusChanged.fire(false);
        console.error('[RPC Client] Disconnected');
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

    /**
     * Get server version information
     */
    async getServerVersion(): Promise<ServerVersionInfo> {
        this.ensureConnected();
        return await this.connection!.sendRequest('GetServerVersion');
    }

    /**
     * Shutdown the server gracefully (for upgrades)
     */
    async shutdownServer(): Promise<void> {
        this.ensureConnected();
        await this.connection!.sendRequest('ShutdownServer');
    }

    private ensureConnected(): void {
        if (!this.isConnected || !this.connection) {
            throw new Error('RPC server is not ready yet. Please wait a moment and try again.');
        }
    }
}
