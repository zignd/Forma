// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Text;

namespace Forma
{
    public static class SvgThorvgBackendDefaults
    {
        public static SvgBackendHealth Health => SvgThorvgBackend.Instance.Health;

        public static void Install()
        {
            SvgBackendRegistry.Register(SvgThorvgBackend.Instance);
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
                throw new InvalidOperationException("ThorVG health verification produced an empty raster.");
            return backend.Health;
        }
    }
}
