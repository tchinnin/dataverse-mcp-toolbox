using System.Reflection;
using NJsonSchema;
using NJsonSchema.Generation;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace DataverseMCPToolBox.Extensibility.Helpers;

/// <summary>
/// Utility class for generating JSON schemas from C# types and method signatures.
/// Uses NJsonSchema with camelCase serialization matching the server conventions.
/// </summary>
public static class SchemaGenerator
{
    private static readonly JsonSchemaGenerator _schemaGenerator;

    static SchemaGenerator()
    {
        // Configure schema generator with camelCase naming to match server JSON-RPC conventions
        var settings = new SystemTextJsonSchemaGeneratorSettings
        {
            DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
            GenerateAbstractProperties = true,
            FlattenInheritanceHierarchy = false,
            SerializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }
        };

        _schemaGenerator = new JsonSchemaGenerator(settings);
    }

    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// Properties will be serialized in camelCase.
    /// </summary>
    /// <typeparam name="T">The type to generate schema for</typeparam>
    /// <returns>JSON schema describing the type structure</returns>
    public static JsonSchema GenerateSchema<T>()
    {
        return _schemaGenerator.Generate(typeof(T));
    }

    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// Properties will be serialized in camelCase.
    /// </summary>
    /// <param name="type">The type to generate schema for</param>
    /// <returns>JSON schema describing the type structure</returns>
    public static JsonSchema GenerateSchema(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _schemaGenerator.Generate(type);
    }

    /// <summary>
    /// Generates a JSON schema from a method's parameters.
    /// Useful for auto-generating tool input schemas from [McpTool] decorated methods.
    /// </summary>
    /// <param name="method">The method to analyze</param>
    /// <returns>JSON schema with properties matching method parameters</returns>
    public static JsonSchema GenerateSchemaFromMethod(MethodInfo method)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));

        var schema = new JsonSchema
        {
            Type = JsonObjectType.Object,
            Title = $"{method.Name} Parameters",
            AdditionalPropertiesSchema = null,
            AllowAdditionalProperties = false
        };

        var parameters = method.GetParameters();

        foreach (var parameter in parameters)
        {
            // Skip special parameters (IDataverseContext, CancellationToken)
            if (typeof(Abstractions.IDataverseContext).IsAssignableFrom(parameter.ParameterType) ||
                parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            var parameterSchema = _schemaGenerator.Generate(parameter.ParameterType);
            
            // Convert parameter name to camelCase
            var propertyName = ToCamelCase(parameter.Name ?? parameter.ParameterType.Name);
            
            var property = new JsonSchemaProperty
            {
                Type = parameterSchema.Type,
                Reference = parameterSchema
            };
            schema.Properties.Add(propertyName, property);

            // Mark as required if parameter has no default value
            if (!parameter.IsOptional)
            {
                schema.RequiredProperties.Add(propertyName);
            }
        }

        return schema;
    }

    /// <summary>
    /// Converts a string to camelCase format.
    /// </summary>
    /// <param name="value">The string to convert</param>
    /// <returns>camelCase formatted string</returns>
    public static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Converts a PascalCase or camelCase string to kebab-case format.
    /// Used for tool name normalization.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <returns>kebab-case formatted string</returns>
    public static string ToKebabCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Insert hyphens before uppercase letters (except the first one)
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c))
            {
                result.Append('-');
            }
            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }

    /// <summary>
    /// Validates that a tool name follows kebab-case convention.
    /// </summary>
    /// <param name="toolName">The tool name to validate</param>
    /// <returns>True if the name is valid kebab-case, false otherwise</returns>
    public static bool IsValidKebabCase(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        // Must be lowercase letters, numbers, and hyphens only
        // Cannot start or end with hyphen
        // Cannot have consecutive hyphens
        if (toolName.StartsWith("-") || toolName.EndsWith("-"))
            return false;

        if (toolName.Contains("--"))
            return false;

        return toolName.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-');
    }
}
