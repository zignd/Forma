// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text;

namespace Forma.Tests
{
    [TestFixture]
    public sealed class SvgThorvgBackendTest
    {
        [TestCaseSource(typeof(SvgProfileV1Fixtures), nameof(SvgProfileV1Fixtures.All))]
        public void RasterizesProfileV1AtApprovedScales(string name, string svg)
        {
            SvgThorvgBackendDefaults.Install();
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(svg));
            using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
            foreach (var scale in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f })
            {
                var width = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.X * scale));
                var height = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.Y * scale));
                var raster = SvgBackendRegistry.Backend.Rasterize(document, width, height);
                Assert.That(raster.Pixels.Where((_, index) => index % 4 == 3), Has.Some.GreaterThan((byte)0), $"{name} at {scale}x");
                for (var index = 0; index < raster.Pixels.Length; index += 4)
                {
                    var alpha = raster.Pixels[index + 3];
                    Assert.That(raster.Pixels[index], Is.LessThanOrEqualTo(alpha), $"{name} red at {scale}x");
                    Assert.That(raster.Pixels[index + 1], Is.LessThanOrEqualTo(alpha), $"{name} green at {scale}x");
                    Assert.That(raster.Pixels[index + 2], Is.LessThanOrEqualTo(alpha), $"{name} blue at {scale}x");
                }
            }
        }

        [Test]
        public void InstallsAndRasterizesPremultipliedRgba()
        {
            var health = SvgThorvgBackendDefaults.Verify();
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
                "<svg xmlns='http://www.w3.org/2000/svg' width='4' height='4'><rect width='4' height='4' fill='#ff0000' fill-opacity='.5'/></svg>"));
            using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
            var raster = SvgBackendRegistry.Backend.Rasterize(document, 4, 4);

            Assert.Multiple(() =>
            {
                Assert.That(health.BackendId, Is.EqualTo("thorvg"));
                Assert.That(health.Version, Is.EqualTo("1.1.0"));
                Assert.That(health.ProfileVersion, Is.EqualTo("1"));
                Assert.That(health.LinkMode, Is.EqualTo(SvgBackendLinkMode.Dynamic));
                Assert.That(raster.Pixels, Has.Length.EqualTo(4 * 4 * 4));
                Assert.That(raster.Pixels.Where((_, index) => index % 4 == 3), Has.Some.GreaterThan((byte)0));
            });

            for (var index = 0; index < raster.Pixels.Length; index += 4)
            {
                var alpha = raster.Pixels[index + 3];
                Assert.That(raster.Pixels[index], Is.LessThanOrEqualTo(alpha));
                Assert.That(raster.Pixels[index + 1], Is.LessThanOrEqualTo(alpha));
                Assert.That(raster.Pixels[index + 2], Is.LessThanOrEqualTo(alpha));
            }
        }

        [Test]
        public void RasterizesEveryDefaultThemeSvg()
        {
            SvgThorvgBackendDefaults.Install();
            var assembly = typeof(SvgImageSource).Assembly;
            var resources = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("Forma.ThemeIcons.Svg.", StringComparison.Ordinal) && name.EndsWith(".svg", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(resources, Has.Length.EqualTo(79));
            foreach (var resource in resources)
            {
                var source = SvgImageSource.FromManifestResource(assembly, resource);
                using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
                var raster = SvgBackendRegistry.Backend.Rasterize(document, 32, 32);
                Assert.That(raster.Pixels.Where((_, index) => index % 4 == 3), Has.Some.GreaterThan((byte)0), resource);
            }
        }

        [Test]
        public void RejectsWrongDocumentTypeAndDisposedDocument()
        {
            SvgThorvgBackendDefaults.Install();
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
                "<svg xmlns='http://www.w3.org/2000/svg' width='2' height='2'><rect width='2' height='2'/></svg>"));
            var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
            document.Dispose();

            Assert.That(
                () => SvgBackendRegistry.Backend.Rasterize(document, 2, 2),
                Throws.TypeOf<ObjectDisposedException>());
            Assert.That(
                () => SvgBackendRegistry.Backend.Rasterize(new WrongDocument(), 2, 2),
                Throws.TypeOf<ArgumentException>());
        }

        private sealed class WrongDocument : ISvgBackendDocument
        {
            public void Dispose()
            {
            }
        }
    }
}
