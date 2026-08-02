// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;

return await UnicodePipeline.RunAsync(args);

internal static class UnicodePipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var root = FindRepositoryRoot();
            if (args.Length == 1 && args[0] == "generate") await GenerateAsync(root);
            else if (args.Length == 1 && args[0] == "verify") await VerifyAsync(root);
            else
            {
                Console.Error.WriteLine("Usage: Forma.UnicodePipeline generate | verify");
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

    private static async Task GenerateAsync(string root)
    {
        var outputs = await CreateOutputsAsync(root);
        foreach (var output in outputs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output.Path)!);
            await File.WriteAllBytesAsync(output.Path, output.Content);
        }
        Console.WriteLine($"Generated Unicode {outputs[0].Version} managed tables and conformance cases.");
    }

    private static async Task VerifyAsync(string root)
    {
        var outputs = await CreateOutputsAsync(root);
        foreach (var output in outputs)
        {
            if (!File.Exists(output.Path)) throw new FileNotFoundException($"Generated Unicode output is missing: {output.Path}");
            var actual = await File.ReadAllBytesAsync(output.Path);
            if (!actual.SequenceEqual(output.Content))
                throw new InvalidDataException($"Generated Unicode output is stale: {Path.GetRelativePath(root, output.Path)}");
        }
        Console.WriteLine($"Unicode {outputs[0].Version} outputs are current and byte-deterministic.");
    }

    private static async Task<List<GeneratedOutput>> CreateOutputsAsync(string root)
    {
        var manifestPath = Path.Combine(root, "assets/unicode/manifest.json");
        var manifest = JsonSerializer.Deserialize<UnicodeManifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Unable to parse the Unicode manifest.");
        if (manifest.UnicodeVersion != "17.0.0") throw new InvalidDataException("Unicode 17.0.0 must remain explicitly pinned.");
        if (manifest.Files.Count == 0) throw new InvalidDataException("The Unicode manifest contains no files.");

        using var client = new HttpClient();
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || !Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) || entry.Sha256.Length != 64)
                throw new InvalidDataException($"Invalid Unicode manifest entry: {entry.Name}");
            var bytes = await client.GetByteArrayAsync(uri);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException($"Unicode source hash mismatch for {entry.Name}: expected {entry.Sha256}, found {hash}.");
            if (!files.TryAdd(entry.Name, Encoding.UTF8.GetString(bytes)))
                throw new InvalidDataException($"Duplicate Unicode manifest name: {entry.Name}");
        }

        var graphemeRanges = ParsePropertyRanges(Require(files, "GraphemeBreakProperty.txt"), 1, null);
        var wordRanges = ParsePropertyRanges(Require(files, "WordBreakProperty.txt"), 1, null);
        var pictographicRanges = ParsePropertyRanges(Require(files, "emoji-data.txt"), 1, "Extended_Pictographic");
        var indicRanges = ParsePropertyRanges(Require(files, "DerivedCoreProperties.txt"), 2, "InCB");
        var scriptAliases = ParseScriptAliases(Require(files, "PropertyValueAliases.txt"));
        var scriptRanges = ApplyScriptAliases(ParsePropertyRanges(Require(files, "Scripts.txt"), 1, null), scriptAliases);
        var scriptExtensionRanges = ParsePropertyRanges(Require(files, "ScriptExtensions.txt"), 1, null);
        var lineBreakRanges = ParsePropertyRanges(Require(files, "LineBreak.txt"), 1, null);
        var eastAsianWidthRanges = ParsePropertyRanges(Require(files, "EastAsianWidth.txt"), 1, null);
        var generalCategoryRanges = ParsePropertyRanges(Require(files, "DerivedGeneralCategory.txt"), 1, null);
        var bidiClassRanges = ParsePropertyRanges(Require(files, "DerivedBidiClass.txt"), 1, null);
        var bidiBrackets = ParseBidiBrackets(Require(files, "BidiBrackets.txt"));
        var bidiMirrors = ParseCodePointMap(Require(files, "BidiMirroring.txt"));
        var graphemeTests = ParseBreakTests(Require(files, "GraphemeBreakTest.txt"));
        var wordBreakTests = ParseBreakTests(Require(files, "WordBreakTest.txt"));
        var lineBreakTests = ParseBreakTests(Require(files, "LineBreakTest.txt"));
        return new List<GeneratedOutput>
        {
            TextOutput(Path.Combine(root, "src/Forma/Generated/UnicodeGraphemeData.g.cs"), manifest.UnicodeVersion, EmitRuntime(manifest.UnicodeVersion, graphemeRanges, wordRanges, pictographicRanges, indicRanges, scriptRanges, scriptExtensionRanges, lineBreakRanges, eastAsianWidthRanges, generalCategoryRanges, bidiClassRanges, bidiBrackets, bidiMirrors)),
            TextOutput(Path.Combine(root, "tests/Forma.Tests/Generated/UnicodeGraphemeBreakCases.g.cs"), manifest.UnicodeVersion, EmitTests(manifest.UnicodeVersion, "UnicodeGraphemeBreakCases", graphemeTests)),
            TextOutput(Path.Combine(root, "tests/Forma.Tests/Generated/UnicodeWordBreakCases.g.cs"), manifest.UnicodeVersion, EmitTests(manifest.UnicodeVersion, "UnicodeWordBreakCases", wordBreakTests)),
            TextOutput(Path.Combine(root, "tests/Forma.Tests/Generated/UnicodeLineBreakCases.g.cs"), manifest.UnicodeVersion, EmitTests(manifest.UnicodeVersion, "UnicodeLineBreakCases", lineBreakTests)),
            new(Path.Combine(root, "tests/Assets/Text/BidiTest.txt.gz"), manifest.UnicodeVersion, Compress(Require(files, "BidiTest.txt"))),
            new(Path.Combine(root, "tests/Assets/Text/BidiCharacterTest.txt.gz"), manifest.UnicodeVersion, Compress(Require(files, "BidiCharacterTest.txt")))
        };
    }

    private static GeneratedOutput TextOutput(string path, string version, string content) =>
        new(path, version, new UTF8Encoding(false).GetBytes(content));

    private static byte[] Compress(string content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true))
            gzip.Write(new UTF8Encoding(false).GetBytes(content));
        return output.ToArray();
    }

    private static List<BidiBracket> ParseBidiBrackets(string content)
    {
        var brackets = new List<BidiBracket>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length != 3 || fields[2] is not ("o" or "c"))
                throw new InvalidDataException($"Invalid BidiBrackets record: {rawLine.Trim()}");
            brackets.Add(new BidiBracket(Convert.ToInt32(fields[0], 16), Convert.ToInt32(fields[1], 16), fields[2] == "o"));
        }
        brackets.Sort(static (left, right) => left.CodePoint.CompareTo(right.CodePoint));
        return brackets;
    }

    private static List<CodePointMap> ParseCodePointMap(string content)
    {
        var values = new List<CodePointMap>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length != 2) throw new InvalidDataException($"Invalid code-point map record: {rawLine.Trim()}");
            values.Add(new CodePointMap(Convert.ToInt32(fields[0], 16), Convert.ToInt32(fields[1], 16)));
        }
        values.Sort(static (left, right) => left.CodePoint.CompareTo(right.CodePoint));
        return values;
    }

    private static Dictionary<string, string> ParseScriptAliases(string content)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length >= 3 && fields[0] == "sc") aliases[fields[2]] = fields[1];
        }
        return aliases;
    }

    private static List<PropertyRange> ApplyScriptAliases(List<PropertyRange> ranges, Dictionary<string, string> aliases)
    {
        for (var index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            if (!aliases.TryGetValue(range.Property, out var alias))
                throw new InvalidDataException($"Missing ISO 15924 alias for script {range.Property}.");
            ranges[index] = range with { Property = alias };
        }
        return Merge(ranges);
    }

    private static List<PropertyRange> ParsePropertyRanges(string content, int propertyField, string? requiredProperty)
    {
        var ranges = new List<PropertyRange>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var fields = line.Split(';', StringSplitOptions.TrimEntries);
            if (fields.Length <= propertyField) continue;
            if (requiredProperty != null && !string.Equals(fields[1], requiredProperty, StringComparison.Ordinal)) continue;
            var property = fields[propertyField];
            var bounds = fields[0].Split("..", StringSplitOptions.TrimEntries);
            var start = Convert.ToInt32(bounds[0], 16);
            var end = bounds.Length == 1 ? start : Convert.ToInt32(bounds[1], 16);
            ranges.Add(new PropertyRange(start, end, property));
        }
        ranges.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return Merge(ranges);
    }

    private static List<PropertyRange> Merge(List<PropertyRange> ranges)
    {
        var merged = new List<PropertyRange>();
        foreach (var range in ranges)
        {
            if (merged.Count > 0 && merged[^1].End + 1 == range.Start && merged[^1].Property == range.Property)
            {
                merged[^1] = merged[^1] with { End = range.End };
                continue;
            }
            merged.Add(range);
        }
        return merged;
    }

    private static List<BreakTest> ParseBreakTests(string content)
    {
        var tests = new List<BreakTest>();
        foreach (var rawLine in content.Split('\n'))
        {
            var expression = rawLine.Split('#')[0].Trim();
            if (expression.Length == 0) continue;
            var codePoints = new List<int>();
            var scalarBoundaries = new List<int>();
            var scalarIndex = 0;
            foreach (var token in expression.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token == "÷") { scalarBoundaries.Add(scalarIndex); continue; }
                if (token == "×") continue;
                codePoints.Add(Convert.ToInt32(token, 16));
                scalarIndex++;
            }
            tests.Add(new BreakTest(codePoints, scalarBoundaries.Distinct().ToList()));
        }
        return tests;
    }

    private static string EmitRuntime(
        string version,
        List<PropertyRange> grapheme,
        List<PropertyRange> word,
        List<PropertyRange> pictographic,
        List<PropertyRange> indic,
        List<PropertyRange> scripts,
        List<PropertyRange> scriptExtensions,
        List<PropertyRange> lineBreaks,
        List<PropertyRange> eastAsianWidths,
        List<PropertyRange> generalCategories,
        List<PropertyRange> bidiClasses,
        List<BidiBracket> bidiBrackets,
        List<CodePointMap> bidiMirrors)
    {
        var output = new StringBuilder(Header(version));
        output.AppendLine("namespace Forma;");
        output.AppendLine();
        output.AppendLine("internal static class UnicodeGraphemeData");
        output.AppendLine("{");
        output.AppendLine($"    internal const string UnicodeVersion = \"{version}\";");
        EmitArray(output, "GraphemeRanges", grapheme);
        EmitArray(output, "WordBreakRanges", word);
        EmitArray(output, "ExtendedPictographicRanges", pictographic);
        EmitArray(output, "IndicConjunctRanges", indic);
        EmitArray(output, "ScriptRanges", scripts);
        EmitArray(output, "ScriptExtensionRanges", scriptExtensions);
        EmitArray(output, "LineBreakRanges", lineBreaks);
        EmitArray(output, "EastAsianWidthRanges", eastAsianWidths);
        EmitArray(output, "GeneralCategoryRanges", generalCategories);
        EmitArray(output, "BidiClassRanges", bidiClasses);
        EmitBidiBrackets(output, bidiBrackets);
        EmitCodePointMap(output, "BidiMirrors", bidiMirrors);
        output.AppendLine("}");
        output.AppendLine();
        output.AppendLine("internal readonly record struct UnicodePropertyRange(int Start, int End, string Property);");
        output.AppendLine("internal readonly record struct UnicodeBidiBracket(int CodePoint, int PairedCodePoint, bool IsOpening);");
        output.AppendLine("internal readonly record struct UnicodeCodePointMap(int CodePoint, int Value);");
        return output.ToString();
    }

    private static void EmitArray(StringBuilder output, string name, List<PropertyRange> ranges)
    {
        output.AppendLine($"    internal static readonly UnicodePropertyRange[] {name} =");
        output.AppendLine("    {");
        foreach (var range in ranges)
            output.AppendLine($"        new(0x{range.Start:X}, 0x{range.End:X}, \"{range.Property}\"),");
        output.AppendLine("    };");
    }

    private static void EmitBidiBrackets(StringBuilder output, List<BidiBracket> brackets)
    {
        output.AppendLine("    internal static readonly UnicodeBidiBracket[] BidiBrackets =");
        output.AppendLine("    {");
        foreach (var bracket in brackets)
            output.AppendLine($"        new(0x{bracket.CodePoint:X}, 0x{bracket.PairedCodePoint:X}, {bracket.IsOpening.ToString().ToLowerInvariant()}),");
        output.AppendLine("    };");
    }

    private static void EmitCodePointMap(StringBuilder output, string name, List<CodePointMap> values)
    {
        output.AppendLine($"    internal static readonly UnicodeCodePointMap[] {name} =");
        output.AppendLine("    {");
        foreach (var value in values)
            output.AppendLine($"        new(0x{value.CodePoint:X}, 0x{value.Value:X}),");
        output.AppendLine("    };");
    }

    private static string EmitTests(string version, string className, List<BreakTest> tests)
    {
        var output = new StringBuilder(Header(version));
        output.AppendLine("namespace Forma.Tests;");
        output.AppendLine();
        output.AppendLine($"internal static class {className}");
        output.AppendLine("{");
        output.AppendLine("    internal static readonly (int[] CodePoints, int[] ScalarBoundaries)[] All =");
        output.AppendLine("    {");
        foreach (var test in tests)
        {
            output.Append("        (new[] { ");
            output.Append(string.Join(", ", test.CodePoints.Select(value => $"0x{value:X}")));
            output.Append(" }, new[] { ");
            output.Append(string.Join(", ", test.ScalarBoundaries));
            output.AppendLine(" }),");
        }
        output.AppendLine("    };");
        output.AppendLine("}");
        return output.ToString();
    }

    private static string Header(string version) => $"// <auto-generated> Unicode {version}; run `make unicode`. </auto-generated>\n// Copyright (c) 2026 Igor Hipólito Vieira\n// SPDX-License-Identifier: MIT\n\n";
    private static string Require(Dictionary<string, string> files, string name) => files.TryGetValue(name, out var value) ? value : throw new InvalidDataException($"Unicode manifest is missing {name}.");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run Forma.UnicodePipeline from the Forma repository.");
    }

    private sealed class UnicodeManifest
    {
        public string UnicodeVersion { get; set; } = string.Empty;
        public List<UnicodeFile> Files { get; set; } = new();
    }

    private sealed class UnicodeFile
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    private sealed record GeneratedOutput(string Path, string Version, byte[] Content);
    private sealed record PropertyRange(int Start, int End, string Property);
    private sealed record BidiBracket(int CodePoint, int PairedCodePoint, bool IsOpening);
    private sealed record CodePointMap(int CodePoint, int Value);
    private sealed record BreakTest(List<int> CodePoints, List<int> ScalarBoundaries);
}