// Copyright (c) 2026 Igor Hipolito Vieira
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.ObjectModel;
using Forma;
using Forma.Xaml;

namespace Forma.Catalog;

public sealed record CatalogGridRow(string Name, string Team, int Score);

public sealed class CatalogGridStoryViewModel
{
    public CatalogGridStoryViewModel()
    {
        Rows = Enumerable.Range(1, 5_000)
            .Select(index => new CatalogGridRow($"Contributor {index:0000}", $"Team {(char)('A' + index % 8)}", (index * 37) % 100))
            .ToArray();
    }

    public CatalogGridRow[] Rows { get; }
}

public sealed class CatalogGridNameColumn : DataGridTextColumn
{
    public CatalogGridNameColumn()
    {
        Binding = DataGridBinding<string>.Create<CatalogGridRow>(row => row.Name);
        SortBinding = DataGridSortBinding.Create<CatalogGridRow, string>(row => row.Name);
    }
}

public sealed class CatalogGridTeamColumn : DataGridTextColumn
{
    public CatalogGridTeamColumn()
    {
        Binding = DataGridBinding<string>.Create<CatalogGridRow>(row => row.Team);
        SortBinding = DataGridSortBinding.Create<CatalogGridRow, string>(row => row.Team);
    }
}

public sealed class CatalogGridScoreColumn : DataGridTextColumn
{
    public CatalogGridScoreColumn()
    {
        Binding = DataGridBinding<string>.Create<CatalogGridRow>(row => row.Score.ToString());
        SortBinding = DataGridSortBinding.Create<CatalogGridRow, int>(row => row.Score);
    }
}

public sealed class FlatDataGridStoryView : BoxContainer
{
    private DataGrid _grid;
    private LineEdit _filter;

    public FlatDataGridStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogGridStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this);
        _grid = scope.Find<DataGrid>("FlatGrid");
        _filter = scope.Find<LineEdit>("GridFilter");
    }

    public CatalogGridStoryViewModel ViewModel { get; }

    private void OnFilterChanged(LineEdit sender, string text)
    {
        var query = text?.Trim();
        _grid.Filter = string.IsNullOrEmpty(query)
            ? null
            : item =>
            {
                var row = (CatalogGridRow)item;
                return row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.Team.Contains(query, StringComparison.OrdinalIgnoreCase);
            };
        _grid.RefreshFilter();
    }

    private void OnClearFilter(object sender, EventArgs args) => _filter.Text = string.Empty;
}

public sealed class CatalogTreeRow
{
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public ObservableCollection<CatalogTreeRow> Children { get; } = new();
    public bool IsExpanded { get; set; }
}

public sealed class CatalogTreeStoryViewModel
{
    public CatalogTreeStoryViewModel()
    {
        Roots = new ObservableCollection<CatalogTreeRow>();
        for (var rootIndex = 0; rootIndex < 100; rootIndex++)
        {
            var root = new CatalogTreeRow { Name = $"Workspace {rootIndex + 1:000}", Kind = "Workspace", IsExpanded = rootIndex == 0 };
            for (var childIndex = 0; childIndex < 100; childIndex++)
                root.Children.Add(new CatalogTreeRow { Name = $"Document {rootIndex + 1:000}-{childIndex + 1:000}", Kind = "Document" });
            Roots.Add(root);
        }
    }

    public ObservableCollection<CatalogTreeRow> Roots { get; }
}

public sealed class CatalogTreeNameColumn : DataGridExpanderColumn
{
    public CatalogTreeNameColumn()
    {
        Children = DataGridBinding<IEnumerable>.Create<CatalogTreeRow>(row => row.Children);
        HasChildren = DataGridBinding<bool>.Create<CatalogTreeRow>(row => row.Children.Count != 0);
        IsExpanded = DataGridBinding<bool>.Create<CatalogTreeRow>(row => row.IsExpanded, write: (row, value) => row.IsExpanded = value);
        Column = new DataGridTextColumn
        {
            Binding = DataGridBinding<string>.Create<CatalogTreeRow>(row => row.Name),
            SortBinding = DataGridSortBinding.Create<CatalogTreeRow, string>(row => row.Name),
        };
        SortBinding = DataGridSortBinding.Create<CatalogTreeRow, string>(row => row.Name);
    }
}

public sealed class CatalogTreeKindColumn : DataGridTextColumn
{
    public CatalogTreeKindColumn()
    {
        Binding = DataGridBinding<string>.Create<CatalogTreeRow>(row => row.Kind);
        SortBinding = DataGridSortBinding.Create<CatalogTreeRow, string>(row => row.Kind);
    }
}

public sealed class HierarchicalDataGridStoryView : BoxContainer
{
    private DataGrid _grid;
    private LineEdit _filter;
    private int _addedChildren;

    public HierarchicalDataGridStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogTreeStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this);
        _grid = scope.Find<DataGrid>("TreeGrid");
        _filter = scope.Find<LineEdit>("TreeFilter");
    }

    public CatalogTreeStoryViewModel ViewModel { get; }

    private void OnExpandAll(object sender, EventArgs args) => _grid.ExpandAll();
    private void OnCollapseAll(object sender, EventArgs args) => _grid.CollapseAll();

    private void OnFilterChanged(LineEdit sender, string text)
    {
        var query = text?.Trim();
        _grid.FilterMode = DataGridFilterMode.IncludeAncestorsOfMatches;
        _grid.Filter = string.IsNullOrEmpty(query)
            ? null
            : item => ((CatalogTreeRow)item).Name.Contains(query, StringComparison.OrdinalIgnoreCase);
        _grid.RefreshFilter();
    }

    private void OnAddChild(object sender, EventArgs args)
    {
        var root = ViewModel.Roots[0];
        root.Children.Insert(0, new CatalogTreeRow
        {
            Name = $"Live document {++_addedChildren}",
            Kind = "Observable",
        });
        _grid.Expand(_grid.GetRowPath(0));
    }
}
