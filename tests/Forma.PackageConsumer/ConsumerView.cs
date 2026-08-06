// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Collections;
using System.Collections.ObjectModel;
using Forma.Xaml;

namespace Forma.PackageConsumer;

public sealed class ConsumerView : Control
{
    public int AttachedHandlerCalls { get; private set; }

    public ConsumerView()
    {
        FormaXamlLoader.Load(this);
    }

    private void OnStyleTargetAttached(object? sender, EventArgs args) => AttachedHandlerCalls++;
}

public sealed class ConsumerButton : BaseButton
{
}

public sealed class ConsumerResourceValue
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ConsumerTarget : Control
{
    private EventHandler? _stopRequested;

    public int StopRequestedSubscriberCount { get; private set; }
    public ConsumerResourceValue Value { get; set; } = new ConsumerResourceValue { Name = "Underlying" };

    public event EventHandler? StopRequested
    {
        add { _stopRequested += value; StopRequestedSubscriberCount++; }
        remove { _stopRequested -= value; StopRequestedSubscriberCount--; }
    }

    public void RaiseStopRequested() => _stopRequested?.Invoke(this, EventArgs.Empty);
}

public sealed class ConsumerViewModel : INotifyPropertyChanged
{
    private string _message = string.Empty;
    private bool _isActive;

    public ConsumerViewModel()
    {
        GridRows = new[] { new ConsumerGridRow("Beta", 2), new ConsumerGridRow("Alpha", 1) };
        TreeRows = new ObservableCollection<ConsumerTreeRow>
        {
            new ConsumerTreeRow
            {
                Name = "Root",
                IsExpanded = true,
                Children = { new ConsumerTreeRow { Name = "Child" } },
            },
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConsumerGridRow[] GridRows { get; }
    public ObservableCollection<ConsumerTreeRow> TreeRows { get; }

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            _message = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
}

public sealed record ConsumerGridRow(string Name, int Order);

public sealed class ConsumerGridNameColumn : DataGridTextColumn
{
    public ConsumerGridNameColumn()
    {
        Binding = DataGridBinding<string>.Create<ConsumerGridRow>(row => row.Name);
        SortBinding = DataGridSortBinding.Create<ConsumerGridRow, string>(row => row.Name);
    }
}

public sealed class ConsumerGridOrderColumn : DataGridTextColumn
{
    public ConsumerGridOrderColumn()
    {
        Binding = DataGridBinding<string>.Create<ConsumerGridRow>(row => row.Order.ToString());
        SortBinding = DataGridSortBinding.Create<ConsumerGridRow, int>(row => row.Order);
    }
}

public sealed class ConsumerTreeRow
{
    public string Name { get; init; } = string.Empty;
    public bool IsExpanded { get; set; }
    public ObservableCollection<ConsumerTreeRow> Children { get; } = new();
}

public sealed class ConsumerTreeColumn : DataGridExpanderColumn
{
    public ConsumerTreeColumn()
    {
        Children = DataGridBinding<IEnumerable>.Create<ConsumerTreeRow>(row => row.Children);
        HasChildren = DataGridBinding<bool>.Create<ConsumerTreeRow>(row => row.Children.Count != 0);
        IsExpanded = DataGridBinding<bool>.Create<ConsumerTreeRow>(row => row.IsExpanded, write: (row, value) => row.IsExpanded = value);
        Column = new DataGridTextColumn { Binding = DataGridBinding<string>.Create<ConsumerTreeRow>(row => row.Name) };
        SortBinding = DataGridSortBinding.Create<ConsumerTreeRow, string>(row => row.Name);
    }
}