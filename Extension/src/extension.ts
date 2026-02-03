import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
import { ConnectionStorageService } from './services/ConnectionStorageService';
import { ConnectionsTreeDataProvider } from './providers/ConnectionsTreeDataProvider';
import { PluginsTreeProvider } from './providers/PluginsTreeProvider';
import { ServerInfoTreeProvider } from './providers/ServerInfoTreeProvider';
import { registerCommands } from './commands/connectionCommands';
import { registerPluginCommands } from './commands/pluginCommands';
import { registerServerCommands } from './commands/serverCommands';
import { DataverseMCPToolBoxRpcClient } from './services/DataverseMCPToolBoxRpcClient';
import { TokenStorageService } from './services/TokenStorageService';
import { ServerManager } from './services/ServerManager';
import { BundledPluginsConfig } from './models/BundledPluginConfig';
import { McpServerDefinitionProvider } from './providers/McpServerDefinitionProvider';

// Global RPC client instance
let rpcClient: DataverseMCPToolBoxRpcClient;
let serverManager: ServerManager;
let mcpProvider: McpServerDefinitionProvider;
let serverStatusBar: vscode.StatusBarItem;

// Socket directory for Named Pipes (Unix domain sockets)
// Use os.tmpdir() for cross-platform temp directory (respects $TMPDIR on macOS)
// Short path to respect 104-char Unix socket limit
const SOCKET_DIR = path.join(os.tmpdir(), 'dvmcptb-sockets');

/**
 * Extension activation entry point
 */
