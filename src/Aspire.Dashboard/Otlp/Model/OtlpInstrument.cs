// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Aspire.Dashboard.Otlp.Model.MetricValues;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;

namespace Aspire.Dashboard.Otlp.Model;

[DebuggerDisplay("Name = {Name}, Unit = {Unit}, Type = {Type}")]
public class OtlpInstrument
{
    public string Name { get; init; }
    public string Description { get; init; }
    public string Unit { get; init; }
    public Metric.DataOneofCase Type { get; init; }
    public OtlpMeter Parent { get; init; }

    public Dictionary<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> Dimensions { get; } = new(ScopeAttributesComparer.Instance);

    public OtlpInstrument(Metric mData, OtlpMeter parent)
    {
        Name = mData.Name;
        Description = mData.Description;
        Unit = mData.Unit;
        Type = mData.DataCase;
        Parent = parent;
    }

    public void AddInstrumentValuesFromGrpc(Metric mData, ref KeyValuePair<string, string>[]? tempAttributes)
    {
        switch (mData.DataCase)
        {
            case Metric.DataOneofCase.Gauge:
                foreach (var d in mData.Gauge.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddPointValue(d);
                }
                break;
            case Metric.DataOneofCase.Sum:
                foreach (var d in mData.Sum.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddPointValue(d);
                }
                break;
            case Metric.DataOneofCase.Histogram:
                foreach (var d in mData.Histogram.DataPoints)
                {
                    FindScope(d.Attributes, ref tempAttributes).AddHistogramValue(d);
                }
                break;
        }
    }

    private DimensionScope FindScope(RepeatedField<KeyValue> attributes, ref KeyValuePair<string, string>[]? tempAttributes)
    {
        // We want to find the dimension scope that matches the attributes, but we don't want to allocate.
        // Copy values to a temporary reusable array.
        OtlpHelpers.CopyKeyValuePairs(attributes, ref tempAttributes);
        Array.Sort(tempAttributes, KeyValuePairComparer.Instance);

        var comparableAttributes = tempAttributes.AsMemory(0, attributes.Count);

        if (!Dimensions.TryGetValue(comparableAttributes, out var dimension))
        {
            var durableAttributes = comparableAttributes.ToArray();
            Dimensions.Add(durableAttributes, dimension = new DimensionScope(durableAttributes));
        }
        return dimension;
    }

    private sealed class ScopeAttributesComparer : IEqualityComparer<ReadOnlyMemory<KeyValuePair<string, string>>>
    {
        public static readonly ScopeAttributesComparer Instance = new();

        public bool Equals(ReadOnlyMemory<KeyValuePair<string, string>> x, ReadOnlyMemory<KeyValuePair<string, string>> y)
        {
            return x.Span.SequenceEqual(y.Span);
        }

        public int GetHashCode([DisallowNull] ReadOnlyMemory<KeyValuePair<string, string>> obj)
        {
            var hashcode = 0;
            foreach (KeyValuePair<string, string> pair in obj.Span)
            {
                hashcode ^= pair.Key.GetHashCode();
                hashcode ^= pair.Value.GetHashCode();
            }
            return hashcode;
        }
    }

    private sealed class KeyValuePairComparer : IComparer<KeyValuePair<string, string>>
    {
        public static readonly KeyValuePairComparer Instance = new();

        public int Compare(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }
    }    
}
