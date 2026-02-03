using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Primary service for managing Dataverse connections
/// Supports state sharing between instances via ConnectionStateService
/// </summary>
public class DataverseConnectionService : IAsyncDisposable
{
    private const string ServiceName = "DataverseConnectionService";
    private readonly DataverseAuthService _authService;
    private readonly Dictionary<string, ServiceClient> _activeConnections;
    private readonly ConnectionStateService _connectionStateService;

    public DataverseConnectionService(ConnectionStateService connectionStateService)
    {
        _authService = new DataverseAuthService();
        _activeConnections = new Dictionary<string, ServiceClient>();
        _connectionStateService = connectionStateService ?? throw new ArgumentNullException(nameof(connectionStateService));
        
        Logger.LogInfo(ServiceName, "Initialized with connection state sharing");
    }

    /// <summary>
    /// Create a new connection to a Dataverse environment
    /// </summary>
    /// <param name="request">Connection information</param>
    /// <returns>Result of the connection attempt</returns>
    public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
    {
        // Validate request
        var validation = InputValidator.ValidateConnectionRequest(request);
        if (!validation.IsValid)
        {
            Logger.LogError(ServiceName, $"Validation failed: {validation.Error}");
            return ConnectionResult.Failure(validation.Error!);
        }

        try
        {
            Microsoft.Identity.Client.AuthenticationResult authResult;

            // Check if an existing token is provided
            if (!string.IsNullOrEmpty(request.AccessToken))
            {
                // Try to use existing token or refresh it
                try
                {
                    authResult = await _authService.AuthenticateWithTokenAsync(request.EnvironmentUrl, request.AccessToken, request.RefreshToken);
                }
                catch
                {
                    // If token is not valid, perform interactive auth
                    authResult = await _authService.AuthenticateInteractiveAsync(request.EnvironmentUrl);
                }
            }
            else
            {
                // No token provided, perform interactive auth
                authResult = await _authService.AuthenticateInteractiveAsync(request.EnvironmentUrl);
            }

            if (authResult == null || string.IsNullOrEmpty(authResult.AccessToken))
            {
                return ConnectionResult.AuthenticationFailure("no access token obtained");
            }

            // Step 2: Create ServiceClient with the token
            var serviceClient = new ServiceClient(
                instanceUrl: new Uri(request.EnvironmentUrl),
                tokenProviderFunction: async (uri) => await Task.FromResult(authResult.AccessToken),
                useUniqueInstance: true
            );

            // Step 3: Test the connection
            if (!serviceClient.IsReady)
            {
                return ConnectionResult.ConnectionFailure(serviceClient.LastError);
            }

            // Step 4: Get user information
            var userId = serviceClient.OAuthUserId;
            
            // Use provided connection ID if exists (re-auth), otherwise create new one
            string connectionId = !string.IsNullOrEmpty(request.ConnectionId) 
                ? request.ConnectionId 
                : Guid.NewGuid().ToString();

            Logger.LogInfo(ServiceName, $"Using connection ID: {connectionId} (provided: {!string.IsNullOrEmpty(request.ConnectionId)})");
            
            // Store/Update the connection in memory
            _activeConnections[connectionId] = serviceClient;

            // Persist in shared state with tokens to allow recreation
            // Extract environment name from URL for display
            var uri = new Uri(request.EnvironmentUrl);
            var environmentName = uri.Host.Split('.').FirstOrDefault() ?? "Dataverse";
            
            await _connectionStateService.SaveConnectionAsync(connectionId, new ConnectionInfo
            {
                ConnectionId = connectionId,
                EnvironmentUrl = request.EnvironmentUrl,
                ConnectionName = $"{environmentName} Connection",
                CreatedAt = DateTime.UtcNow,
                IsValid = true,
                AccessToken = authResult.AccessToken,
                RefreshToken = authResult.Account?.HomeAccountId?.Identifier,
                ExpiresOn = authResult.ExpiresOn.ToString("o")
            });

            Logger.LogInfo(ServiceName, $"Connection {connectionId} persisted to shared state with auth tokens");

            return ConnectionResult.SuccessResult(
                connectionId,
                request.EnvironmentUrl,
                userId.ToString(),
                authResult.Account?.Username,
                authResult.AccessToken,
                authResult.Account?.HomeAccountId?.Identifier,
                authResult.ExpiresOn.ToString("o")
            );
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Error creating connection");
            return ConnectionResult.Failure($"Error creating connection: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieve an active connection by ID
    /// If connection is not in memory but exists in shared state, attempts to recreate it
    /// </summary>
    public async Task<ServiceClient?> GetConnectionAsync(string connectionId)
    {
        // Check in-memory first
        if (_activeConnections.TryGetValue(connectionId, out var connection))
        {
            return connection;
        }

        // Check persisted state (may be from another instance)
        var connectionInfo = await _connectionStateService.GetConnectionAsync(connectionId);
        if (connectionInfo != null && connectionInfo.IsValid)
        {
            Logger.LogInfo(ServiceName, $"Connection {connectionId} found in shared state but not in memory");
            
            // Try to recreate connection from persisted tokens
            if (!string.IsNullOrEmpty(connectionInfo.AccessToken))
            {
                Logger.LogInfo(ServiceName, "Attempting to recreate connection from persisted tokens...");
                var recreatedClient = RecreateConnectionFromTokens(connectionInfo);
                
                if (recreatedClient != null && recreatedClient.IsReady)
                {
                    // Cache the recreated connection
                    _activeConnections[connectionId] = recreatedClient;
                    Logger.LogSuccess(ServiceName, $"Successfully recreated connection {connectionId}");
                    return recreatedClient;
                }
                else
                {
                    Logger.LogError(ServiceName, "Failed to recreate connection - client not ready");
                }
            }
            else
            {
                Logger.LogWarning(ServiceName, "No tokens available in shared state - cannot recreate connection");
            }
        }

        return null;
    }
    
    /// <summary>
    /// Synchronous version for backward compatibility with non-async callers
    /// Prefer using GetConnectionAsync when possible
    /// </summary>
    public ServiceClient? GetConnection(string connectionId)
    {
        return GetConnectionAsync(connectionId).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// Recreate a ServiceClient from persisted connection info with tokens
    /// </summary>
    private ServiceClient? RecreateConnectionFromTokens(ConnectionInfo connectionInfo)
    {
        try
        {
            var serviceClient = new ServiceClient(
                instanceUrl: new Uri(connectionInfo.EnvironmentUrl),
                tokenProviderFunction: async (uri) => await Task.FromResult(connectionInfo.AccessToken!),
                useUniqueInstance: true
            );
            
            return serviceClient.IsReady ? serviceClient : null;
        }
        catch (Exception ex)
        {
            Logger.LogException(ServiceName, ex, "Error recreating connection");
            return null;
        }
    }

    /// <summary>
    /// Close a connection
    /// </summary>
    public async Task CloseConnectionAsync(string connectionId)
    {
        if (_activeConnections.TryGetValue(connectionId, out var connection))
        {
            connection.Dispose();
            _activeConnections.Remove(connectionId);
        }

        // Remove from shared state
        await _connectionStateService.RemoveConnectionAsync(connectionId);
        Logger.LogInfo(ServiceName, $"Connection {connectionId} removed from shared state");
    }

    /// <summary>
    /// Close all active connections
    /// </summary>
    public async Task CloseAllConnectionsAsync()
    {
        foreach (var connection in _activeConnections.Values)
        {
            connection.Dispose();
        }
        _activeConnections.Clear();

        // Clear shared state
        await _connectionStateService.ClearAllAsync();
        Logger.LogInfo(ServiceName, "All connections cleared from shared state");
    }

    /// <summary>
    /// Test if a connection is still valid
    /// </summary>
    public bool TestConnection(string connectionId)
    {
        var connection = GetConnection(connectionId);
        return connection?.IsReady ?? false;
    }

    /// <summary>
    /// Retrieve organization information for a connection
    /// </summary>
    public async Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId)
    {
        var connection = GetConnection(connectionId);
        if (connection == null || !connection.IsReady)
            return null;

        try
        {
            // Récupérer les détails via l'API Dataverse
            var orgDetail = new OrganizationDetail
            {
                OrganizationId = connection.ConnectedOrgId.ToString(),
                FriendlyName = connection.ConnectedOrgFriendlyName,
                UniqueName = connection.ConnectedOrgUniqueName,
                Version = connection.ConnectedOrgVersion.ToString()
            };

            return orgDetail;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieve WhoAmI information for a connection
    /// </summary>
    public async Task<WhoAmIResult> GetWhoAmIAsync(string connectionId)
    {
        var connection = GetConnection(connectionId);
        if (connection == null || !connection.IsReady)
        {
            return new WhoAmIResult
            {
                Success = false,
                ErrorMessage = "Connection not found or not ready"
            };
        }

        try
        {
            // Execute WhoAmI request
            var whoAmIRequest = new Microsoft.Crm.Sdk.Messages.WhoAmIRequest();
            var whoAmIResponse = (Microsoft.Crm.Sdk.Messages.WhoAmIResponse)await connection.ExecuteAsync(whoAmIRequest);

            // Get user details
            var userId = whoAmIResponse.UserId;
            var businessUnitId = whoAmIResponse.BusinessUnitId;
            var orgId = whoAmIResponse.OrganizationId;

            // Get user name
            var userEntity = await connection.RetrieveAsync("systemuser", userId, new ColumnSet("fullname"));
            var userName = userEntity.GetAttributeValue<string>("fullname");

            // Get business unit name
            var buEntity = await connection.RetrieveAsync("businessunit", businessUnitId, new ColumnSet("name"));
            var businessUnitName = buEntity.GetAttributeValue<string>("name");

            return new WhoAmIResult
            {
                Success = true,
                EnvironmentUrl = connection.ConnectedOrgPublishedEndpoints[Microsoft.Xrm.Sdk.Discovery.EndpointType.WebApplication],
                UserId = userId.ToString(),
                UserName = userName,
                BusinessUnitId = businessUnitId.ToString(),
                BusinessUnitName = businessUnitName,
                OrganizationId = orgId.ToString()
            };
        }
        catch (Exception ex)
        {
            return new WhoAmIResult
            {
                Success = false,
                ErrorMessage = $"Error executing WhoAmI: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Dispose all active connections asynchronously
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await CloseAllConnectionsAsync();
        GC.SuppressFinalize(this);
    }
}

