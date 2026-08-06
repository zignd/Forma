// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Forma;
using Forma.Xaml;
using Microsoft.Xna.Framework;

namespace Forma.Catalog;

public sealed class CatalogStoryViewModel : INotifyPropertyChanged
{
    private string _liveText = "Forma office 你好";
    private string _status = string.Empty;

    public event PropertyChangedEventHandler PropertyChanged;
    public string LiveText { get => _liveText; set => Set(ref _liveText, value); }
    public string Status { get => _status; set => Set(ref _status, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class CatalogBindingStoryViewModel : INotifyPropertyChanged
{
    private string _projectName = "Aurora UI";
    private float _completion = 64;
    private bool _autoSaveEnabled = true;

    public event PropertyChangedEventHandler PropertyChanged;
    public string ProjectName { get => _projectName; set { if (Set(ref _projectName, value)) NotifySummary(); } }
    public float Completion { get => _completion; set { if (Set(ref _completion, value)) NotifySummary(); } }
    public bool AutoSaveEnabled { get => _autoSaveEnabled; set { if (Set(ref _autoSaveEnabled, value)) NotifySummary(); } }
    public string CompletionText => $"Progress: {Completion:0}%";
    public string Summary => $"{ProjectName} is {Completion:0}% complete · autosave {(AutoSaveEnabled ? "on" : "off")}";

    public void Reset()
    {
        ProjectName = "Aurora UI";
        Completion = 64;
        AutoSaveEnabled = true;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    private void NotifySummary()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CompletionText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
    }
}

public sealed class CatalogAnimationStoryViewModel : INotifyPropertyChanged
{
    private bool _isLooping;

    public event PropertyChangedEventHandler PropertyChanged;
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            if (_isLooping == value) return;
            _isLooping = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLooping)));
        }
    }
}

public sealed record CatalogTemplateItem(string Name);

public sealed class CatalogThemeButton : BaseButton
{
}

public sealed class CompositionSystemsStoryView : BoxContainer
{
    public CompositionSystemsStoryView() : base(Orientation.Vertical) => FormaXamlLoader.Load(this);

    public int ThemeReplacementCount { get; private set; }

    private void OnApplyThemeTemplate(object sender, EventArgs args)
    {
        var template = (ControlTemplate)Resources["ThemeButtonTemplate"];
        Context.Theme.SetControlTemplate<CatalogThemeButton>(template);
        ThemeReplacementCount++;
    }
}

public sealed record CatalogCollectionItem(string Name);

public sealed class CatalogCollectionStoryViewModel : INotifyPropertyChanged
{
    private int _nextItem = 7;
    private string _lastMutation = "Ready";

    public CatalogCollectionStoryViewModel()
    {
        MutableItems = new ObservableCollection<CatalogCollectionItem>(
            Enumerable.Range(1, 6).Select(index => new CatalogCollectionItem($"Item {index:00}")));
        LargeItems = Enumerable.Range(0, 10_000)
            .Select(index => new CatalogCollectionItem($"Virtual {index:00000}"))
            .ToArray();
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public ObservableCollection<CatalogCollectionItem> MutableItems { get; }
    public CatalogCollectionItem[] LargeItems { get; }
    public string LastMutation { get => _lastMutation; private set { _lastMutation = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastMutation))); } }

    public void Add()
    {
        MutableItems.Insert(0, new CatalogCollectionItem($"Item {_nextItem++:00}"));
        LastMutation = "Add";
    }

    public void Remove()
    {
        if (MutableItems.Count != 0) MutableItems.RemoveAt(0);
        LastMutation = "Remove";
    }

    public void Move()
    {
        if (MutableItems.Count > 1) MutableItems.Move(0, MutableItems.Count - 1);
        LastMutation = "Move";
    }

    public void Replace()
    {
        if (MutableItems.Count != 0) MutableItems[0] = new CatalogCollectionItem($"Replacement {_nextItem++:00}");
        LastMutation = "Replace";
    }

    public void Reset()
    {
        MutableItems.Clear();
        MutableItems.Add(new CatalogCollectionItem("Reset item"));
        LastMutation = "Reset";
    }
}

public sealed class CollectionSystemsStoryView : BoxContainer
{
    public CollectionSystemsStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogCollectionStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
    }

    public CatalogCollectionStoryViewModel ViewModel { get; }

    private void OnAddItem(object sender, EventArgs args) => ViewModel.Add();
    private void OnRemoveItem(object sender, EventArgs args) => ViewModel.Remove();
    private void OnMoveItem(object sender, EventArgs args) => ViewModel.Move();
    private void OnReplaceItem(object sender, EventArgs args) => ViewModel.Replace();
    private void OnResetItems(object sender, EventArgs args) => ViewModel.Reset();
}

public sealed class CatalogEventfulRow : Control
{
    public CatalogEventfulRow() => FormaXamlLoader.Load(this);

    public int HandlerCalls { get; private set; }

    private void OnRowPressed(object sender, EventArgs args) => HandlerCalls++;
}

public sealed class DynamicSizesStoryView : BoxContainer
{
    public DynamicSizesStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogStoryViewModel { Status = "28 px" };
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
        var family = NameScope.GetNameScope(this).Find<OptionButton>("fontFamily");
        family.AddItem("Inter");
        family.AddItem("Noto Sans SC");
    }

    public CatalogStoryViewModel ViewModel { get; }
}

