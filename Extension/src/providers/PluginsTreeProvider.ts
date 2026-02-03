import * as vscode from 'vscode';
import { PluginInfo } from '../models/PluginInfo';
import { ToolInfo } from '../models/ToolInfo';
import { DataverseMCPToolBoxRpcClient } from '../services/DataverseMCPToolBoxRpcClient';

/**
 * Tree item for plugin or tool
 */
export class PluginTreeItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        public readonly contextValue: 'plugin' | 'tool',
        public readonly plugin?: PluginInfo,
        public readonly tool?: ToolInfo
    ) {
        super(label, collapsibleState);

        if (contextValue === 'plugin') {
            this.description = `v${plugin?.version}`;
            this.tooltip = `${plugin?.name} v${plugin?.version}\nby ${plugin?.author}\n\n${plugin?.description}`;
            this.iconPath = new vscode.ThemeIcon('package');
        } else if (contextValue === 'tool') {
            this.description = tool?.description;
            this.tooltip = `${tool?.name}\n\n${tool?.description}`;
            this.iconPath = new vscode.ThemeIcon('tools');
        }
    }
}

/**
 * Tree data provider for plugins and their tools
 */
export class PluginsTreeProvider implements vscode.TreeDataProvider<PluginTreeItem | vscode.TreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<PluginTreeItem | vscode.TreeItem | undefined | null | void> = new vscode.EventEmitter<PluginTreeItem | vscode.TreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<PluginTreeItem | vscode.TreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private plugins: PluginInfo[] = [];

    constructor(private rpcClient: DataverseMCPToolBoxRpcClient) {}

    /**
     * Refresh the tree view
     */
    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Load plugins from RPC server
     */
    async loadPlugins(): Promise<void> {
        if (!this.rpcClient.isServerConnected()) {
            console.error('[PluginsTree] Cannot load plugins - server not connected');
            this.plugins = [];
            this.refresh();
            return;
        }

        try {
            this.plugins = await this.rpcClient.listPlugins();
            this.refresh();
        } catch (error) {
            console.error('Failed to load plugins:', error);
            this.plugins = [];
            this.refresh();
        }
    }

    /**
     * Get tree item
     */
    getTreeItem(element: PluginTreeItem | vscode.TreeItem): vscode.TreeItem {
        return element;
    }

    /**
     * Get children for a tree item
     */
    async getChildren(element?: PluginTreeItem | vscode.TreeItem): Promise<Array<PluginTreeItem | vscode.TreeItem>> {
        if (!element) {
            // Show warning if server not connected
            if (!this.rpcClient.isServerConnected()) {
                const warningItem = new vscode.TreeItem('⚠️ Server Not Running', vscode.TreeItemCollapsibleState.None);
                warningItem.tooltip = 'The Dataverse MCP Server must be started by GitHub Copilot.\nClick the status bar to troubleshoot or reload VS Code.';
                warningItem.description = 'Click status bar for help';
                warningItem.iconPath = new vscode.ThemeIcon('warning', new vscode.ThemeColor('editorWarning.foreground'));
                return [warningItem];
            }

            // Root level: return plugins
            if (this.plugins.length === 0) {
                return [];
            }

            return this.plugins.map(plugin => 
                new PluginTreeItem(
                    plugin.name,
                    vscode.TreeItemCollapsibleState.Collapsed,
                    'plugin',
                    plugin
                )
            );
        } else if (element instanceof PluginTreeItem && element.contextValue === 'plugin' && element.plugin) {
            // Child level: return tools for a plugin
            return element.plugin.tools.map((tool: ToolInfo) =>
                new PluginTreeItem(
                    tool.name,
                    vscode.TreeItemCollapsibleState.None,
                    'tool',
                    element.plugin,
                    tool
                )
            );
        }

        return [];
    }

    /**
     * Get parent of a tree item
     */
    getParent(element: PluginTreeItem): PluginTreeItem | undefined {
        // Tools have plugins as parents, plugins have no parent
        if (element.contextValue === 'tool' && element.plugin) {
            return new PluginTreeItem(
                element.plugin.name,
                vscode.TreeItemCollapsibleState.Collapsed,
                'plugin',
                element.plugin
            );
        }
        return undefined;
    }
}
