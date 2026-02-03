using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DataverseMCPToolBox.Extensibility.Helpers;

/// <summary>
/// Factory for creating standardized JSON serialization settings
/// Ensures consistent JSON handling across tools and plugins
/// </summary>
public static class JsonSerializationSettings
{
    /// <summary>
    /// Creates JSON serializer settings with camelCase property naming
    /// Ignores null values during serialization
    /// </summary>
    /// <returns>Configured JsonSerializerSettings instance</returns>
    public static JsonSerializerSettings CreateCamelCase()
    {
        return new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Gets a singleton instance of camelCase JSON serializer settings
    /// Use this for performance-critical scenarios to avoid creating new settings each time
    /// </summary>
    public static JsonSerializerSettings CamelCaseSettings { get; } = CreateCamelCase();
}
