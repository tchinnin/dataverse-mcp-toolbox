using System.IO.Pipes;
using System.Runtime.InteropServices;
using StreamJsonRpc;
using Newtonsoft.Json.Serialization;
using DataverseMCPToolBox.Abstractions;

namespace DataverseMCPToolBox.Services;

/// <summary>
/// Named Pipe RPC Server implementation
/// Accepts multiple concurrent connections (Extension + Bridge(s))
/// Platform-agnostic (Windows, macOS, Linux)
/// </summary>
public class NamedPipeRpcServer : IRpcServer
{
    private readonly string _pipeName;
    private readonly List<object> _services = new();
    private readonly List<Task> _clientTasks = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private readonly JsonMessageFormatter _formatter;

    /// <summary>
    /// Create a Named Pipe RPC server
    /// </summary>
    /// <param name="pipeName">Pipe name (without platform-specific prefix)</param>
    public NamedPipeRpcServer(string pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be null or empty", nameof(pipeName));
        }

        _pipeName = pipeName;

        // Configure JSON formatter with camelCase for TypeScript compatibility
        _formatter = new JsonMessageFormatter
        {
            JsonSerializer =
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        };

        Console.Error.WriteLine($"[NamedPipeRpcServer] Created with pipe name: {_pipeName}");
    }

    /// <summary>
    /// Register a service to be exposed via RPC
    /// Services must be registered before calling StartAsync()
    /// </summary>
    public void RegisterService(object service)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        _services.Add(service);
        Console.Error.WriteLine($"[NamedPipeRpcServer] Registered service: {service.GetType().Name}");
    }

    /// <summary>
    /// Start the Named Pipe server and begin accepting connections
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_acceptLoopTask != null)
        {
            throw new InvalidOperationException("Server is already running");
        }

        if (_services.Count == 0)
        {
            throw new InvalidOperationException("No services registered. Call RegisterService() before StartAsync()");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Console.Error.WriteLine($"[NamedPipeRpcServer] Starting server on pipe: {_pipeName}");
        Console.Error.WriteLine($"[NamedPipeRpcServer] Platform: {GetPlatformName()}");
        Console.Error.WriteLine($"[NamedPipeRpcServer] Registered services: {_services.Count}");
        
        // Log temp directory and expected socket path for diagnostics
        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        Console.Error.WriteLine($"[NamedPipeRpcServer] Path.GetTempPath() = {tempPath}");
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // NamedPipeServerStream automatically adds "CoreFxPipe_" prefix on Unix
            var expectedSocketPath = Path.Combine(tempPath, $"CoreFxPipe_{_pipeName}");
            Console.Error.WriteLine($"[NamedPipeRpcServer] Expected Unix socket path: {expectedSocketPath}");
            Console.Error.WriteLine($"[NamedPipeRpcServer] Socket path length: {expectedSocketPath.Length} chars (limit: 104)");
        }
        
        Console.Error.WriteLine("========================================");
        Console.Error.WriteLine("Server ready - waiting for Named Pipe connections...");
        Console.Error.WriteLine("========================================");

        // Start accept loop in background
        _acceptLoopTask = AcceptConnectionsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the server and close all active connections
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null || _acceptLoopTask == null)
        {
            Console.Error.WriteLine("[NamedPipeRpcServer] Server not running");
            return;
        }

        Console.Error.WriteLine("[NamedPipeRpcServer] Stopping server...");

        // Cancel the accept loop
        _cts.Cancel();

        // Wait for accept loop to complete
        try
        {
            await _acceptLoopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
        }

        // Wait for all client connections to complete
        Console.Error.WriteLine($"[NamedPipeRpcServer] Waiting for {_clientTasks.Count} client(s) to disconnect...");
        await Task.WhenAll(_clientTasks.Where(t => !t.IsCompleted));

        Console.Error.WriteLine("[NamedPipeRpcServer] Server stopped");

        _cts.Dispose();
        _cts = null;
        _acceptLoopTask = null;
        _clientTasks.Clear();
    }

    /// <summary>
    /// Accept connections loop - runs until cancelled
    /// </summary>
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Create a new Named Pipe server stream for each connection
                // On Unix, this creates a socket file at Path.GetTempPath()/CoreFxPipe_{pipeName}
                var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // Verify socket file was created on Unix
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
                    var socketPath = Path.Combine(tempPath, $"CoreFxPipe_{_pipeName}");
                    var socketExists = File.Exists(socketPath);
                    Console.Error.WriteLine($"[NamedPipeRpcServer] Socket file created: {socketPath}");
                    Console.Error.WriteLine($"[NamedPipeRpcServer] Socket file exists: {socketExists}");
                }

                Console.Error.WriteLine("[NamedPipeRpcServer] Waiting for client connection...");

                // Wait for client connection with cancellation support
                try
                {
                    await pipeServer.WaitForConnectionAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    pipeServer.Dispose();
                    Console.Error.WriteLine("[NamedPipeRpcServer] Connection wait cancelled (shutdown)");
                    break;
                }
                catch (IOException ex)
                {
                    // Handle pipe errors (e.g., pipe broken, already exists)
                    Console.Error.WriteLine($"[NamedPipeRpcServer] Error waiting for connection: {ex.Message}");
                    pipeServer.Dispose();
                    
                    // Wait a bit before retrying
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                Console.Error.WriteLine("[NamedPipeRpcServer] New client connected");

                // Handle this client in a separate task
                var clientTask = HandleClientAsync(pipeServer, cancellationToken);
                _clientTasks.Add(clientTask);

                // Clean up completed tasks to prevent memory leak
                _clientTasks.RemoveAll(t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[NamedPipeRpcServer] Accept loop cancelled");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NamedPipeRpcServer] Fatal error in accept loop: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Handle a single client connection
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            Console.Error.WriteLine($"[Client-{clientId}] Starting JSON-RPC session");

            // Create message handler with newline-delimited protocol
            var messageHandler = new NewLineDelimitedMessageHandler(pipeServer, pipeServer, _formatter);

            using var jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler);

            // Register all services with this client connection
            foreach (var service in _services)
            {
                jsonRpc.AddLocalRpcTarget(service, new JsonRpcTargetOptions
                {
                    NotifyClientOfEvents = false
                });
            }

            jsonRpc.StartListening();
            Console.Error.WriteLine($"[Client-{clientId}] JSON-RPC session active");

            // Wait for completion or cancellation
            await Task.WhenAny(jsonRpc.Completion, Task.Delay(-1, cancellationToken));

            Console.Error.WriteLine($"[Client-{clientId}] Session ended");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Client-{clientId}] Error: {ex.Message}");
        }
        finally
        {
            pipeServer.Dispose();
            Console.Error.WriteLine($"[Client-{clientId}] Connection closed");
        }
    }

    /// <summary>
    /// Get platform name for logging
    /// </summary>
    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }
        else
        {
            return "Unknown";
        }
    }
}