export async function activate(context: vscode.ExtensionContext) {
    console.log('Dataverse Connection Manager is now active');

    // Initialize storage services first
    const storageService = new ConnectionStorageService(context);
    const tokenStorageService = new TokenStorageService(context);

    console.error('[Extension] Initializing Dataverse MCP Toolbox extension...');

    // CRITICAL: Deactivate all connections on startup to ensure fresh validation
    // This prevents stale connections from appearing as active with expired tokens
    await storageService.deactivateAllConnections();
    console.log('All connections deactivated on startup - user must explicitly select a connection');

    // Initialize tree data providers
    const treeDataProvider = new ConnectionsTreeDataProvider(storageService);

    // Register connections tree view
    const treeView = vscode.window.createTreeView('dataverseConnectionsList', {
        treeDataProvider: treeDataProvider,
        showCollapseAll: false
    });

    // Add tree view to subscriptions
    context.subscriptions.push(treeView);

    // Initialize ServerManager to manage server binary lifecycle
    serverManager = new ServerManager(context);
    context.subscriptions.push(serverManager);

    // Create socket directory for Named Pipes (Unix domain sockets)
    if (!fs.existsSync(SOCKET_DIR)) {
        fs.mkdirSync(SOCKET_DIR, { recursive: true, mode: 0o755 });
        console.error(`[Extension] Created socket directory: ${SOCKET_DIR}`);
    } else {
        console.error(`[Extension] Socket directory exists: ${SOCKET_DIR}`);
    }

    // Cleanup old socket files (orphaned or > 24h old)
    cleanupOldSockets(SOCKET_DIR);

    // Create output channel for server logs
    const serverOutputChannel = vscode.window.createOutputChannel('Dataverse MCP Server');
    context.subscriptions.push(serverOutputChannel);

    // Initialize RPC client with output channel for logging
    rpcClient = new DataverseMCPToolBoxRpcClient(serverOutputChannel);

    // Initialize MCP provider
    mcpProvider = new McpServerDefinitionProvider(context);
    const providerDisposable = vscode.lm.registerMcpServerDefinitionProvider('dataverseMcpToolbox', mcpProvider);
    context.subscriptions.push(providerDisposable);
    context.subscriptions.push(mcpProvider);
    console.error('[Extension] MCP Server Definition Provider registered');
    
    // Create status bar item for server connection status
    serverStatusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    serverStatusBar.command = 'dataversemcptoolbox.openServerActions';
    serverStatusBar.tooltip = 'Click to manage Dataverse MCP Server';
    updateServerStatusBar(false); // Initially disconnected
    serverStatusBar.show();
    context.subscriptions.push(serverStatusBar);
    
    // Subscribe to connection status changes
    context.subscriptions.push(
        rpcClient.onConnectionStatusChanged((connected) => {
            updateServerStatusBar(connected);
            updateConnectionContext(connected);
            // Refresh tree views when connection status changes
            treeDataProvider.refresh();
            pluginsTreeProvider.refresh();
        })
    );
    
    // Initialize plugins tree provider (will be populated after RPC connection)
    const pluginsTreeProvider = new PluginsTreeProvider(rpcClient);
    const pluginsTreeView = vscode.window.createTreeView('dataversemcptoolbox.pluginsView', {
        treeDataProvider: pluginsTreeProvider,
        showCollapseAll: true
    });
    context.subscriptions.push(pluginsTreeView);

    // Initialize server info tree provider
    const serverInfoProvider = new ServerInfoTreeProvider(serverManager);
    const serverInfoTreeView = vscode.window.createTreeView('dataversemcptoolbox.serverInfoView', {
        treeDataProvider: serverInfoProvider,
        showCollapseAll: false
    });
    context.subscriptions.push(serverInfoTreeView);

    // Set RPC client in server info provider for connection status
    serverInfoProvider.setRpcClient(rpcClient);

    // Ensure server is installed and connect to it
    ensureServerAndConnect(context, treeDataProvider, pluginsTreeProvider, serverInfoProvider)
        .catch((error) => {
            console.error('Failed to initialize Dataverse MCP Server:', error);
            vscode.window.showErrorMessage(
                'Failed to initialize Dataverse MCP Server. Some features may not work.',
                'Retry'
            ).then((selection) => {
                if (selection === 'Retry') {
                    ensureServerAndConnect(context, treeDataProvider, pluginsTreeProvider, serverInfoProvider);
                }
            });
        });

    // Register all commands (they will check connection before use)
    registerCommands(context, storageService, tokenStorageService, treeDataProvider, rpcClient);
    registerPluginCommands(context, rpcClient, pluginsTreeProvider);
    registerServerCommands(context, serverManager, rpcClient, serverInfoProvider, mcpProvider, SOCKET_DIR);

    // Register command to open server actions
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.openServerActions', async () => {
            const items: vscode.QuickPickItem[] = [];
            
            if (!rpcClient.isServerConnected()) {
                items.push({
                    label: '$(refresh) Reload VS Code',
                    description: 'Restart VS Code to let GitHub Copilot start the server',
                    detail: 'The server should be automatically started by GitHub Copilot via MCP provider'
                });
                items.push({
                    label: '$(output) Show Output',
                    description: 'View extension logs'
                });
            } else {
                items.push({
                    label: '$(check) Server Connected',
                    description: 'Dataverse MCP Server is running'
                });
                items.push({
                    label: '$(info) Show Server Info',
                    description: 'View server version and status'
                });
            }
            
            const selection = await vscode.window.showQuickPick(items, {
                title: 'Dataverse MCP Server Actions',
                placeHolder: 'Select an action'
            });
            
            if (!selection) {
                return;
            }
            
            if (selection.label.includes('Reload VS Code')) {
                await vscode.commands.executeCommand('workbench.action.reloadWindow');
            } else if (selection.label.includes('Show Output')) {
                vscode.commands.executeCommand('workbench.action.output.toggleOutput');
            } else if (selection.label.includes('Show Server Info')) {
                vscode.commands.executeCommand('dataversemcptoolbox.serverInfoView.focus');
            }
        })
    );

    // Register command to manually start server
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.startServerManually', async () => {
            if (rpcClient.isServerConnected()) {
                vscode.window.showInformationMessage('Server is already running and connected.');
                return;
            }

            try {
                await ensureServerAndConnect(context, treeDataProvider, pluginsTreeProvider, serverInfoProvider);
                vscode.window.showInformationMessage('Server started successfully!');
            } catch (error) {
                console.error('Failed to start server manually:', error);
                vscode.window.showErrorMessage(`Failed to start server: ${error}`);
            }
        })
    );

    // Register command to show connection diagnostics
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.showConnectionDiagnostics', async () => {
            const isConnected = rpcClient.isServerConnected();
            const pipeName = rpcClient.getPipeName();
            
            let message = `**Server Connection Diagnostics**\n\n`;
            message += `Connection Status: ${isConnected ? '✅ Connected' : '❌ Disconnected'}\n`;
            message += `Named Pipe: ${pipeName}\n`;
            
            if (isConnected) {
                try {
                    const plugins = await rpcClient.listPlugins();
                    message += `\nPlugins Loaded: ${plugins.length}\n`;
                    if (plugins.length > 0) {
                        plugins.forEach(p => {
                            message += `  - ${p.name} v${p.version} (${p.tools.length} tools)\n`;
                        });
                    }
                } catch (error) {
                    message += `\n⚠️ Error fetching plugin info: ${error}\n`;
                }
            } else {
                message += `\n💡 The server should be started by GitHub Copilot via MCP provider.\n`;
                message += `Try reloading VS Code to ensure MCP configuration is loaded.`;
            }
            
            vscode.window.showInformationMessage(message, { modal: true });
        })
    );

    // Register update server command
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.updateServer', async () => {
            try {
                const versionInfo = await serverManager.checkForUpdates();

                if (versionInfo.updateAvailable) {
                    await checkAndNotifyServerUpdate(context, serverInfoProvider);
                } else {
                    vscode.window.showInformationMessage(
                        `Dataverse MCP Server is up to date (v${versionInfo.currentVersion || 'unknown'})`
                    );
                }
            } catch (error) {
                vscode.window.showErrorMessage(`Failed to check for updates: ${error}`);
            }
        })
    );

    // Show welcome message on first activation (optional)
    const hasShownWelcome = context.globalState.get<boolean>('hasShownWelcome', false);
    if (!hasShownWelcome) {
        vscode.window.showInformationMessage(
            'Welcome to Dataverse Connection Manager! Click the + icon to add your first connection.',
            'Got it'
        ).then(() => {
            context.globalState.update('hasShownWelcome', true);
        });
    }

    // Note: No connection should be active on startup
    // User must explicitly select a connection to activate it with token validation
    console.log('Extension activated - no active connection (user must select one)');
}

