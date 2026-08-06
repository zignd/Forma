// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json;

if (args.Length != 3) throw new ArgumentException("Usage: Forma.Svg.Compare <skia-dir> <thorvg-dir> <report-dir>");
var left = Load(args[0]);
var right = Load(args[1]);
var reportDirectory = Path.GetFullPath(args[2]);
Directory.CreateDirectory(reportDirectory);
var failures = new List<string>();
var results = new List<object>();

foreach (var leftEntry in left.Entries)
{
    var rightEntry = right.Entries.SingleOrDefault(candidate => candidate.Key == leftEntry.Key)
        ?? throw new InvalidOperationException($"ThorVG manifest lacks {leftEntry.Key}.");
    if (leftEntry.Width != rightEntry.Width || leftEntry.Height != rightEntry.Height)
        throw new InvalidOperationException($"Dimension mismatch for {leftEntry.Key}.");

    var leftPixels = File.ReadAllBytes(Path.Combine(left.Directory, leftEntry.File));
    var rightPixels = File.ReadAllBytes(Path.Combine(right.Directory, rightEntry.File));
    if (leftPixels.Length != rightPixels.Length || leftPixels.Length != leftEntry.Width * leftEntry.Height * 4)
        throw new InvalidOperationException($"Buffer mismatch for {leftEntry.Key}.");

    var leftBounds = Bounds(leftPixels, leftEntry.Width, leftEntry.Height);
    var rightBounds = Bounds(rightPixels, rightEntry.Width, rightEntry.Height);
    var boundLimit = (int)Math.Ceiling(leftEntry.Scale);
    var boundsDelta = Math.Max(Math.Max(Math.Abs(leftBounds.X0 - rightBounds.X0), Math.Abs(leftBounds.Y0 - rightBounds.Y0)),
        Math.Max(Math.Abs(leftBounds.X1 - rightBounds.X1), Math.Abs(leftBounds.Y1 - rightBounds.Y1)));
    var leftCoverage = AlphaCoverage(leftPixels);
    var rightCoverage = AlphaCoverage(rightPixels);
    var coveragePercent = Math.Abs(leftCoverage - rightCoverage) / (leftEntry.Width * leftEntry.Height) * 100;
    var differences = leftPixels.Zip(rightPixels, (first, second) => Math.Abs(first - second)).Order().ToArray();
    var mean = differences.Average();
    var percentile95 = differences[(int)Math.Floor((differences.Length - 1) * .95)];
    var coverageLimit = leftEntry.Group == "theme" ? 4 : 8;
    var meanLimit = leftEntry.Group == "theme" ? 12 : 26;
    var percentile95Limit = leftEntry.Group == "theme" ? 48 : 128;
    var passed = leftBounds.Valid && rightBounds.Valid && boundsDelta <= boundLimit && coveragePercent <= coverageLimit && mean <= meanLimit && percentile95 <= percentile95Limit;
    if (!passed)
        failures.Add($"{leftEntry.Key}: bounds={boundsDelta}/{boundLimit}, coverage={coveragePercent:F2}/{coverageLimit}%, mean={mean:F2}/{meanLimit}, p95={percentile95}/{percentile95Limit}");
    results.Add(new { leftEntry.Group, leftEntry.Name, leftEntry.Scale, boundsDelta, boundLimit, coveragePercent, coverageLimit, mean, meanLimit, percentile95, percentile95Limit, passed });
}

if (right.Entries.Count != left.Entries.Count) throw new InvalidOperationException("Backend manifests have different entry counts.");
File.WriteAllText(Path.Combine(reportDirectory, "comparison.json"), JsonSerializer.Serialize(new
{
    left = new { left.Backend, left.Version, left.Profile },
    right = new { right.Backend, right.Version, right.Profile },
    compared = results.Count,
    failures,
    results,
}, new JsonSerializerOptions { WriteIndented = true }));
WriteContactSheet(left, Path.Combine(reportDirectory, $"contact-sheet-{left.Backend}.ppm"));
WriteContactSheet(right, Path.Combine(reportDirectory, $"contact-sheet-{right.Backend}.ppm"));
if (failures.Count > 0) throw new InvalidOperationException($"{failures.Count} raster comparisons exceeded tolerance. First: {failures[0]}");
Console.WriteLine($"Compared {results.Count} {left.Backend}/{right.Backend} rasters within Profile v1 tolerances.");

static BackendManifest Load(string directory)
{
    directory = Path.GetFullPath(directory);
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
    var root = json.RootElement;
    var entries = root.GetProperty("entries").EnumerateArray().Select(item => new RasterEntry(
        item.GetProperty("group").GetString()!, item.GetProperty("name").GetString()!, item.GetProperty("scale").GetSingle(),
        item.GetProperty("width").GetInt32(), item.GetProperty("height").GetInt32(), item.GetProperty("file").GetString()!)).ToList();
    return new BackendManifest(directory, root.GetProperty("backend").GetString()!, root.GetProperty("version").GetString()!, root.GetProperty("profile").GetString()!, entries);
}

static BoundsValue Bounds(byte[] pixels, int width, int height)
{
    var x0 = width; var y0 = height; var x1 = -1; var y1 = -1;
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
        if (pixels[(y * width + x) * 4 + 3] != 0) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
    return new BoundsValue(x0, y0, x1, y1);
}

static double AlphaCoverage(byte[] pixels)
{
    long total = 0;
    for (var index = 3; index < pixels.Length; index += 4) total += pixels[index];
    return total / 255d;
}

static void WriteContactSheet(BackendManifest manifest, string path)
{
    const int cell = 48;
    const int columns = 12;
    var rows = (manifest.Entries.Count + columns - 1) / columns;
    var rgb = new byte[columns * cell * rows * cell * 3];
    for (var index = 0; index < manifest.Entries.Count; index++)
    {
        var entry = manifest.Entries[index];
        var pixels = File.ReadAllBytes(Path.Combine(manifest.Directory, entry.File));
        var originX = index % columns * cell;
        var originY = index / columns * cell;
        var ratio = Math.Min((cell - 4d) / entry.Width, (cell - 4d) / entry.Height);
        var drawWidth = Math.Max(1, (int)(entry.Width * ratio));
        var drawHeight = Math.Max(1, (int)(entry.Height * ratio));
        for (var y = 0; y < drawHeight; y++)
        for (var x = 0; x < drawWidth; x++)
        {
            var sourceX = Math.Min(entry.Width - 1, (int)(x / ratio));
            var sourceY = Math.Min(entry.Height - 1, (int)(y / ratio));
            var source = (sourceY * entry.Width + sourceX) * 4;
            var target = ((originY + 2 + y) * columns * cell + originX + 2 + x) * 3;
            var alpha = pixels[source + 3];
            var background = ((x / 6 + y / 6) & 1) == 0 ? 224 : 192;
            for (var channel = 0; channel < 3; channel++) rgb[target + channel] = (byte)(pixels[source + channel] + background * (255 - alpha) / 255);
        }
    }
    using var stream = File.Create(path);
    stream.Write(Encoding.ASCII.GetBytes($"P6\n{columns * cell} {rows * cell}\n255\n"));
    stream.Write(rgb);
}

internal sealed record BackendManifest(string Directory, string Backend, string Version, string Profile, List<RasterEntry> Entries);
internal sealed record RasterEntry(string Group, string Name, float Scale, int Width, int Height, string File) { internal string Key => $"{Group}/{Name}/{Scale:R}"; }
internal readonly record struct BoundsValue(int X0, int Y0, int X1, int Y1) { internal bool Valid => X1 >= X0 && Y1 >= Y0; }