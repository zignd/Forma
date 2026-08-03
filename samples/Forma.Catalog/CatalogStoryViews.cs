// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Forma;
using Forma.Xaml;

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

public sealed class IconInventoryStoryView : BoxContainer
{
    public IconInventoryStoryView() : base(Orientation.Vertical) => FormaXamlLoader.Load(this);
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