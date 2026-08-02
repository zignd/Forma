// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Catalog;

internal sealed class RuntimeCatalogTextInputAdapter : IDisposable
{
    private readonly Action<char> _handler;

    public RuntimeCatalogTextInputAdapter(Game game, Action<char> handler)
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