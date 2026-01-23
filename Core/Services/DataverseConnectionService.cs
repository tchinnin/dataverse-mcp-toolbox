using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseMCPToolBox.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service principal pour gérer les connexions à Dataverse
/// </summary>
public class DataverseConnectionService
{
    private readonly DataverseAuthService _authService;
    private readonly Dictionary<string, ServiceClient> _activeConnections;

    public DataverseConnectionService()
    {
        _authService = new DataverseAuthService();
        _activeConnections = new Dictionary<string, ServiceClient>();
    }

    /// <summary>
    /// Crée une nouvelle connexion à un environnement Dataverse
    /// </summary>
    /// <param name="request">Les informations de connexion</param>
    /// <returns>Le résultat de la tentative de connexion</returns>
    public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
    {
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
            var connectionId = Guid.NewGuid().ToString();
            
            // Stocker la connexion active
            _activeConnections[connectionId] = serviceClient;

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
    /// </summary>
    public ServiceClient? GetConnection(string connectionId)
    {
        return _activeConnections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    /// <summary>
    /// Ferme une connexion
    /// </summary>
    public void CloseConnection(string connectionId)
    {
        if (_activeConnections.TryGetValue(connectionId, out var connection))
        {
            connection.Dispose();
            _activeConnections.Remove(connectionId);
        }
    }

    /// <summary>
    /// Ferme toutes les connexions actives
    /// </summary>
    public void CloseAllConnections()
    {
        foreach (var connection in _activeConnections.Values)
        {
            connection.Dispose();
        }
        _activeConnections.Clear();
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
}
