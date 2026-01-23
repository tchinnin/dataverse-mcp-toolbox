using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.JsonRpc;

/// <summary>
/// Contrat de service exposé via JSON-RPC 2.0
/// </summary>
public interface IDataverseMCPToolBoxRpcService
{
    // Connection management
    Task<ConnectionResult> CreateConnectionAsync(ConnectionRequest request);
    Task<bool> TestConnectionAsync(string connectionId);
    Task<OrganizationDetail?> GetOrganizationDetailsAsync(string connectionId);
    Task<WhoAmIResult> GetWhoAmIAsync(string connectionId);
    Task CloseConnectionAsync(string connectionId);
    Task CloseAllConnectionsAsync();

    // Plugin management
    Task SetPluginDirectoryAsync(string directoryPath);
    Task<PluginInstallResult> InstallPluginAsync(PluginInstallRequest request);
    Task<bool> UninstallPluginAsync(string packageId);
    Task ReloadPluginsAsync();
    Task<List<PluginInfo>> ListPluginsAsync();

    // Tool management
    Task<List<ToolInfo>> ListToolsAsync();
    Task<ToolCallResult> CallToolAsync(ToolCallRequest request);
}
