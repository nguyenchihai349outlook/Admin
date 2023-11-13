// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// Represents an Azure Application Insights resource.
/// </summary>
/// <param name="name">Name of the resource.</param>
public class AzureApplicationInsightsResource(string name) : Resource(name), IResourceWithConnectionString, IAzureResource
{
    public string? ConnectionString { get; set; }

    public string? GetConnectionString() => ConnectionString;
}