/**
 * Get the Bridge executable path from the server path
 * The Bridge executable is in the same directory as the Core server
 */
function getBridgePath(serverPath: string): string {
    const serverDir = path.dirname(serverPath);
    const platform = process.platform;
    
    let bridgeName: string;
    if (platform === 'win32') {
        bridgeName = 'DataverseMCPToolBox.Bridge.exe';
    } else {
        bridgeName = 'DataverseMCPToolBox.Bridge';
    }
    
    return path.join(serverDir, bridgeName);
}

/**
 * Ensure server is installed and connect to it
 */
async function ensureServerAndConnect(
    context: vscode.ExtensionContext, 
    treeDataProvider: ConnectionsTreeDataProvider,
    pluginsTreeProvider: PluginsTreeProvider, 
    serverInfoProvider: ServerInfoTreeProvider
): Promise<void> {
    // Check if server binary exists
    const serverPath = await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Setting up Dataverse MCP Server...',
        cancellable: false
    }, async (progress) => {
        progress.report({ message: 'Checking installation...' });
        try {
            return await serverManager.ensureServerInstalled();
        } catch (error) {
            // First install - binary not found
            progress.report({ message: 'Downloading latest version from NuGet...' });
            return await serverManager.ensureServerInstalled();
        }
    });

    console.error(`[Extension] Server binary ready at: ${serverPath}`);

    // Set up plugin directory in globalStoragePath (survives extension updates)
    const pluginDirectory = path.join(context.globalStoragePath, 'plugins');
    console.error(`[Extension] Plugin directory: ${pluginDirectory}`);

    // Ensure the plugins directory exists
    if (!fs.existsSync(pluginDirectory)) {
        fs.mkdirSync(pluginDirectory, { recursive: true });
    }

    // Update MCP provider configuration
    // The provider handles registration with VS Code's MCP infrastructure
    const pipeName = rpcClient.getPipeName();
    const bridgePath = getBridgePath(serverPath);
    mcpProvider.updateConfiguration(bridgePath, pluginDirectory, pipeName, SOCKET_DIR);
    console.error('[Extension] MCP provider configuration updated');

    // Connect to RPC server (should be started by Copilot via MCP provider)
    try {
        const serverPath = await serverManager.ensureServerInstalled();
        const pluginDirectory = path.join(context.globalStorageUri.fsPath, 'plugins');
        await rpcClient.connect(serverPath, pluginDirectory, SOCKET_DIR);
        console.error('[Extension] Connected to Dataverse RPC server');
        updateServerStatusBar(true);
        updateConnectionContext(true);
    } catch (error) {
        console.error('[Extension] Failed to connect to server:', error);
        updateServerStatusBar(false);
        updateConnectionContext(false);
        vscode.window.showErrorMessage(
            'Could not connect to Dataverse MCP Server. The server should be started by GitHub Copilot. ' +
            'Try reloading VS Code to ensure MCP configuration is loaded.',
            'Reload Now'
        ).then(selection => {
            if (selection === 'Reload Now') {
                vscode.commands.executeCommand('workbench.action.reloadWindow');
            }
        });
        throw error;
    }

    // Set RPC client reference in ServerManager for version queries
    serverManager.setRpcClient(rpcClient);

    // Set RPC client in tree providers for server status checking
    treeDataProvider.setRpcClient(rpcClient);

    // Close any lingering RPC connections from previous sessions
    try {
        await rpcClient.closeAllConnections();
        console.error('[Extension] Closed all lingering RPC connections from previous session');
    } catch (error) {
        console.error('Error closing lingering connections:', error);
    }

    // Check if bundled plugins have been installed
    const bundledPluginsInstalled = context.globalState.get<boolean>('bundledPluginsInstalled', false);

    if (!bundledPluginsInstalled) {
        // Install bundled plugins on first activation with progress notification
        console.error('[Extension] First activation: installing bundled plugins...');
        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'Installing bundled MCP plugins...',
            cancellable: false
        }, async (progress) => {
            await installBundledPlugins(rpcClient, context, progress);
        });
        await context.globalState.update('bundledPluginsInstalled', true);
    }

    // Load and display plugins
    await pluginsTreeProvider.loadPlugins();
    const loadedPlugins = await rpcClient.listPlugins();
    console.error(`[Extension] Plugins loaded successfully - Found ${loadedPlugins.length} plugin(s)`);
    if (loadedPlugins.length > 0) {
        loadedPlugins.forEach(p => console.error(`[Extension]   - ${p.name} v${p.version} (${p.tools.length} tools)`));
        
        // Count total tools
        const totalTools = loadedPlugins.reduce((sum, p) => sum + p.tools.length, 0);
        console.error(`[Extension] ✓ Total ${totalTools} MCP tool(s) available via MCP protocol`);
    } else {
        console.error('[Extension] ⚠️ No plugins found! Check plugin directory and installation.');
    }

    // Load server info
    await serverInfoProvider.updateVersionInfo();
    console.error('[Extension] Server info loaded successfully');

    // Check for server updates after 2 seconds (don't block activation)
    setTimeout(() => checkAndNotifyServerUpdate(context, serverInfoProvider), 2000);
}

