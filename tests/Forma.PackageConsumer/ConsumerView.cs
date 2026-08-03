using System.ComponentModel;
using Forma.Xaml;

namespace Forma.PackageConsumer;

public sealed class ConsumerView : Control
{
    public ConsumerView()
    {
        FormaXamlLoader.Load(this);
    }
}

public sealed class ConsumerViewModel : INotifyPropertyChanged
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