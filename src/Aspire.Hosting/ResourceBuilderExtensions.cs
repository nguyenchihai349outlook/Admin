// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

public static class ResourceBuilderExtensions
{
    private const string ConnectionStringEnvironmentName = "ConnectionStrings__";

    public static AllocatedEndpointAnnotation? GetAllocatedEndpoint<T>(this IDistributedApplicationResourceBuilder<T> builder, string name) where T : IDistributedApplicationResource
    {
        return builder.Resource.Annotations.OfType<AllocatedEndpointAnnotation>().SingleOrDefault();
    }

    public static IDistributedApplicationResourceBuilder<T> WithEnvironment<T>(this IDistributedApplicationResourceBuilder<T> builder, string name, string? value) where T : IDistributedApplicationResource
    {
        return builder.WithAnnotation(new EnvironmentCallbackAnnotation(name, () => value ?? string.Empty));
    }

    public static IDistributedApplicationResourceBuilder<T> WithEnvironment<T>(this IDistributedApplicationResourceBuilder<T> builder, string name, Func<string> callback) where T : IDistributedApplicationResourceWithEnvironment
    {
        return builder.WithAnnotation(new EnvironmentCallbackAnnotation(name, callback));
    }

    public static IDistributedApplicationResourceBuilder<T> WithEnvironment<T>(this IDistributedApplicationResourceBuilder<T> builder, Action<EnvironmentCallbackContext> callback) where T : IDistributedApplicationResourceWithEnvironment
    {
        return builder.WithAnnotation(new EnvironmentCallbackAnnotation(callback));
    }

    private static bool ContainsAmbiguousEndpoints(IEnumerable<AllocatedEndpointAnnotation> endpoints)
    {
        // An ambiguous endpoint is where any scheme (
        return endpoints.GroupBy(e => e.UriScheme).Any(g => g.Count() > 1);
    }

    private static Action<EnvironmentCallbackContext> CreateServiceReferenceEnvironmentPopulationCallback(ServiceReferenceAnnotation serviceReferencesAnnotation)
    {
        return (context) =>
        {
            var name = serviceReferencesAnnotation.Resource.Name;

            var allocatedEndPoints = serviceReferencesAnnotation.Resource.Annotations
                .OfType<AllocatedEndpointAnnotation>()
                .Where(a => serviceReferencesAnnotation.UseAllBindings || serviceReferencesAnnotation.BindingNames.Contains(a.Name));

            var containsAmiguousEndpoints = ContainsAmbiguousEndpoints(allocatedEndPoints);

            var i = 0;
            foreach (var allocatedEndPoint in allocatedEndPoints)
            {
                var bindingNameQualifiedUriStringKey = $"services__{name}__{i++}";
                context.EnvironmentVariables[bindingNameQualifiedUriStringKey] = allocatedEndPoint.BindingNameQualifiedUriString;

                if (!containsAmiguousEndpoints)
                {
                    var uriStringKey = $"services__{name}__{i++}";
                    context.EnvironmentVariables[uriStringKey] = allocatedEndPoint.UriString;
                }
            }
        };
    }

    public static IDistributedApplicationResourceBuilder<TDestination> WithReference<TDestination, TSource>(this IDistributedApplicationResourceBuilder<TDestination> builder, IDistributedApplicationResourceBuilder<TSource> source, string? connectionName = null, bool optional = false)
        where TDestination : IDistributedApplicationResourceWithEnvironment
        where TSource : IDistributedApplicationResource
    {
        var resource = source.Resource;
        connectionName ??= resource.Name;

        return builder.WithEnvironment(context =>
        {
            var connectionStringName = $"{ConnectionStringEnvironmentName}{connectionName}";

            if (context.PublisherName == "manifest")
            {
                if (resource is IDistributedApplicationResourceWithConnectionString)
                {
                    context.EnvironmentVariables[connectionStringName] = $"{{{resource.Name}.connectionString}}";
                }

                if (resource is IDistributedApplicationResourceWithServiceBindings)
                {
                    // TBD
                }

                return;
            }

            if (resource is IDistributedApplicationResourceWithConnectionString resourceWithConnectionString)
            {
                var connectionString = resourceWithConnectionString.GetConnectionString() ??
                    builder.ApplicationBuilder.Configuration.GetConnectionString(resource.Name);

                if (string.IsNullOrEmpty(connectionString))
                {
                    if (optional)
                    {
                        // This is an optional connection string, so we can just return.
                        return;
                    }

                    throw new DistributedApplicationException($"A connection string for '{resource.Name}' could not be retrieved.");
                }

                context.EnvironmentVariables[connectionStringName] = connectionString;
            }


            if (resource is IDistributedApplicationResourceWithServiceBindings resourceWithServiceBindings)
            {
                resourceWithServiceBindings.PopulateServiceBindings(context);
            }
        });
    }

    public static IDistributedApplicationResourceBuilder<T> WithServiceBinding<T>(this IDistributedApplicationResourceBuilder<T> builder, int? hostPort = null, string? scheme = null, string? name = null) where T : IDistributedApplicationResource
    {
        if (builder.Resource.Annotations.OfType<ServiceBindingAnnotation>().Any(sb => sb.Name == name))
        {
            throw new DistributedApplicationException($"Service binding with name '{name}' already exists");
        }

        var annotation = new ServiceBindingAnnotation(ProtocolType.Tcp, scheme, name, port: hostPort);
        return builder.WithAnnotation(annotation);
    }

    public static IDistributedApplicationResourceBuilder<T> WithServiceBindingForPublisher<T>(this IDistributedApplicationResourceBuilder<T> builder, string publisherName, string bindingName, Func<ServiceBindingCallbackContext, ServiceBindingAnnotation>? callback = null) where T : IDistributedApplicationResource
    {
        return builder.WithAnnotation(new ServiceBindingCallbackAnnotation(publisherName, bindingName, callback));
    }
}
