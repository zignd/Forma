// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Threading;

namespace Forma
{
    /// <summary>Identifies the bounded SVG features advertised by the installed companion backend.</summary>
    [Flags]
    public enum SvgBackendFeatures
    {
        None = 0,
        Paths = 1 << 0,
        Gradients = 1 << 1,
        Clips = 1 << 2,
        Transforms = 1 << 3,
        LocalReferences = 1 << 4,
        CurrentColor = 1 << 5,
    }

    /// <summary>Describes backend registration, native availability, version, and diagnostics.</summary>
    public readonly struct SvgBackendHealth
    {
        internal SvgBackendHealth(bool isRegistered, bool isNativeAvailable, string name, string version, SvgBackendFeatures supportedFeatures, string diagnostic)
        {
            IsRegistered = isRegistered;
            IsNativeAvailable = isNativeAvailable;
            Name = name ?? string.Empty;
            Version = version ?? string.Empty;
            SupportedFeatures = supportedFeatures;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool IsRegistered { get; }
        public bool IsNativeAvailable { get; }
        public bool IsAvailable => IsRegistered && IsNativeAvailable;
        public string Name { get; }
        public string Version { get; }
        public SvgBackendFeatures SupportedFeatures { get; }
        public string Diagnostic { get; }
    }

    /// <summary>Exposes health information for the statically registered runtime SVG backend.</summary>
    public static class SvgRuntime
    {
        /// <summary>Gets the current backend health without creating a graphics device.</summary>
        public static SvgBackendHealth Health => SvgBackendRegistry.Health;
    }

    internal interface IDefaultThemeSvgSourceProvider
    {
        SvgImageSource GetSource(string name);
    }

    internal static class DefaultThemeSvgProviderRegistry
    {
        private static IDefaultThemeSvgSourceProvider _provider;
        internal static bool IsAvailable => Volatile.Read(ref _provider) != null;
        internal static SvgImageSource GetSource(string name) => Volatile.Read(ref _provider)?.GetSource(name);
        internal static void Register(IDefaultThemeSvgSourceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var current = Interlocked.CompareExchange(ref _provider, provider, null);
            if (current != null && !ReferenceEquals(current, provider))
                throw new InvalidOperationException("A default-theme SVG source provider is already registered.");
        }
    }

    internal interface ISvgBackendDocument : IDisposable
    {
    }

    internal readonly struct SvgRasterData
    {
        internal SvgRasterData(int width, int height, byte[] pixels)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (pixels.Length != checked(width * height * 4)) throw new ArgumentException("SVG raster pixels must contain premultiplied RGBA8 data.", nameof(pixels));
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        internal int Width { get; }
        internal int Height { get; }
        internal byte[] Pixels { get; }
    }

    internal interface ISvgRasterizerBackend
    {
        SvgBackendHealth Health { get; }
        ISvgBackendDocument Parse(byte[] source);
        SvgRasterData Rasterize(ISvgBackendDocument document, int width, int height);
    }

    internal static class SvgBackendRegistry
    {
        private static ISvgRasterizerBackend _backend;
        private static int _started;

        internal static SvgBackendHealth Health => Volatile.Read(ref _backend)?.Health ??
            new SvgBackendHealth(false, false, string.Empty, string.Empty, SvgBackendFeatures.None, "No SVG backend is registered. Reference and initialize the runtime-matched Forma.Svg package.");

        internal static ISvgRasterizerBackend Backend
        {
            get
            {
                Volatile.Write(ref _started, 1);
                var backend = Volatile.Read(ref _backend);
                if (backend == null)
                    throw new InvalidOperationException("No SVG backend is registered. Reference and initialize the runtime-matched Forma.Svg package.");
                if (!backend.Health.IsNativeAvailable)
                    throw new InvalidOperationException(backend.Health.Diagnostic);
                return backend;
            }
        }

        internal static void Register(ISvgRasterizerBackend backend)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            var current = Volatile.Read(ref _backend);
            if (ReferenceEquals(current, backend)) return;
            if (current != null)
                throw new InvalidOperationException($"SVG backend '{current.Health.Name}' is already registered.");
            if (Volatile.Read(ref _started) != 0)
                throw new InvalidOperationException("The SVG backend cannot change after the first document is parsed.");
            Volatile.Write(ref _backend, backend);
        }
    }
}