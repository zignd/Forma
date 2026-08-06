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
        Shapes = 1 << 6,
        Strokes = 1 << 7,
        Styles = 1 << 8,
        Masks = 1 << 9,
        ViewBoxes = 1 << 10,
        PreserveAspectRatio = 1 << 11,
        Opacity = 1 << 12,
    }

    /// <summary>Identifies how backend native code is supplied to the process.</summary>
    public enum SvgBackendLinkMode
    {
        Managed,
        Dynamic,
        Static,
    }

    /// <summary>Identifies where an SVG backend's native implementation comes from.</summary>
    public enum SvgNativeAvailability
    {
        Unavailable,
        Managed,
        Packaged,
        HostProvided,
    }

    /// <summary>Describes backend registration, native availability, version, and diagnostics.</summary>
    public readonly struct SvgBackendHealth
    {
        private const int MaxDiagnosticLength = 512;

        internal SvgBackendHealth(
            bool isRegistered,
            bool isNativeAvailable,
            string backendId,
            string name,
            string version,
            string profileVersion,
            SvgBackendFeatures supportedFeatures,
            SvgNativeAvailability nativeAvailability,
            SvgBackendLinkMode linkMode,
            string diagnostic)
        {
            IsRegistered = isRegistered;
            IsNativeAvailable = isNativeAvailable;
            BackendId = backendId ?? string.Empty;
            Name = name ?? string.Empty;
            Version = version ?? string.Empty;
            ProfileVersion = profileVersion ?? string.Empty;
            SupportedFeatures = supportedFeatures;
            NativeAvailability = nativeAvailability;
            LinkMode = linkMode;
            Diagnostic = BoundDiagnostic(diagnostic);
        }

        public bool IsRegistered { get; }
        public bool IsNativeAvailable { get; }
        public bool IsAvailable => IsRegistered && IsNativeAvailable;
        public string BackendId { get; }
        public string Name { get; }
        public string Version { get; }
        public string ProfileVersion { get; }
        public SvgBackendFeatures SupportedFeatures { get; }
        public SvgNativeAvailability NativeAvailability { get; }
        public SvgBackendLinkMode LinkMode { get; }
        public string Diagnostic { get; }

        private static string BoundDiagnostic(string diagnostic)
        {
            if (string.IsNullOrEmpty(diagnostic)) return string.Empty;
            return diagnostic.Length <= MaxDiagnosticLength
                ? diagnostic
                : diagnostic.Substring(0, MaxDiagnosticLength);
        }
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
            new SvgBackendHealth(
                false,
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                SvgBackendFeatures.None,
                SvgNativeAvailability.Unavailable,
                SvgBackendLinkMode.Managed,
                "No SVG backend is registered. Reference and explicitly install a runtime-matched Forma SVG backend package.");

        internal static ISvgRasterizerBackend Backend
        {
            get
            {
                Volatile.Write(ref _started, 1);
                var backend = Volatile.Read(ref _backend);
                if (backend == null)
                    throw new InvalidOperationException("No SVG backend is registered. Reference and explicitly install a runtime-matched Forma SVG backend package.");
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
                throw new InvalidOperationException($"SVG backend '{current.Health.BackendId}' is already registered; cannot install '{backend.Health.BackendId}'.");
            if (Volatile.Read(ref _started) != 0)
                throw new InvalidOperationException($"SVG backend '{backend.Health.BackendId}' cannot be installed after SVG parsing has started.");
            Volatile.Write(ref _backend, backend);
        }
    }
}