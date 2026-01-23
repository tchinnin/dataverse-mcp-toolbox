using DataverseMCPToolBox.Models;
using DataverseMCPToolBox.Services;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Implémentation du service RPC qui expose les fonctionnalités Dataverse
/// </summary>
public class DataverseMCPToolBoxRpcService : IDataverseMCPToolBoxRpcService
{
    private readonly DataverseConnectionService _connectionService;

    public DataverseMCPToolBoxRpcService()
    {
        _connectionService = new DataverseConnectionService();
    }

    public async Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request)
    {
        return await _connectionService.CreateConnectionAsync(request);
    }

    public Task<bool> TestConnectionAsync(string connectionId)
    {
        var result = _connectionService.TestConnection(connectionId);
        return Task.FromResult(result);
    }

    public async Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId)
    {
        return await _connectionService.GetOrganizationDetailsAsync(connectionId);
    }

    public async Task<WhoAmIResult> GetWhoAmIAsync(string connectionId)
    {
        return await _connectionService.GetWhoAmIAsync(connectionId);
    }

    public Task CloseConnectionAsync(string connectionId)
    {
        _connectionService.CloseConnection(connectionId);
        return Task.CompletedTask;
    }

    public Task CloseAllConnectionsAsync()
    {
        _connectionService.CloseAllConnections();
        return Task.CompletedTask;
    }
}
