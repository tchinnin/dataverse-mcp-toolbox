import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { DataverseConnection } from '../models/DataverseConnection';

/**
 * Service for managing Dataverse connection storage using file-based persistence
 * Connections are stored in globalStoragePath to survive extension updates
 */
export class ConnectionStorageService {
    private static readonly CONNECTIONS_FILE = 'connections.json';
    private static readonly LEGACY_STORAGE_KEY = 'dataverse.connections';
    private readonly connectionsFilePath: string;
    
    constructor(private context: vscode.ExtensionContext) {
        // Use globalStoragePath for persistent storage across extension updates
        this.connectionsFilePath = path.join(context.globalStoragePath, ConnectionStorageService.CONNECTIONS_FILE);
        
        // Ensure directory exists
        this.ensureStorageDirectory();
        
        // Migrate from old globalState storage if needed
        this.migrateFromGlobalState();
    }

    /**
     * Ensure storage directory exists
     */
    private ensureStorageDirectory(): void {
        const dir = path.dirname(this.connectionsFilePath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
    }

    /**
     * Migrate connections from old globalState storage to file-based storage
     */
    private migrateFromGlobalState(): void {
        // Check if file already exists
        if (fs.existsSync(this.connectionsFilePath)) {
            return; // Already migrated
        }

        // Try to get connections from old storage
        const legacyConnections = this.context.globalState.get<DataverseConnection[]>(
            ConnectionStorageService.LEGACY_STORAGE_KEY,
            []
        );

        if (legacyConnections.length > 0) {
            console.log(`[ConnectionStorage] Migrating ${legacyConnections.length} connections from globalState to file storage`);
            this.saveConnections(legacyConnections);
            
            // Clear old storage after successful migration
            this.context.globalState.update(ConnectionStorageService.LEGACY_STORAGE_KEY, undefined);
        }
    }

    /**
     * Load connections from file
     */
    private loadConnections(): DataverseConnection[] {
        if (!fs.existsSync(this.connectionsFilePath)) {
            return [];
        }

        try {
            const data = fs.readFileSync(this.connectionsFilePath, 'utf-8');
            return JSON.parse(data) as DataverseConnection[];
        } catch (error) {
            console.error('[ConnectionStorage] Failed to load connections:', error);
            return [];
        }
    }

    /**
     * Save connections to file (atomic write with temp file)
     */
    private saveConnections(connections: DataverseConnection[]): void {
        try {
            const tempPath = `${this.connectionsFilePath}.tmp`;
            const data = JSON.stringify(connections, null, 2);
            
            // Write to temp file first
            fs.writeFileSync(tempPath, data, 'utf-8');
            
            // Atomic rename
            fs.renameSync(tempPath, this.connectionsFilePath);
        } catch (error) {
            console.error('[ConnectionStorage] Failed to save connections:', error);
            throw error;
        }
    }

    /**
     * Get all stored connections
     */
    public getConnections(): DataverseConnection[] {
        return this.loadConnections();
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
        this.saveConnections(connections);
        
        return newConnection;
    }

    /**
     * Remove a connection by ID
     */
    public async removeConnection(id: string): Promise<void> {
        const connections = this.getConnections();
        const filtered = connections.filter(c => c.id !== id);
        this.saveConnections(filtered);
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
        
        this.saveConnections(connections);
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
        this.saveConnections([]);
    }

    /**
     * Update an existing connection
     */
    public async updateConnection(connection: DataverseConnection): Promise<void> {
        const connections = this.getConnections();
        const index = connections.findIndex(c => c.id === connection.id);
        
        if (index !== -1) {
            connections[index] = connection;
            this.saveConnections(connections);
        }
    }

    /**
     * Deactivate all connections (used on extension startup)
     */
    public async deactivateAllConnections(): Promise<void> {
        const connections = this.getConnections();
        connections.forEach(c => {
            c.isActive = false;
        });
        this.saveConnections(connections);
    }

    /**
     * Get the path to the connections file (for debugging)
     */
    public getStoragePath(): string {
        return this.connectionsFilePath;
    }
}
