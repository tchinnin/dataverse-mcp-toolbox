using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Exceptions;
using DataverseMCPToolBox.Extensibility.Helpers;
using DataverseMCPToolBox.Extensibility.Models;
using NJsonSchema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DataverseMCPToolBox.Extensibility;

/// <summary>
/// Generic abstract base class for implementing strongly-typed MCP tools.
/// Handles parameter deserialization, schema generation, and result wrapping.
/// Tool names are validated to enforce kebab-case convention.
/// </summary>
/// <typeparam name="TInput">The input parameter type for the tool</typeparam>
/// <typeparam name="TOutput">The output result type from the tool</typeparam>
public abstract class McpToolBase<TInput, TOutput> : IMcpTool
{
    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public JsonSchema InputSchema { get; }

    /// <summary>
    /// Initializes a new instance of McpToolBase with the specified name and description.
    /// Tool name must be in kebab-case format (e.g., "list-entities", "create-record").
    /// </summary>
    /// <param name="name">The unique name of the tool in kebab-case format</param>
    /// <param name="description">The description of what the tool does</param>
    /// <exception cref="ArgumentException">Thrown when tool name is not in kebab-case format</exception>
    protected McpToolBase(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool name cannot be null or empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Tool description cannot be null or empty", nameof(description));

        // Enforce kebab-case naming convention
        if (!SchemaGenerator.IsValidKebabCase(name))
        {
            throw new ArgumentException(
                $"Tool name '{name}' must be in kebab-case format (lowercase letters, numbers, and hyphens only). " +
                $"Example: 'list-entities', 'create-record'",
                nameof(name));
        }

        Name = name;
        Description = description;
        InputSchema = SchemaGenerator.GenerateSchema<TInput>();

        Console.Error.WriteLine($"[{GetType().Name}] Tool '{name}' initialized");
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteAsync(
        string parametersJson,
        IDataverseContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Deserialize and validate parameters
            TInput? parameters;
            try
            {
                parameters = string.IsNullOrWhiteSpace(parametersJson)
                    ? default
                    : JsonConvert.DeserializeObject<TInput>(parametersJson, _jsonSettings);

                // Validate against schema
                var errors = InputSchema.Validate(parametersJson ?? "{}");
                if (errors.Count > 0)
                {
                    return ToolExecutionResult.Failure(new ToolError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = "Input parameters failed schema validation",
                        Details = errors.Select(e => new { e.Property, e.Kind, e.Path }).ToList()
                    });
                }
            }
            catch (JsonException ex)
            {
                return ToolExecutionResult.Failure(new ToolError
                {
                    Code = "VALIDATION_ERROR",
                    Message = $"Failed to parse input parameters: {ex.Message}"
                });
            }

            // Execute tool logic
            var result = await ExecuteAsync(parameters!, context, cancellationToken).ConfigureAwait(false);

            return ToolExecutionResult.Success(result);
        }
        catch (ToolExecutionException ex)
        {
            return ToolExecutionResult.Failure(new ToolError
            {
                Code = ex.ErrorCode,
                Message = ex.Message,
                Details = ex.ErrorDetails
            });
        }
        catch (OperationCanceledException)
        {
            return ToolExecutionResult.Failure(new ToolError
            {
                Code = "OPERATION_CANCELLED",
                Message = "Tool execution was cancelled"
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{GetType().Name}] Unexpected error in tool '{Name}': {ex}");
            return ToolExecutionResult.Failure(ex);
        }
    }

    /// <summary>
    /// Executes the tool logic with strongly-typed parameters.
    /// Override this method to implement the tool's functionality.
    /// </summary>
    /// <param name="parameters">The deserialized and validated input parameters</param>
    /// <param name="context">The Dataverse connection context</param>
    /// <param name="cancellationToken">Cancellation token to abort long-running operations</param>
    /// <returns>The tool's output result</returns>
    protected abstract Task<TOutput> ExecuteAsync(
        TInput parameters,
        IDataverseContext context,
        CancellationToken cancellationToken);
}
