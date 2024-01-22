// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// Selects endpoints by iterating through the list of endpoints in a round-robin fashion.
/// </summary>
public class RoundRobinServiceEndPointSelector : IServiceEndPointSelector
{
    private uint _next;
    private ServiceEndPointCollection? _endPoints;

    /// <inheritdoc/>
    public void SetEndPoints(ServiceEndPointCollection endPoints)
    {
        _endPoints = endPoints;
    }

    /// <inheritdoc/>
    public bool TryGetEndPoint(object? context, [NotNullWhen(true)] out ServiceEndPoint? endpoint)
    {
        if (_endPoints is not { Count: > 0 } collection)
        {
            endpoint = null;
            return false;
        }

        endpoint = collection[(int)(unchecked(Interlocked.Increment(ref _next) - 1) % collection.Count)];
        return true;
    }
}
