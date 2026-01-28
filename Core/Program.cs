using StreamJsonRpc;
using DataverseMCPToolBox.JsonRpc;
using System.Diagnostics;
using Newtonsoft.Json.Serialization;

namespace DataverseMCPToolBox;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Rediriger les logs vers stderr pour ne pas polluer stdin/stdout
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Error));
            Trace.AutoFlush = true;

            // UNIFIED INSTANCE ARCHITECTURE:
            // Server ALWAYS uses newline-delimited protocol (required by VS Code MCP and GitHub Copilot)
            // Extension has been updated to use NewlineDelimitedMessageReader/Writer
            // This allows Extension and Copilot to share the SAME server instance
            // Both Management RPC and MCP Protocol services are registered on the same instance

            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("Dataverse MCP ToolBox Server starting...");
            Console.Error.WriteLine("Protocol: Newline-delimited JSON (unified for Extension + Copilot)");
            Console.Error.WriteLine("Architecture: SINGLE INSTANCE - No file-based state sharing needed");
            Console.Error.WriteLine("========================================");

            // Initialize plugin directory from environment variable or default path
            string? pluginDirectory = Environment.GetEnvironmentVariable("DATAVERSE_MCP_PLUGIN_DIR");
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                // Use a default path in user's home directory
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                pluginDirectory = Path.Combine(homeDir, ".dataverse-mcp-toolbox", "plugins");
                Console.Error.WriteLine($"[Startup] Using default plugin directory: {pluginDirectory}");
            }
            else
            {
                Console.Error.WriteLine($"[Startup] Using plugin directory from environment: {pluginDirectory}");
            }

            // Ensure plugin directory exists
            Directory.CreateDirectory(pluginDirectory);

            // Créer le service RPC de gestion avec plugin directory
            var managementService = new DataverseMCPToolBoxRpcService(pluginDirectory);
            Console.Error.WriteLine("✓ Management RPC service created");

            // Configurer le formatter JSON avec camelCase pour la compatibilité TypeScript
            var formatter = new JsonMessageFormatter
            {
                JsonSerializer = 
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }
            };

            // UNIFIED PROTOCOL: Always use newline-delimited JSON
            Console.Error.WriteLine("[Startup] Using newline-delimited JSON protocol");
            var messageHandler = new NewLineDelimitedMessageHandler(
                Console.OpenStandardOutput(), 
                Console.OpenStandardInput(), 
                formatter
            );
            
            using var jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler);
            
            // Register management service (connection management, plugin management)
            jsonRpc.AddLocalRpcTarget(managementService, new JsonRpcTargetOptions
            {
                NotifyClientOfEvents = false
            });
            Console.Error.WriteLine("✓ Management RPC service registered");

            // Store jsonRpc reference in management service for MCP service registration
            // This will register MCP Protocol service (initialize, tools/list, tools/call)
            managementService.SetJsonRpcConnection(jsonRpc);
            Console.Error.WriteLine("✓ MCP Protocol service registered");

            jsonRpc.StartListening();

            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("JSON-RPC Server ready and listening on stdin/stdout");
            Console.Error.WriteLine("Waiting for commands...");
            Console.Error.WriteLine("========================================");

            // Attendre la fin de la connexion
            await jsonRpc.Completion;

            Console.Error.WriteLine("JSON-RPC Server shutting down");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex}");
            Environment.Exit(1);
        }
    }
}
