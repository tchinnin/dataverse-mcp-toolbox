using DataverseMCPToolBox.JsonRpc;
using DataverseMCPToolBox.Services;
using System.Diagnostics;

namespace DataverseMCPToolBox;

/// <summary>
/// Dataverse MCP ToolBox - Serveur Principal (Sidecar Architecture)
/// Ce serveur gère toutes les opérations Dataverse et expose ses services via Named Pipes
/// - Extension VS Code → Named Pipe Client
/// - Copilot MCP → Bridge (STDIO) → Named Pipe Client
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Rediriger les logs vers stderr pour ne pas polluer stdout
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
            Trace.AutoFlush = true;

            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("Dataverse MCP ToolBox - Core Server");
            Console.Error.WriteLine("Architecture: Sidecar with Named Pipes");
            Console.Error.WriteLine("========================================");

            // Read pipe name from environment variable (required for multi-instance support)
            string pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME")
                ?? "DataverseMCPToolBox"; // Fallback for local testing

            Console.Error.WriteLine($"[Startup] Using Named Pipe: {pipeName}");
            
            // Log TMPDIR to verify it was set correctly by Extension
            string? tmpDir = Environment.GetEnvironmentVariable("TMPDIR");
            Console.Error.WriteLine($"[Startup] TMPDIR environment variable: {tmpDir ?? "(not set)"}");
            
            // Log what Path.GetTempPath() returns (this is read at .NET runtime startup from TMPDIR)
            string tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            Console.Error.WriteLine($"[Startup] Path.GetTempPath() returns: {tempPath}");
            // Initialize plugin directory from environment variable or default path
            string? pluginDirectory = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PLUGIN_DIR");
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                pluginDirectory = Path.Combine(homeDir, ".dataverse-mcp-toolbox", "plugins");
                Console.Error.WriteLine($"[Startup] Using default plugin directory: {pluginDirectory}");
            }
            else
            {
                Console.Error.WriteLine($"[Startup] Using plugin directory from environment: {pluginDirectory}");
            }

            Directory.CreateDirectory(pluginDirectory);

            // Create the RPC service (shared by all connections)
            var rpcService = new DataverseMCPToolBoxRpcService(pluginDirectory);
            Console.Error.WriteLine("✓ RPC service created (singleton for all clients)");

            // Create Named Pipe RPC server
            // Socket location controlled by TMPDIR environment variable (set by Extension before spawn)
            var rpcServer = new NamedPipeRpcServer(pipeName);

            // Register services
            rpcServer.RegisterService(rpcService);
            rpcServer.RegisterService(rpcService.McpProtocolService);

            Console.Error.WriteLine("✓ Services registered with RPC server");

            // Handle shutdown signals
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.Error.WriteLine("\n[Shutdown] Received shutdown signal...");
                cts.Cancel();
            };

            // Start the server
            await rpcServer.StartAsync(cts.Token);

            // Wait for shutdown signal
            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }

            // Stop the server gracefully
            await rpcServer.StopAsync();

            Console.Error.WriteLine("[Shutdown] Server stopped successfully");
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[Shutdown] Server cancelled");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fatal] Error: {ex}");
            Environment.Exit(1);
        }
    }
}
