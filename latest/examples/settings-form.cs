// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Forma.Xaml;

namespace Forma.QuickStart;

public sealed class SettingsFormView : BoxContainer
{
    public SettingsFormView() : this(new SettingsFormViewModel()) { }

    public SettingsFormView(SettingsFormViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);

        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("SettingsFormView did not create a namescope.");
        var displayName = scope.Find<LineEdit>("DisplayNameInput");
        var save = scope.Find<Button>("SaveButton");
        displayName.FocusNext = save;
        save.FocusPrevious = displayName;
        save.Pressed += (_, _) => viewModel.Save();
    }
}

public sealed class SettingsFormViewModel : INotifyPropertyChanged
{
    private string _displayName = string.Empty;
    private string _status = "Enter a display name to continue.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!Set(ref _displayName, value)) return;
            Status = CanSave
                ? $"Ready to save settings for {DisplayName.Trim()}."
                : "Display name must contain at least three characters.";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        }
    }

    public bool CanSave => DisplayName.Trim().Length >= 3;

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public void Save()
    {
        if (CanSave) Status = $"Saved settings for {DisplayName.Trim()}.";
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}