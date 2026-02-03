using System.Diagnostics;
using System.Runtime.InteropServices;
using DataverseMCPToolBox.Bridge.Services;
using DataverseMCPToolBox.Abstractions;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Bridge;

/// <summary>
/// Dataverse MCP ToolBox Bridge - STDIO to Named Pipe Proxy
/// Spawned by GitHub Copilot via MCP configuration
/// Forwards STDIO (MCP protocol) ↔ Named Pipe (Core Server)
/// </summary>
class Program
{
    private const string LogPrefix = "[Bridge]";

    /// <summary>
    /// Entry point for the Bridge application
    /// Reads DATAVERSE_MCP_PIPE_NAME from environment and forwards STDIO ↔ Named Pipe
    /// </summary>
    /// <param name="args">Command line arguments (unused)</param>
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
            string? pipeName = Environment.GetEnvironmentVariable(EnvironmentVariables.PipeName);
            if (string.IsNullOrEmpty(pipeName))
            {
                LogError($"{EnvironmentVariables.PipeName} environment variable not set");
                Console.Error.WriteLine($"{LogPrefix} This variable must be set by the VS Code Extension or MCP configuration");
                Console.Error.WriteLine($"{LogPrefix} Expected format: {EnvironmentVariables.PipeName}=DataverseMCPToolBox-<pid>");
                Environment.Exit(1);
                return;
            }

            Console.Error.WriteLine($"{LogPrefix} Target pipe: {pipeName}");
            
            // Log diagnostics for troubleshooting
            LogDiagnostics(pipeName);

            // Create Named Pipe RPC client
            using var rpcClient = new NamedPipeRpcClient(pipeName);

            // Connect to Core Server in RAW mode (no JSON-RPC initialization)
            // This allows pure stream forwarding without conflict
            Console.Error.WriteLine($"{LogPrefix} Connecting to Core Server...");
            try
            {
                await rpcClient.ConnectRawAsync();
                Console.Error.WriteLine($"{LogPrefix} ✓ Connected to Core Server (RAW mode)");
            }
            catch (TimeoutException ex)
            {
                LogError(ex.Message);
                Console.Error.WriteLine($"{LogPrefix} Make sure the Core Server is running (started by VS Code Extension)");
                Environment.Exit(1);
                return;
            }
            catch (Exception ex)
            {
                LogError($"Failed to connect - {ex.Message}");
                Environment.Exit(1);
                return;
            }

            // Get the underlying pipe stream for direct forwarding
            var pipeStream = rpcClient.GetPipeStream();
            if (pipeStream == null)
            {
                LogError("Failed to get pipe stream");
                Console.Error.WriteLine($"{LogPrefix} This may indicate the connection was not established in RAW mode");
                Console.Error.WriteLine($"{LogPrefix} Check Core Server logs for connection issues");
                Environment.Exit(1);
                return;
            }

            Console.Error.WriteLine($"{LogPrefix} Starting bidirectional forwarding...");
            Console.Error.WriteLine($"{LogPrefix} STDIN → Pipe | Pipe → STDOUT");

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

            Console.Error.WriteLine($"{LogPrefix} Connection closed");
        }
        catch (Exception ex)
        {
            LogError($"Fatal error: {ex.Message}");
            Console.Error.WriteLine($"{LogPrefix} Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Log diagnostic information for troubleshooting connection issues
    /// </summary>
    private static void LogDiagnostics(string pipeName)
    {
        string? tmpDir = Environment.GetEnvironmentVariable(EnvironmentVariables.TmpDir);
        Console.Error.WriteLine($"{LogPrefix} TMPDIR: {tmpDir ?? "(not set)"}");
        
        string tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        Console.Error.WriteLine($"{LogPrefix} Temp path: {tempPath}");
        
        if (PipePathHelper.IsUnix)
        {
            string socketPath = PipePathHelper.GetUnixSocketPath(pipeName);
            Console.Error.WriteLine($"{LogPrefix} Expected socket: {socketPath}");
        }
    }

    /// <summary>
    /// Log an error message with consistent formatting
    /// </summary>
    private static void LogError(string message)
    {
        Console.Error.WriteLine($"{LogPrefix} ✗ ERROR: {message}");
    }

    /// <summary>
    /// Forward data from source stream to destination stream
    /// Used for bidirectional STDIO ↔ Named Pipe forwarding
    /// </summary>
    /// <param name="source">Source stream to read from</param>
    /// <param name="destination">Destination stream to write to</param>
    /// <param name="streamDirection">Direction label for logging (e.g., "STDIN→PIPE")</param>
    private static async Task ForwardStreamAsync(Stream source, Stream destination, string streamDirection)
    {
        try
        {
            var buffer = new byte[NetworkConstants.StreamBufferSize];
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                await destination.FlushAsync();

                // Optional: Log forwarding activity (commented to reduce noise)
                // Console.Error.WriteLine($"{LogPrefix}-{streamDirection} Forwarded {bytesRead} bytes");
            }

            Console.Error.WriteLine($"{LogPrefix}-{streamDirection} Stream ended");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{LogPrefix}-{streamDirection} Error: {ex.Message}");
        }
    }
}
