// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// Selects endpoints from a collection of endpoints.
/// </summary>
public interface IServiceEndPointSelector
{
    /// <summary>
    /// Sets the collection of endpoints which this instance will select from.
    /// </summary>
    /// <param name="endPoints">The collection of endpoints to select from.</param>
    void SetEndPoints(ServiceEndPointCollection endPoints);

    /// <summary>
    /// Selects an endpoints from the collection provided by the most recent call to <see cref="SetEndPoints(ServiceEndPointCollection)"/>.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="endpoint">The selected endpoint.</param>
    /// <returns><see langword="true"/> if an endpoint was available, <see langword="false"/> otherwise.</returns>
    bool TryGetEndPoint(object? context, [NotNullWhen(true)] out ServiceEndPoint? endpoint);
}
