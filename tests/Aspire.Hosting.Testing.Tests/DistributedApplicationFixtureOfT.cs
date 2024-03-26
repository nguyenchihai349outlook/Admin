using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Aspire.Hosting.Testing.Tests;

public sealed class DistributedApplicationFixture<TEntryPoint> : DistributedApplicationFactory<TEntryPoint>, IAsyncLifetime where TEntryPoint : class
{
    private readonly IMessageSink  _messageSink;
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task? _logWatcherTask;

    public DistributedApplicationFixture(IMessageSink messageSink)
    {
        if (Environment.GetEnvironmentVariable("BUILD_BUILDID") != null)
        {
            throw new SkipException("These tests can only run in local environments.");
        }

        _messageSink = messageSink;
    }

    protected override void OnBuilderCreating(DistributedApplicationOptions applicationOptions, HostApplicationBuilderSettings hostOptions)
    {
        base.OnBuilderCreating(applicationOptions, hostOptions);
    }

    protected override void OnBuilderCreated(DistributedApplicationBuilder applicationBuilder)
    {
        base.OnBuilderCreated(applicationBuilder);
    }

    protected override void OnBuilding(DistributedApplicationBuilder applicationBuilder)
    {
        base.OnBuilding(applicationBuilder);
    }

    protected override void OnBuilt(DistributedApplication application)
    {
        Application = application;
        _logWatcherTask = WatchLogs(
            application,
            entry => _messageSink.OnMessage(new DiagnosticMessage($"[{DateTime.Now.ToString(CultureInfo.CurrentCulture)}] [{entry.Resource.Name}] {entry.Content}")),
            _shutdownCts.Token);
        base.OnBuilt(application);
    }

    private record struct ResourceOutputEntry(IResource Resource, string Content, bool IsErrorOutput);

    private static async Task WatchLogs(DistributedApplication application, Action<ResourceOutputEntry> onOutput, CancellationToken cancellationToken)
    {
        var resourceLogger = application.Services.GetRequiredService<ResourceLoggerService>();
        var appModel = application.Services.GetRequiredService<DistributedApplicationModel>();
        var watchers = new List<Task>();
        var output = Channel.CreateUnbounded<ResourceOutputEntry>();
        foreach (var resource in appModel.Resources)
        {
            watchers.Add(WatchResourceLogs(resourceLogger, resource, output.Writer, cancellationToken));
        }

        var pumpLogs = PumpLogsAsync(output.Reader, onOutput);
        await Task.WhenAll(watchers);
        output.Writer.Complete();
        await pumpLogs.ConfigureAwait(false);
    }

    private static async Task PumpLogsAsync(ChannelReader<ResourceOutputEntry> entries, Action<ResourceOutputEntry> onOutput)
    {
        await foreach (var entry in entries.ReadAllAsync().ConfigureAwait(false))
        {
            onOutput(entry);
        }
    }

    private static async Task WatchResourceLogs(ResourceLoggerService loggerService, IResource resource, ChannelWriter<ResourceOutputEntry> output, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var entries in loggerService.WatchAsync(resource).WithCancellation(cancellationToken))
            {
                foreach (var (content, isError) in entries)
                {
                    await output.WriteAsync(new(resource, content, isError), cancellationToken: CancellationToken.None);
                }
            }
        }
    }

    public DistributedApplication Application { get; private set; } = null!;

    public async Task InitializeAsync() => await StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        _shutdownCts.Dispose();
        if (_logWatcherTask is { } task)
        {
            await task.ConfigureAwait(false);
        }
    }
}
