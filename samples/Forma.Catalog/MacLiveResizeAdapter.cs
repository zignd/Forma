// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

internal sealed class MacLiveResizeAdapter : ILiveResizeAdapter
{
    private const uint Sdl2WindowEvent = 0x200;
    private const byte Sdl2WindowExposed = 3;
    private const byte Sdl2WindowResized = 5;
    private const uint Sdl3WindowExposed = 516;
    private const uint Sdl3WindowPixelSizeChanged = 519;
    private const string CocoaWindowProperty = "SDL.window.cocoa.window";
    private const nint LayerContentsRedrawDuringViewResize = 2;
    private const nint LayerContentsPlacementTopLeft = 11;

    private readonly Game _game;
    private readonly uint _windowId;
    private readonly IntPtr _nativeWindow;
    private readonly IntPtr _inLiveResizeSelector;
    private readonly IntPtr _preservesContentDuringLiveResizeSelector;
    private readonly IntPtr _setPreservesContentDuringLiveResizeSelector;
    private readonly bool _preservesContentDuringLiveResize;
    private readonly NativeViewPolicy[] _nativeViewPolicies;
    private readonly bool _usesSdl3;
    private readonly Sdl2EventFilter _sdl2Filter;
    private readonly Sdl2EventFilter _sdl2Watch;
    private readonly Sdl3EventFilter _sdl3Filter;
    private readonly Sdl3EventFilter _sdl3Watch;
    private readonly Sdl2EventFilter _previousSdl2Filter;
    private readonly Sdl3EventFilter _previousSdl3Filter;
    private readonly IntPtr _previousFilterUserData;
    private bool _ticking;
    private bool _disposed;

    private MacLiveResizeAdapter(Game game, bool usesSdl3)
    {
        _game = game;
        _usesSdl3 = usesSdl3;
        _inLiveResizeSelector = sel_registerName("inLiveResize");
        _preservesContentDuringLiveResizeSelector = sel_registerName("preservesContentDuringLiveResize");
        _setPreservesContentDuringLiveResizeSelector = sel_registerName("setPreservesContentDuringLiveResize:");

        if (usesSdl3)
        {
            _windowId = SDL3_GetWindowID(game.Window.Handle);
            var properties = SDL3_GetWindowProperties(game.Window.Handle);
            _nativeWindow = SDL3_GetPointerProperty(properties, CocoaWindowProperty, IntPtr.Zero);
            _sdl3Filter = HandleSdl3Event;
            _sdl3Watch = WatchSdl3Event;
            SDL3_GetEventFilter(out _previousSdl3Filter, out _previousFilterUserData);
            SDL3_SetEventFilter(_sdl3Filter, IntPtr.Zero);
            SDL3_AddEventWatch(_sdl3Watch, IntPtr.Zero);
        }
        else
        {
            _windowId = SDL2_GetWindowID(game.Window.Handle);
            _nativeWindow = GetSdl2CocoaWindow(game.Window.Handle);
            _sdl2Filter = HandleSdl2Event;
            _sdl2Watch = WatchSdl2Event;
            SDL2_GetEventFilter(out _previousSdl2Filter, out _previousFilterUserData);
            SDL2_SetEventFilter(_sdl2Filter, IntPtr.Zero);
            SDL2_AddEventWatch(_sdl2Watch, IntPtr.Zero);
        }

        _preservesContentDuringLiveResize = objc_msgSend_bool(
            _nativeWindow,
            _preservesContentDuringLiveResizeSelector);
        objc_msgSend_void_bool(_nativeWindow, _setPreservesContentDuringLiveResizeSelector, false);
        _nativeViewPolicies = ConfigureNativeViewPolicies(_nativeWindow);
    }

