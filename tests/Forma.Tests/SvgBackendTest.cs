// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Text;

namespace Forma.Tests
{
    [TestFixture]
    public sealed class SvgBackendTest
    {
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><path d='M0 0h8v8H0z' fill='#f00'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><linearGradient id='g'><stop stop-color='#f00'/><stop offset='1' stop-color='#00f'/></linearGradient></defs><rect width='8' height='8' fill='url(#g)'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><clipPath id='c'><circle cx='4' cy='4' r='3'/></clipPath></defs><rect width='8' height='8' clip-path='url(#c)' fill='#0f0'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='4' height='4' transform='translate(2 2) rotate(15 2 2)' fill='#00f'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><path id='p' d='M1 1h6v6H1z'/></defs><use href='#p' fill='#fc0'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8' color='#40c080'><rect width='8' height='8' fill='currentColor'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><g fill='#f80' stroke='#048' stroke-width='1' opacity='.8'><rect x='1' y='1' width='4' height='4' rx='1'/><circle cx='9' cy='3' r='2'/><ellipse cx='13' cy='3' rx='2' ry='1'/><line x1='1' y1='8' x2='5' y2='8'/><polyline points='7,9 9,7 11,9'/><polygon points='12,9 14,7 15,9'/></g></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><style>.paint{fill:#2ac;stroke:#fff;stroke-width:1;stroke-linecap:round;stroke-linejoin:bevel;stroke-dasharray:2 1}</style><path class='paint' d='M1 7V1H7' fill-rule='evenodd'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12'><path d='M1 10L6 1L11 10Z' fill='#f00' fill-opacity='.5' stroke='#00f' stroke-opacity='.75' stroke-width='2' stroke-linecap='square' stroke-linejoin='miter' stroke-miterlimit='4' stroke-dasharray='3 1'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='4' height='4' transform='translate(2 2) scale(.8) skewX(10) skewY(5) matrix(1 0 0 1 0 0)' fill='#c0f'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><radialGradient id='g' spreadMethod='reflect' gradientTransform='scale(.8)' colorspace='sRGB'><stop stop-color='#fff' stop-opacity='.9'/><stop offset='1' stop-color='#000'/></radialGradient></defs><circle cx='4' cy='4' r='4' fill='url(#g)'/></svg>")]
        [TestCase("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><mask id='m'><rect width='4' height='8' fill='#fff'/></mask></defs><rect width='8' height='8' fill='#0cf' mask='url(#m)'/></svg>")]
        public void RasterizesSupportedFeatureFixtures(string svg)
        {
            SvgBackendDefaults.Install();
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(svg));
            var backend = SvgBackendRegistry.Backend;
            using var document = backend.Parse(source.CopySource());
            var raster = backend.Rasterize(document, 16, 16);

            Assert.Multiple(() =>
            {
                Assert.That(raster.Width, Is.EqualTo(16));
                Assert.That(raster.Height, Is.EqualTo(16));
                Assert.That(raster.Pixels, Has.Length.EqualTo(16 * 16 * 4));
                Assert.That(raster.Pixels.Where((_, index) => index % 4 == 3), Has.Some.GreaterThan((byte)0));
            });
        }

