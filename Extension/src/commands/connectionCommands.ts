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
        // Check if server is connected
        if (!rpcClient.isServerConnected()) {
            const selection = await vscode.window.showErrorMessage(
                'Cannot add connection: Dataverse MCP Server is not running. The server must be started by GitHub Copilot via MCP configuration.',
                'Reload VS Code',
                'Open MCP Config'
            );
            
            if (selection === 'Reload VS Code') {
                await vscode.commands.executeCommand('workbench.action.reloadWindow');
            } else if (selection === 'Open MCP Config') {
                await vscode.commands.executeCommand('dataversemcptoolbox.openMcpConfiguration');
            }
            return;
        }

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

                // Save connection locally using server-generated ID
                const connection = await storageService.addConnection(
                    result.connectionId!, 
                    name.trim(), 
                    url.trim()
                );

                // Store tokens securely
                if (result.accessToken) {
                    await tokenStorageService.storeTokens(connection.id, {
                        accessToken: result.accessToken,
                        refreshToken: result.refreshToken,
                        expiresOn: result.expiresOn
                    });
                    console.log(`Stored tokens for new connection ${name} (expires: ${result.expiresOn})`);
                }

                // Set as active connection
                await storageService.setActiveConnection(connection.id);
                console.log(`New connection ${name} created and set as active`);
                
                // Notify RPC server of active connection for MCP
                try {
                    await rpcClient.setActiveConnection(connection.id);
                    console.error(`[Commands] Active connection set in MCP server: ${connection.id}`);
                } catch (error) {
                    console.error('[Commands] Failed to set active connection in MCP server:', error);
                }

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
                await storageService.deactivateAllConnections();
                vscode.window.showInformationMessage('Connection deactivated');
            } else {
                // Activate the selected connection with token validation
                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: `Activating ${item.connection.name}...`,
                    cancellable: false
                }, async (progress) => {
                    // Check if we have stored tokens
                    const storedTokens = await tokenStorageService.getTokens(item.connection.id);
                    
                    let result;
                    
                    if (storedTokens) {
                        // Check if token is expired
                        const isExpired = tokenStorageService.isTokenExpired(storedTokens);
                        
                        if (!isExpired) {
                            // Token still valid, try to connect
                            progress.report({ message: 'Connecting with stored credentials...' });
                            console.log(`Token for ${item.connection.name} is still valid, attempting connection...`);
                            
                            try {
                                result = await rpcClient.createConnection({
                                    connectionId: item.connection.id,
                                    environmentUrl: item.connection.url,
                                    connectionName: item.connection.name,
                                    accessToken: storedTokens.accessToken,
                                    refreshToken: storedTokens.refreshToken
                                });

                                if (!result.success) {
                                    throw new Error('Token validation failed');
                                }
                                
                                console.log(`Successfully connected with stored token for ${item.connection.name}`);
                            } catch (error) {
                                // Token rejected by server, need to re-authenticate
                                console.log(`Stored token rejected for ${item.connection.name}, re-authenticating...`);
                                progress.report({ message: 'Token expired, re-authenticating...' });
                                result = await rpcClient.createConnection({
                                    connectionId: item.connection.id,
                                    environmentUrl: item.connection.url,
                                    connectionName: item.connection.name
                                });
                            }
                        } else {
                            // Token is expired, try refresh via server (server will handle refresh token)
                            progress.report({ message: 'Token expired, refreshing...' });
                            console.log(`Token for ${item.connection.name} is expired, attempting refresh...`);
                            
                            try {
                                result = await rpcClient.createConnection({
                                    connectionId: item.connection.id,
                                    environmentUrl: item.connection.url,
                                    connectionName: item.connection.name,
                                    accessToken: storedTokens.accessToken,
                                    refreshToken: storedTokens.refreshToken
                                });

                                if (!result.success) {
                                    throw new Error('Token refresh failed');
                                }
                                
                                console.log(`Successfully refreshed token for ${item.connection.name}`);
                            } catch (error) {
                                // Refresh failed, need interactive authentication
                                console.log(`Token refresh failed for ${item.connection.name}, requiring interactive authentication...`);
                                progress.report({ message: 'Re-authenticating interactively...' });
                                result = await rpcClient.createConnection({
                                    connectionId: item.connection.id,
                                    environmentUrl: item.connection.url,
                                    connectionName: item.connection.name
                                });
                            }
                        }
                    } else {
                        // No tokens stored, need interactive authentication
                        progress.report({ message: 'No stored credentials, authenticating...' });
                        console.log(`No stored tokens for ${item.connection.name}, requiring authentication...`);
                        result = await rpcClient.createConnection({
                            connectionId: item.connection.id,  // Pass existing connection ID
                            environmentUrl: item.connection.url,
                            connectionName: item.connection.name
                        });
                    }

                    if (!result.success) {
                        throw new Error(result.errorMessage || 'Failed to connect');
                    }

                    // Store new tokens securely
                    if (result.accessToken) {
                        await tokenStorageService.storeTokens(item.connection.id, {
                            accessToken: result.accessToken,
                            refreshToken: result.refreshToken,
                            expiresOn: result.expiresOn
                        });
                        console.log(`Stored new tokens for ${item.connection.name} (expires: ${result.expiresOn})`);
                    }

                    // Set as active
                    await storageService.setActiveConnection(item.connection.id);
                    console.log(`${item.connection.name} is now the active connection`);
                    
                    // Notify RPC server of active connection for MCP
                    try {
                        await rpcClient.setActiveConnection(item.connection.id);
                        console.error(`[Commands] Active connection set in MCP server: ${item.connection.id}`);
                    } catch (error) {
                        console.error('[Commands] Failed to set active connection in MCP server:', error);
                    }
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
