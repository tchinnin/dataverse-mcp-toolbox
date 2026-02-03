import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { DataverseConnection } from '../models/DataverseConnection';

/**
 * Service for managing Dataverse connection storage using file-based persistence
 * Connections are stored in globalStorageUri to survive extension updates
 */
export class ConnectionStorageService {
    private static readonly CONNECTIONS_FILE = 'connections.json';
    private static readonly LEGACY_STORAGE_KEY = 'dataverse.connections';
    private readonly connectionsFileUri: vscode.Uri;
    
    constructor(private context: vscode.ExtensionContext) {
        // Use globalStorageUri for persistent storage across extension updates
        this.connectionsFileUri = vscode.Uri.joinPath(context.globalStorageUri, ConnectionStorageService.CONNECTIONS_FILE);
        
        // Ensure directory exists
        this.ensureStorageDirectory();
        
        // Migrate from old globalState storage if needed
        this.migrateFromGlobalState();
    }

    /**
     * Ensure storage directory exists
     * Surface operation: Uses workspace.fs for directory creation
     */
    private ensureStorageDirectory(): void {
        const dirUri = vscode.Uri.joinPath(this.connectionsFileUri, '..');
        // Create directory asynchronously (fire and forget - will be available when needed)
        vscode.workspace.fs.createDirectory(dirUri).then(
            () => {},
            (error) => console.error('[ConnectionStorage] Failed to create directory:', error)
        );
    }

    /**
     * Migrate connections from old globalState storage to file-based storage
     * Surface operation: Uses workspace.fs for file existence check
     */
    private migrateFromGlobalState(): void {
        // Check if file already exists using workspace.fs
        vscode.workspace.fs.stat(this.connectionsFileUri).then(
            () => {
                // File exists, check if it contains old-style IDs that need migration
                this.migrateLegacyConnectionIds();
            },
            () => {
                // File doesn't exist, try to migrate from globalState
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
        );
    }

    /**
     * Migrate connections with old conn_* IDs to server GUIDs
     * Mark them for re-authentication since we need server to generate new IDs
     */
    private migrateLegacyConnectionIds(): void {
        const connections = this.loadConnections();
        let needsMigration = false;

        for (const conn of connections) {
            // Check if this is an old-style ID (starts with "conn_")
            if (conn.id.startsWith('conn_')) {
                needsMigration = true;
                console.log(`[ConnectionStorage] Found legacy connection ID: ${conn.id} (${conn.name})`);
                
                // If metadata contains rpcConnectionId, migrate to use it
                if (conn.metadata?.rpcConnectionId) {
                    console.log(`[ConnectionStorage] Migrating ${conn.name} from ${conn.id} to ${conn.metadata.rpcConnectionId}`);
                    conn.id = conn.metadata.rpcConnectionId;
                    delete conn.metadata.rpcConnectionId;
                    if (Object.keys(conn.metadata).length === 0) {
                        delete conn.metadata;
                    }
                } else {
                    // No server ID available - connection will need re-authentication
                    // For now, keep the old ID but log a warning
                    console.warn(`[ConnectionStorage] Connection ${conn.name} has legacy ID but no server GUID. Will require re-authentication.`);
                }
            }
        }

        if (needsMigration) {
            console.log('[ConnectionStorage] Saving migrated connections');
            this.saveConnections(connections);
        }
    }

    /**
     * Load connections from file
     * Surface operation: Uses workspace.fs for reading small JSON file
     */
    private loadConnections(): DataverseConnection[] {
        try {
            // Synchronous read using Node.js fs (workspace.fs is async)
            // For migration simplicity, keeping sync operations for now
            // TODO: Consider async refactoring in future
            if (!fs.existsSync(this.connectionsFileUri.fsPath)) {
                return [];
            }
            
            const data = fs.readFileSync(this.connectionsFileUri.fsPath, 'utf-8');
            return JSON.parse(data) as DataverseConnection[];
        } catch (error) {
            console.error('[ConnectionStorage] Failed to load connections:', error);
            return [];
        }
    }

    /**
     * Save connections to file (atomic write with temp file)
     * Surface operation: Uses Node.js fs for atomic writes (workspace.fs doesn't support atomic operations)
     */
    private saveConnections(connections: DataverseConnection[]): void {
        try {
            const tempPath = `${this.connectionsFileUri.fsPath}.tmp`;
            const data = JSON.stringify(connections, null, 2);
            
            // Write to temp file first
            fs.writeFileSync(tempPath, data, 'utf-8');
            
            // Atomic rename
            fs.renameSync(tempPath, this.connectionsFileUri.fsPath);
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
     * Save a new connection using server-generated ID
     * @param id Server-generated GUID from RPC connection
     * @param name User-friendly connection name
     * @param url Dataverse environment URL
     */
    public async addConnection(id: string, name: string, url: string): Promise<DataverseConnection> {
        const connections = this.getConnections();
        
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
     * Get the Uri to the connections file
     */
    public getStorageUri(): vscode.Uri {
        return this.connectionsFileUri;
    }

    /**
     * Get the path to the connections file (for debugging)
     * @deprecated Use getStorageUri() instead
     */
    public getStoragePath(): string {
        return this.connectionsFileUri.fsPath;
    }
}
