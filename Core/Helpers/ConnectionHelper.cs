using Microsoft.PowerPlatform.Dataverse.Client;
using DataverseMCPToolBox.Models;

namespace DataverseMCPToolBox.Helpers;

/// <summary>
/// Helper methods for common connection validation patterns
/// </summary>
public static class ConnectionHelper
{
    /// <summary>
    /// Validate that a ServiceClient exists and is ready for operations
    /// Returns appropriate error result if validation fails
    /// </summary>
    /// <param name="serviceClient">The ServiceClient to validate</param>
    /// <param name="connectionId">Connection ID for error messages</param>
    /// <returns>Null if valid, otherwise ToolCallResult with error</returns>
    public static ToolCallResult? ValidateConnection(ServiceClient? serviceClient, string connectionId)
    {
        if (serviceClient == null)
        {
            return ToolCallResult.ConnectionNotFound(connectionId);
        }

        if (!serviceClient.IsReady)
        {
            return ToolCallResult.ConnectionNotReady(connectionId);
        }

        return null; // Connection is valid
    }
}
