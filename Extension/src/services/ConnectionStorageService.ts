import * as vscode from 'vscode';
import { DataverseConnection } from '../models/DataverseConnection';

/**
 * Service for managing Dataverse connection storage
 */
export class ConnectionStorageService {
    private static readonly STORAGE_KEY = 'dataverse.connections';
    
    constructor(private context: vscode.ExtensionContext) {}

    /**
     * Get all stored connections
     */
    public getConnections(): DataverseConnection[] {
        return this.context.globalState.get<DataverseConnection[]>(ConnectionStorageService.STORAGE_KEY, []);
    }

    /**
     * Save a new connection
     */
    public async addConnection(name: string, url: string): Promise<DataverseConnection> {
        const connections = this.getConnections();
        
        // Generate a unique ID
        const id = `conn_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        
        const newConnection: DataverseConnection = {
            id,
            name,
            url,
            isActive: false
        };
        
        connections.push(newConnection);
        await this.context.globalState.update(ConnectionStorageService.STORAGE_KEY, connections);
        
        return newConnection;
    }

    /**
     * Remove a connection by ID
     */
    public async removeConnection(id: string): Promise<void> {
        const connections = this.getConnections();
        const filtered = connections.filter(c => c.id !== id);
        await this.context.globalState.update(ConnectionStorageService.STORAGE_KEY, filtered);
    }

    /**
     * Set a connection as active (deactivates all others)
     */
    public async setActiveConnection(id: string): Promise<void> {
        const connections = this.getConnections();
        
        // Deactivate all connections first
        connections.forEach(c => {
            c.isActive = c.id === id;
        });
        
        await this.context.globalState.update(ConnectionStorageService.STORAGE_KEY, connections);
    }

    /**
     * Get the currently active connection
     */
    public getActiveConnection(): DataverseConnection | undefined {
        const connections = this.getConnections();
        return connections.find(c => c.isActive);
    }

    /**
     * Clear all connections (for testing/debugging)
     */
    public async clearAllConnections(): Promise<void> {
        await this.context.globalState.update(ConnectionStorageService.STORAGE_KEY, []);
    }

    /**
     * Update an existing connection
     */
    public async updateConnection(connection: DataverseConnection): Promise<void> {
        const connections = this.getConnections();
        const index = connections.findIndex(c => c.id === connection.id);
        
        if (index !== -1) {
            connections[index] = connection;
            await this.context.globalState.update(ConnectionStorageService.STORAGE_KEY, connections);
        }
    }
}
