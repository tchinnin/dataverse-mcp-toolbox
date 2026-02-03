using System.IO.Pipes;
using StreamJsonRpc;
using Newtonsoft.Json.Serialization;
using DataverseMCPToolBox.Abstractions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Bridge.Services;

/// <summary>
/// Named Pipe RPC Client implementation
/// Connects to Core Server via Named Pipe
/// Used by Bridge to forward MCP requests
/// </summary>
public class NamedPipeRpcClient : IRpcClient, IDisposable
{
    private readonly string _pipeName;
    private readonly TimeSpan _connectionTimeout;
    private NamedPipeClientStream? _pipeClient;
    private StreamJsonRpc.JsonRpc? _jsonRpc;
    private readonly JsonMessageFormatter _formatter;
    private bool _disposed;

    /// <summary>
    /// Create a Named Pipe RPC client
    /// </summary>
    /// <param name="pipeName">Pipe name (without platform-specific prefix)</param>
    /// <param name="connectionTimeout">Timeout for connection attempts (default: 10 seconds)</param>
    public NamedPipeRpcClient(string pipeName, TimeSpan? connectionTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be null or empty", nameof(pipeName));
        }

        _pipeName = pipeName;
        _connectionTimeout = connectionTimeout ?? NetworkConstants.DefaultConnectionTimeout;

        // Configure JSON formatter with camelCase for TypeScript compatibility
        _formatter = JsonFormatterFactory.CreateCamelCaseFormatter();

        Console.Error.WriteLine($"[NamedPipeRpcClient] Created with pipe name: {_pipeName}");
    }

    /// <summary>
    /// Check if client is connected
    /// </summary>
    public bool IsConnected => _pipeClient?.IsConnected ?? false;

    /// <summary>
    /// Connect to the Named Pipe server in RAW mode (no JSON-RPC)
    /// Use this for pure stream forwarding without JSON-RPC processing
    /// </summary>
    public async Task ConnectRawAsync(CancellationToken cancellationToken = default)
    {
        if (_pipeClient != null)
        {
            throw new InvalidOperationException("Client is already connected. Call DisconnectAsync() first.");
        }

        Console.Error.WriteLine($"[NamedPipeRpcClient] Connecting to pipe in RAW mode: {_pipeName}");

        // Create pipe client
        _pipeClient = new NamedPipeClientStream(
            ".",                    // Server name (local machine)
            _pipeName,              // Pipe name
            PipeDirection.InOut,    // Bidirectional
            PipeOptions.Asynchronous);

        // Connect with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_connectionTimeout);

        try
        {
            await _pipeClient.ConnectAsync(cts.Token);
            Console.Error.WriteLine("[NamedPipeRpcClient] Connected successfully (RAW mode - no JSON-RPC)");
            // NOTE: We deliberately do NOT initialize JSON-RPC in raw mode
            // The pipe stream can be accessed directly via GetPipeStream()
            return;
        }
        catch (OperationCanceledException)
        {
            _pipeClient.Dispose();
            _pipeClient = null;

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Connection cancelled by caller", cancellationToken);
            }

            throw new TimeoutException(
                $"Could not connect to Named Pipe '{_pipeName}' within {_connectionTimeout.TotalSeconds}s. " +
                "Ensure the Core Server is running.");
        }
        catch (IOException ex)
        {
            _pipeClient.Dispose();
            _pipeClient = null;
            throw new InvalidOperationException($"Failed to connect to Named Pipe '{_pipeName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Connect to the Named Pipe server
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_pipeClient != null)
        {
            throw new InvalidOperationException("Client is already connected. Call DisconnectAsync() first.");
        }

        Console.Error.WriteLine($"[NamedPipeRpcClient] Connecting to pipe: {_pipeName}");

        // Create pipe client
        _pipeClient = new NamedPipeClientStream(
            ".",                    // Server name (local machine)
            _pipeName,              // Pipe name
            PipeDirection.InOut,    // Bidirectional
            PipeOptions.Asynchronous);

        // Connect with timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_connectionTimeout);

        try
        {
            await _pipeClient.ConnectAsync(cts.Token);
            Console.Error.WriteLine("[NamedPipeRpcClient] Connected successfully");
        }
        catch (OperationCanceledException)
        {
            _pipeClient.Dispose();
            _pipeClient = null;

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Connection cancelled by caller", cancellationToken);
            }

            throw new TimeoutException(
                $"Could not connect to Named Pipe '{_pipeName}' within {_connectionTimeout.TotalSeconds}s. " +
                "Ensure the Core Server is running.");
        }
        catch (IOException ex)
        {
            _pipeClient.Dispose();
            _pipeClient = null;
            throw new InvalidOperationException($"Failed to connect to Named Pipe '{_pipeName}': {ex.Message}", ex);
        }

        // Create JSON-RPC connection
        var messageHandler = new NewLineDelimitedMessageHandler(_pipeClient, _pipeClient, _formatter);
        _jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler);

        // Enable tracing for debugging (can be disabled in production)
        _jsonRpc.TraceSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;

        // Handle disconnection
        _jsonRpc.Disconnected += (sender, args) =>
        {
            Console.Error.WriteLine($"[NamedPipeRpcClient] Disconnected: {args.Reason}");
        };

        _jsonRpc.StartListening();
        Console.Error.WriteLine("[NamedPipeRpcClient] JSON-RPC session started");
    }

    /// <summary>
    /// Disconnect from the Named Pipe server
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_jsonRpc != null)
        {
            Console.Error.WriteLine("[NamedPipeRpcClient] Disconnecting...");

            try
            {
                // Dispose JSON-RPC connection gracefully
                _jsonRpc.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[NamedPipeRpcClient] Error during disconnect: {ex.Message}");
            }

            _jsonRpc = null;
        }

        if (_pipeClient != null)
        {
            try
            {
                _pipeClient.Dispose();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[NamedPipeRpcClient] Error disposing pipe: {ex.Message}");
            }

            _pipeClient = null;
        }

        Console.Error.WriteLine("[NamedPipeRpcClient] Disconnected");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Invoke a remote procedure call and get the result
    /// </summary>
    public async Task<TResult> InvokeAsync<TResult>(string method, object? args = null)
    {
        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");
        }

        try
        {
            if (args != null)
            {
                return await _jsonRpc.InvokeAsync<TResult>(method, args);
            }
            else
            {
                return await _jsonRpc.InvokeAsync<TResult>(method);
            }
        }
        catch (RemoteInvocationException ex)
        {
            // Unwrap remote exception for clearer error messages
            throw new InvalidOperationException($"Remote method '{method}' failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Invoke a remote procedure call without expecting a result
    /// </summary>
    public async Task InvokeAsync(string method, object? args = null)
    {
        if (_jsonRpc == null)
        {
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");
        }

        try
        {
            if (args != null)
            {
                await _jsonRpc.InvokeAsync(method, args);
            }
            else
            {
                await _jsonRpc.InvokeAsync(method);
            }
        }
        catch (RemoteInvocationException ex)
        {
            // Unwrap remote exception for clearer error messages
            throw new InvalidOperationException($"Remote method '{method}' failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get the underlying Named Pipe stream for direct forwarding
    /// </summary>
    public NamedPipeClientStream? GetPipeStream() => _pipeClient;

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisconnectAsync().GetAwaiter().GetResult();
        _disposed = true;
    }
}
