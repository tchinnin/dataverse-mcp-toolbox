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
export class PluginsTreeProvider implements vscode.TreeDataProvider<PluginTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<PluginTreeItem | undefined | null | void> = new vscode.EventEmitter<PluginTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<PluginTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

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
        try {
            this.plugins = await this.rpcClient.listPlugins();
            this.refresh();
        } catch (error) {
            console.error('Failed to load plugins:', error);
            vscode.window.showErrorMessage(`Failed to load plugins: ${error}`);
            this.plugins = [];
            this.refresh();
        }
    }

    /**
     * Get tree item
     */
    getTreeItem(element: PluginTreeItem): vscode.TreeItem {
        return element;
    }

    /**
     * Get children for a tree item
     */
    async getChildren(element?: PluginTreeItem): Promise<PluginTreeItem[]> {
        if (!element) {
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
        } else if (element.contextValue === 'plugin' && element.plugin) {
            // Child level: return tools for this plugin
            return element.plugin.tools.map(tool =>
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
