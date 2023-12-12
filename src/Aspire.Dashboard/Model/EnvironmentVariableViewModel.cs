// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

public sealed class EnvironmentVariableViewModel : IEquatable<EnvironmentVariableViewModel>
{
    public required string Name { get; init; }
    public string? Value { get; init; }
    public bool IsValueMasked { get; set; } = true;
    public bool FromSpec { get; set; }

    public bool Equals(EnvironmentVariableViewModel? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(Name, other.Name)
            && StringComparer.Ordinal.Equals(Value, other.Value)
            && FromSpec == other.FromSpec;
    }
}
