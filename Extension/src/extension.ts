import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { ConnectionStorageService } from './services/ConnectionStorageService';
import { ConnectionsTreeDataProvider } from './providers/ConnectionsTreeDataProvider';
import { PluginsTreeProvider } from './providers/PluginsTreeProvider';
import { registerCommands } from './commands/connectionCommands';
import { registerPluginCommands } from './commands/pluginCommands';
import { DataverseMCPToolBoxRpcClient } from './services/DataverseMCPToolBoxRpcClient';
import { TokenStorageService } from './services/TokenStorageService';

// Global RPC client instance
let rpcClient: DataverseMCPToolBoxRpcClient;

/**
 * Extension activation entry point
 */
export async function activate(context: vscode.ExtensionContext) {
    console.log('Dataverse Connection Manager is now active');

    // Initialize storage services first
    const storageService = new ConnectionStorageService(context);
    const tokenStorageService = new TokenStorageService(context);

    // Initialize tree data providers
    const treeDataProvider = new ConnectionsTreeDataProvider(storageService);

    // Register connections tree view
    const treeView = vscode.window.createTreeView('dataverseConnectionsList', {
        treeDataProvider: treeDataProvider,
        showCollapseAll: false
    });

    // Add tree view to subscriptions
    context.subscriptions.push(treeView);

    // Initialize RPC client in background
    rpcClient = new DataverseMCPToolBoxRpcClient();
    
    // Initialize plugins tree provider (will be populated after RPC connection)
    const pluginsTreeProvider = new PluginsTreeProvider(rpcClient);
    const pluginsTreeView = vscode.window.createTreeView('dataversemcptoolbox.pluginsView', {
        treeDataProvider: pluginsTreeProvider,
        showCollapseAll: true
    });
    context.subscriptions.push(pluginsTreeView);

    // Connect asynchronously without blocking extension activation
    rpcClient.connect(context.extensionPath)
        .then(async () => {
            console.log('Connected to Dataverse RPC server');
            
            // Set up plugin directory in extension folder
            const pluginDirectory = path.join(context.extensionPath, 'plugins');
            console.log(`Setting plugin directory to: ${pluginDirectory}`);
            
            // Ensure the plugins directory exists
            if (!fs.existsSync(pluginDirectory)) {
                fs.mkdirSync(pluginDirectory, { recursive: true });
                console.log(`Created plugins directory at: ${pluginDirectory}`);
            }
            
            await rpcClient.setPluginDirectory(pluginDirectory);
            console.log(`Plugin directory configured successfully`);

            // Check if bundled plugins have been installed
            const bundledPluginsInstalled = context.globalState.get<boolean>('bundledPluginsInstalled', false);
            
            if (!bundledPluginsInstalled) {
                // Install bundled plugins on first activation
                console.log('First activation: installing bundled plugins...');
                await installBundledPlugins(rpcClient, context);
                await context.globalState.update('bundledPluginsInstalled', true);
            }

            // Load and display plugins
            await pluginsTreeProvider.loadPlugins();
            console.log('Plugins loaded successfully');
        })
        .catch((error) => {
            console.error('Failed to connect to RPC server:', error);
            vscode.window.showErrorMessage('Failed to start Dataverse backend service. Some features may not work.');
        });

    // Register all commands (they will check connection before use)
    registerCommands(context, storageService, tokenStorageService, treeDataProvider, rpcClient);
    registerPluginCommands(context, rpcClient, pluginsTreeProvider);

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

    // Log active connection status
    const activeConnection = storageService.getActiveConnection();
    if (activeConnection) {
        console.log(`Active connection: ${activeConnection.name} (${activeConnection.url})`);
    } else {
        console.log('No active connection');
    }
}

/**
 * Install bundled plugins on first activation
 */
async function installBundledPlugins(rpcClient: DataverseMCPToolBoxRpcClient, context: vscode.ExtensionContext): Promise<void> {
    // Define bundled plugins (to be installed from NuGet)
    const bundledPlugins = [
        { packageId: 'DataverseMCPToolBox.WhoAmI', version: undefined } // undefined = latest
    ];

    for (const plugin of bundledPlugins) {
        try {
            console.log(`Installing bundled plugin: ${plugin.packageId}`);
            const result = await rpcClient.installPlugin(plugin);
            
            if (result.success) {
                console.log(`Successfully installed: ${plugin.packageId}`);
            } else {
                console.error(`Failed to install ${plugin.packageId}: ${result.errorMessage}`);
            }
        } catch (error) {
            console.error(`Error installing bundled plugin ${plugin.packageId}:`, error);
        }
    }

    // Reload plugins after installation
    await rpcClient.reloadPlugins();
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
