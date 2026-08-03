// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using Microsoft.Xna.Framework;

namespace Forma.Catalog;

internal interface ILiveResizeAdapter : IDisposable
{
    bool TryGetLogicalViewport(out Vector2 viewport);
}

internal static class LiveResizeAdapter
{
    public static ILiveResizeAdapter TryCreate(Game game)
    {
        if (OperatingSystem.IsMacOS()) return MacLiveResizeAdapter.TryCreate(game);
        if (OperatingSystem.IsWindows()) return WindowsLiveResizeAdapter.TryCreate(game);
        if (OperatingSystem.IsLinux()) return LinuxResizeAdapter.TryCreate(game);
        return null;
    }
}