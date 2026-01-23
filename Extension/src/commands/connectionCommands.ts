import * as vscode from 'vscode';
import { ConnectionStorageService } from '../services/ConnectionStorageService';
import { ConnectionsTreeDataProvider, ConnectionTreeItem } from '../providers/ConnectionsTreeDataProvider';
import { DataverseMCPToolBoxRpcClient } from '../services/DataverseMCPToolBoxRpcClient';
import { TokenStorageService } from '../services/TokenStorageService';
import { WhoAmIPanel } from '../panels/WhoAmIPanel';

/**
 * Register all commands for connection management
 */
export function registerCommands(
    context: vscode.ExtensionContext,
    storageService: ConnectionStorageService,
    tokenStorageService: TokenStorageService,
    treeDataProvider: ConnectionsTreeDataProvider,
    rpcClient: DataverseMCPToolBoxRpcClient
): void {
    
    // Command: Add new connection
    const addConnectionCmd = vscode.commands.registerCommand('dataversemcptoolbox.addConnection', async () => {
        // Prompt for connection name
        const name = await vscode.window.showInputBox({
            prompt: 'Enter a name for the connection',
            placeHolder: 'e.g., Dev Environment',
            validateInput: (value) => {
                if (!value || value.trim().length === 0) {
                    return 'Connection name is required';
                }
                return null;
            }
        });

        if (!name) {
            return; // User cancelled
        }

        // Prompt for Dataverse URL
        const url = await vscode.window.showInputBox({
            prompt: 'Enter the Dataverse environment URL',
            placeHolder: 'e.g., https://org.crm.dynamics.com',
            validateInput: (value) => {
                if (!value || value.trim().length === 0) {
                    return 'URL is required';
                }
                // Basic URL validation
                if (!value.startsWith('http://') && !value.startsWith('https://')) {
                    return 'URL must start with http:// or https://';
                }
                return null;
            }
        });

        if (!url) {
            return; // User cancelled
        }

        try {
            // Show progress indicator
            const result = await vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: `Connecting to ${name}...`,
                cancellable: false
            }, async (progress) => {
                progress.report({ message: 'Authenticating...' });

                // Create real connection via RPC
                const result = await rpcClient.createConnection({
                    environmentUrl: url.trim(),
                    connectionName: name.trim()
                });

                if (!result.success) {
                    throw new Error(result.errorMessage || 'Unknown error');
                }

                progress.report({ message: 'Saving connection...' });

                // Save connection locally with RPC connection ID
                const connection = await storageService.addConnection(name.trim(), url.trim());
                
                // Store the RPC connection ID in the connection metadata
                connection.metadata = { rpcConnectionId: result.connectionId };
                await storageService.updateConnection(connection);

                // Store tokens securely
                if (result.accessToken) {
                    await tokenStorageService.storeTokens(connection.id, {
                        accessToken: result.accessToken,
                        refreshToken: result.refreshToken,
                        expiresOn: result.expiresOn
                    });
                }

                // Set as active connection
                await storageService.setActiveConnection(connection.id);

                return result;
            });

            treeDataProvider.refresh();
            vscode.window.showInformationMessage(`✅ Connected to "${name}" successfully!`);
        } catch (error: any) {
            vscode.window.showErrorMessage(`Failed to connect: ${error.message || error}`);
        }
    });

    // Command: Remove connection
    const removeConnectionCmd = vscode.commands.registerCommand('dataversemcptoolbox.removeConnection', async (item: ConnectionTreeItem) => {
        if (!item || !item.connection) {
            vscode.window.showWarningMessage('No connection selected');
            return;
        }

        const confirm = await vscode.window.showWarningMessage(
            `Are you sure you want to remove "${item.connection.name}"?`,
            { modal: true },
            'Remove'
        );

        if (confirm === 'Remove') {
            try {
                // Delete stored tokens
                await tokenStorageService.deleteTokens(item.connection.id);
                
                // Close RPC connection if exists
                const rpcConnectionId = item.connection.metadata?.rpcConnectionId;
                if (rpcConnectionId) {
                    await rpcClient.closeConnection(rpcConnectionId);
                }
                
                await storageService.removeConnection(item.connection.id);
                treeDataProvider.refresh();
                vscode.window.showInformationMessage(`Connection "${item.connection.name}" removed`);
            } catch (error) {
                vscode.window.showErrorMessage(`Failed to remove connection: ${error}`);
            }
        }
    });

    // Command: Set active connection
    const setActiveConnectionCmd = vscode.commands.registerCommand('dataversemcptoolbox.setActiveConnection', async (item: ConnectionTreeItem) => {
        if (!item || !item.connection) {
            vscode.window.showWarningMessage('No connection selected');
            return;
        }

        try {
            // If clicking on already active connection, deactivate it
            if (item.connection.isActive) {
                // Close RPC connection
                const rpcConnectionId = item.connection.metadata?.rpcConnectionId;
                if (rpcConnectionId) {
                    await rpcClient.closeConnection(rpcConnectionId);
                }
                
                // Deactivate all by setting a non-existent ID
                const connections = storageService.getConnections();
                connections.forEach(c => c.isActive = false);
                await storageService.setActiveConnection(''); // This will deactivate all
                vscode.window.showInformationMessage('Connection deactivated');
            } else {
                // Try to connect with stored tokens or prompt for new auth
                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: `Activating ${item.connection.name}...`,
                    cancellable: false
                }, async (progress) => {
                    progress.report({ message: 'Checking credentials...' });

                    // Check if we have stored tokens
                    const storedTokens = await tokenStorageService.getTokens(item.connection.id);
                    
                    let result;
                    if (storedTokens && !tokenStorageService.isTokenExpired(storedTokens)) {
                        // Try to connect with existing tokens
                        progress.report({ message: 'Connecting with stored credentials...' });
                        
                        try {
                            result = await rpcClient.createConnection({
                                environmentUrl: item.connection.url,
                                connectionName: item.connection.name,
                                accessToken: storedTokens.accessToken,
                                refreshToken: storedTokens.refreshToken
                            });

                            if (!result.success) {
                                throw new Error('Token no longer valid');
                            }
                        } catch {
                            // Tokens are invalid, need to re-authenticate
                            progress.report({ message: 'Re-authenticating...' });
                            result = await rpcClient.createConnection({
                                environmentUrl: item.connection.url,
                                connectionName: item.connection.name
                            });
                        }
                    } else {
                        // No valid tokens, authenticate
                        progress.report({ message: 'Authenticating...' });
                        result = await rpcClient.createConnection({
                            environmentUrl: item.connection.url,
                            connectionName: item.connection.name
                        });
                    }

                    if (!result.success) {
                        throw new Error(result.errorMessage || 'Failed to connect');
                    }

                    // Update connection with new RPC connection ID
                    item.connection.metadata = { rpcConnectionId: result.connectionId };
                    await storageService.updateConnection(item.connection);

                    // Store new tokens
                    if (result.accessToken) {
                        await tokenStorageService.storeTokens(item.connection.id, {
                            accessToken: result.accessToken,
                            refreshToken: result.refreshToken,
                            expiresOn: result.expiresOn
                        });
                    }

                    // Set as active
                    await storageService.setActiveConnection(item.connection.id);
                });

                vscode.window.showInformationMessage(`✅ "${item.connection.name}" is now active`);
            }
            
            treeDataProvider.refresh();
        } catch (error: any) {
            vscode.window.showErrorMessage(`Failed to set active connection: ${error.message || error}`);
        }
    });

    // Command: Refresh connections list
    const refreshConnectionsCmd = vscode.commands.registerCommand('dataversemcptoolbox.refreshConnections', () => {
        treeDataProvider.refresh();
        vscode.window.showInformationMessage('Connections refreshed');
    });

    // Command: Show WhoAmI information
    const showWhoAmICmd = vscode.commands.registerCommand('dataversemcptoolbox.showWhoAmI', async (item: ConnectionTreeItem) => {
        if (!item || !item.connection) {
            vscode.window.showWarningMessage('No connection selected');
            return;
        }

        if (!item.connection.isActive) {
            vscode.window.showWarningMessage('Connection is not active');
            return;
        }

        try {
            await vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: 'Retrieving connection information...',
                cancellable: false
            }, async () => {
                const rpcConnectionId = item.connection.metadata?.rpcConnectionId;
                if (!rpcConnectionId) {
                    throw new Error('No active RPC connection found');
                }

                // Call WhoAmI via RPC
                const whoAmIResult = await rpcClient.getWhoAmI(rpcConnectionId);

                if (!whoAmIResult.success) {
                    throw new Error(whoAmIResult.errorMessage || 'Failed to retrieve connection info');
                }

                // Show the information in a webview panel
                WhoAmIPanel.show(whoAmIResult);
            });
        } catch (error: any) {
            vscode.window.showErrorMessage(`Failed to retrieve connection info: ${error.message || error}`);
        }
    });

    // Register all commands
    context.subscriptions.push(addConnectionCmd);
    context.subscriptions.push(removeConnectionCmd);
    context.subscriptions.push(setActiveConnectionCmd);
    context.subscriptions.push(refreshConnectionsCmd);
    context.subscriptions.push(showWhoAmICmd);
}
