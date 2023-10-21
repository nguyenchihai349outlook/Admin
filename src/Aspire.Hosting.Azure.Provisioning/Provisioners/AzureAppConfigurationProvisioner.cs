// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppConfiguration;
using Azure.ResourceManager.AppConfiguration.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Azure.Provisioning;

internal sealed class AzureAppConfigurationProvisioner(ILogger<AzureAppConfigurationProvisioner> logger) : AzureResourceProvisioner<AzureAppConfigurationResource>
{
    public override bool ConfigureResource(IConfiguration configuration, AzureAppConfigurationResource resource)
    {
        if (configuration.GetConnectionString(resource.Name) is string connectionString)
        {
            resource.ConnectionString = connectionString;
            return true;
        }
        return false;
    }

    public override async Task GetOrCreateResourceAsync(
        ArmClient armClient,
        SubscriptionResource subscription,
        ResourceGroupResource resourceGroup,
        Dictionary<string, ArmResource> resourceMap,
        AzureLocation location,
        AzureAppConfigurationResource resource,
        Guid principalId,
        JsonObject userSecrets,
        CancellationToken cancellationToken)
    {
        resourceMap.TryGetValue(resource.Name, out var azureResource);

        if (azureResource is not null && azureResource is not AppConfigurationStoreResource)
        {
            logger.LogWarning("Resource {resourceName} is not an app configuration resource. Deleting it.", resource.Name);

            await armClient.GetGenericResource(azureResource.Id).DeleteAsync(WaitUntil.Started, cancellationToken).ConfigureAwait(false);
        }

        var appConfigurationResource = azureResource as AppConfigurationStoreResource;

        if (appConfigurationResource is null)
        {
            var appConfigurationName = Guid.NewGuid().ToString().Replace("-", string.Empty)[0..20];

            logger.LogInformation("Creating app configuration {appConfigurationName} in {location}...", appConfigurationName, location);

            var appConfigurationCreateOrUpdateContent = new AppConfigurationStoreData(location, new AppConfigurationSku("Standard"));

            appConfigurationCreateOrUpdateContent.Tags.Add(AzureProvisioner.AspireResourceNameTag, resource.Name);

            // Now we can create a storage account with defined account name and parameters
            var response = await resourceGroup.GetAppConfigurationStores().CreateOrUpdateAsync(WaitUntil.Completed, appConfigurationName, appConfigurationCreateOrUpdateContent, cancellationToken).ConfigureAwait(false);
            appConfigurationResource = response.Value;
        }

        resource.ConnectionString = appConfigurationResource.Data.Endpoint;

        var connectionStrings = userSecrets.Prop("ConnectionStrings");
        connectionStrings[resource.Name] = resource.ConnectionString;
    }
}
