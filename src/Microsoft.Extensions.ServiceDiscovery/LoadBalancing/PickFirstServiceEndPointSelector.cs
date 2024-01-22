// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// A service endpoint selector which always returns the first endpoint in a collection.
/// </summary>
public class PickFirstServiceEndPointSelector : IServiceEndPointSelector
{
    private ServiceEndPointCollection? _endPoints;

    /// <inheritdoc/>
    public bool TryGetEndPoint(object? context, [NotNullWhen(true)] out ServiceEndPoint? endpoint)
    {
        if (_endPoints is not { Count: > 0 } endPoints)
        {
            endpoint = null;
            return false;
        }

        endpoint = endPoints[0];
        return true;
    }

    /// <inheritdoc/>
    public void SetEndPoints(ServiceEndPointCollection endPoints)
    {
        _endPoints = endPoints;
    }
}
