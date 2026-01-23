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

            Console.Error.WriteLine("Dataverse MCP ToolBox JSON-RPC Server starting...");

            // Créer le service RPC
            var rpcService = new DataverseMCPToolBoxRpcService();

            // Configurer le formatter JSON avec camelCase pour la compatibilité TypeScript
            var formatter = new JsonMessageFormatter
            {
                JsonSerializer = 
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }
            };

            // Configurer JSON-RPC sur stdin/stdout avec Content-Length headers (format standard)
            var messageHandler = new HeaderDelimitedMessageHandler(
                Console.OpenStandardOutput(), 
                Console.OpenStandardInput(), 
                formatter
            );
            using var jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler, rpcService);
            jsonRpc.StartListening();

            Console.Error.WriteLine("JSON-RPC Server ready and listening on stdin/stdout");

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
