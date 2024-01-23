// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a resource that can be hosted by an application.
/// </summary>
public interface IResource
{
    /// <summary>
    /// Gets the name of the resource.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the annotations associated with the resource.
    /// </summary>
    ResourceMetadataCollection Annotations { get; }
}

public interface IAbstractResource : IResource
{
    IResource? Implementation { get; }
}

public interface IAbstractResource<TImplementation> : IAbstractResource where TImplementation : IResource
{
    IResource? IAbstractResource.Implementation => Implementation;
    new TImplementation? Implementation { get; set; }
}

public static class AbstractResourceExtensions
{
    public static IResource? GetImplementationResource<TResource>(this IResourceBuilder<TResource> builder) where TResource : IResource => builder.Resource is IAbstractResource abstractResource ? abstractResource.Implementation : builder.Resource;
    public static TImplementation? GetImplementationResource<TImplementation>(this IResourceBuilder<IAbstractResource<TImplementation>> builder) where TImplementation : IResource => builder.Resource.Implementation;
    public static TImplementation? GetImplementation<TImplementation>(this IAbstractResource<TImplementation> resource) where TImplementation : IResource => resource.Implementation;
    public static bool HasImplementationResource<TImplementation>(this IResourceBuilder<IAbstractResource<TImplementation>> builder) where TImplementation : IResource => builder.Resource.HasImplementation();
    public static bool HasImplementation<TImplementation>(this IAbstractResource<TImplementation> resource) where TImplementation : IResource => resource.Implementation is not null;
    public static IResourceBuilder<TResource> WithDefault<TResource>(this IResourceBuilder<TResource> builder, Action<IResourceBuilder<TResource>> action) where TResource : IAbstractResource<TResource>
    {
        builder.WithBuildCallback(context =>
        {
            if (builder.Resource.Implementation is null)
            {
                action(builder);
            }
        });

        return builder;
    }
}
