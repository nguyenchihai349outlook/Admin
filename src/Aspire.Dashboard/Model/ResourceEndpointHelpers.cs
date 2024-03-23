// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Aspire.Dashboard.Model;

internal static class ResourceEndpointHelpers
{
    /// <summary>
    /// A resource has services and endpoints. These can overlap. This method attempts to return a single list without duplicates.
    /// </summary>
    public static List<DisplayedEndpoint> GetEndpoints(ResourceViewModel resource)
    {
        return (from u in resource.Urls
                let uri = new Uri(u.Url)
                select new DisplayedEndpoint
                {
                    Name = u.Name,
                    Text = u.Url,
                    Address = uri.Host,
                    Port = uri.Port,
                    Url = uri.Scheme is "http" or "https" ? u.Url : null
                })
                .ToList();
    }
}

[DebuggerDisplay("Name = {Name}, Text = {Text}, Address = {Address}:{Port}, Url = {Url}")]
public sealed class DisplayedEndpoint
{
    public required string Name { get; set; }
    public required string Text { get; set; }
    public string? Address { get; set; }
    public int? Port { get; set; }
    public string? Url { get; set; }
}
