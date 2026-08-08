// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Forma.Xaml;

namespace Forma.QuickStart;

public sealed class DataGridExampleView : BoxContainer
{
    public DataGridExampleView() : this(new DataGridExampleViewModel()) { }

    public DataGridExampleView(DataGridExampleViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("DataGridExampleView did not create a namescope.");
        scope.Find<Button>("AdvanceButton").Pressed += (_, _) => viewModel.AdvanceSelected();
        scope.Find<Button>("AddButton").Pressed += (_, _) => viewModel.AddQuest();
    }
}

public sealed class QuestRow : INotifyPropertyChanged
{
    private int _progress;

    public QuestRow(string name, string region, int progress)
    {
        Name = name;
        Region = region;
        _progress = progress;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; }
    public string Region { get; }
    public int Progress => _progress;
    public string ProgressText => $"{Progress}%";

    public void Advance()
    {
        _progress = Math.Min(100, _progress + 10);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressText)));
    }
}

public sealed class DataGridExampleViewModel : INotifyPropertyChanged
{
    private int _selectedIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<QuestRow> Rows { get; } = new()
    {
        new("Signal relay", "North ridge", 30),
        new("Archive scan", "Lower vault", 60),
        new("Beacon repair", "Eastern pass", 80),
    };

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => Set(ref _selectedIndex, value);
    }

    public void AdvanceSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Rows.Count) Rows[SelectedIndex].Advance();
    }

    public void AddQuest()
    {
        Rows.Add(new QuestRow($"Field task {Rows.Count + 1}", "Unassigned", 0));
        SelectedIndex = Rows.Count - 1;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class QuestNameColumn : DataGridTextColumn
{
    public QuestNameColumn() => Binding = DataGridBinding<string>.Create<QuestRow>(row => row.Name);
}

public sealed class QuestRegionColumn : DataGridTextColumn
{
    public QuestRegionColumn() => Binding = DataGridBinding<string>.Create<QuestRow>(row => row.Region);
}

public sealed class QuestProgressColumn : DataGridTextColumn
{
    public QuestProgressColumn() => Binding = DataGridBinding<string>.Create<QuestRow>(row => row.ProgressText);
}