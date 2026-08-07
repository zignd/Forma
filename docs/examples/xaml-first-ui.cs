using System.ComponentModel;
using System.Runtime.CompilerServices;
using Forma.Xaml;

namespace Forma.QuickStart;

public sealed class FirstView : BoxContainer
{
    public FirstView() : this(new FirstViewModel()) { }

    public FirstView(FirstViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("FirstView did not create a namescope.");
        scope.Find<Button>("GreetButton").Pressed += (_, _) => viewModel.Greet();
    }
}

public sealed class FirstViewModel : INotifyPropertyChanged
{
    private string _name = "Player";
    private string _greeting = "Ready.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Greeting
    {
        get => _greeting;
        private set => Set(ref _greeting, value);
    }

    public void Greet() => Greeting = $"Hello, {Name.Trim()}!";

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
