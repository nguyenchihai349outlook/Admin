// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Model;
using Aspire.Hosting.Dashboard;
using Xunit;

namespace Aspire.Hosting.Tests.Dashboard;

public class ResourcePublisherTests
{
    [Fact]
    public async Task ProducesExpectedSnapshotAndUpdates()
    {
        ResourcePublisher publisher = new();

        var a = CreateResource("A");
        var b = CreateResource("B");
        var c = CreateResource("C");

        await publisher.IntegrateAsync(a, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(b, ResourceChangeType.Upsert).ConfigureAwait(false);

        var (snapshot, subscription) = publisher.Subscribe();

        Assert.Equal(2, snapshot.Count);
        Assert.Contains(a, snapshot);
        Assert.Contains(b, snapshot);

        using AutoResetEvent sync = new(initialState: false);
        List<ResourceChange> changes = [];

        var task = Task.Run(async () =>
        {
            await foreach (var change in subscription)
            {
                changes.Add(change);
                sync.Set();
            }
        });

        await publisher.IntegrateAsync(c, ResourceChangeType.Upsert).ConfigureAwait(false);

        sync.WaitOne(TimeSpan.FromSeconds(1));

        var change = Assert.Single(changes);
        Assert.Equal(ResourceChangeType.Upsert, change.ChangeType);
        Assert.Same(c, change.Resource);

        publisher.Dispose();

        await task;
    }

    [Fact]
    public async Task SupportsMultipleSubscribers()
    {
        ResourcePublisher publisher = new();

        var a = CreateResource("A");
        var b = CreateResource("B");
        var c = CreateResource("C");

        await publisher.IntegrateAsync(a, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(b, ResourceChangeType.Upsert).ConfigureAwait(false);

        var (snapshot1, subscription1) = publisher.Subscribe();
        var (snapshot2, subscription2) = publisher.Subscribe();

        Assert.Equal(2, snapshot1.Count);
        Assert.Equal(2, snapshot2.Count);

        await publisher.IntegrateAsync(c, ResourceChangeType.Upsert).ConfigureAwait(false);

        var enumerator1 = subscription1.GetAsyncEnumerator();
        var enumerator2 = subscription2.GetAsyncEnumerator();

        await enumerator1.MoveNextAsync();
        await enumerator2.MoveNextAsync();

        Assert.Equal(ResourceChangeType.Upsert, enumerator1.Current.ChangeType);
        Assert.Equal(ResourceChangeType.Upsert, enumerator2.Current.ChangeType);
        Assert.Same(c, enumerator1.Current.Resource);
        Assert.Same(c, enumerator2.Current.Resource);

        publisher.Dispose();

        Assert.False(await enumerator1.MoveNextAsync());
        Assert.False(await enumerator2.MoveNextAsync());
    }

    [Fact]
    public async Task MergesResourcesInSnapshot()
    {
        using ResourcePublisher publisher = new();

        var a1 = CreateResource("A", "state1");
        var a2 = CreateResource("A", "state2");
        var a3 = CreateResource("A", "state3");

        await publisher.IntegrateAsync(a1, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(a2, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(a3, ResourceChangeType.Upsert).ConfigureAwait(false);

        var (snapshot, _) = publisher.Subscribe();

        Assert.Same(a3, Assert.Single(snapshot));
    }

    [Fact]
    public async Task SwallowsUnchangedResourceUpdates()
    {
        ResourcePublisher publisher = new();

        var a1 = CreateResource("A");
        var a2 = CreateResource("A");
        var a3 = CreateResource("A");

        await publisher.IntegrateAsync(a1, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(a2, ResourceChangeType.Upsert).ConfigureAwait(false);

        var (snapshot, subscription) = publisher.Subscribe();

        Assert.Same(a1, Assert.Single(snapshot));

        var received = false;
        var task = Task.Run(async () =>
        {
            await foreach (var _ in subscription.ConfigureAwait(false))
            {
                received = true;
            }
        });

        await publisher.IntegrateAsync(a3, ResourceChangeType.Upsert).ConfigureAwait(false);
        
        publisher.Dispose();

        await task;

        Assert.False(received);
    }

    [Fact]
    public async Task SubscriptionWithCancellation_Canceled()
    {
        using CancellationTokenSource cts = new();
        ResourcePublisher publisher = new();

        var (snapshot, subscription) = publisher.Subscribe();
        Assert.Single(publisher._outgoingChannels);

        var task = Task.Run(async () =>
        {
            await foreach (var _ in subscription.WithCancellation(cts.Token).ConfigureAwait(false))
            {
            }
        });

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        Assert.Empty(publisher._outgoingChannels);
    }

    [Fact]
    public async Task DeletesRemoveFromSnapshot()
    {
        using ResourcePublisher publisher = new();

        var a = CreateResource("A");
        var b = CreateResource("B");

        await publisher.IntegrateAsync(a, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(b, ResourceChangeType.Upsert).ConfigureAwait(false);
        await publisher.IntegrateAsync(a, ResourceChangeType.Delete).ConfigureAwait(false);

        var (snapshot, _) = publisher.Subscribe();

        Assert.Same(b, Assert.Single(snapshot));
    }

    private static ContainerViewModel CreateResource(string name, string state = "")
    {
        return new ContainerViewModel()
        {
            Name = name,
            Uid = "",
            State = state,
            CreationTimeStamp = null,
            DisplayName = "",
            Endpoints = [],
            Environment = [],
            ExpectedEndpointsCount = null,
            LogSource = null!,
            Services = [],
            Args = [],
            Command = "",
            ContainerId = "",
            Image = "",
            Ports = []
        };
    }
}
