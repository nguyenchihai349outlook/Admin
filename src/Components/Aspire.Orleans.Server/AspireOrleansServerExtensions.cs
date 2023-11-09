// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans.Clustering.AzureStorage;
using Orleans.Configuration;
using Orleans.TestingHost.UnixSocketTransport;

namespace Aspire.Orleans.Server;

public static class AspireOrleansServerExtensions
{
    public static IHostApplicationBuilder UseOrleansAspire(this IHostApplicationBuilder builder)
    {
        builder.AddAzureTableService("clustering");
        builder.AddAzureBlobService("grainstorage");
        builder.Services.AddOrleans(siloBuilder =>
        {
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
}