/**
 * Check for server updates and notify user if available
 */
async function checkAndNotifyServerUpdate(context: vscode.ExtensionContext, serverInfoProvider: ServerInfoTreeProvider): Promise<void> {
    try {
        const versionInfo = await serverManager.checkForUpdates();

        if (versionInfo.updateAvailable) {
            const currentVersion = versionInfo.currentVersion || 'unknown';
            const latestVersion = versionInfo.latestVersion;

            const selection = await vscode.window.showInformationMessage(
                `Dataverse MCP Server update available: v${latestVersion} (current: v${currentVersion})`,
                'Update Now',
                'Remind Me Later'
            );

            if (selection === 'Update Now') {
                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: `Updating Dataverse MCP Server to v${latestVersion}...`,
                    cancellable: false
                }, async (progress) => {
                    progress.report({ message: 'Shutting down server...' });

                    // Upgrade server (includes graceful shutdown)
                    progress.report({ message: 'Downloading new version...' });
                    const newServerPath = await serverManager.upgradeServer();

                    // Update MCP provider configuration with new path
                    const pluginDirectory = path.join(context.globalStoragePath, 'plugins');
                    const pipeName = rpcClient.getPipeName();
                    const bridgePath = getBridgePath(newServerPath);
                    mcpProvider.updateConfiguration(bridgePath, pluginDirectory, pipeName, SOCKET_DIR);

                    // Refresh server info view
                    await serverInfoProvider.updateVersionInfo();

                    // Prompt to reload VS Code
                    const reloadSelection = await vscode.window.showInformationMessage(
                        `Successfully updated to Dataverse MCP Server v${latestVersion}. Please reload VS Code to start the new version.`,
                        'Reload Now',
                        'Later'
                    );

                    if (reloadSelection === 'Reload Now') {
                        await vscode.commands.executeCommand('workbench.action.reloadWindow');
                    }
                });
            }
        }
    } catch (error) {
        console.error('Failed to check for server updates:', error);
    }
}

