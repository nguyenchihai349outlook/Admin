// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aspire.Dashboard.Model;

namespace Aspire.Hosting.Dashboard;

/// <summary>
/// Builds a collection of resources by integrating incoming resource changes,
/// and allowing multiple subscribers to receive the current resource collection
/// snapshot and future updates.
/// </summary>
internal sealed class ResourcePublisher : IDisposable
{
    private readonly object _syncLock = new();
    private bool _disposed;
    private readonly Dictionary<string, ResourceViewModel> _snapshot = [];
    // Internal for testing
    internal ImmutableHashSet<Channel<ResourceChange>> _outgoingChannels = [];

    public ResourceSubscription Subscribe()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateUnbounded<ResourceChange>();

        ImmutableInterlocked.Update(ref _outgoingChannels, static (set, channel) => set.Add(channel), channel);

        List<ResourceViewModel> snapshot;
        lock (_syncLock)
        {
            snapshot = _snapshot.Values.ToList();
        }

        return new ResourceSubscription(
            Snapshot: snapshot,
            Subscription: StreamUpdates());

        async IAsyncEnumerable<ResourceChange> StreamUpdates([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (channel.Reader.TryRead(out var item))
                    {
                        yield return item;
                    }
                }
            }
            finally
            {
                ImmutableInterlocked.Update(ref _outgoingChannels, static (set, channel) => set.Remove(channel), channel);
            }
        }
    }

    /// <summary>
    /// Integrates a changed resource within the cache, and broadcasts the update to any subscribers.
    /// </summary>
    /// <param name="resource">The resource that was modified.</param>
    /// <param name="changeType">The change type (Added, Modified, Deleted).</param>
    /// <returns>A task that completes when the cache has been updated and all subscribers notified.</returns>
    public async ValueTask IntegrateAsync(ResourceViewModel resource, ResourceChangeType changeType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncLock)
        {
            switch (changeType)
            {
                case ResourceChangeType.Upsert:
                    if (_snapshot.TryGetValue(resource.Name, out var existing) && existing.Equals(resource))
                    {
                        // The new value is identical to the prior one, so don't send it.
                        return;
                    }

                    _snapshot[resource.Name] = resource;
                    break;

                case ResourceChangeType.Delete:
                    _snapshot.Remove(resource.Name);
                    break;
            }
        }

        // The publisher could be disposed while writing. WriteAsync will throw ChannelClosedException.
        foreach (var channel in _outgoingChannels)
        {
            await channel.Writer.WriteAsync(new(changeType, resource)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _disposed = true;

        foreach (var item in _outgoingChannels)
        {
            item.Writer.Complete();
        }

        _outgoingChannels = _outgoingChannels.Clear();
    }
}
