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
    private DateTime _currentDataTime;

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
        _currentDataTime = GetCurrentDataTime();

        foreach (var dimensions in Dimensions)
        {
            foreach (var attribute in dimensions.Attributes)
            {
                var dimensionModel = _viewModel.DimensionFilters.SingleOrDefault(d => d.Name == attribute.Key);
                if (dimensionModel is null)
                {
                    dimensionModel = new DimensionFilterViewModel()
                    {
                        Name = attribute.Key
                    };
                    dimensionModel.Values.Add(new DimensionValueViewModel
                    {
                        Name = "All",
                        All = true
                    });
                    _viewModel.DimensionFilters.Add(dimensionModel);
                }
                var valueModel = dimensionModel.Values.SingleOrDefault(v => v.Name == attribute.Value);
                if (valueModel is null)
                {
                    valueModel = new DimensionValueViewModel()
                    {
                        Name = attribute.Value,
                        All = false
                    };
                    dimensionModel.Values.Add(valueModel);
                }
            }
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

    private (List<double?> Y, List<DateTime> X, TimeSpan PointDuration) CalculateChartValues(List<DimensionScope> dimensions, int pointCount, bool tickUpdate, DateTime inProgressDataTime)
    {
        var pointDuration = Duration / pointCount;
        var yValues = new List<double?>();
        var xValues = new List<DateTime>();
        var startDate = _currentDataTime;
        DateTime? firstPointEndTime = null;

        for (var pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            var start = CalcOffset(pointIndex, startDate, pointDuration);
            var end = CalcOffset(pointIndex - 1, startDate, pointDuration);
            firstPointEndTime ??= end;

            xValues.Add(end.ToLocalTime());

            if (TryCalculateValue(dimensions, start, end, out var tickPointValue))
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

        if (tickUpdate && TryCalculateValue(dimensions, firstPointEndTime!.Value, inProgressDataTime, out var inProgressPointValue))
        {
            yValues.Add(inProgressPointValue);
            xValues.Add(inProgressDataTime.ToLocalTime());
        }
        else
        {
            yValues.Add(yValues.Last());
            xValues.Add(xValues.Last());
        }

        return (yValues, xValues, pointDuration);
    }

    private static bool TryCalculateValue(List<DimensionScope> dimensions, DateTime start, DateTime end, out double pointValue)
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
                        _ => throw new InvalidOperationException("Unexpected metric type: " + metric.GetType())
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

        while (_currentDataTime.Add(_tickDuration) < inProgressDataTime)
        {
            _currentDataTime = _currentDataTime.Add(_tickDuration);
        }

        if (_dimensionsOrDurationChanged)
        {
            _dimensionsOrDurationChanged = false;

            await UpdateChart(tickUpdate: false, inProgressDataTime).ConfigureAwait(false);
        }
        else if (_lastUpdateTime.Add(TimeSpan.FromSeconds(0.2)) < DateTime.UtcNow)
        {
            _lastUpdateTime = DateTime.UtcNow;
            await UpdateChart(tickUpdate: true, inProgressDataTime).ConfigureAwait(false);
        }
    }

    private async Task UpdateChart(bool tickUpdate, DateTime inProgressDataTime)
    {
        var matchedDimensions = Dimensions.Where(MatchDimension).ToList();
        var (yValues, xValues, pointDuration) = CalculateChartValues(matchedDimensions, GRAPH_POINT_COUNT, tickUpdate, inProgressDataTime);

        var yLabel = Instrument.Unit.TrimStart('{').TrimEnd('}').Pluralize().Titleize();
        await JSRuntime.InvokeVoidAsync("initializeGraph",
            ChartDivId,
            yLabel,
            yValues,
            xValues,
            inProgressDataTime.ToLocalTime(),
            (inProgressDataTime - Duration).ToLocalTime()).ConfigureAwait(false);
    }
}
