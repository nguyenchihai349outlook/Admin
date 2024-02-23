// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Dashboard;

/// <summary>
/// Represents an annotation for properties to be displayed on the dashboard.
/// </summary>
public class DashboardPropertiesAnnotation() : IResourceAnnotation
{
    /// <summary>
    /// 
    /// </summary>
    public List<string> Urls { get; } = [];

    /// <summary>
    /// Gets the properties to be displayed on the dashboard.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = [];
}
