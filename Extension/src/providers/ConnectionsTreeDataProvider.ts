import * as vscode from 'vscode';
import { DataverseConnection } from '../models/DataverseConnection';
import { ConnectionStorageService } from '../services/ConnectionStorageService';
import type { DataverseMCPToolBoxRpcClient } from '../services/DataverseMCPToolBoxRpcClient';

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
export class ConnectionsTreeDataProvider implements vscode.TreeDataProvider<ConnectionTreeItem | vscode.TreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ConnectionTreeItem | vscode.TreeItem | undefined | null | void> = new vscode.EventEmitter<ConnectionTreeItem | vscode.TreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ConnectionTreeItem | vscode.TreeItem | undefined | null | void> = this._onDidChangeTreeData.event;
    private rpcClient: DataverseMCPToolBoxRpcClient | null = null;

    constructor(private storageService: ConnectionStorageService) {}

    /**
     * Set the RPC client for connection status checking
     */
    setRpcClient(client: DataverseMCPToolBoxRpcClient): void {
        this.rpcClient = client;
    }

    /**
     * Refresh the tree view
     */
    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Get tree item representation
     */
    getTreeItem(element: ConnectionTreeItem | vscode.TreeItem): vscode.TreeItem {
        return element;
    }

    /**
     * Get children of an element (or root if element is undefined)
     */
    async getChildren(element?: ConnectionTreeItem | vscode.TreeItem): Promise<Array<ConnectionTreeItem | vscode.TreeItem>> {
        if (element) {
            // No child elements for connections
            return [];
        }

        // If server is not connected, return empty array to hide the entire tree view
        if (this.rpcClient && !this.rpcClient.isServerConnected()) {
            return [];
        }

        // Return all connections as root elements
        const connections = this.storageService.getConnections();
        
        if (connections.length === 0) {
            // Return empty array, VSCode will show the welcome view
            return [];
        }

        const connectionItems = connections.map(conn => 
            new ConnectionTreeItem(conn, vscode.TreeItemCollapsibleState.None)
        );

        return connectionItems;
    }
}
