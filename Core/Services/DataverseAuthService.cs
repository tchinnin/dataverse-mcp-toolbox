using Microsoft.Identity.Client;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// OAuth authentication service for Dataverse using MSAL
/// </summary>
public class DataverseAuthService
{
    private readonly IPublicClientApplication _publicClientApp;

    public DataverseAuthService()
    {
        _publicClientApp = PublicClientApplicationBuilder
            .Create(AuthenticationConstants.ClientId)
            .WithAuthority(AuthenticationConstants.Authority)
            .WithRedirectUri("http://localhost")
            .Build();
    }

    /// <summary>
    /// Authenticate user via interactive OAuth flow in browser
    /// </summary>
    /// <param name="environmentUrl">Dataverse environment URL (e.g., https://org.crm.dynamics.com)</param>
    /// <returns>Authentication result with access token for Dataverse connection</returns>
    public async Task<AuthenticationResult> AuthenticateInteractiveAsync(string environmentUrl)
    {
        try
        {
            // Extract environment-specific scope
            var uri = new Uri(environmentUrl);
            var scope = $"{uri.Scheme}://{uri.Host}/.default";

            // Try to get token silently first (from cache)
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
                    // Silent token acquisition failed, continue with interactive authentication
                }
            }

            // Interactive authentication - launches browser
            var result = await _publicClientApp
                .AcquireTokenInteractive(new[] { scope })
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false) // Use system browser
                .ExecuteAsync();

            return result;
        }
        catch (MsalException ex)
        {
            throw new InvalidOperationException($"Authentication error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Authenticate with existing token or attempt to refresh it
    /// </summary>
    /// <param name="environmentUrl">Dataverse environment URL</param>
    /// <param name="accessToken">Existing access token</param>
    /// <param name="refreshToken">Refresh token (can be null)</param>
    /// <returns>Authentication result</returns>
    public async Task<AuthenticationResult> AuthenticateWithTokenAsync(string environmentUrl, string accessToken, string? refreshToken)
    {
        try
        {
            // Extract environment-specific scope
            var uri = new Uri(environmentUrl);
            var scope = $"{uri.Scheme}://{uri.Host}/.default";

            // Try to get token silently from MSAL cache
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
                    // Token cannot be refreshed silently
                    throw new InvalidOperationException("Token has expired and requires interactive authentication");
                }
            }

            throw new InvalidOperationException("No cached account, interactive authentication required");
        }
        catch (MsalException ex)
        {
            throw new InvalidOperationException($"Error refreshing token: {ex.Message}", ex);
        }
    }
}
