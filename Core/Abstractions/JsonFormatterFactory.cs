using StreamJsonRpc;
using Newtonsoft.Json.Serialization;

namespace DataverseMCPToolBox.Abstractions;

/// <summary>
/// Factory for creating JSON-RPC message formatters with consistent configuration
/// Ensures TypeScript/C# interoperability with camelCase property names
/// </summary>
public static class JsonFormatterFactory
{
    /// <summary>
    /// Create a JSON message formatter configured with camelCase property names
    /// This ensures compatibility between C# (PascalCase) and TypeScript (camelCase)
    /// </summary>
    /// <returns>Configured JSON message formatter</returns>
    public static JsonMessageFormatter CreateCamelCaseFormatter()
    {
        return new JsonMessageFormatter
        {
            JsonSerializer =
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        };
    }
}