/**
 * Install bundled plugins on first activation
 */
async function installBundledPlugins(
    rpcClient: DataverseMCPToolBoxRpcClient, 
    context: vscode.ExtensionContext,
    progress: vscode.Progress<{ message?: string; increment?: number }>
): Promise<void> {
    // Load bundled plugins configuration from JSON file
    let bundledPlugins: Array<{ packageId: string; version?: string | null }> = [];
    
    try {
        const configPath = path.join(context.extensionPath, 'bundledPlugins.json');
        console.error(`[Extension] Loading bundled plugins config from: ${configPath}`);
        
        if (fs.existsSync(configPath)) {
            const configContent = fs.readFileSync(configPath, 'utf-8');
            const config: BundledPluginsConfig = JSON.parse(configContent);
            bundledPlugins = config.plugins.map(p => ({
                packageId: p.packageId,
                version: p.version === null ? undefined : p.version
            }));
            console.error(`[Extension] Found ${bundledPlugins.length} bundled plugin(s) to install`);
        } else {
            console.error(`[Extension] ⚠️ bundledPlugins.json not found at: ${configPath}`);
            vscode.window.showWarningMessage('Bundled plugins configuration file not found.');
            return;
        }
    } catch (error) {
        console.error('[Extension] ❌ Failed to load bundled plugins config:', error);
        vscode.window.showErrorMessage(`Failed to load bundled plugins configuration: ${error}`);
        return;
    }

    if (bundledPlugins.length === 0) {
        console.error('[Extension] No bundled plugins configured');
        return;
    }

    const successfulInstalls: string[] = [];
    const failedInstalls: Array<{packageId: string, error: string}> = [];

    for (let i = 0; i < bundledPlugins.length; i++) {
        const plugin = bundledPlugins[i];
        try {
            progress.report({ 
                message: `Installing ${plugin.packageId}...`,
                increment: (i / bundledPlugins.length) * 100
            });
            console.error(`[Extension] Installing bundled plugin: ${plugin.packageId}`);
            
            const result = await rpcClient.installPlugin({
                packageId: plugin.packageId,
                version: plugin.version || undefined
            });
            
            if (result.success) {
                const pluginName = result.pluginInfo?.name || plugin.packageId;
                successfulInstalls.push(pluginName);
                console.error(`[Extension] Successfully installed: ${pluginName}`);
            } else {
                const errorMsg = result.errorMessage || 'Unknown error';
                failedInstalls.push({ packageId: plugin.packageId, error: errorMsg });
                console.error(`[Extension] Failed to install ${plugin.packageId}: ${errorMsg}`);
            }
        } catch (error) {
            const errorMsg = error instanceof Error ? error.message : String(error);
            failedInstalls.push({ packageId: plugin.packageId, error: errorMsg });
            console.error(`[Extension] Error installing bundled plugin ${plugin.packageId}:`, error);
        }
    }

    // Reload plugins after installation
    progress.report({ message: 'Loading plugins...', increment: 100 });
    await rpcClient.reloadPlugins();

    // Show completion notification
    if (failedInstalls.length === 0 && successfulInstalls.length > 0) {
        vscode.window.showInformationMessage(
            `Successfully installed ${successfulInstalls.length} plugin(s): ${successfulInstalls.join(', ')}`
        );
    } else if (failedInstalls.length > 0 && successfulInstalls.length > 0) {
        vscode.window.showWarningMessage(
            `Installed ${successfulInstalls.length} plugin(s), but ${failedInstalls.length} failed. Check output for details.`
        );
    } else if (failedInstalls.length > 0) {
        vscode.window.showErrorMessage(
            `Failed to install bundled plugins. Check output for details.`
        );
    }
}

