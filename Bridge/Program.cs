using System.Diagnostics;
using DataverseMCPToolBox.Bridge.Services;

namespace DataverseMCPToolBox.Bridge;

/// <summary>
/// Dataverse MCP ToolBox Bridge - STDIO to Named Pipe Proxy
/// Spawned by GitHub Copilot via MCP configuration
/// Forwards STDIO (MCP protocol) ↔ Named Pipe (Core Server)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Redirect logs to stderr to avoid polluting stdio
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
        Trace.AutoFlush = true;

        Console.Error.WriteLine("========================================");
        Console.Error.WriteLine("Dataverse MCP ToolBox Bridge");
        Console.Error.WriteLine("Architecture: STDIO ↔ Named Pipe Proxy");
        Console.Error.WriteLine("========================================");

        try
        {
            // Read pipe name from environment variable (REQUIRED)
            string? pipeName = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PIPE_NAME");
            if (string.IsNullOrEmpty(pipeName))
            {
                Console.Error.WriteLine("[Bridge] ERROR: DATAVERSE_MCP_PIPE_NAME environment variable not set");
                Console.Error.WriteLine("[Bridge] This variable must be set by the VS Code Extension or MCP configuration");
                Console.Error.WriteLine("[Bridge] Expected format: DATAVERSE_MCP_PIPE_NAME=DataverseMCPToolBox-<pid>");
                Environment.Exit(1);
                return;
            }

            Console.Error.WriteLine($"[Bridge] Target pipe: {pipeName}");

            // Create Named Pipe RPC client
            using var rpcClient = new NamedPipeRpcClient(pipeName, TimeSpan.FromSeconds(10));

            // Connect to Core Server
            Console.Error.WriteLine("[Bridge] Connecting to Core Server...");
            try
            {
                await rpcClient.ConnectAsync();
                Console.Error.WriteLine("[Bridge] ✓ Connected to Core Server");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"[Bridge] ERROR: {ex.Message}");
                Console.Error.WriteLine("[Bridge] Make sure the Core Server is running (started by VS Code Extension)");
                Environment.Exit(1);
                return;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Bridge] ERROR: Failed to connect - {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Get the underlying pipe stream for direct forwarding
            var pipeStream = rpcClient.GetPipeStream();
            if (pipeStream == null)
            {
                Console.Error.WriteLine("[Bridge] ERROR: Failed to get pipe stream");
                Environment.Exit(1);
                return;
            }

            Console.Error.WriteLine("[Bridge] Starting bidirectional forwarding...");
            Console.Error.WriteLine("[Bridge] STDIN → Pipe | Pipe → STDOUT");

            // Create bidirectional forwarding tasks
            var stdinToPipe = ForwardStreamAsync(
                Console.OpenStandardInput(),
                pipeStream,
                "STDIN→PIPE"
            );

            var pipeToStdout = ForwardStreamAsync(
                pipeStream,
                Console.OpenStandardOutput(),
                "PIPE→STDOUT"
            );

            // Wait for either direction to complete (disconnection)
            await Task.WhenAny(stdinToPipe, pipeToStdout);

            Console.Error.WriteLine("[Bridge] Connection closed");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Bridge] Fatal error: {ex.Message}");
            Console.Error.WriteLine($"[Bridge] Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Forward data from source stream to destination stream
    /// Used for bidirectional STDIO ↔ Named Pipe forwarding
    /// </summary>
    private static async Task ForwardStreamAsync(Stream source, Stream destination, string label)
    {
        try
        {
            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                await destination.FlushAsync();

                // Optional: Log forwarding activity (commented to reduce noise)
                // Console.Error.WriteLine($"[Bridge-{label}] Forwarded {bytesRead} bytes");
            }

            Console.Error.WriteLine($"[Bridge-{label}] Stream ended");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Bridge-{label}] Error: {ex.Message}");
        }
    }
}
