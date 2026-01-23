using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Contrat de service exposé via JSON-RPC 2.0
/// </summary>
public interface IDataverseMCPToolBoxRpcService
{
    Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request);
    Task<bool> TestConnectionAsync(string connectionId);
    Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId);
    Task<WhoAmIResult> GetWhoAmIAsync(string connectionId);
    Task CloseConnectionAsync(string connectionId);
    Task CloseAllConnectionsAsync();
}
