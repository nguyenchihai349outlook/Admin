// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Aspire.Dashboard.Model;

public class CounterChartViewModel
{
    public List<DimensionFilterViewModel> DimensionFilters { get; } = new();
}

[DebuggerDisplay("{DebuggerToString(),nq}")]
public class DimensionFilterViewModel
{
    private bool _allSelected;

    public required string Name { get; init; }
    public List<DimensionValueViewModel> Values { get; } = new();
    public IEnumerable<DimensionValueViewModel> SelectedValues { get; set; } = Array.Empty<DimensionValueViewModel>();
    public bool PopupVisible { get; set; }

    public IEnumerable<string> GetSelectedValues()
    {
        var selectedNames = SelectedValues.Where(v => !v.All).Select(v => v.Name).ToList();
        if (selectedNames.Count == 0)
        {
            selectedNames = ["(None)"];
        }

        return selectedNames;
    }

    private string DebuggerToString() => $"Name = {Name}, SelectedValues = {SelectedValues.Count()}";

    public void SelectedValuesChanged()
    {
        if (_allSelected)
        {
            if (!SelectedValues.Any(v => v.All))
            {
                // All was unselected. Clear the selection.
                SelectedValues = Array.Empty<DimensionValueViewModel>();
                _allSelected = false;
            }
            else if (SelectedValues.Count() != Values.Count)
            {
                // Any other value was unselected. Clear all.
                SelectedValues = SelectedValues.Where(v => !v.All).ToArray();
                _allSelected = false;
            }
        }
        else
        {
            if (SelectedValues.Any(v => v.All))
            {
                // All was selected. Select everything.
                SelectedValues = Values.ToArray();
                _allSelected = true;
            }
            else if (SelectedValues.Count(v => !v.All) == Values.Count(v => !v.All))
            {
                // Everything but all was selected. Select all.
                SelectedValues = Values.ToArray();
                _allSelected = true;
            }
        }
    }
}

[DebuggerDisplay("Name = {Name}, All = {All}")]
public class DimensionValueViewModel
{
    public required string Name { get; init; }
    public bool All { get; init; }
    public bool Empty { get; init; }
}

