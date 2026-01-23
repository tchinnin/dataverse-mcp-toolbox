import * as vscode from 'vscode';
import { ConnectionStorageService } from './services/ConnectionStorageService';
import { ConnectionsTreeDataProvider } from './providers/ConnectionsTreeDataProvider';
import { registerCommands } from './commands/connectionCommands';
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

    // Initialize tree data provider
    const treeDataProvider = new ConnectionsTreeDataProvider(storageService);

    // Register tree view
    const treeView = vscode.window.createTreeView('dataverseConnectionsList', {
        treeDataProvider: treeDataProvider,
        showCollapseAll: false
    });

    // Add tree view to subscriptions
    context.subscriptions.push(treeView);

    // Initialize RPC client in background
    rpcClient = new DataverseMCPToolBoxRpcClient();
    
    // Connect asynchronously without blocking extension activation
    rpcClient.connect(context.extensionPath)
        .then(() => {
            console.log('Connected to Dataverse RPC server');
        })
        .catch((error) => {
            console.error('Failed to connect to RPC server:', error);
            vscode.window.showErrorMessage('Failed to start Dataverse backend service. Some features may not work.');
        });

    // Register all commands (they will check connection before use)
    registerCommands(context, storageService, tokenStorageService, treeDataProvider, rpcClient);

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
 * Extension deactivation cleanup
 */
export async function deactivate() {
    console.log('Dataverse Connection Manager is now deactivated');
    
    // Disconnect RPC client
    if (rpcClient) {
        await rpcClient.disconnect();
    }
}
