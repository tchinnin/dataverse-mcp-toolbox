namespace DataverseMCPToolBox.Abstractions;

/// <summary>
/// Generic RPC server abstraction - transport-agnostic
/// Implementation can use Named Pipes, TCP, Unix Sockets, etc.
/// Services should depend on this interface, not specific transport implementations
/// </summary>
public interface IRpcServer
{
    /// <summary>
    /// Start the RPC server and begin accepting connections
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the RPC server and close all connections gracefully
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Register a service instance to handle RPC calls
    /// The service's public methods will be exposed as RPC endpoints
    /// </summary>
    /// <param name="service">Service instance to register</param>
    void RegisterService(object service);
}
