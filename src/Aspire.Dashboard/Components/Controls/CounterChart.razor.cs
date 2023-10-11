// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Model;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Otlp.Model.MetricValues;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aspire.Dashboard.Components;

public partial class CounterChart : ComponentBase, IAsyncDisposable
{
    private const int GRAPH_POINT_COUNT = 30; // 3 minutes

    private static int s_lastId;

    private readonly int _instanceID = ++s_lastId;
    private string ChartDivId => $"lineChart{_instanceID}";

    private bool _dimensionsOrDurationChanged = true;

    //private double[]? _chartValues;

    private readonly CounterChartViewModel _viewModel = new();

    private PeriodicTimer? _tickTimer;
    private Task? _tickTask;
    private TimeSpan _tickDuration;
    private DateTime _lastUpdateTime;
    private DateTime _currentDataStartTime;

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    [Parameter, EditorRequired]
    public required OtlpInstrument Instrument { get; set; }

    [Parameter, EditorRequired]
    public required DimensionScope[] Dimensions { get; set; }

    [Parameter, EditorRequired]
    public required TimeSpan Duration { get; set; }

    private static DateTime GetCurrentDataTime()
    {
        return DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(1)); // Compensate for delay in receiving metrics from sevices.;
    }

    protected override void OnInitialized()
    {
        _currentDataStartTime = GetCurrentDataTime();

        foreach (var item in Instrument.KnownAttributeValues.OrderBy(kvp => kvp.Key))
        {
            var dimensionModel = new DimensionFilterViewModel
            {
                Name = item.Key
            };
            dimensionModel.Values.Add(new DimensionValueViewModel
            {
                Name = "(All)",
                All = true
            });
            dimensionModel.Values.AddRange(item.Value.OrderBy(v => v).Select(v =>
            {
                var empty = string.IsNullOrEmpty(v);
                return new DimensionValueViewModel
                {
                    Name = empty ? "(Empty)" : v,
                    Empty = empty,
                };
            }));
            _viewModel.DimensionFilters.Add(dimensionModel);
        }

        foreach (var item in _viewModel.DimensionFilters)
        {
            item.SelectedValues = item.Values.ToList();
            item.SelectedValuesChanged();
        }

        _tickTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _tickTask = Task.Run(UpdateData);
    }

    private async Task DimensionValuesChangedAsync(DimensionFilterViewModel dimensionViewModel)
    {
        dimensionViewModel.SelectedValuesChanged();
        _dimensionsOrDurationChanged = true;
        await InvokeAsync(StateHasChanged).ConfigureAwait(false);
    }

    private sealed class Trace
    {
        public Trace(string name, double?[] values)
        {
            Name = name;
            Values = values;
        }

        public string Name { get; }
        public double?[] Values { get; }
    }

    private (List<Trace> Y, List<DateTime> X) CalculateHistogramValues(List<DimensionScope> dimensions, int pointCount, bool tickUpdate, DateTime inProgressDataTime)
    {
        var pointDuration = Duration / pointCount;
        var yValues = new Dictionary<int, List<double?>>
        {
            [50] = new(),
            [90] = new(),
            [99] = new(),
        };
        var xValues = new List<DateTime>();
        var startDate = _currentDataStartTime;
        DateTime? firstPointEndTime = null;

        // Generate the points in reverse order so that the chart is drawn from right to left.
        // Add a couple of extra points to the end so that the chart is drawn all the way to the right edge.
        for (var pointIndex = 0; pointIndex < (pointCount + 2); pointIndex++)
        {
            var start = CalcOffset(pointIndex, startDate, pointDuration);
            var end = CalcOffset(pointIndex - 1, startDate, pointDuration);
            firstPointEndTime ??= end;

            xValues.Add(end.ToLocalTime());

            TryCalculateHistogramPoints(dimensions, start, end, yValues);

            //if ()
            //{
            //    yValues.Add(tickPointValue);
            //}
            //else
            //{
            //    yValues.Add(null);
            //}
        }

        foreach (var item in yValues)
        {
            item.Value.Reverse();
        }
        xValues.Reverse();

        if (tickUpdate && TryCalculateHistogramPoints(dimensions, firstPointEndTime!.Value, inProgressDataTime, yValues))
        {
            //yValues.Add(inProgressPointValue);
            xValues.Add(inProgressDataTime.ToLocalTime());
        }

        return (yValues.Select(kvp => new Trace($"{kvp.Key}th Percentile", kvp.Value.ToArray())).ToList(), xValues);
    }

    private static HistogramValue GetHistogramValue(MetricValueBase metric)
    {
        if (metric is HistogramValue histogramValue)
        {
            return histogramValue;
        }

        throw new InvalidOperationException("Unexpected metric type: " + metric.GetType());
    }

    private static bool TryCalculateHistogramPoints(List<DimensionScope> dimensions, DateTime start, DateTime end, Dictionary<int, List<double?>> yValues)
    {
        var hasValue = false;

        ulong[]? currentBucketCounts = null;
        double[]? explicitBounds = null;

        start = start.Subtract(TimeSpan.FromSeconds(1));
        end = end.Add(TimeSpan.FromSeconds(1));

        foreach (var dimension in dimensions)
        {
            for (var i = dimension.Values.Count - 1; i >= 0; i--)
            {
                if (i == 0)
                {
                    continue;
                }

                var metric = dimension.Values[i];
                if ((metric.Start <= end && metric.End >= start) || (metric.Start >= start && metric.End <= end))
                {
                    var histogramValue = GetHistogramValue(metric);
                    explicitBounds ??= histogramValue.ExplicitBounds;

                    var previousHistogramValue = GetHistogramValue(dimension.Values[i - 1]);

                    if (currentBucketCounts is null)
                    {
                        currentBucketCounts = new ulong[histogramValue.Values.Length];
                    }
                    else if (currentBucketCounts.Length != histogramValue.Values.Length)
                    {
                        throw new InvalidOperationException("Histogram values changed size");
                    }

                    for (var valuesIndex = 0; valuesIndex < histogramValue.Values.Length; valuesIndex++)
                    {
                        var newValue = histogramValue.Values[valuesIndex];
                        // Histogram values are culmulative, so subtract the previous value to get the diff.
                        newValue -= previousHistogramValue.Values[valuesIndex];

                        currentBucketCounts[valuesIndex] += newValue;
                    }

                    hasValue = true;
                }
            }
        }

        if (hasValue)
        {
            foreach (var percentileValues in yValues)
            {
                var percentileValue = CalculatePercentile(percentileValues.Key, currentBucketCounts!, explicitBounds!);
                percentileValues.Value.Add(percentileValue);
            }
        }

        return hasValue;
    }

    private static double CalculatePercentile(int percentile, ulong[] counts, double[] explicitBounds)
    {
        if (percentile < 0 || percentile > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be between 0 and 100.");
        }

        var totalCount = 0ul;
        foreach (var count in counts)
        {
            totalCount += count;
        }

        var targetCount = (percentile / 100.0) * totalCount;
        var accumulatedCount = 0ul;

        for (var i = 0; i < explicitBounds.Length; i++)
        {
            accumulatedCount += counts[i];

            if (accumulatedCount >= targetCount)
            {
                return explicitBounds[i];
            }
        }

        // If the percentile is larger than any bucket value, return the last value
        return explicitBounds[explicitBounds.Length - 1];
    }

    private (List<Trace> Y, List<DateTime> X) CalculateChartValues(List<DimensionScope> dimensions, int pointCount, bool tickUpdate, DateTime inProgressDataTime, string yLabel)
    {
        var pointDuration = Duration / pointCount;
        var yValues = new List<double?>();
        var xValues = new List<DateTime>();
        var startDate = _currentDataStartTime;
        DateTime? firstPointEndTime = null;

        // Generate the points in reverse order so that the chart is drawn from right to left.
        // Add a couple of extra points to the end so that the chart is drawn all the way to the right edge.
        for (var pointIndex = 0; pointIndex < (pointCount + 2); pointIndex++)
        {
            var start = CalcOffset(pointIndex, startDate, pointDuration);
            var end = CalcOffset(pointIndex - 1, startDate, pointDuration);
            firstPointEndTime ??= end;

            xValues.Add(end.ToLocalTime());

            if (TryCalculatePoint(dimensions, start, end, out var tickPointValue))
            {
                yValues.Add(tickPointValue);
            }
            else
            {
                yValues.Add(null);
            }
        }

        yValues.Reverse();
        xValues.Reverse();

        if (tickUpdate && TryCalculatePoint(dimensions, firstPointEndTime!.Value, inProgressDataTime, out var inProgressPointValue))
        {
            yValues.Add(inProgressPointValue);
            xValues.Add(inProgressDataTime.ToLocalTime());
        }

        return ([ new Trace(yLabel, yValues.ToArray()) ], xValues);
    }

    private static bool TryCalculatePoint(List<DimensionScope> dimensions, DateTime start, DateTime end, out double pointValue)
    {
        var hasValue = false;
        pointValue = 0d;

        foreach (var dimension in dimensions)
        {
            for (var i = dimension.Values.Count - 1; i >= 0; i--)
            {
                var metric = dimension.Values[i];
                if ((metric.Start <= end && metric.End >= start) || (metric.Start >= start && metric.End <= end))
                {
                    var value = metric switch
                    {
                        MetricValue<long> longMetric => longMetric.Value,
                        MetricValue<double> doubleMetric => doubleMetric.Value,
                        _ => 0// throw new InvalidOperationException("Unexpected metric type: " + metric.GetType())
                    };

                    pointValue += value;
                    hasValue = true;
                }
            }
        }

        return hasValue;
    }

    private static DateTime CalcOffset(int pointIndex, DateTime now, TimeSpan pointDuration)
    {
        return now.Subtract(pointDuration * pointIndex);
    }

    private bool MatchDimension(DimensionScope dimension)
    {
        foreach (var dimensionFilter in _viewModel.DimensionFilters)
        {
            if (!MatchFilter(dimension.Attributes, dimensionFilter))
            {
                return false;
            }
        }
        return true;
    }

    private static bool MatchFilter(KeyValuePair<string, string>[] attributes, DimensionFilterViewModel filter)
    {
        var value = OtlpHelpers.GetValue(attributes, filter.Name);
        foreach (var item in filter.SelectedValues)
        {
            if (item.All)
            {
                return true;
            }
            if (item.Empty)
            {
                return string.IsNullOrEmpty(value);
            }
            if (item.Name == value)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnParametersSet()
    {
        _tickDuration = Duration / GRAPH_POINT_COUNT;
    }

    private async Task StopTickTimerAsync()
    {
        _tickTimer?.Dispose();
        if (_tickTask is { } t)
        {
            await t.ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopTickTimerAsync().ConfigureAwait(false);
    }

    private async Task UpdateData()
    {
        var timer = _tickTimer;
        while (await timer!.WaitForNextTickAsync().ConfigureAwait(false))
        {
            await InvokeAsync(StateHasChanged).ConfigureAwait(false);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var inProgressDataTime = GetCurrentDataTime();

        while (_currentDataStartTime.Add(_tickDuration) < inProgressDataTime)
        {
            _currentDataStartTime = _currentDataStartTime.Add(_tickDuration);
        }

        if (_dimensionsOrDurationChanged)
        {
            _dimensionsOrDurationChanged = false;

            await UpdateChart(tickUpdate: false, inProgressDataTime).ConfigureAwait(false);
        }
        else if (_lastUpdateTime.Add(TimeSpan.FromSeconds(0.2)) < DateTime.UtcNow)
        {
            // Throttle how often the chart is updated.
            _lastUpdateTime = DateTime.UtcNow;
            await UpdateChart(tickUpdate: true, inProgressDataTime).ConfigureAwait(false);
        }
    }

    private async Task UpdateChart(bool tickUpdate, DateTime inProgressDataTime)
    {
        var matchedDimensions = Dimensions.Where(MatchDimension).ToList();
        List<Trace> yValues;
        List<DateTime> xValues;
        if (Instrument.Type != OpenTelemetry.Proto.Metrics.V1.Metric.DataOneofCase.Histogram)
        {
            var yLabel = Instrument.Unit.TrimStart('{').TrimEnd('}').Pluralize().Titleize();
            (yValues, xValues) = CalculateChartValues(matchedDimensions, GRAPH_POINT_COUNT, tickUpdate, inProgressDataTime, yLabel);
        }
        else
        {
            (yValues, xValues) = CalculateHistogramValues(matchedDimensions, GRAPH_POINT_COUNT, tickUpdate, inProgressDataTime);
        }

        var traces = yValues.Select(y => new
        {
            name = y.Name,
            values = y.Values
        }).ToArray();

        if (!tickUpdate)
        {
            await JSRuntime.InvokeVoidAsync("initializeChart",
                ChartDivId,
                traces,
                xValues,
                inProgressDataTime.ToLocalTime(),
                (inProgressDataTime - Duration).ToLocalTime()).ConfigureAwait(false);
        }
        else
        {
            await JSRuntime.InvokeVoidAsync("updateChart",
                ChartDivId,
                traces,
                xValues,
                inProgressDataTime.ToLocalTime(),
                (inProgressDataTime - Duration).ToLocalTime()).ConfigureAwait(false);
        }
    }
}
