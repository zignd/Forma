// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma
{
    internal sealed class RuntimeTextInputAdapter : IDisposable
    {
        private readonly GameWindow _window;
        private readonly Action<char> _handler;

        public RuntimeTextInputAdapter(Game game, Action<char> handler)
        {
            _window = game.Window;
            _handler = handler;
            _window.TextInput += OnTextInput;
        }

        private void OnTextInput(object sender, TextInputEventArgs e) => _handler(e.Character);

        public void Dispose() => _window.TextInput -= OnTextInput;
    }
}