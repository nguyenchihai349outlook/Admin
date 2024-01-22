// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// Selects endpoints using the Power of Two Choices algorithm for distributed load balancing based on
/// the last-known load of the candidate endpoints.
/// </summary>
public class PowerOfTwoChoicesServiceEndPointSelector : IServiceEndPointSelector
{
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
            endpoint = default;
            return false;
        }

        if (collection.Count == 1)
        {
            endpoint = collection[0];
            return true;
        }

        var first = collection[Random.Shared.Next(collection.Count)];
        ServiceEndPoint second;
        do
        {
            second = collection[Random.Shared.Next(collection.Count)];
        } while (ReferenceEquals(first, second));

        // Note that this relies on fresh data to be effective.
        if (first.Features.Get<IEndPointLoadFeature>() is { } firstLoad
            && second.Features.Get<IEndPointLoadFeature>() is { } secondLoad)
        {
            endpoint = firstLoad.CurrentLoad < secondLoad.CurrentLoad ? first : second;
            return true;
        }

        // Degrade to random.
        endpoint = first;
        return false;
    }
}
