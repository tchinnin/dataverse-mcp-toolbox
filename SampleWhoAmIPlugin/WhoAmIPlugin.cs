using DataverseMCPToolBox.Extensibility;
using DataverseMCPToolBox.Extensibility.Abstractions;
using DataverseMCPToolBox.Extensibility.Attributes;
using DataverseMCPToolBox.Extensibility.Exceptions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using WhoAmI.Models;

namespace WhoAmI;

/// <summary>
/// Plugin that provides user information retrieval from Dataverse.
/// Provides a "who-am-i" tool to retrieve current user information.
/// </summary>
[McpPlugin("whoami", "1.0.0",
    Author = "Théophile CHIN-NIN",
    Description = "Dataverse user information retrieval using the WhoAmI operation")]
public class WhoAmIPlugin : PluginBase
{
    /// <summary>
    /// Initializes the plugin with the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies</param>
    /// <param name="cancellationToken">Cancellation token for initialization</param>
    public override Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("[WhoAmIPlugin] Initializing WhoAmI Plugin v1.0.0");
        return base.InitializeAsync(serviceProvider, cancellationToken);
    }

    /// <summary>
    /// Returns all MCP tools provided by this plugin.
    /// Combines attribute-discovered tools with explicitly created tools.
    /// </summary>
    public override IEnumerable<IMcpTool> GetTools()
    {
        // Get tools discovered from [McpTool] attributes
        var attributeTools = base.GetTools();

        // Add strongly-typed tools explicitly
        var customTools = new IMcpTool[]
        {
            new WhoAmITool()
        };

        return attributeTools.Concat(customTools);
    }

    /// <summary>
    /// Disposes the plugin and releases resources.
    /// </summary>
    public override void Dispose()
    {
        Console.Error.WriteLine("[WhoAmIPlugin] Disposing WhoAmI Plugin");
        base.Dispose();
    }
}

/// <summary>
/// Strongly-typed tool that retrieves the current user's information from Dataverse.
/// Uses the WhoAmI message to get user ID, business unit ID, and organization ID,
/// then retrieves additional user details like display name.
/// </summary>
public class WhoAmITool : McpToolBase<object, WhoAmIOutput>
{
    public WhoAmITool()
        : base("who-am-i", "Retrieves information about the currently connected Dataverse user including display name, user ID, business unit ID, organization ID, and environment URL")
    {
    }

    /// <summary>
    /// Executes the who-am-i tool to retrieve user information.
    /// </summary>
    /// <param name="parameters">Input parameters (none required for this tool)</param>
    /// <param name="context">Dataverse context with authenticated service client</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>User information including display name, IDs, and environment URL</returns>
    protected override async Task<WhoAmIOutput> ExecuteAsync(
        object? parameters,
        IDataverseContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.Error.WriteLine("[WhoAmITool] Executing who-am-i tool");

            // Execute WhoAmI request to get basic user information
            var whoAmIRequest = new WhoAmIRequest();
            var whoAmIResponse = (WhoAmIResponse)await context.ServiceClient
                .ExecuteAsync(whoAmIRequest, cancellationToken);

            Console.Error.WriteLine($"[WhoAmITool] Retrieved user ID: {whoAmIResponse.UserId}");

            // Retrieve the user's full name from the systemuser table
            string userDisplayName = "Unknown User";
            try
            {
                var userEntity = await context.ServiceClient.RetrieveAsync(
                    "systemuser",
                    whoAmIResponse.UserId,
                    new ColumnSet("fullname"),
                    cancellationToken);

                userDisplayName = userEntity.GetAttributeValue<string>("fullname") ?? "Unknown User";
                Console.Error.WriteLine($"[WhoAmITool] Retrieved user display name: {userDisplayName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WhoAmITool] Warning: Could not retrieve user display name: {ex.Message}");
                // Continue with default display name
            }

            // Build the output
            var output = new WhoAmIOutput
            {
                UserDisplayName = userDisplayName,
                UserId = whoAmIResponse.UserId,
                BusinessUnitId = whoAmIResponse.BusinessUnitId,
                OrganizationId = whoAmIResponse.OrganizationId,
                EnvironmentUrl = context.OrganizationUrl,
                Message = $"Successfully retrieved information for user '{userDisplayName}'"
            };

            Console.Error.WriteLine($"[WhoAmITool] Successfully completed who-am-i operation");
            return output;
        }
        catch (ToolExecutionException)
        {
            // Re-throw structured errors as-is
            throw;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[WhoAmITool] Operation was cancelled");
            throw new ToolExecutionException(
                "OPERATION_CANCELLED",
                "The who-am-i operation was cancelled");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WhoAmITool] Error executing who-am-i: {ex.Message}");
            throw new ToolExecutionException(
                "DATAVERSE_ERROR",
                $"Failed to retrieve user information: {ex.Message}",
                new { environmentUrl = context.OrganizationUrl },
                ex);
        }
    }
}
