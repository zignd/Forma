// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;

namespace Forma.Xaml.Build.Integration;

public sealed class EventfulRow : Control
{
    public int HandlerCalls { get; private set; }

    public EventfulRow()
    {
        FormaXamlLoader.Load(this);
    }

    private void OnRowStopRequested(object? sender, EventArgs args) => HandlerCalls++;
}