        [Test]
        public void InstallsParsesAndRasterizesPremultipliedRgba()
        {
            SvgBackendDefaults.Install();
            var health = SvgRuntime.Health;
            var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
                "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='12' viewBox='0 0 16 12'><defs><linearGradient id='g'><stop offset='0' stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff' stop-opacity='.5'/></linearGradient></defs><rect width='16' height='12' fill='url(#g)'/></svg>"));
            var backend = SvgBackendRegistry.Backend;
            using var document = backend.Parse(source.CopySource());
            var raster = backend.Rasterize(document, 32, 24);

            Assert.Multiple(() =>
            {
                Assert.That(health.IsAvailable, Is.True, health.Diagnostic);
                Assert.That(health.Name, Is.EqualTo("Svg.Skia"));
                Assert.That(health.Version, Does.StartWith("5.2.0"));
                Assert.That(health.SupportedFeatures, Is.EqualTo(
                    SvgBackendFeatures.Paths | SvgBackendFeatures.Gradients | SvgBackendFeatures.Clips |
                    SvgBackendFeatures.Transforms | SvgBackendFeatures.LocalReferences | SvgBackendFeatures.CurrentColor));
                Assert.That(raster.Width, Is.EqualTo(32));
                Assert.That(raster.Height, Is.EqualTo(24));
                Assert.That(raster.Pixels, Has.Length.EqualTo(32 * 24 * 4));
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
        public void HonorsViewBoxPreserveAspectRatioModes()
        {
            SvgBackendDefaults.Install();
            var backend = SvgBackendRegistry.Backend;
            var meet = Rasterize("xMidYMid meet");
            var slice = Rasterize("xMidYMid slice");
            var none = Rasterize("none");

            Assert.Multiple(() =>
            {
                Assert.That(Alpha(meet, 10, 1), Is.EqualTo(0));
                Assert.That(Alpha(meet, 10, 10), Is.GreaterThan(0));
                Assert.That(Alpha(slice, 10, 1), Is.GreaterThan(0));
                Assert.That(Alpha(none, 10, 1), Is.GreaterThan(0));
            });

            SvgRasterData Rasterize(string preserveAspectRatio)
            {
                var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 5' preserveAspectRatio='{preserveAspectRatio}'><rect width='10' height='5' fill='#fff'/></svg>";
                var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(svg));
                using var document = backend.Parse(source.CopySource());
                return backend.Rasterize(document, 20, 20);
            }

            static byte Alpha(SvgRasterData raster, int x, int y) => raster.Pixels[(y * raster.Width + x) * 4 + 3];
        }
        [Test]
        public void EveryDefaultThemeSvgResourceParsesAndRasterizes()
        {
            SvgBackendDefaults.Install();
            var assembly = typeof(SvgBackendDefaults).Assembly;
            var resources = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("Forma.ThemeIcons.Svg.", StringComparison.Ordinal) && name.EndsWith(".svg", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(resources, Has.Length.EqualTo(67));
            foreach (var resource in resources)
            {
                var source = SvgImageSource.FromManifestResource(assembly, resource);
                using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
                var width = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.X * 1.25f));
                var height = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.Y * 1.25f));
                var raster = SvgBackendRegistry.Backend.Rasterize(document, width, height);
                Assert.That(raster.Pixels.Where((_, index) => index % 4 == 3), Has.Some.Not.EqualTo((byte)0), resource);
            }
        }

        [Test]
        public void AssertsPaintOutputsForStrokeOpacityDashAndLinecap()
        {
            SvgBackendDefaults.Install();
            var backend = SvgBackendRegistry.Backend;

            // fill-opacity=0.5 on a solid rect must produce partial premultiplied alpha in the interior.
            var fillOpacityRaster = RasterizeSvg("<svg xmlns='http://www.w3.org/2000/svg' width='4' height='4'><rect width='4' height='4' fill='#ff0000' fill-opacity='0.5'/></svg>", 4, 4);
            Assert.That(fillOpacityRaster.Pixels[(1 * 4 + 1) * 4 + 3], Is.GreaterThan((byte)20).And.LessThan((byte)210),
                "fill-opacity=0.5 must produce partial premultiplied alpha.");

            // stroke-opacity=0.5 must produce partial-alpha stroke pixels.
            var strokeOpacityRaster = RasterizeSvg("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='6'><path d='M0 3L8 3' stroke='#0000ff' stroke-opacity='0.5' stroke-width='2' fill='none'/></svg>", 8, 6);
            Assert.That(strokeOpacityRaster.Pixels[(3 * 8 + 4) * 4 + 3], Is.GreaterThan((byte)10).And.LessThan((byte)210),
                "stroke-opacity=0.5 must produce partial premultiplied alpha on the stroke.");

            // stroke-dasharray=4 4 must produce opaque dash-on pixels and transparent gap pixels.
            var dashRaster = RasterizeSvg("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='6'><path d='M0 3L16 3' stroke='#000000' stroke-width='2' stroke-dasharray='4 4' stroke-linecap='butt' fill='none'/></svg>", 16, 6);
            Assert.Multiple(() =>
            {
                Assert.That(dashRaster.Pixels[(3 * 16 + 2) * 4 + 3], Is.GreaterThan((byte)0), "stroke-dasharray must paint interior dash-on pixels.");
                Assert.That(dashRaster.Pixels[(3 * 16 + 6) * 4 + 3], Is.EqualTo((byte)0), "stroke-dasharray must leave interior gap pixels fully transparent.");
            });

            // stroke-linecap=square extends by stroke-width/2 past the endpoint; butt does not.
            // Line from (4,4) to (8,4), stroke-width=4: butt covers x=4..8; square covers x=2..10.
            var buttRaster = RasterizeSvg("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='8'><path d='M4 4L8 4' stroke='#000' stroke-width='4' stroke-linecap='butt' fill='none'/></svg>", 12, 8);
            var squareRaster = RasterizeSvg("<svg xmlns='http://www.w3.org/2000/svg' width='12' height='8'><path d='M4 4L8 4' stroke='#000' stroke-width='4' stroke-linecap='square' fill='none'/></svg>", 12, 8);
            Assert.Multiple(() =>
            {
                Assert.That(buttRaster.Pixels[(4 * 12 + 2) * 4 + 3], Is.EqualTo((byte)0), "stroke-linecap=butt must not extend before the endpoint.");
                Assert.That(squareRaster.Pixels[(4 * 12 + 2) * 4 + 3], Is.GreaterThan((byte)0), "stroke-linecap=square must extend by half stroke-width before the endpoint.");
            });

            SvgRasterData RasterizeSvg(string svg, int width, int height)
            {
                var src = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(svg));
                using var doc = backend.Parse(src.CopySource());
                return backend.Rasterize(doc, width, height);
            }
        }
    }
}