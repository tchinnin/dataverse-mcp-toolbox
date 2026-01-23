using System.Reflection;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;
using DataverseMCPToolBox.Extensibility.Exceptions;
using DataverseMCPToolBox.Extensibility.Helpers;
using DataverseMCPToolBox.Extensibility.Models;
using NJsonSchema;
using Newtonsoft.Json.Linq;

namespace DataverseMCPToolBox.Extensibility;

/// <summary>
/// Abstract base class for implementing MCP plugins.
/// Provides automatic tool discovery from [McpTool] decorated methods and plugin lifecycle management.
/// </summary>
public abstract class PluginBase : IPlugin, IToolProvider
{
    private bool _disposed;
    private List<IMcpTool>? _tools;

    /// <summary>
    /// Gets the service provider for resolving dependencies.
    /// Available after InitializeAsync is called.
    /// </summary>
    protected IServiceProvider? Services { get; private set; }

    /// <summary>
    /// Initializes the plugin with the provided service provider.
    /// Override this method to perform custom initialization logic.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="cancellationToken">Cancellation token for initialization</param>
    public virtual Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Console.Error.WriteLine($"[{GetType().Name}] Plugin initialized");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the collection of MCP tools provided by this plugin.
    /// Automatically discovers methods decorated with [McpTool] attribute.
    /// </summary>
    public virtual IEnumerable<IMcpTool> GetTools()
    {
        if (_tools != null)
            return _tools;

        _tools = new List<IMcpTool>();

        var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods)
        {
            var toolAttribute = method.GetCustomAttribute<McpToolAttribute>();
            if (toolAttribute == null)
                continue;

            try
            {
                // Determine tool name (use attribute name or convert method name to kebab-case)
                var toolName = !string.IsNullOrWhiteSpace(toolAttribute.Name)
                    ? toolAttribute.Name
                    : SchemaGenerator.ToKebabCase(method.Name);

                // Validate kebab-case
                if (!SchemaGenerator.IsValidKebabCase(toolName))
                {
                    Console.Error.WriteLine($"[{GetType().Name}] Invalid tool name '{toolName}' for method {method.Name}. Tool names must be in kebab-case format. Skipping.");
                    continue;
                }

                // Generate schema from method parameters
                var schema = SchemaGenerator.GenerateSchemaFromMethod(method);

                // Create tool wrapper
                var tool = new MethodTool(toolName, toolAttribute.Description, schema, method, this);
                _tools.Add(tool);

                Console.Error.WriteLine($"[{GetType().Name}] Registered tool: {toolName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{GetType().Name}] Error registering tool from method {method.Name}: {ex.Message}");
            }
        }

        return _tools;
    }

    /// <summary>
    /// Disposes the plugin and releases resources.
    /// Override this method to perform custom cleanup logic.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
            return;

        Console.Error.WriteLine($"[{GetType().Name}] Plugin disposed");
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal helper class that wraps a [McpTool] decorated method as an IMcpTool.
    /// </summary>
    private class MethodTool : IMcpTool
    {
        private readonly MethodInfo _method;
        private readonly object _target;

        public string Name { get; }
        public string Description { get; }
        public JsonSchema InputSchema { get; }

        public MethodTool(string name, string description, JsonSchema inputSchema, MethodInfo method, object target)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            _method = method;
            _target = target;
        }

        public async Task<ToolExecutionResult> ExecuteAsync(
            string parametersJson,
            IDataverseContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Parse JSON parameters
                var jsonObject = string.IsNullOrWhiteSpace(parametersJson)
                    ? new JObject()
                    : JObject.Parse(parametersJson);

                // Build method parameters
                var methodParameters = _method.GetParameters();
                var args = new object?[methodParameters.Length];

                for (int i = 0; i < methodParameters.Length; i++)
                {
                    var param = methodParameters[i];

                    // Handle special parameters
                    if (typeof(IDataverseContext).IsAssignableFrom(param.ParameterType))
                    {
                        args[i] = context;
                    }
                    else if (param.ParameterType == typeof(CancellationToken))
                    {
                        args[i] = cancellationToken;
                    }
                    else
                    {
                        // Extract from JSON (camelCase property name)
                        var propertyName = ToCamelCase(param.Name ?? param.ParameterType.Name);
                        var token = jsonObject[propertyName];

                        if (token != null)
                        {
                            args[i] = token.ToObject(param.ParameterType);
                        }
                        else if (param.IsOptional)
                        {
                            args[i] = param.DefaultValue;
                        }
                        else
                        {
                            return ToolExecutionResult.Failure(new ToolError
                            {
                                Code = "VALIDATION_ERROR",
                                Message = $"Required parameter '{propertyName}' is missing"
                            });
                        }
                    }
                }

                // Invoke method
                var result = _method.Invoke(_target, args);

                // Handle async methods
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);

                    // Extract result from Task<T>
                    var resultProperty = task.GetType().GetProperty("Result");
                    var taskResult = resultProperty?.GetValue(task);
                    return ToolExecutionResult.Success(taskResult);
                }

                return ToolExecutionResult.Success(result);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ToolExecutionException toolEx)
            {
                return ToolExecutionResult.Failure(new ToolError
                {
                    Code = toolEx.ErrorCode,
                    Message = toolEx.Message,
                    Details = toolEx.ErrorDetails
                });
            }
            catch (TargetInvocationException ex)
            {
                return ToolExecutionResult.Failure(ex.InnerException ?? ex);
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Failure(ex);
            }
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length == 1)
                return value.ToLowerInvariant();

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}
