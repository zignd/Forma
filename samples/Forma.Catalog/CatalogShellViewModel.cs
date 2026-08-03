// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Forma.Catalog;

public sealed class CatalogShellViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string _storyTitle = string.Empty;
    private string _storyCategory = string.Empty;
    private string _description = string.Empty;
    private string _countText = string.Empty;
    private bool _dynamicTextEnabled;

    public event PropertyChangedEventHandler PropertyChanged;
    public string SearchText { get => _searchText; set => Set(ref _searchText, value); }
    public string StoryTitle { get => _storyTitle; set => Set(ref _storyTitle, value); }
    public string StoryCategory { get => _storyCategory; set => Set(ref _storyCategory, value); }
    public string Description { get => _description; set => Set(ref _description, value); }
    public string CountText { get => _countText; set => Set(ref _countText, value); }
    public bool DynamicTextEnabled { get => _dynamicTextEnabled; set => Set(ref _dynamicTextEnabled, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}