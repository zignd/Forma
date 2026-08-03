using Forma.Xaml;
using Microsoft.Xna.Framework;
using System.ComponentModel;

namespace Forma.Xaml.Build.Integration;

public sealed class HudView : Control
{
    public HudView()
    {
        FormaXamlLoader.Load(this);
    }
}

internal static class Program
{
    private static int Main()
    {
        var view = new HudView();
        var model = new HudViewModel { Message = "Ready" };
        view.DataContext = model;
        var label = NameScope.GetNameScope(view)?.Find<Label>("Child");
        var editor = NameScope.GetNameScope(view)?.Find<LineEdit>("Editor");
        if (view.Name != "HudRoot" || view.Position != new Vector2(3, 4) || view.Children.Count != 2 || label?.Text != "Ready" || editor?.Text != "Ready")
            throw new InvalidOperationException("Compiled Forma XAML did not populate the code-behind root.");
        model.Message = "Updated";
        if (label.Text != "Updated") throw new InvalidOperationException("Compiled one-way binding did not observe its typed source property.");
        editor.Text = "Edited";
        if (model.Message != "Edited") throw new InvalidOperationException("Compiled two-way binding did not write through its typed source property.");
        Console.WriteLine("Forma XAML build integration: PASS");
        return 0;
    }
}

public sealed class HudViewModel : INotifyPropertyChanged
{
    private string _message = string.Empty;
    public event PropertyChangedEventHandler? PropertyChanged;
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
}