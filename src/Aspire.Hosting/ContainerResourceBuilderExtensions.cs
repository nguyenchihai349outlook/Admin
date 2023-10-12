// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public static class ContainerResourceBuilderExtensions
{
    public static IDistributedApplicationResourceBuilder<ContainerResource> AddContainer(this IDistributedApplicationBuilder builder, string name, string image)
    {
        return builder.AddContainer(name, image, "latest");
    }

    public static IDistributedApplicationResourceBuilder<ContainerResource> AddContainer(this IDistributedApplicationBuilder builder, string name, string image, string tag)
    {
        var container = new ContainerResource(name);
        return builder.AddResource(container)
                      .WithAnnotation(new ContainerImageAnnotation { Image = image, Tag = tag });
    }

    public static IDistributedApplicationResourceBuilder<T> WithServiceBinding<T>(this IDistributedApplicationResourceBuilder<T> builder, int containerPort, int? hostPort = null, string? scheme = null, string? name = null) where T : IDistributedApplicationResource
    {
        if (builder.Resource.Annotations.OfType<ServiceBindingAnnotation>().Any(sb => sb.Name == name))
        {
            throw new DistributedApplicationException($"Service binding with name '{name}' already exists");
        }

        var annotation = new ServiceBindingAnnotation(
            protocol: ProtocolType.Tcp,
            uriScheme: scheme,
            name: name,
            port: hostPort,
            containerPort: containerPort);

        return builder.WithAnnotation(annotation);
    }

    public static IDistributedApplicationResourceBuilder<T> WithVolumeMount<T>(this IDistributedApplicationResourceBuilder<T> builder, string source, string target, VolumeMountType type = default, bool isReadOnly = false) where T : ContainerResource
    {
        var annotation = new VolumeMountAnnotation(source, target, type, isReadOnly);
        return builder.WithAnnotation(annotation);
    }

    public static IDistributedApplicationResourceBuilder<T> AddEndpoint<T>(this IDistributedApplicationResourceBuilder<T> builder, string name, int port)
        where T : IDistributedApplicationResourceWithServiceBindings
    {
        var ep = new EndpointResource<T>(name, port, builder.Resource);
        builder.ApplicationBuilder.AddResource(ep);
        return builder;
    }
    public static EndpointResource<T> Endpoint<T>(this IDistributedApplicationResourceBuilder<T> builder, string name)
        where T : IDistributedApplicationResourceWithServiceBindings
    {
        return builder.ApplicationBuilder.Resources.OfType<EndpointResource<T>>()
                .Single(e => e.Name == name && ReferenceEquals(e.Parent, builder.Resource));
    }
}

public class EndpointResource<T>(string name, int port, T parent) : IDistributedApplicationResourceWithParent<T>
    where T : IDistributedApplicationResourceWithServiceBindings
{
    public T Parent => parent;

    public string Name => name;

    public int Port => port;

    public ResourceMetadataCollection Annotations { get; } = [];
}

public interface IDistributedApplicationResourceWithServiceBindings : IDistributedApplicationResource
{
    // Not a fan of this
    public void PopulateServiceBindings(EnvironmentCallbackContext context)
    {
        var allocatedEndPoints = Annotations.OfType<AllocatedEndpointAnnotation>();

        var containsAmiguousEndpoints = ContainsAmbiguousEndpoints(allocatedEndPoints);

        var i = 0;
        foreach (var allocatedEndPoint in allocatedEndPoints)
        {
            var bindingNameQualifiedUriStringKey = $"services__{Name}__{i++}";
            context.EnvironmentVariables[bindingNameQualifiedUriStringKey] = allocatedEndPoint.BindingNameQualifiedUriString;

            if (!containsAmiguousEndpoints)
            {
                var uriStringKey = $"services__{Name}__{i++}";
                context.EnvironmentVariables[uriStringKey] = allocatedEndPoint.UriString;
            }
        }
    }

    private static bool ContainsAmbiguousEndpoints(IEnumerable<AllocatedEndpointAnnotation> endpoints)
    {
        // An ambiguous endpoint is where any scheme (
        return endpoints.GroupBy(e => e.UriScheme).Any(g => g.Count() > 1);
    }
}
