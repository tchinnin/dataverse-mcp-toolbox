namespace DataverseMCPToolBox.Extensibility.Abstractions;

/// <summary>
/// Defines the lifecycle contract for MCP plugins.
/// Plugins are discovered at runtime and initialized by the host server.
/// </summary>
public interface IPlugin : IDisposable
{
    /// <summary>
    /// Initializes the plugin with the provided service provider.
    /// Called once during plugin discovery and registration.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="cancellationToken">Cancellation token for initialization</param>
    /// <returns>Task representing the asynchronous initialization</returns>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
