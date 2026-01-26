import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
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

// Global RPC client instance
let rpcClient: DataverseMCPToolBoxRpcClient;
let serverManager: ServerManager;

/**
 * Extension activation entry point
 */
export async function activate(context: vscode.ExtensionContext) {
    console.log('Dataverse Connection Manager is now active');

    // Initialize storage services first
    const storageService = new ConnectionStorageService(context);
    const tokenStorageService = new TokenStorageService(context);

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

    // Initialize RPC client
    rpcClient = new DataverseMCPToolBoxRpcClient();
    
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

    // Ensure server is installed and connect to it
    ensureServerAndConnect(context, pluginsTreeProvider, serverInfoProvider)
        .catch((error) => {
            console.error('Failed to initialize Dataverse MCP Server:', error);
            vscode.window.showErrorMessage(
                'Failed to initialize Dataverse MCP Server. Some features may not work.',
                'Retry'
            ).then((selection) => {
                if (selection === 'Retry') {
                    ensureServerAndConnect(context, pluginsTreeProvider, serverInfoProvider);
                }
            });
        });

    // Register all commands (they will check connection before use)
    registerCommands(context, storageService, tokenStorageService, treeDataProvider, rpcClient);
    registerPluginCommands(context, rpcClient, pluginsTreeProvider);
    registerServerCommands(context, serverManager, rpcClient, serverInfoProvider);

    // Register update server command
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.updateServer', async () => {
            try {
                const versionInfo = await serverManager.checkForUpdates();

                if (versionInfo.updateAvailable) {
                    await checkAndNotifyServerUpdate(context, serverInfoProvider);
                } else {
                    vscode.window.showInformationMessage(
                        `Dataverse MCP Server is up to date (v${versionInfo.installedVersion || 'unknown'})`
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
 * Ensure server is installed and connect to it
 */
async function ensureServerAndConnect(context: vscode.ExtensionContext, pluginsTreeProvider: PluginsTreeProvider, serverInfoProvider: ServerInfoTreeProvider): Promise<void> {
    // Download/verify server installation with progress
    const serverPath = await vscode.window.withProgress({
        location: vscode.ProgressLocation.Notification,
        title: 'Setting up Dataverse MCP Server...',
        cancellable: false
    }, async (progress) => {
        progress.report({ message: 'Checking installation...' });
        return await serverManager.ensureServerInstalled();
    });

    console.error(`[Extension] Server ready at: ${serverPath}`);

    // Connect to RPC server
    await rpcClient.connect(serverPath);
    console.error('[Extension] Connected to Dataverse RPC server');

    // Close any lingering RPC connections from previous sessions
    try {
        await rpcClient.closeAllConnections();
        console.error('[Extension] Closed all lingering RPC connections from previous session');
    } catch (error) {
        console.error('Error closing lingering connections:', error);
    }

    // Set up plugin directory in globalStoragePath (survives extension updates)
    const pluginDirectory = path.join(context.globalStoragePath, 'plugins');
    console.error(`[Extension] Setting plugin directory to: ${pluginDirectory}`);

    // Ensure the plugins directory exists
    if (!fs.existsSync(pluginDirectory)) {
        fs.mkdirSync(pluginDirectory, { recursive: true });
        console.error(`[Extension] Created plugins directory at: ${pluginDirectory}`);
    }

    await rpcClient.setPluginDirectory(pluginDirectory);
    console.error(`[Extension] Plugin directory configured successfully`);

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
            const currentVersion = versionInfo.installedVersion || 'unknown';
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
                    progress.report({ message: 'Downloading...' });

                    // Disconnect current server
                    await rpcClient.disconnect();

                    // Upgrade server
                    const newServerPath = await serverManager.upgradeServer();

                    // Reconnect with new version
                    progress.report({ message: 'Restarting server...' });
                    await rpcClient.connect(newServerPath);

                    // Reconfigure plugin directory
                    const pluginDirectory = path.join(context.globalStoragePath, 'plugins');
                    await rpcClient.setPluginDirectory(pluginDirectory);

                    // Refresh server info view
                    await serverInfoProvider.updateVersionInfo();

                    vscode.window.showInformationMessage(`Successfully updated to Dataverse MCP Server v${latestVersion}`);
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
 * Extension deactivation cleanup
 */
export async function deactivate() {
    console.log('Dataverse Connection Manager is now deactivated');
    
    // Disconnect RPC client
    if (rpcClient) {
        await rpcClient.disconnect();
    }
}
