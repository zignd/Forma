// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forma;
using Forma.Tests;

var stopwatch = Stopwatch.StartNew();
#if THORVG
var health = SvgThorvgBackendDefaults.Health;
SvgThorvgBackendDefaults.Install();
#else
var health = SvgSkiaBackendDefaults.Health;
SvgSkiaBackendDefaults.Install();
#endif
stopwatch.Stop();
var healthMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
if (!health.IsAvailable) throw new InvalidOperationException(health.Diagnostic);

var assembly = typeof(SvgImageSource).Assembly;
var resources = assembly.GetManifestResourceNames()
    .Where(name => name.StartsWith("Forma.ThemeIcons.Svg.", StringComparison.Ordinal) && name.EndsWith(".svg", StringComparison.Ordinal))
    .OrderBy(name => name, StringComparer.Ordinal)
    .ToArray();
if (resources.Length != 67) throw new InvalidOperationException($"Expected 67 theme SVG resources, found {resources.Length}.");

var sources = resources.Select(name => SvgImageSource.FromManifestResource(assembly, name)).ToArray();
var allocatedBefore = GC.GetTotalAllocatedBytes(true);
stopwatch.Restart();
var documents = sources.Select(source => SvgBackendRegistry.Backend.Parse(source.CopySource())).ToArray();
stopwatch.Stop();
var parseMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

stopwatch.Restart();
var rasterBytes = 0;
foreach (var document in documents)
    rasterBytes += SvgBackendRegistry.Backend.Rasterize(document, 32, 32).Pixels.Length;
stopwatch.Stop();
var rasterMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore;

foreach (var document in documents) document.Dispose();

Console.WriteLine(JsonSerializer.Serialize(new
{
    backend = health.BackendId,
    version = health.Version,
    profile = health.ProfileVersion,
    icons = resources.Length,
    healthMilliseconds,
    parseMilliseconds,
    rasterMilliseconds,
    allocatedBytes,
    rasterBytes,
}));

var outputOption = Array.IndexOf(args, "--raster-output");
if (outputOption >= 0)
{
    if (outputOption + 1 >= args.Length) throw new ArgumentException("--raster-output requires a directory.");
    var outputDirectory = Path.GetFullPath(args[outputOption + 1]);
    Directory.CreateDirectory(outputDirectory);
    var entries = new List<object>();
    var scales = new[] { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f };

    foreach (var fixture in SvgProfileV1Corpus.All)
    {
        var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(fixture.Svg));
        using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
        foreach (var scale in scales)
            WriteRaster("profile", fixture.Name, source, document, scale);
    }

    foreach (var resource in resources)
    {
        var name = resource[(resource.LastIndexOf('.', resource.Length - 5) + 1)..^4];
        var source = SvgImageSource.FromManifestResource(assembly, resource);
        using var document = SvgBackendRegistry.Backend.Parse(source.CopySource());
        foreach (var scale in scales)
            WriteRaster("theme", name, source, document, scale);
    }

    File.WriteAllText(Path.Combine(outputDirectory, "manifest.json"), JsonSerializer.Serialize(new
    {
        backend = health.BackendId,
        version = health.Version,
        profile = health.ProfileVersion,
        entries,
    }, new JsonSerializerOptions { WriteIndented = true }));

    void WriteRaster(string group, string name, SvgImageSource source, ISvgBackendDocument document, float scale)
    {
        var width = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.X * scale));
        var height = Math.Max(1, (int)MathF.Ceiling(source.IntrinsicSize.Y * scale));
        var raster = SvgBackendRegistry.Backend.Rasterize(document, width, height);
        var scaleName = scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', '_');
        var fileName = $"{group}-{name}-{scaleName}x.rgba";
        File.WriteAllBytes(Path.Combine(outputDirectory, fileName), raster.Pixels);
        entries.Add(new
        {
            group,
            name,
            scale,
            width,
            height,
            file = fileName,
            sha256 = Convert.ToHexString(SHA256.HashData(raster.Pixels)).ToLowerInvariant(),
        });
    }
}