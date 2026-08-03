// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Forma.Catalog;

internal sealed class LinuxResizeAdapter : ILiveResizeAdapter
{
    private readonly Game _game;
    private readonly bool _usesSdl3;

    private LinuxResizeAdapter(Game game, bool usesSdl3)
    {
        _game = game;
        _usesSdl3 = usesSdl3;
    }

    public static LinuxResizeAdapter TryCreate(Game game)
    {
        if (!OperatingSystem.IsLinux()) return null;

        try
        {
#if FORMA_CATALOG_FNA
            var usesSdl3 = Environment.GetEnvironmentVariable("FNA_PLATFORM_BACKEND") != "SDL2";
#else
            const bool usesSdl3 = false;
#endif
            var adapter = new LinuxResizeAdapter(game, usesSdl3);
            var windowId = usesSdl3
                ? SDL3_GetWindowID(game.Window.Handle)
                : SDL2_GetWindowID(game.Window.Handle);
            return windowId != 0 ? adapter : null;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Console.Error.WriteLine($"Forma Catalog: Linux resize tracking is unavailable: {exception.Message}");
            return null;
        }
    }

    public bool TryGetLogicalViewport(out Vector2 viewport)
    {
        int width;
        int height;
        if (_usesSdl3)
            SDL3_GetWindowSize(_game.Window.Handle, out width, out height);
        else
            SDL2_GetWindowSize(_game.Window.Handle, out width, out height);

        if (width <= 0 || height <= 0)
        {
            viewport = default;
            return false;
        }
        viewport = new Vector2(width, height);
        return true;
    }

    public void Dispose()
    {
    }

    [DllImport("libSDL2-2.0.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowID")]
    private static extern uint SDL2_GetWindowID(IntPtr window);

    [DllImport("libSDL2-2.0.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowSize")]
    private static extern void SDL2_GetWindowSize(IntPtr window, out int width, out int height);

    [DllImport("libSDL3.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowID")]
    private static extern uint SDL3_GetWindowID(IntPtr window);

    [DllImport("libSDL3.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowSize")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL3_GetWindowSize(IntPtr window, out int width, out int height);
}