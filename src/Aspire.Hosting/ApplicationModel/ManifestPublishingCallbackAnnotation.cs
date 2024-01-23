// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Publishing;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an annotation that provides a callback to be executed during manifest publishing.
/// </summary>
public class ManifestPublishingCallbackAnnotation(Action<ManifestPublishingContext>? callback) : IResourceAnnotation
{
    /// <summary>
    /// Gets the callback action for publishing the manifest.
    /// </summary>
    public Action<ManifestPublishingContext>? Callback { get; } = callback;
    
    /// <summary>
    /// Represents a <see langword="null"/>-based callback annotation for manifest 
    /// publishing used in scenarios where it's ignored.
    /// </summary>
    public static ManifestPublishingCallbackAnnotation Ignore { get; } = new(null);
}

public record class OperationContext(IDistributedApplicationBuilder Builder, string OperationType)
{
    public const string Run = nameof(Run);
    public const string PublishManifest = nameof(PublishManifest);
};

/// <summary>
/// Represents an annotation that provides a callback to be executed when building the application model.
/// </summary>
public class BuildActionAnnotation(Action<OperationContext> callback) : IResourceAnnotation
{
    /// <summary>
    /// Gets the callback action used during build before running the application.
    /// </summary>
    public Action<OperationContext> Callback { get; } = callback;

    internal bool HasRun { get; set; }
    
    /// <summary>
    /// Represents a callback which performs no action.
    /// </summary>
    public static BuildActionAnnotation Ignore { get; } = new(_ => { });
}

