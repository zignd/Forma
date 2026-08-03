using Microsoft.Xna.Framework;

namespace Forma.Xaml.Game;

internal sealed class RuntimeGameTextInputAdapter : IDisposable
{
    private readonly GameWindow _window;
    private readonly Action<char> _handler;

    public RuntimeGameTextInputAdapter(Microsoft.Xna.Framework.Game game, Action<char> handler)
    {
        _window = game.Window;
        _handler = handler;
        _window.TextInput += OnTextInput;
    }

    private void OnTextInput(object sender, TextInputEventArgs args) => _handler(args.Character);
    public void Dispose() => _window.TextInput -= OnTextInput;
}