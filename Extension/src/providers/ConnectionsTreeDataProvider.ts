import * as vscode from 'vscode';
import { DataverseConnection } from '../models/DataverseConnection';
import { ConnectionStorageService } from '../services/ConnectionStorageService';

/**
 * Tree item representing a Dataverse connection
 */
export class ConnectionTreeItem extends vscode.TreeItem {
    constructor(
        public readonly connection: DataverseConnection,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState
    ) {
        super(connection.name, collapsibleState);
        
        this.tooltip = `${connection.name}\n${connection.url}`;
        this.description = connection.url;
        this.contextValue = connection.isActive ? 'activeConnection' : 'connection';
        
        // Show star icon for active connection
        if (connection.isActive) {
            this.iconPath = new vscode.ThemeIcon('star-full', new vscode.ThemeColor('charts.yellow'));
        } else {
            this.iconPath = new vscode.ThemeIcon('database');
        }
    }
}

/**
 * TreeDataProvider for displaying Dataverse connections
 */
export class ConnectionsTreeDataProvider implements vscode.TreeDataProvider<ConnectionTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ConnectionTreeItem | undefined | null | void> = new vscode.EventEmitter<ConnectionTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ConnectionTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    constructor(private storageService: ConnectionStorageService) {}

    /**
     * Refresh the tree view
     */
    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Get tree item representation
     */
    getTreeItem(element: ConnectionTreeItem): vscode.TreeItem {
        return element;
    }

    /**
     * Get children of an element (or root if element is undefined)
     */
    getChildren(element?: ConnectionTreeItem): Thenable<ConnectionTreeItem[]> {
        if (element) {
            // No child elements for connections
            return Promise.resolve([]);
        }

        // Return all connections as root elements
        const connections = this.storageService.getConnections();
        
        if (connections.length === 0) {
            // Return empty array, VSCode will show the welcome view
            return Promise.resolve([]);
        }

        const items = connections.map(conn => 
            new ConnectionTreeItem(conn, vscode.TreeItemCollapsibleState.None)
        );

        return Promise.resolve(items);
    }
}
