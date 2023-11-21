// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Orleans.Server;

internal sealed class OrleansServerSettings
{
    /// <summary>
    /// Gets the cluster membership settings.
    /// </summary>
    public ConnectionSettings? Clustering { get; set; }

    public ConnectionSettings? Reminders { get; set; }

    public Dictionary<string, ConnectionSettings>? GrainStorage { get; set; }

    public Dictionary<string, ConnectionSettings>? Streaming { get; set; }
}
