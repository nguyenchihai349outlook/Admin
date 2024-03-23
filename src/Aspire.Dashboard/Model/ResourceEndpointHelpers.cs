// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Aspire.Dashboard.Model;

internal static class ResourceEndpointHelpers
{
    /// <summary>
    /// A resource has services and endpoints. These can overlap. This method attempts to return a single list without duplicates.
    /// </summary>
    public static List<DisplayedEndpoint> GetEndpoints(ResourceViewModel resource, bool includeInteralUrls = false)
    {
        static (Uri? Uri, bool IsValid) ParseUri(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? (uri, true) : (null, false);

        return (from u in resource.Urls
                let parsedUri = ParseUri(u.Url)
                let include = (includeInteralUrls && u.IsInternal) || !u.IsInternal
                where parsedUri.IsValid && include
                select new DisplayedEndpoint
                {
                    Name = u.Name,
                    Text = u.Url,
                    Address = parsedUri.Uri!.Host,
                    Port = parsedUri.Uri.Port,
                    Url = parsedUri.Uri.Scheme is "http" or "https" ? u.Url : null
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
