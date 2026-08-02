// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

namespace Forma.Tests;

public sealed class IconPipelineTest
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"forma-icon-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "assets/theme-icons/svg"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void Generate_WritesCompleteDeterministicDensityOutputs()
    {
        WriteSvg("valid.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"6\"><rect width=\"8\" height=\"6\"/></svg>");
        WriteConfig([Icon("valid", "valid.svg", Hash("valid.svg"))]);
        var first = Path.Combine(_root, "first");
        var second = Path.Combine(_root, "second");

        IconPipeline.Generate(_root, first);
        IconPipeline.Generate(_root, second);

        foreach (var fileName in new[] { "theme-icons-1x.png", "theme-icons-2x.png", "theme-icons.json" })
            Assert.That(File.ReadAllBytes(Path.Combine(first, fileName)), Is.EqualTo(File.ReadAllBytes(Path.Combine(second, fileName))));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, "theme-icons.json")));
        var atlases = manifest.RootElement.GetProperty("Atlases").EnumerateArray().ToArray();
        var icons = manifest.RootElement.GetProperty("Icons").EnumerateArray().ToArray();
        Assert.That(atlases, Has.Length.EqualTo(2));
        Assert.That(icons, Has.Length.EqualTo(2));
        Assert.That(icons.Select(icon => icon.GetProperty("LogicalWidth").GetInt32()), Is.All.EqualTo(8));
        Assert.That(icons.Select(icon => icon.GetProperty("LogicalHeight").GetInt32()), Is.All.EqualTo(6));
        foreach (var atlas in atlases)
        {
            var density = atlas.GetProperty("Density").GetInt32();
            var icon = icons.Single(entry => entry.GetProperty("Density").GetInt32() == density);
            Assert.That(icon.GetProperty("X").GetInt32() + icon.GetProperty("Width").GetInt32(), Is.LessThanOrEqualTo(atlas.GetProperty("Width").GetInt32()));
            Assert.That(icon.GetProperty("Y").GetInt32() + icon.GetProperty("Height").GetInt32(), Is.LessThanOrEqualTo(atlas.GetProperty("Height").GetInt32()));
            using var bitmap = SKBitmap.Decode(Path.Combine(first, atlas.GetProperty("FileName").GetString()!));
            var x = icon.GetProperty("X").GetInt32();
            var y = icon.GetProperty("Y").GetInt32();
            Assert.That(bitmap.GetPixel(x - 1, y).Alpha, Is.Zero);
            Assert.That(bitmap.GetPixel(x, y - 1).Alpha, Is.Zero);
            Assert.That(bitmap.GetPixel(x + icon.GetProperty("Width").GetInt32(), y).Alpha, Is.Zero);
            Assert.That(bitmap.GetPixel(x, y + icon.GetProperty("Height").GetInt32()).Alpha, Is.Zero);
        }
    }

    [Test]
    public void Generate_RejectsDuplicateLogicalNames()
    {
        WriteConfig([Icon("duplicate", "one.svg"), Icon("duplicate", "two.svg")]);

        Assert.That(() => IconPipeline.Generate(_root, Path.Combine(_root, "out")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("Duplicate icon name"));
    }

    [Test]
    public void Generate_RejectsIncompleteMappingsAndUnclassifiedLicenses()
    {
        var incomplete = Icon("incomplete", "incomplete.svg");
        incomplete.Bindings = [];
        WriteConfig([incomplete]);
        Assert.That(() => IconPipeline.Generate(_root, Path.Combine(_root, "out")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("mapping is incomplete"));

        var unclassified = Icon("unclassified", "unclassified.svg");
        unclassified.License = "Unknown";
        WriteConfig([unclassified]);
        Assert.That(() => IconPipeline.Generate(_root, Path.Combine(_root, "out")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("Unclassified icon license"));
    }

    [Test]
    public void Generate_RejectsSourceHashDrift()
    {
        WriteSvg("drift.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"/>");
        WriteConfig([Icon("drift", "drift.svg", new string('0', 64))]);

        Assert.That(() => IconPipeline.Generate(_root, Path.Combine(_root, "out")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("Source hash mismatch"));
    }

    [Test]
    public void Generate_RejectsZeroSizedSvg()
    {
        WriteSvg("empty.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"0\" height=\"0\"/>");
        WriteConfig([Icon("empty", "empty.svg", Hash("empty.svg"))]);

        Assert.That(() => IconPipeline.Generate(_root, Path.Combine(_root, "out")), Throws.TypeOf<InvalidDataException>().With.Message.Contains("zero size"));
    }

    private void WriteConfig(IReadOnlyList<TestIcon> icons)
    {
        var directory = Path.Combine(_root, "assets/theme-icons");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "imports.json"), JsonSerializer.Serialize(new
        {
            SourceRevision = new string('a', 40),
            Icons = icons,
        }));
    }

    private void WriteSvg(string fileName, string source) => File.WriteAllText(Path.Combine(_root, "assets/theme-icons/svg", fileName), source);

    private string Hash(string fileName) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, "assets/theme-icons/svg", fileName)))).ToLowerInvariant();

    private static TestIcon Icon(string name, string fileName, string hash = "") => new()
    {
        Name = name,
        Source = $"scene/theme/icons/{fileName}",
        Sha256 = hash,
        License = "Godot-MIT",
        Bindings = ["Control:test"],
        States = ["normal"],
        Directionality = "none",
    };

    private sealed class TestIcon
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public List<string> Bindings { get; set; } = [];
        public List<string> States { get; set; } = [];
        public string Directionality { get; set; } = string.Empty;
    }
}