    public static MacLiveResizeAdapter TryCreate(Game game)
    {
        if (!OperatingSystem.IsMacOS()) return null;

        try
        {
#if FORMA_CATALOG_FNA
            var usesSdl3 = Environment.GetEnvironmentVariable("FNA_PLATFORM_BACKEND") != "SDL2";
#else
            const bool usesSdl3 = false;
#endif
            var adapter = new MacLiveResizeAdapter(game, usesSdl3);
            if (adapter._windowId != 0 && adapter._nativeWindow != IntPtr.Zero) return adapter;
            adapter.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Console.Error.WriteLine($"Forma Catalog: macOS live resize is unavailable: {exception.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        objc_msgSend_void_bool(
            _nativeWindow,
            _setPreservesContentDuringLiveResizeSelector,
            _preservesContentDuringLiveResize);
        RestoreNativeViewPolicies(_nativeViewPolicies);

        if (_usesSdl3)
        {
            SDL3_RemoveEventWatch(_sdl3Watch, IntPtr.Zero);
            SDL3_SetEventFilter(_previousSdl3Filter, _previousFilterUserData);
        }
        else
        {
            SDL2_DelEventWatch(_sdl2Watch, IntPtr.Zero);
            SDL2_SetEventFilter(_previousSdl2Filter, _previousFilterUserData);
        }
    }

    private int HandleSdl2Event(IntPtr userData, IntPtr eventPointer)
    {
        if (_previousSdl2Filter != null && _previousSdl2Filter(_previousFilterUserData, eventPointer) == 0) return 0;
        if ((uint)Marshal.ReadInt32(eventPointer) == Sdl2WindowEvent &&
            (uint)Marshal.ReadInt32(eventPointer, 8) == _windowId &&
            Marshal.ReadByte(eventPointer, 12) == Sdl2WindowExposed &&
            IsLiveResizing())
        {
            return 0;
        }
        return 1;
    }

    private int WatchSdl2Event(IntPtr userData, IntPtr eventPointer)
    {
        if ((uint)Marshal.ReadInt32(eventPointer) == Sdl2WindowEvent &&
            (uint)Marshal.ReadInt32(eventPointer, 8) == _windowId &&
            Marshal.ReadByte(eventPointer, 12) == Sdl2WindowResized &&
            IsLiveResizing())
        {
            TickDuringLiveResize();
        }
        return 1;
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private bool HandleSdl3Event(IntPtr userData, IntPtr eventPointer)
    {
        if (_previousSdl3Filter != null && !_previousSdl3Filter(_previousFilterUserData, eventPointer)) return false;
        if ((uint)Marshal.ReadInt32(eventPointer) == Sdl3WindowExposed &&
            (uint)Marshal.ReadInt32(eventPointer, 16) == _windowId &&
            IsLiveResizing())
        {
            return false;
        }
        return true;
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private bool WatchSdl3Event(IntPtr userData, IntPtr eventPointer)
    {
        if ((uint)Marshal.ReadInt32(eventPointer) == Sdl3WindowPixelSizeChanged &&
            (uint)Marshal.ReadInt32(eventPointer, 16) == _windowId &&
            IsLiveResizing())
        {
            TickDuringLiveResize();
        }
        return true;
    }

    public bool TryGetLogicalViewport(out Vector2 viewport)
    {
        viewport = default;
        if (_disposed) return false;

        int width;
        int height;
        if (_usesSdl3)
            SDL3_GetWindowSize(_game.Window.Handle, out width, out height);
        else
            SDL2_GetWindowSize(_game.Window.Handle, out width, out height);

        if (width <= 0 || height <= 0) return false;
        viewport = new Vector2(width, height);
        return true;
    }

    private bool IsLiveResizing()
    {
        return !_disposed && objc_msgSend_bool(_nativeWindow, _inLiveResizeSelector);
    }

    private void TickDuringLiveResize()
    {
        // Cocoa blocks Game.Run while dragging, so render from its synchronous resize notification.
        if (_ticking || !IsLiveResizing()) return;

        try
        {
            _ticking = true;
            SynchronizeGraphicsDevice();
            _game.Tick();
        }
        finally
        {
            _ticking = false;
        }
    }

    private void SynchronizeGraphicsDevice()
    {
        int width;
        int height;
        if (_usesSdl3)
            SDL3_GetWindowSizeInPixels(_game.Window.Handle, out width, out height);
#if FORMA_CATALOG_FNA
        else
            SDL2_Metal_GetDrawableSize(_game.Window.Handle, out width, out height);
#else
        else
            SDL2_GL_GetDrawableSize(_game.Window.Handle, out width, out height);
#endif
        if (width <= 0 || height <= 0) return;

        var graphicsDevice = _game.GraphicsDevice;
        var parameters = graphicsDevice.PresentationParameters;
        if (parameters.BackBufferWidth == width && parameters.BackBufferHeight == height) return;

#if FORMA_CATALOG_FNA
        parameters = parameters.Clone();
        parameters.BackBufferWidth = width;
        parameters.BackBufferHeight = height;
        graphicsDevice.Reset(parameters);
#else
        if (graphicsDevice.RasterizerState.ScissorTestEnable && graphicsDevice.ScissorRectangle == graphicsDevice.Viewport.Bounds)
            graphicsDevice.ScissorRectangle = new Rectangle(0, 0, width, height);
        parameters.BackBufferWidth = width;
        parameters.BackBufferHeight = height;
        graphicsDevice.Viewport = new Viewport(0, 0, width, height);
#endif
    }

    private static IntPtr GetSdl2CocoaWindow(IntPtr window)
    {
        SDL2_GetVersion(out var version);
        var info = new Sdl2SysWmInfo { Version = version };
        return SDL2_GetWindowWMInfo(window, ref info) != 0 ? info.CocoaWindow : IntPtr.Zero;
    }

    private static NativeViewPolicy[] ConfigureNativeViewPolicies(IntPtr window)
    {
        var policies = new List<NativeViewPolicy>();
        var contentView = objc_msgSend_IntPtr(window, sel_registerName("contentView"));
        ConfigureNativeViewPolicies(contentView, policies);
        return policies.ToArray();
    }

    private static void ConfigureNativeViewPolicies(IntPtr view, List<NativeViewPolicy> policies)
    {
        if (view == IntPtr.Zero) return;

        var redrawPolicySelector = sel_registerName("layerContentsRedrawPolicy");
        var placementSelector = sel_registerName("layerContentsPlacement");
        policies.Add(new NativeViewPolicy(
            view,
            objc_msgSend_nint(view, redrawPolicySelector),
            objc_msgSend_nint(view, placementSelector)));
        objc_msgSend_void_nint(view, sel_registerName("setLayerContentsRedrawPolicy:"), LayerContentsRedrawDuringViewResize);
        objc_msgSend_void_nint(view, sel_registerName("setLayerContentsPlacement:"), LayerContentsPlacementTopLeft);

        var subviews = objc_msgSend_IntPtr(view, sel_registerName("subviews"));
        var count = objc_msgSend_nuint(subviews, sel_registerName("count"));
        for (nuint index = 0; index < count; index++)
        {
            var child = objc_msgSend_IntPtr_nuint(subviews, sel_registerName("objectAtIndex:"), index);
            ConfigureNativeViewPolicies(child, policies);
        }
    }

    private static void RestoreNativeViewPolicies(NativeViewPolicy[] policies)
    {
        if (policies == null) return;

        var setRedrawPolicySelector = sel_registerName("setLayerContentsRedrawPolicy:");
        var setPlacementSelector = sel_registerName("setLayerContentsPlacement:");
        foreach (var policy in policies)
        {
            objc_msgSend_void_nint(policy.View, setRedrawPolicySelector, policy.RedrawPolicy);
            objc_msgSend_void_nint(policy.View, setPlacementSelector, policy.Placement);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Sdl2EventFilter(IntPtr userData, IntPtr eventPointer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool Sdl3EventFilter(IntPtr userData, IntPtr eventPointer);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Sdl2Version
    {
        public byte Major;
        public byte Minor;
        public byte Patch;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct Sdl2SysWmInfo
    {
        [FieldOffset(0)] public Sdl2Version Version;
        [FieldOffset(8)] public IntPtr CocoaWindow;
    }

    private readonly record struct NativeViewPolicy(IntPtr View, nint RedrawPolicy, nint Placement);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetEventFilter")]
    private static extern int SDL2_GetEventFilter(out Sdl2EventFilter filter, out IntPtr userData);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_SetEventFilter")]
    private static extern void SDL2_SetEventFilter(Sdl2EventFilter filter, IntPtr userData);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_AddEventWatch")]
    private static extern void SDL2_AddEventWatch(Sdl2EventFilter filter, IntPtr userData);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_DelEventWatch")]
    private static extern void SDL2_DelEventWatch(Sdl2EventFilter filter, IntPtr userData);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowID")]
    private static extern uint SDL2_GetWindowID(IntPtr window);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetVersion")]
    private static extern void SDL2_GetVersion(out Sdl2Version version);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowSize")]
    private static extern void SDL2_GetWindowSize(IntPtr window, out int width, out int height);

    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowWMInfo")]
    private static extern int SDL2_GetWindowWMInfo(IntPtr window, ref Sdl2SysWmInfo info);

#if !FORMA_CATALOG_FNA
    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GL_GetDrawableSize")]
    private static extern void SDL2_GL_GetDrawableSize(IntPtr window, out int width, out int height);
#else
    [DllImport("libSDL2-2.0.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_Metal_GetDrawableSize")]
    private static extern void SDL2_Metal_GetDrawableSize(IntPtr window, out int width, out int height);
#endif

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetEventFilter")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL3_GetEventFilter(out Sdl3EventFilter filter, out IntPtr userData);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_SetEventFilter")]
    private static extern void SDL3_SetEventFilter(Sdl3EventFilter filter, IntPtr userData);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_AddEventWatch")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL3_AddEventWatch(Sdl3EventFilter filter, IntPtr userData);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_RemoveEventWatch")]
    private static extern void SDL3_RemoveEventWatch(Sdl3EventFilter filter, IntPtr userData);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowID")]
    private static extern uint SDL3_GetWindowID(IntPtr window);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowProperties")]
    private static extern uint SDL3_GetWindowProperties(IntPtr window);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowSize")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL3_GetWindowSize(IntPtr window, out int width, out int height);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetWindowSizeInPixels")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SDL3_GetWindowSizeInPixels(IntPtr window, out int width, out int height);

    [DllImport("libSDL3.0.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SDL_GetPointerProperty")]
    private static extern IntPtr SDL3_GetPointerProperty(uint properties, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr defaultValue);

    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_bool(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_nuint(IntPtr receiver, IntPtr selector, nuint value);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_nint(IntPtr receiver, IntPtr selector, nint value);
}