// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Forma.Xaml;

namespace Forma.QuickStart;

public sealed class InventoryListView : BoxContainer
{
    public InventoryListView() : this(new InventoryListViewModel()) { }

    public InventoryListView(InventoryListViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);

        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("InventoryListView did not create a namescope.");
        var list = scope.Find<ListBox>("InventoryList");
        list.SelectionChanged += (_, _) =>
        {
            viewModel.SelectedItem = list.SelectedItem as InventoryItem;
        };
    }
}

public sealed record InventoryItem(string Name, string Category, int Quantity)
{
    public string QuantityText => Quantity.ToString();
}

public sealed class InventoryListViewModel : INotifyPropertyChanged
{
    private InventoryItem? _selectedItem;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<InventoryItem> Items { get; } = Enumerable.Range(1, 24)
        .Select(index => new InventoryItem(
            $"Supply crate {index:00}",
            index % 3 == 0 ? "Utility" : index % 2 == 0 ? "Armor" : "Recovery",
            (index * 3) % 11 + 1))
        .ToArray();

    public InventoryItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value)) return;
            _selectedItem = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionSummary)));
        }
    }

    public string SelectionSummary => SelectedItem is null
        ? "Select an inventory row."
        : $"Selected {SelectedItem.Name} ({SelectedItem.Quantity} available).";
}