using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace DataverseMCPToolBox.Extensibility.Abstractions;

/// <summary>
/// Provides access to an authenticated Dataverse connection and execution context.
/// Passed to tools during execution to perform Dataverse operations.
/// </summary>
public interface IDataverseContext
{
    /// <summary>
    /// Gets the authenticated Dataverse service client for performing operations.
    /// </summary>
    IOrganizationServiceAsync2 ServiceClient { get; }

    /// <summary>
    /// Gets the unique identifier for this connection.
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// Gets the Dataverse organization URL.
    /// </summary>
    string OrganizationUrl { get; }

    /// <summary>
    /// Gets the cancellation token for the current execution context.
    /// Tools should respect this token for long-running operations.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
