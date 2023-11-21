// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans.Clustering.AzureStorage;
using Orleans.Configuration;
using Orleans.TestingHost.UnixSocketTransport;
using static Aspire.Orleans.Server.OrleansServerSettingConstants;

namespace Aspire.Orleans.Server;

public static class AspireOrleansServerExtensions
{
    public static IHostApplicationBuilder UseOrleansAspire(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOrleans(siloBuilder =>
        {
            builder.AddAzureTableService("clustering");
            var serverSettings = new OrleansServerSettings();
            builder.Configuration.GetSection("grains").Bind(serverSettings);

            if (serverSettings.Clustering is { } clusteringSettings)
            {
                ApplyClusteringSettings(builder, clusteringSettings);
            }

            builder.AddAzureBlobService("grainstorage");

            // Enable distributed tracing for open telemetry.
            siloBuilder.AddActivityPropagation();

            siloBuilder.UseAzureStorageClustering((OptionsBuilder<AzureStorageClusteringOptions> optionsBuilder) =>
            {
                optionsBuilder.Configure<TableServiceClient>(
                    (options, tableClient) => options.ConfigureTableServiceClient(() => Task.FromResult(tableClient)));
            });

            siloBuilder.AddAzureBlobGrainStorageAsDefault((OptionsBuilder<AzureBlobStorageOptions> optionsBuilder) =>
            {
                optionsBuilder.Configure<BlobServiceClient>(
                    (options, blobClient) => options.ConfigureBlobServiceClient(() => Task.FromResult(blobClient)));
            });

            // BEGIN: will work only locally for now
            siloBuilder.Configure<EndpointOptions>(options =>
            {
                var rnd = new Random();
                options.SiloPort = rnd.Next(0, 65535);
                options.GatewayPort = rnd.Next(0, 65535);
            });
            siloBuilder.UseUnixSocketConnection();
            // END: will work only locally for now
        });

        return builder;
    }

    private static void ApplyClusteringSettings(IHostApplicationBuilder builder, ISiloBuilder siloBuilder, ConnectionSettings clusteringSettings)
    {
        var type = clusteringSettings.ConnectionType;
        var connectionName = clusteringSettings.ConnectionName;

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException(message: "A value must be specified for \"Clustering.ConnectionType\".", innerException: null);
        }

        if (string.IsNullOrWhiteSpace(connectionName))
        {
            throw new ArgumentException(message: "A value must be specified for \"Clustering.ConnectionName\".", innerException: null);
        }

        if (string.Equals(InternalProviderType, type, StringComparison.OrdinalIgnoreCase))
        {
            siloBuilder.UseLocalhostClustering();
            var connectionString = builder.Configuration.GetConnectionString(connectionName);

            if (connectionString is null || !IPEndPoint.TryParse(connectionString, out var primarySiloEndPoint))
            {
                throw new InvalidOperationException($"Invalid connection string specified for '{connectionName}'.");
            }
        }
        else if (string.Equals(AzureTablesProviderType, type, StringComparison.OrdinalIgnoreCase))
        {
            // Configure a table service client in the dependency injection container.
            builder.AddKeyedAzureTableService(connectionName);

            // Configure Orleans to use the configured table service client.
            siloBuilder.UseAzureStorageClustering(optionsBuilder => optionsBuilder.Configure(
                (AzureStorageClusteringOptions options, IServiceProvider serviceProvider) =>
                {
                    var tableServiceClient = Task.FromResult(serviceProvider.GetRequiredKeyedService<TableServiceClient>(connectionName));
                    options.ConfigureTableServiceClient(() => tableServiceClient);
                }));
        }
        else
        {
            throw new NotSupportedException($"Unsupported connection type \"{type}\".");
        }
    }
}