/**
 * Update status bar item based on connection status
 */
function updateServerStatusBar(connected: boolean): void {
    if (connected) {
        serverStatusBar.text = '$(check) Dataverse Server';
        serverStatusBar.backgroundColor = undefined;
        serverStatusBar.tooltip = 'Dataverse MCP Server is connected. Click for options.';
    } else {
        serverStatusBar.text = '$(warning) Dataverse Server';
        serverStatusBar.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
        serverStatusBar.tooltip = 'Dataverse MCP Server is not running. Click to troubleshoot.';
    }
}

/**
 * Cleanup old socket files to prevent accumulation
 * Removes sockets that are > 24h old or belong to dead processes
 */
function cleanupOldSockets(socketDir: string): void {
    try {
        if (!fs.existsSync(socketDir)) {
            return;
        }

        const files = fs.readdirSync(socketDir);
        const now = Date.now();
        const twentyFourHours = 24 * 60 * 60 * 1000;
        let removedCount = 0;

        for (const file of files) {
            // Only process CoreFxPipe_ socket files
            if (!file.startsWith('CoreFxPipe_')) {
                continue;
            }

            const filePath = path.join(socketDir, file);
            try {
                const stats = fs.statSync(filePath);
                const age = now - stats.mtimeMs;

                // Remove if older than 24 hours
                if (age > twentyFourHours) {
                    fs.unlinkSync(filePath);
                    removedCount++;
                    console.error(`[Cleanup] Removed old socket (${Math.round(age / 3600000)}h old): ${file}`);
                    continue;
                }

                // Extract PID from filename (CoreFxPipe_30896 -> 30896)
                const pidMatch = file.match(/CoreFxPipe_(\d+)/);
                if (pidMatch) {
                    const pid = parseInt(pidMatch[1], 10);
                    
                    // Check if process is still running
                    try {
                        process.kill(pid, 0); // Signal 0 checks if process exists without killing it
                    } catch (error: any) {
                        if (error.code === 'ESRCH') {
                            // Process doesn't exist, remove socket
                            fs.unlinkSync(filePath);
                            removedCount++;
                            console.error(`[Cleanup] Removed orphaned socket (PID ${pid} not running): ${file}`);
                        }
                    }
                }
            } catch (error) {
                console.error(`[Cleanup] Error processing ${file}: ${error}`);
            }
        }

        if (removedCount > 0) {
            console.error(`[Cleanup] Removed ${removedCount} old socket file(s)`);
        } else {
            console.error('[Cleanup] No old sockets to remove');
        }
    } catch (error) {
        console.error(`[Cleanup] Error during socket cleanup: ${error}`);
    }
}

/**
 * Update VS Code context for command enablement
 */
function updateConnectionContext(connected: boolean): void {
    vscode.commands.executeCommand('setContext', 'dataversemcptoolbox.serverConnected', connected);
}

/**
 * Extension deactivation cleanup
 */
export async function deactivate() {
    console.log('Dataverse Connection Manager is now deactivated');
    
    // Disconnect RPC client
    if (rpcClient) {
        await rpcClient.disconnect();
    }
}
