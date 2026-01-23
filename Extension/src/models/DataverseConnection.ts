/**
 * Represents a Dataverse connection configuration
 */
export interface DataverseConnection {
    /**
     * Unique identifier for the connection
     */
    id: string;
    
    /**
     * User-friendly name for the connection
     */
    name: string;
    
    /**
     * Dataverse environment URL
     */
    url: string;
    
    /**
     * Whether this connection is currently active
     */
    isActive: boolean;
    
    /**
     * Additional metadata (e.g., RPC connection ID)
     */
    metadata?: {
        rpcConnectionId?: string;
        [key: string]: any;
    };
}
