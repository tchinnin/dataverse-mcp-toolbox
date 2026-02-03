using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseMCPToolBox.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service principal pour gérer les connexions à Dataverse
/// Supporte le partage d'état entre instances via ConnectionStateService
/// </summary>
public class DataverseConnectionService : IDisposable
{
    private readonly DataverseAuthService _authService;
    private readonly Dictionary<string, ServiceClient> _activeConnections;
    private readonly ConnectionStateService _connectionStateService;

    public DataverseConnectionService(ConnectionStateService connectionStateService)
    {
        _authService = new DataverseAuthService();
        _activeConnections = new Dictionary<string, ServiceClient>();
        _connectionStateService = connectionStateService ?? throw new ArgumentNullException(nameof(connectionStateService));
        
        Console.Error.WriteLine("[DataverseConnectionService] Initialized with connection state sharing");
    }

    /// <summary>
    /// Crée une nouvelle connexion à un environnement Dataverse
    /// </summary>
    /// <param name="request">Les informations de connexion</param>
    /// <returns>Le résultat de la tentative de connexion</returns>
    public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
    {
        // Validate request
        var (isValid, error) = InputValidator.ValidateConnectionRequest(request);
        if (!isValid)
        {
            Console.Error.WriteLine($"[DataverseConnectionService] Validation failed: {error}");
            return new ConnectionResult
            {
                Success = false,
                ErrorMessage = error
            };
        }

        try
        {
            Microsoft.Identity.Client.AuthenticationResult authResult;

            // Vérifier si un token existant est fourni
            if (!string.IsNullOrEmpty(request.AccessToken))
            {
                // Tenter d'utiliser le token existant ou de le rafraîchir
                try
                {
                    authResult = await _authService.AuthenticateWithTokenAsync(request.EnvironmentUrl, request.AccessToken, request.RefreshToken);
                }
                catch
                {
                    // Si le token n'est pas valide, faire une auth interactive
                    authResult = await _authService.AuthenticateInteractiveAsync(request.EnvironmentUrl);
                }
            }
            else
            {
                // Pas de token fourni, faire une auth interactive
                authResult = await _authService.AuthenticateInteractiveAsync(request.EnvironmentUrl);
            }

            if (authResult == null || string.IsNullOrEmpty(authResult.AccessToken))
            {
                return new ConnectionResult
                {
                    Success = false,
                    ErrorMessage = "Échec de l'authentification: aucun token d'accès obtenu"
                };
            }

            // Étape 2: Créer le ServiceClient avec le token
            var serviceClient = new ServiceClient(
                instanceUrl: new Uri(request.EnvironmentUrl),
                tokenProviderFunction: async (uri) => await Task.FromResult(authResult.AccessToken),
                useUniqueInstance: true
            );

            // Étape 3: Tester la connexion
            if (!serviceClient.IsReady)
            {
                return new ConnectionResult
                {
                    Success = false,
                    ErrorMessage = $"Impossible de se connecter: {serviceClient.LastError}"
                };
            }

            // Étape 4: Récupérer les informations de l'utilisateur
            var userId = serviceClient.OAuthUserId;
            
            // Use provided connection ID if exists (re-auth), otherwise create new one
            string connectionId = !string.IsNullOrEmpty(request.ConnectionId) 
                ? request.ConnectionId 
                : Guid.NewGuid().ToString();

            Console.Error.WriteLine($"[DataverseConnectionService] Using connection ID: {connectionId} (provided: {!string.IsNullOrEmpty(request.ConnectionId)})");
            
            // Store/Update the connection in memory
            _activeConnections[connectionId] = serviceClient;

            // Persister dans l'état partagé avec les tokens pour permettre la recréation
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

            Console.Error.WriteLine($"[DataverseConnectionService] Connection {connectionId} persisted to shared state with auth tokens");

            return new ConnectionResult
            {
                Success = true,
                ConnectionId = connectionId,
                OrganizationUrl = request.EnvironmentUrl,
                UserId = userId.ToString(),
                UserName = authResult.Account?.Username,
                AccessToken = authResult.AccessToken,
                RefreshToken = authResult.Account?.HomeAccountId?.Identifier, // MSAL gère le refresh en interne
                ExpiresOn = authResult.ExpiresOn.ToString("o")
            };
        }
        catch (Exception ex)
        {
            return new ConnectionResult
            {
                Success = false,
                ErrorMessage = $"Erreur lors de la création de la connexion: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Récupère une connexion active par son ID
    /// Si la connexion n'est pas en mémoire mais existe dans l'état partagé, tente de la recréer
    /// </summary>
    public ServiceClient? GetConnection(string connectionId)
    {
        // Check in-memory first
        if (_activeConnections.TryGetValue(connectionId, out var connection))
        {
            return connection;
        }

        // Check persisted state (may be from another instance)
#pragma warning disable VSTHRD002
        var connectionInfo = _connectionStateService.GetConnectionAsync(connectionId).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        if (connectionInfo != null && connectionInfo.IsValid)
        {
            Console.Error.WriteLine($"[DataverseConnectionService] Connection {connectionId} found in shared state but not in memory");
            
            // Try to recreate connection from persisted tokens
            if (!string.IsNullOrEmpty(connectionInfo.AccessToken))
            {
                Console.Error.WriteLine($"[DataverseConnectionService] Attempting to recreate connection from persisted tokens...");
                var recreatedClient = RecreateConnectionFromTokens(connectionInfo);
                
                if (recreatedClient != null && recreatedClient.IsReady)
                {
                    // Cache the recreated connection
                    _activeConnections[connectionId] = recreatedClient;
                    Console.Error.WriteLine($"[DataverseConnectionService] ✓ Successfully recreated connection {connectionId}");
                    return recreatedClient;
                }
                else
                {
                    Console.Error.WriteLine($"[DataverseConnectionService] ✗ Failed to recreate connection - client not ready");
                }
            }
            else
            {
                Console.Error.WriteLine($"[DataverseConnectionService] ⚠️  No tokens available in shared state - cannot recreate connection");
            }
        }

        return null;
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
            Console.Error.WriteLine($"[DataverseConnectionService] Error recreating connection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Ferme une connexion
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
        Console.Error.WriteLine($"[DataverseConnectionService] Connection {connectionId} removed from shared state");
    }

    /// <summary>
    /// Ferme toutes les connexions actives
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
        Console.Error.WriteLine("[DataverseConnectionService] All connections cleared from shared state");
    }

    /// <summary>
    /// Teste si une connexion est toujours valide
    /// </summary>
    public bool TestConnection(string connectionId)
    {
        var connection = GetConnection(connectionId);
        return connection?.IsReady ?? false;
    }

    /// <summary>
    /// Récupère les informations d'organisation pour une connexion
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
    /// Récupère les informations WhoAmI pour une connexion
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
            // Exécuter WhoAmI request
            var whoAmIRequest = new Microsoft.Crm.Sdk.Messages.WhoAmIRequest();
            var whoAmIResponse = (Microsoft.Crm.Sdk.Messages.WhoAmIResponse)await connection.ExecuteAsync(whoAmIRequest);

            // Récupérer les détails de l'utilisateur
            var userId = whoAmIResponse.UserId;
            var businessUnitId = whoAmIResponse.BusinessUnitId;
            var orgId = whoAmIResponse.OrganizationId;

            // Récupérer le nom de l'utilisateur
            var userEntity = await connection.RetrieveAsync("systemuser", userId, new ColumnSet("fullname"));
            var userName = userEntity.GetAttributeValue<string>("fullname");

            // Récupérer le nom de la business unit
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
    /// Dispose all active connections
    /// </summary>
    public void Dispose()
    {
#pragma warning disable VSTHRD002
        CloseAllConnectionsAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        GC.SuppressFinalize(this);
    }
}

