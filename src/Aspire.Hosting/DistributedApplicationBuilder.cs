// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dashboard;
using Aspire.Hosting.Dcp;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// A builder for creating instances of <see cref="DistributedApplication"/>.
/// </summary>
public class DistributedApplicationBuilder : IDistributedApplicationBuilder
{
    private readonly HostApplicationBuilder _innerBuilder;
    private readonly string[] _args;

    /// <inheritdoc />
    public IHostEnvironment Environment => _innerBuilder.Environment;

    /// <inheritdoc />
    public ConfigurationManager Configuration => _innerBuilder.Configuration;

    /// <inheritdoc />
    public IServiceCollection Services => _innerBuilder.Services;

    /// <inheritdoc />
    public string AppHostDirectory { get; }

    /// <inheritdoc />
    private readonly ResourceCollection _resources = [];
    public IResourceCollection Resources => _resources;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedApplicationBuilder"/> class with the specified options.
    /// </summary>
    /// <param name="options">The options for the distributed application.</param>
    public DistributedApplicationBuilder(DistributedApplicationOptions options)
    {
        _args = options.Args ?? [];
        _innerBuilder = new HostApplicationBuilder();

        AppHostDirectory = options.ProjectDirectory ?? _innerBuilder.Environment.ContentRootPath;

        // Make the app host directory available to the application via configuration
        _innerBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppHost:Directory"] = AppHostDirectory
        });

        // Core things
        _innerBuilder.Services.AddSingleton(sp => new DistributedApplicationModel(Resources));
        _innerBuilder.Services.AddHostedService<DistributedApplicationRunner>();
        _innerBuilder.Services.AddSingleton(options);

        // DCP stuff
        _innerBuilder.Services.AddLifecycleHook<DcpDistributedApplicationLifecycleHook>();
        _innerBuilder.Services.AddSingleton<ApplicationExecutor>();
        _innerBuilder.Services.AddHostedService<DcpHostService>();

        // Dashboard
        _innerBuilder.Services.AddHostedService<DashboardServiceHost>();
        _innerBuilder.Services.AddHostedService<DashboardWebApplicationHost>();

        // We need a unique path per application instance
        _innerBuilder.Services.AddSingleton(new Locations());
        _innerBuilder.Services.AddSingleton<KubernetesService>();

        // Publishing support
        ConfigurePublishingOptions(options);
        _innerBuilder.Services.AddLifecycleHook<AutomaticManifestPublisherBindingInjectionHook>();
        _innerBuilder.Services.AddLifecycleHook<Http2TransportMutationHook>();
        _innerBuilder.Services.AddKeyedSingleton<IDistributedApplicationPublisher, ManifestPublisher>("manifest");
        _innerBuilder.Services.AddKeyedSingleton<IDistributedApplicationPublisher, DcpPublisher>("dcp");
    }

    private void ConfigurePublishingOptions(DistributedApplicationOptions options)
    {
        var switchMappings = new Dictionary<string, string>()
        {
            { "--publisher", "Publishing:Publisher" },
            { "--output-path", "Publishing:OutputPath" },
            { "--dcp-cli-path", "DcpPublisher:CliPath" },
        };
        _innerBuilder.Configuration.AddCommandLine(options.Args ?? [], switchMappings);
        _innerBuilder.Services.Configure<PublishingOptions>(_innerBuilder.Configuration.GetSection(PublishingOptions.Publishing));
        _innerBuilder.Services.Configure<DcpOptions>(
            o => o.ApplyApplicationConfiguration(
                options,
                dcpPublisherConfiguration: _innerBuilder.Configuration.GetSection(DcpOptions.DcpPublisher),
                publishingConfiguration: _innerBuilder.Configuration.GetSection(PublishingOptions.Publishing)
            )
        );
    }

    /// <inheritdoc />
    public DistributedApplication Build()
    {
        AspireEventSource.Instance.DistributedApplicationBuildStart();
        try
        {
            RunBuildActions();
            var application = new DistributedApplication(_innerBuilder.Build(), _args);
            return application;
        }
        finally
        {
            AspireEventSource.Instance.DistributedApplicationBuildStop();
        }
    }

    private void RunBuildActions()
    {
        var runActions = new List<(BuildActionAnnotation Action, ResourceMetadataCollection Annotations, int OriginalVersion)>();
        var operationContext = new OperationContext(this, OperationContext.Run);
        bool dirty;
        do
        {
            dirty = false;
            var originalResourcesVersion = _resources.Version;

            // Find the actions to run.
            foreach (var resource in _resources)
            {
                foreach (var annotation in resource.Annotations)
                {
                    if (annotation is not BuildActionAnnotation runCallbackAnnotation)
                    {
                        continue;
                    }

                    if (!runCallbackAnnotation.HasRun)
                    {
                        runActions.Add((runCallbackAnnotation, resource.Annotations, resource.Annotations.Version));
                    }
                }
            }

            // Run the actions.
            foreach (var (action, _, _) in runActions)
            {
                action.Callback(operationContext);
                action.HasRun = true;
            }

            // Check whether the actions modified the model and loop if so.
            foreach (var (_, annotations, originalVersion) in runActions)
            {
                if (annotations.Version != originalVersion)
                {
                    runActions.Clear();
                    dirty = true;
                    break;
                }
            }

            dirty |= originalResourcesVersion != _resources.Version;
        } while (dirty);
    }

    /// <inheritdoc />
    public IResourceBuilder<T> AddResource<T>(T resource) where T : IResource
    {
        if (Resources.FirstOrDefault(r => r.Name == resource.Name) is { } existingResource)
        {
            throw new DistributedApplicationException($"Cannot add resource of type '{resource.GetType()}' with name '{resource.Name}' because resource of type '{existingResource.GetType()}' with that name already exists.");
        }

        Resources.Add(resource);
        var builder = new DistributedApplicationResourceBuilder<T>(this, resource);
        return builder;
    }
}
