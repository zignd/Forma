// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;

namespace Forma
{
    internal sealed class SvgThorvgBackend : ISvgRasterizerBackend
    {
        private const SvgBackendFeatures Features = SvgBackendFeatures.Paths | SvgBackendFeatures.Gradients |
            SvgBackendFeatures.Clips | SvgBackendFeatures.Transforms | SvgBackendFeatures.LocalReferences |
            SvgBackendFeatures.Shapes | SvgBackendFeatures.Strokes | SvgBackendFeatures.Styles |
            SvgBackendFeatures.Masks | SvgBackendFeatures.ViewBoxes | SvgBackendFeatures.PreserveAspectRatio |
            SvgBackendFeatures.Opacity | SvgBackendFeatures.CurrentColor;

        internal static SvgThorvgBackend Instance { get; } = new SvgThorvgBackend();

        private SvgThorvgBackend() => Health = Probe();

        public SvgBackendHealth Health { get; }

        public unsafe ISvgBackendDocument Parse(byte[] source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!Health.IsNativeAvailable) throw new SvgLoadException(SvgLoadErrorCode.UnsupportedFeature, Health.Diagnostic);

            fixed (byte* sourcePointer = source)
            {
                var result = ThorvgNative.DocumentCreate(sourcePointer, checked((nuint)source.Length), out var handle);
                if (result != ThorvgResult.Success)
                    throw MapError(result, "ThorVG failed to parse the validated SVG source.");
                return new SvgThorvgDocument(new ThorvgDocumentHandle(handle));
            }
        }

        public unsafe SvgRasterData Rasterize(ISvgBackendDocument document, int width, int height)
        {
            if (document is not SvgThorvgDocument thorvgDocument)
                throw new ArgumentException("SVG document was not created by the ThorVG backend.", nameof(document));
            if (thorvgDocument.Handle.IsClosed || thorvgDocument.Handle.IsInvalid)
                throw new ObjectDisposedException(nameof(document));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            var pixels = new byte[checked(width * height * 4)];
            lock (thorvgDocument.SyncRoot)
            {
                fixed (byte* pixelPointer = pixels)
                {
                    var result = ThorvgNative.DocumentRasterize(
                        thorvgDocument.Handle,
                        checked((uint)width),
                        checked((uint)height),
                        pixelPointer,
                        checked((nuint)pixels.Length));
                    if (result != ThorvgResult.Success)
                        throw MapError(result, "ThorVG failed to rasterize the SVG document.");
                }
            }
            return new SvgRasterData(width, height, pixels);
        }

        private static unsafe SvgBackendHealth Probe()
        {
            try
            {
                var abiVersion = ThorvgNative.AbiVersion();
                if (abiVersion != ThorvgNative.ExpectedAbiVersion)
                    return CreateHealth(false, "unknown", $"ThorVG ABI mismatch: managed expects {ThorvgNative.ExpectedAbiVersion}, native provides {abiVersion}.");
                if (ThorvgNative.Initialize() != ThorvgResult.Success)
                    return CreateHealth(false, "unknown", "ThorVG native engine initialization failed.");

                var version = Marshal.PtrToStringUTF8(ThorvgNative.Version()) ?? "unknown";
                const string probeSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'><rect width='1' height='1'/></svg>";
                var source = System.Text.Encoding.UTF8.GetBytes(probeSvg);
                fixed (byte* sourcePointer = source)
                {
                    if (ThorvgNative.DocumentCreate(sourcePointer, checked((nuint)source.Length), out var handle) != ThorvgResult.Success)
                        return CreateHealth(false, version, $"ThorVG health parse failed: {ThorvgNative.GetLastError()}");
                    using var document = new ThorvgDocumentHandle(handle);
                    byte* pixel = stackalloc byte[4];
                    if (ThorvgNative.DocumentRasterize(document, 1, 1, pixel, 4) != ThorvgResult.Success)
                        return CreateHealth(false, version, $"ThorVG health raster failed: {ThorvgNative.GetLastError()}");
                }
                return CreateHealth(true, version, $"ThorVG is available for {RuntimeInformation.RuntimeIdentifier}.");
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
            {
                return CreateHealth(false, "unknown", $"ThorVG native initialization failed: {exception.GetType().Name}.");
            }
        }

        private static SvgBackendHealth CreateHealth(bool available, string version, string diagnostic) =>
            new SvgBackendHealth(
                true,
                available,
                "thorvg",
                "ThorVG",
                version,
                "1",
                Features,
                available ? NativeAvailability : SvgNativeAvailability.Unavailable,
                LinkMode,
                diagnostic);

        #if FORMA_THORVG_STATIC
            private const SvgNativeAvailability NativeAvailability = SvgNativeAvailability.HostProvided;
            private const SvgBackendLinkMode LinkMode = SvgBackendLinkMode.Static;
        #else
            private const SvgNativeAvailability NativeAvailability = SvgNativeAvailability.Packaged;
            private const SvgBackendLinkMode LinkMode = SvgBackendLinkMode.Dynamic;
        #endif

        private static SvgLoadException MapError(ThorvgResult result, string message)
        {
            var code = result == ThorvgResult.InvalidArgument
                ? SvgLoadErrorCode.InvalidDimensions
                : SvgLoadErrorCode.UnsupportedFeature;
            var detail = ThorvgNative.GetLastError();
            return new SvgLoadException(code, $"{message} Native result: {result}. {detail}".TrimEnd());
        }

        private sealed class SvgThorvgDocument : ISvgBackendDocument
        {
            internal SvgThorvgDocument(ThorvgDocumentHandle handle) => Handle = handle;
            internal ThorvgDocumentHandle Handle { get; }
            internal object SyncRoot { get; } = new object();
            public void Dispose() => Handle.Dispose();
        }
    }
}
