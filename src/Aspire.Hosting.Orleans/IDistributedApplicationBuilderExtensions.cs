// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extensions to <see cref="IDistributedApplicationBuilder"/> related to Orleans.
/// </summary>
public static class IDistributedApplicationBuilderExtensions
{
    public static IResourceBuilder<OrleansResource> AddOrleans(
        this IDistributedApplicationBuilder builder,
        string name)
        => builder.AddResource(new OrleansResource(name));

    public static IResourceBuilder<OrleansResource> UseAzureClustering(
        this IResourceBuilder<OrleansResource> builder,
        IResourceBuilder<AzureTableStorageResource> clustering)
    {
        builder.Resource.ClusteringTable = clustering;
        return builder;
    }

    public static IResourceBuilder<OrleansResource> UseAzureBlobGrainStorage(
        this IResourceBuilder<OrleansResource> builder,
        IResourceBuilder<AzureBlobStorageResource> grainStorage)
    {
        builder.Resource.GrainStorage[grainStorage.Resource.Name] = grainStorage;
        return builder;
    }

    public static IResourceBuilder<T> WithOrleansServer<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<OrleansResource> orleansResourceBuilder)
        where T : IResourceWithEnvironment
    {
        foreach (var (name, storage) in orleansResourceBuilder.Resource.GrainStorage)
        {
            builder.WithReference(storage, connectionName: $"orleans_{orleansResourceBuilder.Resource.Name}_grain_persistence_{name}");
        }

        if (orleansResourceBuilder.Resource.Reminders is { } reminders)
        {
            builder.WithReference(reminders);
        }
        return builder
            .WithReference(orleansResourceBuilder.Resource.ClusteringTable!);
    }

    public static IResourceBuilder<T> WithOrleansClient<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<OrleansResource> orleansResourceBuilder)
        where T : IResourceWithEnvironment
    {
        return builder
            .WithReference(orleansResourceBuilder.Resource.ClusteringTable!);
    }
}
