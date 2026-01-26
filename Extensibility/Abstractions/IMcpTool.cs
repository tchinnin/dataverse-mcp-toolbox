using DataverseMCPToolBox.Extensibility.Models;
using NJsonSchema;

namespace DataverseMCPToolBox.Extensibility.Abstractions;

/// <summary>
/// Defines the contract for an MCP tool that can be discovered and invoked by AI assistants.
/// Tools represent discrete Dataverse operations exposed through the MCP protocol.
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Gets the unique name of the tool in kebab-case format (e.g., "list-entities", "create-record").
    /// This name is used by AI assistants to identify and invoke the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the human-readable description of what the tool does.
    /// This description is shown to AI assistants to help them understand when to use the tool.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON Schema that defines the structure of input parameters for this tool.
    /// The schema is used for validation and to inform AI assistants about required/optional parameters.
    /// </summary>
    JsonSchema InputSchema { get; }

    /// <summary>
    /// Executes the tool with the provided parameters and Dataverse context.
    /// </summary>
    /// <param name="parametersJson">JSON string containing the tool parameters matching the InputSchema</param>
    /// <param name="context">Dataverse connection context for performing operations</param>
    /// <param name="cancellationToken">Cancellation token to abort long-running operations</param>
    /// <returns>Tool execution result containing success status and output content or error details</returns>
    Task<ToolExecutionResult> ExecuteAsync(
        string parametersJson, 
        IDataverseContext context, 
        CancellationToken cancellationToken = default);
}
