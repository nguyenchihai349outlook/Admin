// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;

namespace Aspire.Hosting.ApplicationModel;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(ApplicationResourceCollectionDebugView))]
internal sealed class ResourceCollection : IResourceCollection
{
    private readonly List<IResource> _resources = [];
    internal int Version { get; private set; }

    public IResource this[int index]
    {
        get => _resources[index];
        set
        {
            _resources[index] = value;
            ++Version;
        }
    }
    public int Count => _resources.Count;
    public bool IsReadOnly => false;
    public void Add(IResource item)
    {
        _resources.Add(item);
        ++Version;
    }

    public void Clear()
    {
        _resources.Clear();
        ++Version;
    }
    public bool Contains(IResource item) => _resources.Contains(item);
    public void CopyTo(IResource[] array, int arrayIndex) => _resources.CopyTo(array, arrayIndex);
    public IEnumerator<IResource> GetEnumerator() => _resources.GetEnumerator();
    public int IndexOf(IResource item) => _resources.IndexOf(item);
    public void Insert(int index, IResource item)
    {
        _resources.Insert(index, item);
        ++Version;
    }

    public bool Remove(IResource item)
    {
        if (_resources.Remove(item))
        {
            ++Version;
            return true;
        }

        return false;
    }

    public void RemoveAt(int index)
    {
        _resources.RemoveAt(index);
        ++Version;
    }

    IEnumerator IEnumerable.GetEnumerator() => _resources.GetEnumerator();

    private sealed class ApplicationResourceCollectionDebugView(ResourceCollection collection)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IResource[] Items => collection.ToArray();
    }
}

