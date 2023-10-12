// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Model;
using Aspire.Dashboard.Otlp.Model;
using Aspire.Dashboard.Otlp.Model.MetricValues;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aspire.Dashboard.Components;

public partial class ChartContainer : ComponentBase
{
    private readonly CounterChartViewModel _viewModel = new();

    private List<DimensionScope> _matchedDimensions = new();

    [Inject]
    public required IJSRuntime JSRuntime { get; set; }

    [Parameter, EditorRequired]
    public required OtlpInstrument Instrument { get; set; }

    [Parameter, EditorRequired]
    public required DimensionScope[] Dimensions { get; set; }

    [Parameter, EditorRequired]
    public required TimeSpan Duration { get; set; }

    public Task DimensionValuesChangedAsync(DimensionFilterViewModel dimensionViewModel)
    {
        _matchedDimensions = Dimensions.Where(MatchDimension).ToList();
        return Task.CompletedTask;
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
        // No filter selected.
        if (!filter.SelectedValues.Any())
        {
            return true;
        }

        var value = OtlpHelpers.GetValue(attributes, filter.Name);
        foreach (var item in filter.SelectedValues)
        {
            if (item.Empty && string.IsNullOrEmpty(value))
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
        _viewModel.DimensionFilters.Clear();

        foreach (var item in Instrument.KnownAttributeValues.OrderBy(kvp => kvp.Key))
        {
            var dimensionModel = new DimensionFilterViewModel
            {
                Name = item.Key
            };
            //dimensionModel.Values.Add(new DimensionValueViewModel
            //{
            //    Name = "(All)",
            //    All = true
            //});
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
        }

        _matchedDimensions = Dimensions.Where(MatchDimension).ToList();
    }
}
