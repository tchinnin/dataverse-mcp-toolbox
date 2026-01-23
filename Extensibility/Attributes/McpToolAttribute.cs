namespace DataverseMCPToolBox.Extensibility.Attributes;

/// <summary>
/// Attribute to mark a method in a PluginBase-derived class as an MCP tool.
/// Used for automatic tool discovery and registration.
/// The method must return Task&lt;object&gt; and accept parameters matching the tool's input schema.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique name of the tool in kebab-case format.
    /// If not specified, the method name will be converted to kebab-case.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of what the tool does.
    /// This description is shown to AI assistants to help them understand when to use the tool.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Initializes a new instance of McpToolAttribute.
    /// </summary>
    /// <param name="description">The description of the tool's functionality</param>
    public McpToolAttribute(string description)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }
}
