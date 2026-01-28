import * as vscode from 'vscode';
import * as path from 'path';
import { ServerManager } from '../services/ServerManager';
import { DataverseMCPToolBoxRpcClient } from '../services/DataverseMCPToolBoxRpcClient';
import { ServerInfoTreeProvider } from '../providers/ServerInfoTreeProvider';
import { MCPConfigurationService } from '../services/MCPConfigurationService';

/**
 * Register server management commands
 */
export function registerServerCommands(
    context: vscode.ExtensionContext,
    serverManager: ServerManager,
    rpcClient: DataverseMCPToolBoxRpcClient,
    serverInfoProvider: ServerInfoTreeProvider,
    mcpConfigService: MCPConfigurationService
): void {

    // Command: Upgrade server to latest version
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.upgradeServer', async () => {
            try {
                const versionInfo = await serverManager.checkForUpdates();

                if (!versionInfo.updateAvailable) {
                    vscode.window.showInformationMessage(
                        `MCP Server is already up to date (v${versionInfo.installedVersion || 'unknown'})`
                    );
                    return;
                }

                const currentVersion = versionInfo.installedVersion || 'unknown';
                const latestVersion = versionInfo.latestVersion;

                const selection = await vscode.window.showInformationMessage(
                    `Upgrade MCP Server from v${currentVersion} to v${latestVersion}?`,
                    'Upgrade',
                    'Cancel'
                );

                if (selection !== 'Upgrade') {
                    return;
                }

                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: `Upgrading MCP Server to v${latestVersion}...`,
                    cancellable: false
                }, async (progress) => {
                    progress.report({ message: 'Downloading...' });

                    // Disconnect current server
                    await rpcClient.disconnect();

                    // Upgrade server
                    const newServerPath = await serverManager.upgradeServer();

                    // Get plugin directory
                    const pluginDirectory = path.join(context.globalStoragePath, 'plugins');

                    // Re-register MCP server with new path
                    progress.report({ message: 'Updating MCP configuration...' });
                    await mcpConfigService.updateServerPath(newServerPath, pluginDirectory);

                    // Reconnect with new version
                    progress.report({ message: 'Restarting server...' });
                    await rpcClient.connect(newServerPath, pluginDirectory);

                    vscode.window.showInformationMessage(
                        `Successfully upgraded MCP Server to v${latestVersion}`
                    );

                    // Refresh server info view
                    await serverInfoProvider.updateVersionInfo();
                });
            } catch (error) {
                console.error('Failed to upgrade server:', error);
                vscode.window.showErrorMessage(`Failed to upgrade server: ${error}`);
            }
        })
    );

    // Command: Manually check for server updates
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.checkServerUpdates', async () => {
            try {
                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Checking for MCP Server updates...',
                    cancellable: false
                }, async () => {
                    await serverInfoProvider.updateVersionInfo();
                });

                const versionInfo = await serverManager.checkForUpdates();
                
                if (versionInfo.updateAvailable) {
                    const selection = await vscode.window.showInformationMessage(
                        `Update available: v${versionInfo.latestVersion} (current: v${versionInfo.installedVersion})`,
                        'Upgrade Now',
                        'Later'
                    );

                    if (selection === 'Upgrade Now') {
                        vscode.commands.executeCommand('dataversemcptoolbox.upgradeServer');
                    }
                } else {
                    vscode.window.showInformationMessage(
                        `MCP Server is up to date (v${versionInfo.installedVersion || 'unknown'})`
                    );
                }
            } catch (error) {
                console.error('Failed to check for updates:', error);
                vscode.window.showErrorMessage(`Failed to check for updates: ${error}`);
            }
        })
    );

    // Command: Change server channel (shows quick pick with stable/prerelease options)
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.toggleServerChannel', async () => {
            try {
                const config = vscode.workspace.getConfiguration('dataverse.server');
                const currentChannel = config.get<string>('channel') || 'prerelease';
                
                // Show quick pick to select channel
                const channelChoice = await vscode.window.showQuickPick(
                    [
                        {
                            label: 'Stable',
                            description: 'Only stable releases',
                            value: 'stable',
                            picked: currentChannel === 'stable'
                        },
                        {
                            label: 'Prerelease',
                            description: 'Include pre-release versions',
                            value: 'prerelease',
                            picked: currentChannel === 'prerelease'
                        }
                    ],
                    {
                        title: 'Select Update Channel',
                        placeHolder: 'Choose which versions to receive'
                    }
                );
                
                if (!channelChoice) {
                    return;
                }
                
                // Only update if changed
                if (channelChoice.value !== currentChannel) {
                    await config.update('channel', channelChoice.value, vscode.ConfigurationTarget.Global);
                    
                    vscode.window.showInformationMessage(
                        `Server update channel changed to: ${channelChoice.label}`
                    );
                    
                    // Refresh server info view to show updated channel
                    serverInfoProvider.refresh();
                    
                    // Optionally check for updates with new channel
                    const checkNow = await vscode.window.showInformationMessage(
                        'Channel updated. Check for updates now?',
                        'Check Now',
                        'Later'
                    );
                    
                    if (checkNow === 'Check Now') {
                        vscode.commands.executeCommand('dataversemcptoolbox.checkServerUpdates');
                    }
                }
            } catch (error) {
                console.error('Failed to change server channel:', error);
                vscode.window.showErrorMessage(`Failed to change channel: ${error}`);
            }
        })
    );

    // Command: Toggle enforced version between Auto and specific version
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.toggleEnforcedVersion', async () => {
            try {
                const config = vscode.workspace.getConfiguration('dataverse.server');
                const currentEnforcedVersion = config.get<string>('enforcedVersion') || '';
                
                // If currently Auto (empty), prompt for specific version
                // If currently has a specific version, offer to clear or change
                const options: vscode.QuickPickItem[] = [
                    {
                        label: 'Auto (latest)',
                        description: 'Always use the latest version from selected channel',
                        picked: !currentEnforcedVersion
                    },
                    {
                        label: 'Specific Version',
                        description: currentEnforcedVersion 
                            ? `Currently: ${currentEnforcedVersion}` 
                            : 'Pin to a specific version',
                        picked: !!currentEnforcedVersion
                    }
                ];
                
                const choice = await vscode.window.showQuickPick(options, {
                    title: 'Enforced Version',
                    placeHolder: 'Select version behavior'
                });
                
                if (!choice) {
                    return;
                }
                
                let newEnforcedVersion = '';
                
                if (choice.label === 'Specific Version') {
                    // Prompt for version input
                    const versionInput = await vscode.window.showInputBox({
                        title: 'Enter Specific Version',
                        prompt: 'Enter the version to enforce (e.g., 0.1.0-alpha)',
                        value: currentEnforcedVersion,
                        placeHolder: '0.1.0-alpha',
                        validateInput: (value) => {
                            if (!value) {
                                return 'Version cannot be empty';
                            }
                            // Basic validation for semantic versioning
                            if (!/^\d+\.\d+\.\d+(-[\w.]+)?$/.test(value)) {
                                return 'Invalid version format. Use semantic versioning (e.g., 1.0.0 or 1.0.0-alpha)';
                            }
                            return null;
                        }
                    });
                    
                    if (versionInput === undefined) {
                        return;
                    }
                    
                    newEnforcedVersion = versionInput;
                }
                
                // Save updated enforced version
                await config.update('enforcedVersion', newEnforcedVersion, vscode.ConfigurationTarget.Global);
                
                const displayVersion = newEnforcedVersion || 'Auto (latest)';
                vscode.window.showInformationMessage(
                    `Server enforced version set to: ${displayVersion}`
                );
                
                // Refresh server info view
                serverInfoProvider.refresh();
                
                // Optionally check for updates
                const checkNow = await vscode.window.showInformationMessage(
                    'Version enforcement updated. Check for updates now?',
                    'Check Now',
                    'Later'
                );
                
                if (checkNow === 'Check Now') {
                    vscode.commands.executeCommand('dataversemcptoolbox.checkServerUpdates');
                }
            } catch (error) {
                console.error('Failed to toggle enforced version:', error);
                vscode.window.showErrorMessage(`Failed to set enforced version: ${error}`);
            }
        })
    );

    // Command: Configure server settings (channel and enforced version) - DEPRECATED but kept for compatibility
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.configureServerSettings', async () => {
            try {
                const config = vscode.workspace.getConfiguration('dataverse.server');
                
                // Step 1: Choose channel
                const currentChannel = config.get<string>('channel') || 'prerelease';
                const channelChoice = await vscode.window.showQuickPick(
                    [
                        {
                            label: 'Stable',
                            description: 'Only stable releases',
                            value: 'stable',
                            picked: currentChannel === 'stable'
                        },
                        {
                            label: 'Prerelease',
                            description: 'Include pre-release versions',
                            value: 'prerelease',
                            picked: currentChannel === 'prerelease'
                        }
                    ],
                    {
                        title: 'Select Update Channel',
                        placeHolder: 'Choose which versions to receive'
                    }
                );

                if (!channelChoice) {
                    return;
                }

                // Step 2: Choose version enforcement
                const currentEnforcedVersion = config.get<string>('enforcedVersion') || '';
                const versionChoice = await vscode.window.showQuickPick(
                    [
                        {
                            label: 'Auto (Latest)',
                            description: 'Always use the latest version from selected channel',
                            value: '',
                            picked: !currentEnforcedVersion
                        },
                        {
                            label: 'Specific Version',
                            description: 'Pin to a specific version',
                            value: 'custom',
                            picked: !!currentEnforcedVersion
                        }
                    ],
                    {
                        title: 'Version Selection',
                        placeHolder: 'Choose version update behavior'
                    }
                );

                if (!versionChoice) {
                    return;
                }

                let enforcedVersion = '';
                if (versionChoice.value === 'custom') {
                    const versionInput = await vscode.window.showInputBox({
                        title: 'Enter Specific Version',
                        prompt: 'Enter the version to enforce (e.g., 0.1.0-alpha)',
                        value: currentEnforcedVersion,
                        placeHolder: '0.1.0-alpha',
                        validateInput: (value) => {
                            if (!value) {
                                return 'Version cannot be empty when using specific version';
                            }
                            // Basic validation for semantic versioning
                            if (!/^\d+\.\d+\.\d+(-[\w.]+)?$/.test(value)) {
                                return 'Invalid version format. Use semantic versioning (e.g., 1.0.0 or 1.0.0-alpha)';
                            }
                            return null;
                        }
                    });

                    if (versionInput === undefined) {
                        return;
                    }

                    enforcedVersion = versionInput;
                }

                // Save settings
                await config.update('channel', channelChoice.value, vscode.ConfigurationTarget.Global);
                await config.update('enforcedVersion', enforcedVersion, vscode.ConfigurationTarget.Global);

                vscode.window.showInformationMessage(
                    `Server settings updated: Channel=${channelChoice.label}, Version=${enforcedVersion || 'Auto (latest)'}`
                );

                // Refresh server info view to show updated settings
                serverInfoProvider.refresh();

                // Prompt to check for updates with new settings
                const checkNow = await vscode.window.showInformationMessage(
                    'Settings updated. Check for updates now with new settings?',
                    'Check Now',
                    'Later'
                );

                if (checkNow === 'Check Now') {
                    vscode.commands.executeCommand('dataversemcptoolbox.checkServerUpdates');
                }

            } catch (error) {
                console.error('Failed to configure server settings:', error);
                vscode.window.showErrorMessage(`Failed to configure settings: ${error}`);
            }
        })
    );

    // Command: Open MCP configuration file
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.openMcpConfiguration', () => {
            try {
                mcpConfigService.openMcpConfiguration();
            } catch (error) {
                console.error('Failed to open MCP configuration:', error);
                vscode.window.showErrorMessage(`Failed to open MCP configuration: ${error}`);
            }
        })
    );

    // Command: Re-register MCP server (useful after manual config edits)
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.reregisterMcpServer', async () => {
            try {
                // Ensure server is installed and get its path
                const serverPath = await serverManager.ensureServerInstalled();
                const pluginDirectory = path.join(context.globalStoragePath, 'plugins');
                await mcpConfigService.registerMcpServer(serverPath, pluginDirectory);
            } catch (error) {
                console.error('Failed to re-register MCP server:', error);
                vscode.window.showErrorMessage(`Failed to re-register MCP server: ${error}`);
            }
        })
    );

    // Command: Unregister MCP server from VS Code configuration
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.unregisterMcpServer', async () => {
            try {
                const confirmation = await vscode.window.showWarningMessage(
                    'Are you sure you want to unregister the Dataverse MCP server from VS Code? You can re-register it later.',
                    'Unregister',
                    'Cancel'
                );

                if (confirmation !== 'Unregister') {
                    return;
                }

                await mcpConfigService.unregisterMcpServer();
            } catch (error) {
                console.error('Failed to unregister MCP server:', error);
                vscode.window.showErrorMessage(`Failed to unregister MCP server: ${error}`);
            }
        })
    );
}
