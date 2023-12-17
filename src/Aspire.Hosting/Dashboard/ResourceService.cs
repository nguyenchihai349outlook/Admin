// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Model;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Dashboard;

internal sealed partial class ResourceService : IResourceService, IDisposable
{
    private readonly ResourcePublisher _resourcePublisher;
    private readonly DcpDataSource _dcpDataSource;

    public ResourceService(
        DistributedApplicationModel applicationModel, KubernetesService kubernetesService, IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory)
    {
        ApplicationName = ComputeApplicationName(hostEnvironment.ApplicationName);

        _resourcePublisher = new ResourcePublisher();
        _dcpDataSource = new DcpDataSource(kubernetesService, applicationModel, loggerFactory, _resourcePublisher.IntegrateAsync);

        static string ComputeApplicationName(string applicationName)
        {
            const string AppHostSuffix = ".AppHost";

            if (applicationName.EndsWith(AppHostSuffix, StringComparison.OrdinalIgnoreCase))
            {
                applicationName = applicationName[..^AppHostSuffix.Length];
            }

            return applicationName;
        }
    }

    public string ApplicationName { get; }

    public ResourceSubscription Subscribe() => _resourcePublisher.Subscribe();

    public void Dispose()
    {
        _resourcePublisher.Dispose();
        _dcpDataSource.Dispose();
    }
}
