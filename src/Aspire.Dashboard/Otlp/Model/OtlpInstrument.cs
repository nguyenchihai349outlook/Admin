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
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Unit { get; init; }
    public required OtlpInstrumentType Type { get; init; }
    public required OtlpMeter Parent { get; init; }

    public Dictionary<ReadOnlyMemory<KeyValuePair<string, string>>, DimensionScope> Dimensions { get; } = new(ScopeAttributesComparer.Instance);
    public Dictionary<string, HashSet<string>> KnownAttributeValues { get; } = new();

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
        Array.Sort(tempAttributes, 0, attributes.Count, KeyValuePairComparer.Instance);

        var comparableAttributes = tempAttributes.AsMemory(0, attributes.Count);

        if (!Dimensions.TryGetValue(comparableAttributes, out var dimension))
        {
            dimension = AddDimensionScope(comparableAttributes);
        }
        return dimension;
    }

    private DimensionScope AddDimensionScope(Memory<KeyValuePair<string, string>> comparableAttributes)
    {
        var isFirst = Dimensions.Count == 0;
        var durableAttributes = comparableAttributes.ToArray();
        var dimension = new DimensionScope(durableAttributes);
        Dimensions.Add(durableAttributes, dimension);

        var keys = KnownAttributeValues.Keys.Union(durableAttributes.Select(a => a.Key)).Distinct();
        foreach (var key in keys)
        {
            if (!KnownAttributeValues.TryGetValue(key, out var values))
            {
                KnownAttributeValues.Add(key, values = new HashSet<string>());

                // If the key is new and there are already dimensions, add an empty value because there are dimensions without this key.
                if (!isFirst)
                {
                    values.Add(string.Empty);
                }
            }

            var currentDimensionValue = OtlpHelpers.GetValue(durableAttributes, key);
            values.Add(currentDimensionValue ?? string.Empty);
        }

        return dimension;
    }

    public static OtlpInstrument Clone(OtlpInstrument instrument, bool cloneData, DateTime valuesStart, DateTime valuesEnd)
    {
        var newInstrument = new OtlpInstrument
        {
            Name = instrument.Name,
            Description = instrument.Description,
            Parent = instrument.Parent,
            Type = instrument.Type,
            Unit = instrument.Unit
        };

        if (cloneData)
        {
            foreach (var item in instrument.KnownAttributeValues)
            {
                newInstrument.KnownAttributeValues.Add(item.Key, item.Value.ToHashSet());
            }
            foreach (var item in instrument.Dimensions)
            {
                newInstrument.Dimensions.Add(item.Key, DimensionScope.Clone(item.Value, valuesStart, valuesEnd));
            }
        }

        return newInstrument;
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
