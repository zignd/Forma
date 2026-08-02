// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma.Catalog;

internal sealed class RuntimeCatalogTextInputAdapter : IDisposable
{
    private readonly GameWindow _window;
    private readonly Action<char> _handler;

    public RuntimeCatalogTextInputAdapter(Game game, Action<char> handler)
    {
        _window = game.Window;
        _handler = handler;
        _window.TextInput += OnTextInput;
    }

    private void OnTextInput(object sender, TextInputEventArgs args) => _handler(args.Character);
    public void Dispose() => _window.TextInput -= OnTextInput;
}