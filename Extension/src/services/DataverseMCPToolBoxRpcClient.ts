import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
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

// Helper to get platform-specific pipe path
// socketDir is used on Unix platforms, ignored on Windows (uses \\.\pipe\ namespace)
function getPipePath(socketDir: string): string {
    // Read pipe name from environment variable (set during server startup)
    const pipeName = process.env.DATAVERSE_MCP_PIPE_NAME || 'DataverseMCPToolBox';
    
    if (process.platform === 'win32') {
        // Windows uses named pipe namespace (no file system)
        return `\\\\.\\pipe\\${pipeName}`;
    } else {
        // Unix-like systems use domain socket files
        // CRITICAL: NamedPipeServerStream automatically adds "CoreFxPipe_" prefix on Unix
        // We must match this exact naming convention
        return path.join(socketDir, `CoreFxPipe_${pipeName}`);
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
    private _socketDir: string = '';
    private readonly _onConnectionStatusChanged = new vscode.EventEmitter<boolean>();
    public readonly onConnectionStatusChanged = this._onConnectionStatusChanged.event;
    private outputChannel?: vscode.OutputChannel;

    constructor(outputChannel?: vscode.OutputChannel) {
        this.outputChannel = outputChannel;
    }

    /**
     * Log message to both console and output channel
     */
    private log(message: string): void {
        console.error(message);
        if (this.outputChannel) {
            this.outputChannel.appendLine(message);
        }
    }

    /**
     * Get the current Named Pipe name
     * This is used by the MCP Bridge to connect to the same Core Server
     */
    public getPipeName(): string {
        return this._pipeName || process.env.DATAVERSE_MCP_PIPE_NAME || `${process.pid}`;
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
    async connect(serverPath: string, pluginDirectory?: string, socketDir?: string): Promise<void> {
        if (this.isConnected) {
            return;
        }

        try {
            this.log('[RPC Client] Attempting Named Pipe connection to server...');

            // Initialize socket directory from parameter (os.tmpdir())
            // TMPDIR will be set before server spawn to control Path.GetTempPath()
            if (socketDir) {
                this._socketDir = socketDir;
                // Create socket directory if it doesn't exist
                if (!fs.existsSync(this._socketDir)) {
                    fs.mkdirSync(this._socketDir, { recursive: true });
                    this.log(`[RPC Client] Created socket directory: ${this._socketDir}`);
                } else {
                    this.log(`[RPC Client] Using existing socket directory: ${this._socketDir}`);
                }
            } else {
                this.log('[RPC Client] WARNING: No socket directory provided');
            }

            // CRITICAL: Initialize pipe name BEFORE first connection attempt
            // This ensures getPipePath() returns the correct path from the start
            // Use short name to respect Unix socket 104-char path limit
            if (!this._pipeName) {
                const pipeName = `${process.pid}`; // Just the PID for uniqueness
                this._pipeName = pipeName;
                process.env.DATAVERSE_MCP_PIPE_NAME = pipeName;
                this.log(`[RPC Client] Initialized pipe name: ${pipeName}`);
            }

            // Try to connect to existing server
            let connected = false;
            
            for (let i = 0; i < MAX_CONNECTION_RETRIES; i++) {
                connected = await this.tryConnect();
                if (connected) {
                    break;
                }
                
                // If first attempt failed, try to start the server
                if (i === 0) {
                    this.log('[RPC Client] Server not found, starting main server...');
                    await this.startServer(serverPath, pluginDirectory, this._socketDir);
                }
                
                this.log(`[RPC Client] Connection attempt ${i + 1}/${MAX_CONNECTION_RETRIES}...`);
                await new Promise(resolve => setTimeout(resolve, CONNECTION_RETRY_DELAY));
            }

            if (!connected) {
                const errorMsg = 'Could not connect to Dataverse MCP server via Named Pipe after 15 attempts.';
                this.log(`[RPC Client] ${errorMsg}`);
                vscode.window.showErrorMessage(
                    `${errorMsg} Please check the server logs for details.`,
                    'Show Output',
                    'Retry'
                ).then(selection => {
                    if (selection === 'Show Output') {
                        if (this.outputChannel) {
                            this.outputChannel.show();
                        }
                    } else if (selection === 'Retry') {
                        vscode.commands.executeCommand('dataversemcptoolbox.startServerManually');
                    }
                });
                throw new Error(errorMsg);
            }

            this.log('[RPC Client] Connected to Named Pipe server successfully');
            this.isConnected = true;
            this._onConnectionStatusChanged.fire(true);
        } catch (error) {
            this.log(`Error connecting to .NET server: ${error}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
            throw error;
        }
    }

    /**
     * Start the main server process
     */
    private async startServer(serverPath: string, pluginDirectory?: string, socketDir?: string): Promise<void> {
        this.log('[RPC Client] Starting main server process...');
        this.log(`[RPC Client] Binary path: ${serverPath}`);

        // Verify binary exists
        if (!fs.existsSync(serverPath)) {
            const errorMsg = `Server binary not found at: ${serverPath}`;
            this.log(`[RPC Client] ERROR: ${errorMsg}`);
            vscode.window.showErrorMessage(
                `Dataverse MCP Server binary not found. Please reinstall the extension or check the installation.`,
                'Show Details'
            ).then(selection => {
                if (selection === 'Show Details') {
                    vscode.window.showErrorMessage(errorMsg);
                }
            });
            throw new Error(errorMsg);
        }

        // Set executable permissions on Unix platforms (critical for macOS/Linux)
        if (process.platform !== 'win32') {
            try {
                fs.chmodSync(serverPath, 0o755);
                this.log('[RPC Client] Set executable permissions on server binary');
            } catch (error) {
                const errorMsg = `Failed to set executable permissions on server binary: ${error}`;
                this.log(`[RPC Client] ERROR: ${errorMsg}`);
                vscode.window.showErrorMessage(
                    `Cannot make Dataverse MCP Server executable. Please check file permissions.`,
                    'Show Details'
                ).then(selection => {
                    if (selection === 'Show Details') {
                        vscode.window.showErrorMessage(errorMsg);
                    }
                });
                throw new Error(errorMsg);
            }
        }

        // Generate unique pipe name based on VS Code process PID for multi-instance isolation
        // Note: pipe name should already be initialized in connect(), but we verify it here
        // Use short name to respect Unix socket 104-char path limit
        const pipeName = this._pipeName || `${process.pid}`;
        if (!this._pipeName) {
            this._pipeName = pipeName;
            process.env.DATAVERSE_MCP_PIPE_NAME = pipeName;
            this.log(`[RPC Client] Pipe name initialized in startServer: ${pipeName}`);
        } else {
            this.log(`[RPC Client] Using existing pipe name: ${pipeName}`);
        }

        const env: NodeJS.ProcessEnv = { ...process.env };
        if (pluginDirectory) {
            env['DATAVERSE_MCP_PLUGIN_DIR'] = pluginDirectory;
            this.log(`[RPC Client] Plugin directory: ${pluginDirectory}`);
        }
        // Ensure pipe name is passed to server
        env['DATAVERSE_MCP_PIPE_NAME'] = pipeName;
        
        // CRITICAL: Set TMPDIR before spawning server process (all platforms)
        // .NET's Path.GetTempPath() reads TMPDIR at runtime startup
        // NamedPipeServerStream uses Path.GetTempPath() to create Unix socket files
        if (socketDir) {
            env['TMPDIR'] = socketDir;
            this.log(`[RPC Client] Set TMPDIR for server process: ${socketDir}`);
            
            // Log expected socket path with length for debugging
            const expectedPath = getPipePath(socketDir);
            this.log(`[RPC Client] Expected socket path: ${expectedPath} (length: ${expectedPath.length} chars)`);
        }

        this.log('[RPC Client] Spawning server process...');
        this.serverProcess = cp.spawn(serverPath, [], {
            stdio: ['ignore', 'ignore', 'pipe'], // stderr only for logs
            env,
            detached: false
        });

        // Capture server logs and redirect to output channel
        this.serverProcess.stderr?.on('data', (data: Buffer) => {
            const message = data.toString();
            this.log(`[Server] ${message.trim()}`);
        });

        this.serverProcess.on('exit', (code, signal) => {
            this.log(`[Server] Process exited with code ${code}, signal ${signal}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        this.serverProcess.on('error', (error) => {
            this.log(`[Server] Process error: ${error}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        // Wait for spawn event or error (instead of blind delay)
        await new Promise<void>((resolve, reject) => {
            const timeout = setTimeout(() => {
                const errorMsg = 'Server process spawn timeout (2s)';
                this.log(`[RPC Client] ERROR: ${errorMsg}`);
                vscode.window.showErrorMessage(
                    'Dataverse MCP Server failed to start within timeout. Please check the Output panel for details.',
                    'Show Output'
                ).then(selection => {
                    if (selection === 'Show Output') {
                        if (this.outputChannel) {
                            this.outputChannel.show();
                        }
                    }
                });
                reject(new Error(errorMsg));
            }, 2000);

            this.serverProcess!.once('spawn', () => {
                clearTimeout(timeout);
                this.log(`[RPC Client] Server process spawned successfully (PID: ${this.serverProcess!.pid})`);
                resolve();
            });

            this.serverProcess!.once('error', (error) => {
                clearTimeout(timeout);
                const errorMsg = `Failed to spawn server process: ${error.message}`;
                this.log(`[RPC Client] ERROR: ${errorMsg}`);
                vscode.window.showErrorMessage(
                    `Dataverse MCP Server failed to start: ${error.message}`,
                    'Show Output'
                ).then(selection => {
                    if (selection === 'Show Output') {
                        if (this.outputChannel) {
                            this.outputChannel.show();
                        }
                    }
                });
                reject(new Error(errorMsg));
            });
        });

        // Give server a moment to create the Named Pipe
        this.log('[RPC Client] Waiting for server to initialize Named Pipe...');
        
        // On Unix, verify pipe socket file was created (wait up to 3 seconds)
        if (process.platform !== 'win32') {
            const pipePath = getPipePath(this._socketDir);
            let pipeExists = false;

            this.log(`[RPC Client] Looking for pipe socket at: ${pipePath}`);

            // Wait up to 3 seconds with 150ms intervals (20 attempts)
            for (let i = 0; i < 20; i++) {
                if (fs.existsSync(pipePath)) {
                    pipeExists = true;
                    this.log(`[RPC Client] ✓ Pipe socket found after ${(i + 1) * 150}ms`);
                    break;
                }
                if (i % 5 === 0) {
                    this.log(`[RPC Client] Still waiting for pipe socket... (${i + 1}/20)`);
                }
                await new Promise(resolve => setTimeout(resolve, 150));
            }

            if (!pipeExists) {
                this.log(`[RPC Client] WARNING: Pipe socket not found at ${pipePath} after 3s. Will try to connect anyway...`);
            }
        } else {
            // Windows: just wait a moment for server to initialize
            await new Promise(resolve => setTimeout(resolve, 1000));
        }
    }

    /**
     * Tente de se connecter au serveur Named Pipe existant
     */
    private async tryConnect(): Promise<boolean> {
        return new Promise<boolean>((resolve) => {
            // Use socketDir (should always be set now)
            const pipePath = getPipePath(this._socketDir);
            
            // Check if socket file exists (Unix only)
            if (process.platform !== 'win32') {
                const socketExists = fs.existsSync(pipePath);
                this.log(`[RPC Client] Socket exists before connect: ${socketExists} at ${pipePath}`);
            }
            
            this.log(`[RPC Client] Trying to connect to Named Pipe: ${pipePath}`);
            
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

        this.log('[RPC Client] Setting up JSON-RPC over Named Pipe');

        // Handle pipe errors and closure
        this.pipeStream.on('error', (error) => {
            this.log(`[RPC Client] Named Pipe error: ${error}`);
            vscode.window.showErrorMessage(`Connection to Dataverse RPC server lost: ${error.message}`);
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        this.pipeStream.on('close', () => {
            this.log('[RPC Client] Named Pipe closed');
            this.isConnected = false;
            this._onConnectionStatusChanged.fire(false);
        });

        // Create JSON-RPC connection using newline-delimited protocol
        const reader = new NewlineDelimitedMessageReader(this.pipeStream);
        const writer = new NewlineDelimitedMessageWriter(this.pipeStream);
        this.connection = createMessageConnection(reader, writer);

        // Enable trace for debugging
        this.connection.trace(2, {
            log: (message: string) => this.log(`[JSON-RPC TRACE] ${message}`)
        });

        // Handle connection errors
        this.connection.onError((error: any) => {
            this.log(`[RPC Client] JSON-RPC error: ${error}`);
        });

        this.connection.onClose(() => {
            this.log('[RPC Client] JSON-RPC connection closed');
            this.isConnected = false;
        });

        // Start listening
        this.connection.listen();
    }

    /**
     * Ferme la connexion Named Pipe (peut tuer le serveur si nécessaire)
     */
    async disconnect(): Promise<void> {
        this.log('[RPC Client] Disconnecting from Named Pipe server...');

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
            this.log('[RPC Client] Terminating server process...');
            try {
                // Try graceful shutdown first
                this.serverProcess.kill('SIGTERM');
                
                // Force kill if still running after 2 seconds
                setTimeout(() => {
                    if (this.serverProcess && !this.serverProcess.killed) {
                        this.log('[RPC Client] Force killing server process...');
                        this.serverProcess.kill('SIGKILL');
                    }
                }, 2000);
            } catch (error) {
                this.log(`[RPC Client] Error killing server process: ${error}`);
            }
            this.serverProcess = null;
        }

        this.isConnected = false;
        this._onConnectionStatusChanged.fire(false);
        this.log('[RPC Client] Disconnected');
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
        this.log(`[RPC Client] Setting active connection for MCP: ${connectionId}`);
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
