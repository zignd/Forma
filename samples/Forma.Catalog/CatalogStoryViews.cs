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