public sealed class StylesStoryView : BoxContainer
{
    public StylesStoryView() : base(Orientation.Vertical) => FormaXamlLoader.Load(this);
}

public sealed class TemplateGalleryStoryView : BoxContainer
{
    public TemplateGalleryStoryView() : base(Orientation.Vertical)
    {
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this);
        var list = scope.Find<ListBox>("ReferenceList");
        list.ItemsSource = new[] { new CatalogTemplateItem("Runtime parity") };
        var navigation = scope.Find<ListBox>("ReferenceNavigation");
        navigation.ItemsPanel = new ItemsPanelTemplate(_ => new StackPanel { Orientation = Orientation.Horizontal, Gap = 8 });
        navigation.ItemsSource = new[] { new CatalogTemplateItem("Overview") };
        var grid = scope.Find<DataGrid>("ReferenceGrid");
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Metric",
            Binding = DataGridBinding<string>.Create<int>(value => $"Frame {value}"),
            Width = GridTrackSize.Star(),
        });
        grid.ItemsSource = new[] { 16 };
        var tree = scope.Find<Tree>("ReferenceTree");
        var root = tree.CreateItem();
        root.Text = "Workspace";
        root.CreateChild().Text = "Templates";
        root.CreateChild().Text = "Resources";
    }
}

public sealed class AnimationsStoryView : BoxContainer
{
    public AnimationsStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogAnimationStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
    }

    public CatalogAnimationStoryViewModel ViewModel { get; }
}

public sealed class DataBindingStoryView : BoxContainer
{
    public DataBindingStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogBindingStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
    }

    public CatalogBindingStoryViewModel ViewModel { get; }

    private void OnResetPressed(object sender, System.EventArgs args) => ViewModel.Reset();
}

public sealed class IconInventoryStoryView : BoxContainer
{
    public IconInventoryStoryView() : base(Orientation.Vertical) => FormaXamlLoader.Load(this);
}

public sealed class RuntimeSvgStoryView : BoxContainer
{
    public RuntimeSvgStoryView() : base(Orientation.Vertical) => FormaXamlLoader.Load(this);
}

public sealed class SvgAtlasPreview : DrawingElement
{
    private static readonly DrawingPath UnitRectangle = CreateUnitRectangle();
    private readonly List<(int Column, int Row, Color Color)> _cells = new();
    private int _columns;
    private int _rows;

    public string Summary { get; private set; } = "atlas pending";

    public void RefreshSnapshot()
    {
        var pages = Context?.GetSvgRasterAtlasPages();
        if (pages == null || pages.Count == 0) return;
        var page = pages[0];
        var pixels = page.Pixels.Span;
        var minX = page.Width;
        var minY = page.Height;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < page.Height; y++)
        for (var x = 0; x < page.Width; x++)
        {
            if (pixels[(y * page.Width + x) * 4 + 3] == 0) continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
        if (maxX < minX || maxY < minY) return;

        _columns = 32;
        _rows = 20;
        _cells.Clear();
        for (var row = 0; row < _rows; row++)
        for (var column = 0; column < _columns; column++)
        {
            var x = minX + (maxX - minX) * column / Math.Max(1, _columns - 1);
            var y = minY + (maxY - minY) * row / Math.Max(1, _rows - 1);
            var offset = (y * page.Width + x) * 4;
            var color = new Color(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
            if (color.A != 0) _cells.Add((column, row, color));
        }
        Summary = $"page {page.Width}x{page.Height}, occupied {maxX - minX + 1}x{maxY - minY + 1}";
    }

    protected override void Draw(DrawingContext context)
    {
        if (_columns == 0 || _rows == 0) return;
        var width = Bounds.Width / (float)_columns;
        var height = Bounds.Height / (float)_rows;
        foreach (var cell in _cells)
        {
            var transform = Matrix.CreateScale(width, height, 1) *
                Matrix.CreateTranslation(Bounds.X + cell.Column * width, Bounds.Y + cell.Row * height, 0);
            context.FillPath(UnitRectangle, cell.Color, transform);
        }
    }

    private static DrawingPath CreateUnitRectangle()
    {
        var path = new DrawingPath().MoveTo(Vector2.Zero).LineTo(Vector2.UnitX).LineTo(Vector2.One).LineTo(Vector2.UnitY).Close();
        path.Freeze();
        return path;
    }
}

public sealed class IconCustomizationStoryView : BoxContainer
{
    public IconCustomizationStoryView() : base(Orientation.Vertical)
    {
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this);
        scope.Find<OptionButton>("default").AddItem("Density");
        scope.Find<OptionButton>("override").AddItem("Density");
        scope.Find<OptionButton>("suppressed").AddItem("Density");
    }
}

public sealed class IconDiagnosticsStoryView : BoxContainer
{
    public IconDiagnosticsStoryView() : base(Orientation.Vertical)
    {
        ViewModel = new CatalogStoryViewModel();
        DataContext = ViewModel;
        FormaXamlLoader.Load(this);
    }

    public CatalogStoryViewModel ViewModel { get; }
}