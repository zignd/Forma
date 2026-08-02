// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using Svg.Skia;

return IconPipeline.Run(args);

internal static class IconPipeline
{
    private const int Padding = 2;
    private const int LogicalAtlasWidth = 512;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static int Run(string[] args)
    {
        try
        {
            var root = FindRepositoryRoot();
            if (args.Length == 2 && args[0] == "import") Import(root, Path.GetFullPath(args[1]));
            else if (args.Length == 1 && args[0] == "generate") Generate(root, Path.Combine(root, "src/Forma/Resources/ThemeIcons"));
            else if (args.Length == 1 && args[0] == "verify") Verify(root);
            else
            {
                Console.Error.WriteLine("Usage: Forma.IconPipeline import <godot-root> | generate | verify");
                return 2;
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void Import(string root, string godotRoot)
    {
        var configPath = Path.Combine(root, "assets/theme-icons/imports.json");
        var config = Read<ImportConfig>(configPath);
        var actualRevision = RunProcess("git", $"-C \"{godotRoot}\" rev-parse HEAD").Trim();
        if (!string.Equals(actualRevision, config.SourceRevision, StringComparison.Ordinal))
            throw new InvalidDataException($"Godot revision mismatch: expected {config.SourceRevision}, found {actualRevision}.");

        ValidateConfig(config);
        var svgDirectory = Path.Combine(root, "assets/theme-icons/svg");
        Directory.CreateDirectory(svgDirectory);
        foreach (var icon in config.Icons.OrderBy(icon => icon.Name, StringComparer.Ordinal))
        {
            var source = Path.Combine(godotRoot, icon.Source.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source)) throw new FileNotFoundException($"Godot icon not found: {icon.Source}");
            icon.Sha256 = HashFile(source);
            File.Copy(source, Path.Combine(svgDirectory, Path.GetFileName(icon.Source)), true);
        }
        File.Copy(Path.Combine(godotRoot, "LICENSE.txt"), Path.Combine(root, "assets/theme-icons/LICENSE.Godot.txt"), true);
        Write(configPath, config);
        Console.WriteLine($"Imported {config.Icons.Count} runtime icons from Godot {config.SourceRevision}.");
    }

    internal static void Generate(string root, string outputDirectory)
    {
        var config = Read<ImportConfig>(Path.Combine(root, "assets/theme-icons/imports.json"));
        ValidateConfig(config);
        Directory.CreateDirectory(outputDirectory);
        var atlasFiles = new List<AtlasFile>();
        var generatedEntries = new List<GeneratedIcon>();
        foreach (var density in new[] { 1, 2 })
        {
            var rendered = config.Icons.OrderBy(icon => icon.Name, StringComparer.Ordinal)
                .Select(icon => Load(root, icon, density)).ToList();
            try
            {
                var placements = Pack(rendered, density);
                var width = LogicalAtlasWidth * density;
                var height = placements.Max(placement => placement.Y + placement.Icon.Height + Padding * density);
                var fileName = $"theme-icons-{density}x.png";
                WriteAtlas(Path.Combine(outputDirectory, fileName), width, height, density, placements);
                atlasFiles.Add(new AtlasFile(density, fileName, width, height, HashFile(Path.Combine(outputDirectory, fileName))));
                generatedEntries.AddRange(placements.Select(placement => new GeneratedIcon(
                    placement.Icon.Import.Name,
                    density,
                    placement.X,
                    placement.Y,
                    placement.Icon.Width,
                    placement.Icon.Height,
                    placement.Icon.LogicalWidth,
                    placement.Icon.LogicalHeight,
                    placement.Icon.Import.Source,
                    placement.Icon.Import.Sha256,
                    placement.Icon.Import.Bindings,
                    placement.Icon.Import.States,
                    placement.Icon.Import.Directionality)));
            }
            finally
            {
                foreach (var icon in rendered) icon.Dispose();
            }
        }

        var manifest = new GeneratedManifest(
            1,
            config.SourceRevision,
            "Svg.Skia",
            "3.2.0",
            "PNG RGBA8888 sRGB premultiplied alpha",
            Padding,
            atlasFiles,
            generatedEntries);
        Write(Path.Combine(outputDirectory, "theme-icons.json"), manifest);
        Console.WriteLine($"Generated {config.Icons.Count} icons at 1x and 2x.");
    }

    private static void Verify(string root)
    {
        var canonical = Path.Combine(root, "src/Forma/Resources/ThemeIcons");
        var temporary = Path.Combine(Path.GetTempPath(), $"forma-icons-{Guid.NewGuid():N}");
        try
        {
            Generate(root, temporary);
            foreach (var fileName in new[] { "theme-icons-1x.png", "theme-icons-2x.png", "theme-icons.json" })
            {
                var expected = Path.Combine(canonical, fileName);
                var actual = Path.Combine(temporary, fileName);
                if (!File.Exists(expected)) throw new FileNotFoundException($"Canonical icon output is missing: {expected}");
                if (!File.ReadAllBytes(expected).SequenceEqual(File.ReadAllBytes(actual)))
                    throw new InvalidDataException($"Generated icon output is stale: {fileName}");
            }
            Console.WriteLine("Theme icon outputs are current and byte-deterministic.");
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private static RenderedIcon Load(string root, ImportIcon icon, int density)
    {
        var path = Path.Combine(root, "assets/theme-icons/svg", Path.GetFileName(icon.Source));
        if (!File.Exists(path)) throw new FileNotFoundException($"Imported icon is missing: {path}");
        var hash = HashFile(path);
        if (!string.Equals(hash, icon.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Source hash mismatch for {icon.Name}: expected {icon.Sha256}, found {hash}.");
        using var input = File.OpenRead(path);
        var svg = new SKSvg();
        var picture = svg.Load(input) ?? throw new InvalidDataException($"Unable to load SVG: {path}");
        var bounds = picture.CullRect;
        var logicalWidth = (int)MathF.Ceiling(bounds.Width);
        var logicalHeight = (int)MathF.Ceiling(bounds.Height);
        if (logicalWidth <= 0 || logicalHeight <= 0) throw new InvalidDataException($"Icon has zero size: {icon.Name}");
        return new RenderedIcon(icon, svg, picture, bounds, logicalWidth, logicalHeight, logicalWidth * density, logicalHeight * density);
    }

    private static List<Placement> Pack(IReadOnlyList<RenderedIcon> icons, int density)
    {
        var atlasWidth = LogicalAtlasWidth * density;
        var padding = Padding * density;
        var x = padding;
        var y = padding;
        var rowHeight = 0;
        var placements = new List<Placement>(icons.Count);
        foreach (var icon in icons)
        {
            if (icon.Width + padding * 2 > atlasWidth) throw new InvalidDataException($"Icon exceeds atlas width: {icon.Import.Name}");
            if (x + icon.Width + padding > atlasWidth)
            {
                x = padding;
                y += rowHeight + padding * 2;
                rowHeight = 0;
            }
            placements.Add(new Placement(icon, x, y));
            x += icon.Width + padding * 2;
            rowHeight = Math.Max(rowHeight, icon.Height);
        }
        return placements;
    }

    private static void WriteAtlas(string path, int width, int height, int density, IReadOnlyList<Placement> placements)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Unable to create atlas surface.");
        surface.Canvas.Clear(SKColors.Transparent);
        foreach (var placement in placements)
        {
            surface.Canvas.Save();
            surface.Canvas.Translate(placement.X, placement.Y);
            surface.Canvas.Scale(density);
            surface.Canvas.Translate(-placement.Icon.Bounds.Left, -placement.Icon.Bounds.Top);
            surface.Canvas.DrawPicture(placement.Icon.Picture);
            surface.Canvas.Restore();
        }
        surface.Canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(path);
        data.SaveTo(output);
    }

    private static void ValidateConfig(ImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SourceRevision) || config.SourceRevision.Length != 40)
            throw new InvalidDataException("A 40-character Godot source revision is required.");
        if (config.Icons.Count == 0) throw new InvalidDataException("At least one icon import is required.");
        foreach (var duplicate in config.Icons.GroupBy(icon => icon.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
            throw new InvalidDataException($"Duplicate icon name: {duplicate.Key}");
        foreach (var duplicate in config.Icons.GroupBy(icon => icon.Source, StringComparer.Ordinal).Where(group => group.Count() > 1))
            throw new InvalidDataException($"Duplicate icon source: {duplicate.Key}");
        foreach (var icon in config.Icons)
        {
            if (!icon.Source.StartsWith("scene/theme/icons/", StringComparison.Ordinal) || icon.Source.Contains("editor/", StringComparison.Ordinal))
                throw new InvalidDataException($"Only Godot runtime theme icons may be imported: {icon.Source}");
            if (icon.License != "Godot-MIT") throw new InvalidDataException($"Unclassified icon license: {icon.Name}");
            if (icon.Bindings.Count == 0 || icon.States.Count == 0 || string.IsNullOrWhiteSpace(icon.Directionality))
                throw new InvalidDataException($"Icon mapping is incomplete: {icon.Name}");
            if (!string.IsNullOrEmpty(icon.Sha256) && icon.Sha256.Length != 64)
                throw new InvalidDataException($"Invalid SHA-256 for {icon.Name}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run Forma.IconPipeline from the Forma repository.");
    }

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Unable to parse {path}.");
    private static void Write<T>(string path, T value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string RunProcess(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })
            ?? throw new InvalidOperationException($"Unable to start {fileName}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{fileName} failed: {error.Trim()}");
        return output;
    }

    private sealed class ImportConfig
    {
        public string SourceRevision { get; set; } = string.Empty;
        public List<ImportIcon> Icons { get; set; } = new();
    }
    private sealed class ImportIcon
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public List<string> Bindings { get; set; } = new();
        public List<string> States { get; set; } = new();
        public string Directionality { get; set; } = string.Empty;
    }
    private sealed record AtlasFile(int Density, string FileName, int Width, int Height, string Sha256);
    private sealed record GeneratedIcon(string Name, int Density, int X, int Y, int Width, int Height, int LogicalWidth, int LogicalHeight, string Source, string SourceSha256, IReadOnlyList<string> Bindings, IReadOnlyList<string> States, string Directionality);
    private sealed record GeneratedManifest(int SchemaVersion, string SourceRevision, string Renderer, string RendererVersion, string PixelFormat, int LogicalPadding, IReadOnlyList<AtlasFile> Atlases, IReadOnlyList<GeneratedIcon> Icons);
    private sealed record Placement(RenderedIcon Icon, int X, int Y);
    private sealed record RenderedIcon(ImportIcon Import, SKSvg Svg, SKPicture Picture, SKRect Bounds, int LogicalWidth, int LogicalHeight, int Width, int Height) : IDisposable
    {
        public void Dispose() => Svg.Dispose();
    }
}