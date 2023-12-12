// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Aspire.Dashboard.Extensions;

namespace Aspire.Dashboard.Model;

/// <summary>
/// Immutable snapshot of container state at a point in time.
/// </summary>
public class ContainerViewModel : ResourceViewModel
{
    // IMPORTANT! Be sure to reflect any property changes here in the Equals method below

    public override string ResourceType => "Container";

    public required string? ContainerId { get; init; }
    public required string Image { get; init; }
    public required ImmutableArray<int> Ports { get; init; }
    public required string? Command { get; init; }
    public required ImmutableArray<string>? Args { get; init; }

    internal override bool MatchesFilter(string filter)
    {
        return base.MatchesFilter(filter) || Image.Contains(filter, StringComparisons.UserTextSearch);
    }

    public override bool Equals(ResourceViewModel? other)
    {
        return other is ContainerViewModel container
            && StringComparer.Ordinal.Equals(ContainerId, container.ContainerId)
            && StringComparer.Ordinal.Equals(Image, container.Image)
            && StringComparer.Ordinal.Equals(Command, container.Command)
            && Ports.SequenceEqual(container.Ports)
            && Args is null == container.Args is null
            && (Args is null || Args.Value.SequenceEqual(container.Args!.Value))
            && base.Equals(other);
    }
}
