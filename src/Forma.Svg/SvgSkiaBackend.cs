// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SkiaSharp;
using Svg.Skia;

namespace Forma
{
    internal sealed class SvgSkiaBackend : ISvgRasterizerBackend
    {
        private const SvgBackendFeatures Features = SvgBackendFeatures.Paths | SvgBackendFeatures.Gradients |
            SvgBackendFeatures.Clips | SvgBackendFeatures.Transforms | SvgBackendFeatures.LocalReferences |
            SvgBackendFeatures.CurrentColor | SvgBackendFeatures.Shapes | SvgBackendFeatures.Strokes |
            SvgBackendFeatures.Styles | SvgBackendFeatures.Masks | SvgBackendFeatures.ViewBoxes |
            SvgBackendFeatures.PreserveAspectRatio | SvgBackendFeatures.Opacity;

        internal static SvgSkiaBackend Instance { get; } = new SvgSkiaBackend();

        private SvgSkiaBackend() => Health = Probe();

        public SvgBackendHealth Health { get; }

        public ISvgBackendDocument Parse(byte[] source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                var svg = new SKSvg();
                using var stream = new MemoryStream(source, writable: false);
                var picture = svg.Load(stream);
                if (picture == null)
                {
                    svg.Dispose();
                    throw new SvgLoadException(SvgLoadErrorCode.UnsupportedFeature, "Svg.Skia could not parse the validated SVG source.");
                }
                var bounds = picture.CullRect;
                if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
                {
                    svg.Dispose();
                    throw new SvgLoadException(SvgLoadErrorCode.InvalidDimensions, "Svg.Skia produced invalid SVG picture bounds.");
                }
                return new SvgSkiaDocument(svg, picture, bounds);
            }
            catch (SvgLoadException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SvgLoadException(SvgLoadErrorCode.UnsupportedFeature, "Svg.Skia failed to parse the validated SVG source.", exception);
            }
        }

        public SvgRasterData Rasterize(ISvgBackendDocument document, int width, int height)
        {
            if (document is not SvgSkiaDocument skiaDocument) throw new ArgumentException("SVG document was not created by the Svg.Skia backend.", nameof(document));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            var pixels = new byte[checked(width * height * 4)];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
                using var surface = SKSurface.Create(info, handle.AddrOfPinnedObject(), width * 4)
                    ?? throw new InvalidOperationException("SkiaSharp could not create an SVG raster surface.");
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(width / skiaDocument.Bounds.Width, height / skiaDocument.Bounds.Height);
                canvas.Translate(-skiaDocument.Bounds.Left, -skiaDocument.Bounds.Top);
                canvas.DrawPicture(skiaDocument.Picture);
                canvas.Flush();
            }
            finally
            {
                handle.Free();
            }
            return new SvgRasterData(width, height, pixels);
        }

        private static SvgBackendHealth Probe()
        {
            var version = typeof(SKSvg).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(SKSvg).Assembly.GetName().Version?.ToString()
                ?? "unknown";
            try
            {
                using var surface = SKSurface.Create(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
                if (surface == null) return CreateHealth(false, version, "SkiaSharp could not create a health-probe surface.");
                return CreateHealth(true, version, $"Svg.Skia is available for {RuntimeInformation.RuntimeIdentifier}.");
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
            {
                return CreateHealth(false, version, $"SkiaSharp native initialization failed: {exception.GetType().Name}.");
            }
        }

        private static SvgBackendHealth CreateHealth(bool available, string version, string diagnostic) =>
            new SvgBackendHealth(
                true,
                available,
                "skia",
                "Svg.Skia",
                version,
                "1",
                Features,
                available ? SvgNativeAvailability.Packaged : SvgNativeAvailability.Unavailable,
                SvgBackendLinkMode.Dynamic,
                diagnostic);

        private sealed class SvgSkiaDocument : ISvgBackendDocument
        {
            internal SvgSkiaDocument(SKSvg svg, SKPicture picture, SKRect bounds)
            {
                Svg = svg;
                Picture = picture;
                Bounds = bounds;
            }

            private SKSvg Svg { get; }
            internal SKPicture Picture { get; }
            internal SKRect Bounds { get; }
            public void Dispose() => Svg.Dispose();
        }
    }
}