// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Text;

namespace Forma
{
    public static class SvgBackendDefaults
    {
        public static SvgBackendHealth Health => SvgSkiaBackend.Instance.Health;

        public static void Install()
        {
            SvgBackendRegistry.Register(SvgSkiaBackend.Instance);
            DefaultThemeSvgProviderRegistry.Register(DefaultThemeSvgSourceProvider.Instance);
        }

        public static SvgBackendHealth Verify()
        {
            Install();
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
                "<svg xmlns='http://www.w3.org/2000/svg' width='2' height='2'><rect width='2' height='2' fill='#80c040'/></svg>"));
            var backend = SvgBackendRegistry.Backend;
            using var document = backend.Parse(source.CopySource());
            var raster = backend.Rasterize(document, 2, 2);
            if (raster.Pixels.Length != 16 || !raster.Pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha != 0))
                throw new InvalidOperationException("Svg.Skia health verification produced an empty raster.");
            return backend.Health;
        }
    }

    internal sealed class DefaultThemeSvgSourceProvider : IDefaultThemeSvgSourceProvider
    {
        internal static readonly DefaultThemeSvgSourceProvider Instance = new DefaultThemeSvgSourceProvider();
        private readonly Dictionary<string, SvgImageSource> _sources = new Dictionary<string, SvgImageSource>(StringComparer.Ordinal);

        public SvgImageSource GetSource(string name)
        {
            lock (_sources)
            {
                if (_sources.TryGetValue(name, out var source)) return source;
                source = SvgImageSource.FromManifestResource(typeof(DefaultThemeSvgSourceProvider).Assembly, $"Forma.ThemeIcons.Svg.{name}.svg");
                _sources.Add(name, source);
                return source;
            }
        }
    }
}