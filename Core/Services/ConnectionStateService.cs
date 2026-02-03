using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for managing connection state in-memory for this Core Server instance
/// Each VS Code instance has its own isolated Core Server with independent state
/// Sidecar architecture: State is isolated per instance, no sharing needed
/// </summary>
public class ConnectionStateService
{
    private readonly Dictionary<string, ConnectionInfo> _connections = new();
    private string? _activeConnectionId;
    private readonly object _lock = new();

    public ConnectionStateService(string pluginDirectory)
    {
        // Sidecar architecture: Each Core Server instance has its own in-memory state
        // No file-based persistence needed - state is isolated per VS Code window
        Console.Error.WriteLine("[ConnectionState] Using in-memory state (isolated per instance)");
    }

    /// <summary>
    /// Save connection information to in-memory state
    /// </summary>
    public Task SaveConnectionAsync(string connectionId, ConnectionInfo connectionInfo)
    {
        lock (_lock)
        {
            _connections[connectionId] = connectionInfo;
            Console.Error.WriteLine($"[ConnectionState] Saved connection: {connectionId}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Remove connection from in-memory state
    /// </summary>
    public Task RemoveConnectionAsync(string connectionId)
    {
        lock (_lock)
        {
            _connections.Remove(connectionId);
            Console.Error.WriteLine($"[ConnectionState] Removed connection: {connectionId}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Set the active connection ID
    /// </summary>
    public Task SetActiveConnectionAsync(string? connectionId)
    {
        lock (_lock)
        {
            _activeConnectionId = connectionId;
            Console.Error.WriteLine($"[ConnectionState] Set active connection: {connectionId ?? "(none)"}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the active connection ID
    /// </summary>
    public Task<string?> GetActiveConnectionIdAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_activeConnectionId);
        }
    }

    /// <summary>
    /// Get connection information by ID
    /// </summary>
    public Task<ConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        lock (_lock)
        {
            _connections.TryGetValue(connectionId, out var info);
            return Task.FromResult(info);
        }
    }

    /// <summary>
    /// Get all connections
    /// </summary>
    public Task<Dictionary<string, ConnectionInfo>> GetAllConnectionsAsync()
    {
        lock (_lock)
        {
            // Return a copy to prevent external modifications
            return Task.FromResult(new Dictionary<string, ConnectionInfo>(_connections));
        }
    }

    /// <summary>
    /// Clear all connections and active connection
    /// </summary>
    public Task ClearAllAsync()
    {
        lock (_lock)
        {
            _connections.Clear();
            _activeConnectionId = null;
            Console.Error.WriteLine("[ConnectionState] Cleared all connections");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Connection information stored in the in-memory state
/// Includes authentication tokens for connection management
/// </summary>
public class ConnectionInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string EnvironmentUrl { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsValid { get; set; }
    
    // Authentication tokens for connection recreation
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresOn { get; set; }
}
