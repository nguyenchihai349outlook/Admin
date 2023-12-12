// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Aspire.Dashboard.Extensions;

namespace Aspire.Dashboard.Model;

/// <summary>
/// Base class for immutable snapshots of resource state at a point in time.
/// </summary>
public abstract class ResourceViewModel
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Uid { get; init; }
    public required string? State { get; init; }
    public required DateTime? CreationTimeStamp { get; init; }
    public required ImmutableArray<EnvironmentVariableViewModel> Environment { get; init; }
    public required ILogSource LogSource { get; init; }
    public required ImmutableArray<string> Endpoints { get; init; }
    public required ImmutableArray<ResourceServiceSnapshot> Services { get; init; }
    public required int? ExpectedEndpointsCount { get; init; }

    public abstract string ResourceType { get; }

    public static string GetResourceName(ResourceViewModel resource, IEnumerable<ResourceViewModel> allResources)
    {
        var count = 0;
        foreach (var item in allResources)
        {
            if (item.DisplayName == resource.DisplayName)
            {
                count++;
                if (count >= 2)
                {
                    return ResourceFormatter.GetName(resource.DisplayName, resource.Uid);
                }
            }
        }

        return resource.DisplayName;
    }

    internal virtual bool MatchesFilter(string filter)
    {
        return Name.Contains(filter, StringComparisons.UserTextSearch);
    }

    public override bool Equals(object? obj)
    {
        return obj is ResourceViewModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public virtual bool Equals(ResourceViewModel? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(State, other.State)
            && StringComparer.Ordinal.Equals(Uid, other.Uid)
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && CreationTimeStamp == other.CreationTimeStamp
            && ExpectedEndpointsCount == other.ExpectedEndpointsCount
            && Environment.SequenceEqual(other.Environment)
            && Endpoints.SequenceEqual(other.Endpoints, StringComparer.Ordinal)
            && Services.SequenceEqual(other.Services);
    }
}

public sealed class ResourceServiceSnapshot(string name, string? allocatedAddress, int? allocatedPort) : IEquatable<ResourceServiceSnapshot>
{
    public string Name { get; } = name;
    public string? AllocatedAddress { get; } = allocatedAddress;
    public int? AllocatedPort { get; } = allocatedPort;

    public string AddressAndPort { get; } = $"{allocatedAddress}:{allocatedPort}";

    public bool Equals(ResourceServiceSnapshot? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && StringComparer.Ordinal.Equals(AllocatedAddress, other.AllocatedAddress)
            && AllocatedPort == other.AllocatedPort;
    }
}
