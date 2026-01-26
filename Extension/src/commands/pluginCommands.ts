import * as vscode from 'vscode';
import { DataverseMCPToolBoxRpcClient } from '../services/DataverseMCPToolBoxRpcClient';
import { PluginsTreeProvider, PluginTreeItem } from '../providers/PluginsTreeProvider';

/**
 * Register plugin-related commands
 */
export function registerPluginCommands(
    context: vscode.ExtensionContext,
    rpcClient: DataverseMCPToolBoxRpcClient,
    pluginsTreeProvider: PluginsTreeProvider
): void {
    // Refresh plugins
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.refreshPlugins', async () => {
            try {
                await rpcClient.reloadPlugins();
                await pluginsTreeProvider.loadPlugins();
                vscode.window.showInformationMessage('Plugins refreshed successfully');
            } catch (error) {
                vscode.window.showErrorMessage(`Failed to refresh plugins: ${error}`);
            }
        })
    );

    // Install plugin from NuGet
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.installPlugin', async () => {
            try {
                // Prompt for package ID
                const packageId = await vscode.window.showInputBox({
                    prompt: 'Enter NuGet package ID',
                    placeHolder: 'e.g., DataverseMCPToolBox.WhoAmI',
                    validateInput: (value) => {
                        if (!value || value.trim().length === 0) {
                            return 'Package ID is required';
                        }
                        return null;
                    }
                });

                if (!packageId) {
                    return;
                }

                // Optionally prompt for version
                const version = await vscode.window.showInputBox({
                    prompt: 'Enter version (leave empty for latest)',
                    placeHolder: 'e.g., 1.0.0'
                });

                // Show progress
                await vscode.window.withProgress(
                    {
                        location: vscode.ProgressLocation.Notification,
                        title: `Installing plugin ${packageId}...`,
                        cancellable: false
                    },
                    async (progress) => {
                        progress.report({ increment: 0 });

                        console.log(`Installing plugin from NuGet: ${packageId} ${version || 'latest'}`);
                        
                        const result = await rpcClient.installPlugin({
                            packageId,
                            version: version || undefined
                        });

                        if (result.success) {
                            progress.report({ increment: 100 });
                            console.log(`Plugin installed successfully: ${result.pluginInfo?.name}`);
                            await pluginsTreeProvider.loadPlugins();
                            vscode.window.showInformationMessage(
                                `Plugin ${result.pluginInfo?.name} installed successfully in extension folder`
                            );
                        } else {
                            throw new Error(result.errorMessage || 'Unknown error');
                        }
                    }
                );
            } catch (error) {
                vscode.window.showErrorMessage(`Failed to install plugin: ${error}`);
            }
        })
    );

    // Uninstall plugin
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.uninstallPlugin', async (item: PluginTreeItem) => {
            if (!item || !item.plugin) {
                return;
            }

            const confirm = await vscode.window.showWarningMessage(
                `Uninstall plugin ${item.plugin.name}?`,
                'Uninstall',
                'Cancel'
            );

            if (confirm !== 'Uninstall') {
                return;
            }

            try {
                // Use package ID if available, otherwise use plugin name
                const packageId = item.plugin.packageId || item.plugin.name;
                const success = await rpcClient.uninstallPlugin(packageId);

                if (success) {
                    await pluginsTreeProvider.loadPlugins();
                    vscode.window.showInformationMessage(`Plugin ${item.plugin.name} uninstalled`);
                } else {
                    vscode.window.showErrorMessage(`Failed to uninstall plugin ${item.plugin.name}`);
                }
            } catch (error) {
                vscode.window.showErrorMessage(`Error uninstalling plugin: ${error}`);
            }
        })
    );

    // Call/execute tool
    context.subscriptions.push(
        vscode.commands.registerCommand('dataversemcptoolbox.callTool', async (item: PluginTreeItem) => {
            if (!item || !item.tool) {
                return;
            }

            try {
                // Get active connection ID (assuming there's a way to get it from storage)
                const connectionId = await getActiveConnectionId(context);
                
                if (!connectionId) {
                    vscode.window.showWarningMessage('No active connection. Please set an active connection first.');
                    return;
                }

                // Prompt for parameters if tool requires them
                let parametersJson: string | undefined;
                if (item.tool.inputSchema) {
                    const paramsInput = await vscode.window.showInputBox({
                        prompt: `Enter parameters for ${item.tool.name} as JSON`,
                        placeHolder: '{"param1": "value1"}',
                        value: '{}'
                    });

                    if (paramsInput === undefined) {
                        return; // User cancelled
                    }

                    parametersJson = paramsInput;
                }

                // Execute tool
                await vscode.window.withProgress(
                    {
                        location: vscode.ProgressLocation.Notification,
                        title: `Executing tool ${item.tool.name}...`,
                        cancellable: false
                    },
                    async (progress) => {
                        progress.report({ increment: 0 });

                        const result = await rpcClient.callTool({
                            toolName: item.tool!.name,
                            connectionId,
                            parametersJson
                        });

                        progress.report({ increment: 100 });

                        if (result.isSuccess) {
                            // Display result in output channel or information message
                            const output = vscode.window.createOutputChannel(`Tool: ${item.tool!.name}`);
                            output.clear();
                            output.appendLine('Tool executed successfully!');
                            output.appendLine('');
                            output.appendLine('Result:');
                            output.appendLine(JSON.stringify(result.content, null, 2));
                            output.show();

                            vscode.window.showInformationMessage(`Tool ${item.tool!.name} executed successfully`);
                        } else {
                            vscode.window.showErrorMessage(
                                `Tool execution failed: ${result.error?.message}`,
                                'Show Details'
                            ).then(selection => {
                                if (selection === 'Show Details') {
                                    const output = vscode.window.createOutputChannel(`Tool: ${item.tool!.name}`);
                                    output.clear();
                                    output.appendLine('Tool execution error:');
                                    output.appendLine('');
                                    output.appendLine(`Code: ${result.error?.code}`);
                                    output.appendLine(`Message: ${result.error?.message}`);
                                    if (result.error?.details) {
                                        output.appendLine('');
                                        output.appendLine('Details:');
                                        output.appendLine(JSON.stringify(result.error.details, null, 2));
                                    }
                                    output.show();
                                }
                            });
                        }
                    }
                );
            } catch (error) {
                vscode.window.showErrorMessage(`Error executing tool: ${error}`);
            }
        })
    );
}

/**
 * Helper to get active connection ID from storage
 */
async function getActiveConnectionId(context: vscode.ExtensionContext): Promise<string | undefined> {
    // This is a placeholder - you should import and use ConnectionStorageService
    // For now, we'll just return undefined and let the caller handle it
    const { ConnectionStorageService } = await import('../services/ConnectionStorageService');
    const storageService = new ConnectionStorageService(context);
    const activeConnection = storageService.getActiveConnection();
    return activeConnection?.id;
}
