// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;
using Forma;
using Forma.Tests;

var health = SvgThorvgBackendDefaults.Verify();
if (!health.IsAvailable || health.BackendId != "thorvg" || health.ProfileVersion != "1" ||
    health.LinkMode != SvgBackendLinkMode.Static || health.NativeAvailability != SvgNativeAvailability.HostProvided)
    throw new InvalidOperationException($"Static backend health failed: {health.Diagnostic}");

foreach (var fixture in SvgProfileV1Corpus.All)
{
    var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(fixture.Svg));
    using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
    foreach (var scale in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f })
    {
        var width = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.X * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.Y * scale));
        var raster = SvgBackendRegistry.Backend.Rasterize(document, width, height);
        if (!raster.Pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha != 0))
            throw new InvalidOperationException($"Static profile output was empty: {fixture.Name} at {scale}x.");
    }
}

var lifetimeSource = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
    "<svg xmlns='http://www.w3.org/2000/svg' width='4' height='4'><rect width='4' height='4' fill='#4080c0' fill-opacity='.5'/></svg>"));
for (var iteration = 0; iteration < 1_000; iteration++)
{
    using var document = SvgBackendRegistry.Backend.Parse(lifetimeSource.CopySource());
    _ = SvgBackendRegistry.Backend.Rasterize(document, 8, 8);
}

Console.WriteLine($"Static ThorVG host passed {SvgProfileV1Corpus.All.Count * 6} profile rasters and 1,000 lifetimes ({health.Version}, ABI/profile {health.ProfileVersion}).");