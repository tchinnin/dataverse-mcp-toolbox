using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using DataverseMCPToolBox.Extensibility.Abstractions;

namespace DataverseMCPToolBox.Extensibility.Models;

/// <summary>
/// Sealed implementation of IDataverseContext providing access to an authenticated Dataverse connection.
/// </summary>
public sealed class ConnectionContext : IDataverseContext
{
    /// <inheritdoc />
    public IOrganizationServiceAsync2 ServiceClient { get; init; }

    /// <inheritdoc />
    public string ConnectionId { get; init; }

    /// <inheritdoc />
    public string OrganizationUrl { get; init; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Initializes a new instance of ConnectionContext.
    /// </summary>
    /// <param name="serviceClient">The authenticated Dataverse service client</param>
    /// <param name="connectionId">The connection identifier</param>
    /// <param name="organizationUrl">The Dataverse organization URL</param>
    /// <param name="cancellationToken">The cancellation token for the execution context</param>
    public ConnectionContext(
        IOrganizationServiceAsync2 serviceClient,
        string connectionId,
        string organizationUrl,
        CancellationToken cancellationToken = default)
    {
        ServiceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        OrganizationUrl = organizationUrl ?? throw new ArgumentNullException(nameof(organizationUrl));
        CancellationToken = cancellationToken;
    }
}
