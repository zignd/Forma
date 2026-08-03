using Microsoft.Xna.Framework.Input;

namespace Forma.Xaml.Game;

internal sealed class RuntimeGameTextInputAdapter : IDisposable
{
    private readonly Action<char> _handler;

    public RuntimeGameTextInputAdapter(Microsoft.Xna.Framework.Game game, Action<char> handler)
    {
        _handler = handler;
        TextInputEXT.StartTextInput();
        TextInputEXT.TextInput += _handler;
    }

    public void Dispose()
    {
        TextInputEXT.TextInput -= _handler;
        TextInputEXT.StopTextInput();
    }
}