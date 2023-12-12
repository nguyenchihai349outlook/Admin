// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

/// <summary>
/// Immutable snapshot of project state at a point in time.
/// </summary>
public class ProjectViewModel : ExecutableViewModel
{
    // IMPORTANT! Be sure to reflect any property changes here in the Equals method below

    public override string ResourceType => "Project";

    public required string ProjectPath { get; init; }

    public override bool Equals(ResourceViewModel? other)
    {
        return other is ProjectViewModel project
            && StringComparer.Ordinal.Equals(ProjectPath, project.ProjectPath)
            && base.Equals(other);
    }
}
