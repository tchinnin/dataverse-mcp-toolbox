namespace DataverseMCPToolBox.Abstractions;

/// <summary>
/// Generic RPC client abstraction - transport-agnostic
/// Implementation can use Named Pipes, TCP, Unix Sockets, etc.
/// Used by Bridge to connect to Core Server
/// </summary>
public interface IRpcClient
{
    /// <summary>
    /// Connect to the RPC server
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the RPC server
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Invoke a remote procedure call and get the result
    /// </summary>
    /// <typeparam name="TResult">Expected result type</typeparam>
    /// <param name="method">Method name to invoke</param>
    /// <param name="args">Arguments to pass to the method</param>
    /// <returns>Result from the remote procedure</returns>
    Task<TResult> InvokeAsync<TResult>(string method, object? args = null);

    /// <summary>
    /// Invoke a remote procedure call without expecting a result
    /// </summary>
    /// <param name="method">Method name to invoke</param>
    /// <param name="args">Arguments to pass to the method</param>
    Task InvokeAsync(string method, object? args = null);
}
