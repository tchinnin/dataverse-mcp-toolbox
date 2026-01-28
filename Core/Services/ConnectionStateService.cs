using System.Text.Json;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service for persisting and sharing connection state between multiple server instances
/// Allows Extension instance and MCP instance to share active connection information
/// </summary>
public class ConnectionStateService
{
    private readonly string _stateFilePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConnectionStateService(string pluginDirectory)
    {
        // Store state file in the same directory as plugins for easy access by both instances
        var stateDirectory = Path.Combine(pluginDirectory, ".state");
        Directory.CreateDirectory(stateDirectory);
        _stateFilePath = Path.Combine(stateDirectory, "connections.json");
        
        Console.Error.WriteLine($"[ConnectionState] State file: {_stateFilePath}");
    }

    /// <summary>
    /// Save connection information to shared state file
    /// </summary>
    public async Task SaveConnectionAsync(string connectionId, ConnectionInfo connectionInfo)
    {
        try
        {
            var state = await LoadStateAsync();
            state.Connections[connectionId] = connectionInfo;
            await SaveStateAsync(state);
            
            Console.Error.WriteLine($"[ConnectionState] Saved connection: {connectionId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error saving connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove connection from shared state file
    /// </summary>
    public async Task RemoveConnectionAsync(string connectionId)
    {
        try
        {
            var state = await LoadStateAsync();
            state.Connections.Remove(connectionId);
            await SaveStateAsync(state);
            
            Console.Error.WriteLine($"[ConnectionState] Removed connection: {connectionId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error removing connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Set the active connection ID
    /// </summary>
    public async Task SetActiveConnectionAsync(string? connectionId)
    {
        try
        {
            var state = await LoadStateAsync();
            state.ActiveConnectionId = connectionId;
            await SaveStateAsync(state);
            
            Console.Error.WriteLine($"[ConnectionState] Set active connection: {connectionId ?? "(none)"}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error setting active connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the active connection ID
    /// </summary>
    public async Task<string?> GetActiveConnectionIdAsync()
    {
        try
        {
            var state = await LoadStateAsync();
            return state.ActiveConnectionId;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error getting active connection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get connection information by ID
    /// </summary>
    public async Task<ConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        try
        {
            var state = await LoadStateAsync();
            return state.Connections.TryGetValue(connectionId, out var info) ? info : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error getting connection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get all connections
    /// </summary>
    public async Task<Dictionary<string, ConnectionInfo>> GetAllConnectionsAsync()
    {
        try
        {
            var state = await LoadStateAsync();
            return state.Connections;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error getting all connections: {ex.Message}");
            return new Dictionary<string, ConnectionInfo>();
        }
    }

    /// <summary>
    /// Clear all connections and active connection
    /// </summary>
    public async Task ClearAllAsync()
    {
        try
        {
            var state = new ConnectionState
            {
                Connections = new Dictionary<string, ConnectionInfo>(),
                ActiveConnectionId = null
            };
            await SaveStateAsync(state);
            
            Console.Error.WriteLine("[ConnectionState] Cleared all connections");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error clearing connections: {ex.Message}");
        }
    }

    private async Task<ConnectionState> LoadStateAsync()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new ConnectionState
            {
                Connections = new Dictionary<string, ConnectionInfo>(),
                ActiveConnectionId = null
            };
        }

        try
        {
            var json = await File.ReadAllTextAsync(_stateFilePath);
            var state = JsonSerializer.Deserialize<ConnectionState>(json, _jsonOptions);
            return state ?? new ConnectionState
            {
                Connections = new Dictionary<string, ConnectionInfo>(),
                ActiveConnectionId = null
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ConnectionState] Error loading state file: {ex.Message}");
            return new ConnectionState
            {
                Connections = new Dictionary<string, ConnectionInfo>(),
                ActiveConnectionId = null
            };
        }
    }

    private async Task SaveStateAsync(ConnectionState state)
    {
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_stateFilePath, json);
    }

    /// <summary>
    /// Internal model for persisted connection state
    /// </summary>
    private class ConnectionState
    {
        public Dictionary<string, ConnectionInfo> Connections { get; set; } = new();
        public string? ActiveConnectionId { get; set; }
    }
}

/// <summary>
/// Connection information stored in the shared state file
/// Includes authentication tokens for cross-instance connection recreation
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
