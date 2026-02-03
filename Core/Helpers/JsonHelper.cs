using System.Text.Json;

namespace DataverseMCPToolBox.Helpers;

/// <summary>
/// Centralized JSON serialization options for consistent formatting
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Standard JSON serializer options with camelCase naming policy
    /// Used for consistency between C# (PascalCase) and TypeScript (camelCase)
    /// </summary>
    public static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// JSON serializer options with indentation for human-readable output
    /// </summary>
    public static readonly JsonSerializerOptions IndentedCamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
