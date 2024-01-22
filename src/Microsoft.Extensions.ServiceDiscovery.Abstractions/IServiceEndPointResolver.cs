// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// Functionality for resolving endpoints for a service.
/// </summary>
public interface IServiceEndPointResolver : IAsyncDisposable
{
    /// <summary>
    /// Resolves endpoints for the service.
    /// </summary>
    /// <param name="endPoints">The collection which resolved endpoints will be added to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous resolution operation, which contains result of resolution status.</returns>
    ValueTask<ResolutionStatus> ResolveAsync(ServiceEndPointCollectionSource endPoints, CancellationToken cancellationToken);
}
