using Microsoft.Identity.Client;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Service d'authentification OAuth pour Dataverse utilisant MSAL
/// </summary>
public class DataverseAuthService
{
    private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private const string Authority = "https://login.microsoftonline.com/organizations";
    private static readonly string[] Scopes = new[] { "https://dynamics.crm.dynamics.com/.default" };

    private readonly IPublicClientApplication _publicClientApp;

    public DataverseAuthService()
    {
        _publicClientApp = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority(Authority)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    /// <summary>
    /// Authentifie l'utilisateur via un flux OAuth interactif dans le navigateur
    /// </summary>
    /// <param name="environmentUrl">URL de l'environnement Dataverse (ex: https://org.crm.dynamics.com)</param>
    /// <returns>Le token d'accès pour se connecter à Dataverse</returns>
    public async Task<AuthenticationResult> AuthenticateInteractiveAsync(string environmentUrl)
    {
        try
        {
            // Extraire le scope spécifique à l'environnement
            var uri = new Uri(environmentUrl);
            var scope = $"{uri.Scheme}://{uri.Host}/.default";

            // Tenter d'obtenir un token silencieusement d'abord (depuis le cache)
            var accounts = await _publicClientApp.GetAccountsAsync();
            if (accounts.Any())
            {
                try
                {
                    return await _publicClientApp
                        .AcquireTokenSilent(new[] { scope }, accounts.FirstOrDefault())
                        .ExecuteAsync();
                }
                catch (MsalUiRequiredException)
                {
                    // Le token silencieux a échoué, on continue avec l'authentification interactive
                }
            }

            // Authentification interactive - lance le navigateur
            var result = await _publicClientApp
                .AcquireTokenInteractive(new[] { scope })
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false) // Utilise le navigateur système
                .ExecuteAsync();

            return result;
        }
        catch (MsalException ex)
        {
            throw new InvalidOperationException($"Erreur d'authentification: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Déconnecte tous les comptes en cache
    /// </summary>
    public async Task SignOutAsync()
    {
        var accounts = await _publicClientApp.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _publicClientApp.RemoveAsync(account);
        }
    }

    /// <summary>
    /// Récupère les comptes en cache
    /// </summary>
    public async Task<IEnumerable<IAccount>> GetCachedAccountsAsync()
    {
        return await _publicClientApp.GetAccountsAsync();
    }

    /// <summary>
    /// Authentifie avec un token existant ou tente de le rafraîchir
    /// </summary>
    /// <param name="environmentUrl">URL de l'environnement Dataverse</param>
    /// <param name="accessToken">Token d'accès existant</param>
    /// <param name="refreshToken">Token de rafraîchissement (peut être null)</param>
    /// <returns>Le résultat de l'authentification</returns>
    public async Task<AuthenticationResult> AuthenticateWithTokenAsync(string environmentUrl, string accessToken, string? refreshToken)
    {
        try
        {
            // Extraire le scope spécifique à l'environnement
            var uri = new Uri(environmentUrl);
            var scope = $"{uri.Scheme}://{uri.Host}/.default";

            // Tenter d'obtenir un token silencieusement depuis le cache MSAL
            var accounts = await _publicClientApp.GetAccountsAsync();
            if (accounts.Any())
            {
                try
                {
                    var result = await _publicClientApp
                        .AcquireTokenSilent(new[] { scope }, accounts.FirstOrDefault())
                        .ExecuteAsync();
                    
                    return result;
                }
                catch (MsalUiRequiredException)
                {
                    // Le token ne peut pas être rafraîchi silencieusement
                    throw new InvalidOperationException("Le token a expiré et nécessite une nouvelle authentification interactive");
                }
            }

            throw new InvalidOperationException("Aucun compte en cache, authentification interactive requise");
        }
        catch (MsalException ex)
        {
            throw new InvalidOperationException($"Erreur lors du rafraîchissement du token: {ex.Message}", ex);
        }
    }
}
