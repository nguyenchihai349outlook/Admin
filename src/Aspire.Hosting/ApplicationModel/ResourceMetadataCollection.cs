// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a collection of resource metadata annotations.
/// </summary>
public sealed class ResourceMetadataCollection : IList<IResourceAnnotation>, IReadOnlyList<IResourceAnnotation>
{
    private readonly List<IResourceAnnotation> _annotations = [];
    private int _version;

    internal int Version => _version;

    public int Count => _annotations.Count;

    public bool IsReadOnly => false;

    public IResourceAnnotation this[int index]
    {
        get => _annotations[index];
        set
        {
            _annotations[index] = value;
            ++_version;
        }
    }

    public void Add(IResourceAnnotation item)
    {
        ++_version;
        _annotations.Add(item);
    }

    public void Clear()
    {
        _annotations.Clear();
        ++_version;
    }

    public bool Contains(IResourceAnnotation item) => _annotations.Contains(item);

    public void CopyTo(IResourceAnnotation[] array, int arrayIndex) => _annotations.CopyTo(array, arrayIndex);

    public IEnumerator<IResourceAnnotation> GetEnumerator() => _annotations.GetEnumerator();

    public bool Remove(IResourceAnnotation item)
    {
        if (_annotations.Remove(item))
        {
            ++_version;
            return true;
        }

        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() => _annotations.GetEnumerator();

    public int IndexOf(IResourceAnnotation item) => _annotations.IndexOf(item);

    public void Insert(int index, IResourceAnnotation item)
    {
        _annotations.Insert(index, item);
        ++_version;
    }

    public void RemoveAt(int index)
    {
        _annotations.RemoveAt(index);
        ++_version;
    }
}
