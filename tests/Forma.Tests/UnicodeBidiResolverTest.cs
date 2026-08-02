// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.IO.Compression;

namespace Forma.Tests;

public class UnicodeBidiResolverTest
{
    [TestCase("car means אבג.", 0, 0, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0 })]
    [TestCase("אב(גד[&ef].)gh", 0, 0, new byte[] { 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    public void Resolve_AssignsExpectedLevels(string text, int direction, byte paragraphLevel, byte[] expectedLevels)
    {
        var result = UnicodeBidiResolver.Resolve(text, (BidiParagraphDirection)direction);

        Assert.Multiple(() =>
        {
            Assert.That(result.ParagraphLevel, Is.EqualTo(paragraphLevel));
            Assert.That(result.Levels, Is.EqualTo(expectedLevels.Cast<byte?>().ToArray()));
        });
    }

    [Test]
    public void Resolve_RemovesOverridesAndKeepsIsolatesInVisualOrder()
    {
        var result = UnicodeBidiResolver.Resolve(new[]
        {
            0x202E, 0x0061, 0x202A, 0x0062, 0x202C, 0x2066, 0x0063,
            0x2069, 0x202A, 0x0064, 0x202C, 0x0065, 0x202C
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Levels, Is.EqualTo(new byte?[] { null, 1, null, 2, null, 1, 2, 1, null, 2, null, 1, null }));
            Assert.That(result.VisualOrder, Is.EqualTo(new[] { 11, 9, 7, 6, 5, 3, 1 }));
        });
    }

    [Test]
    public void GetMirror_ReturnsUnicodeMirrorWhenAvailable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnicodeBidiResolver.GetMirror('('), Is.EqualTo(')'));
            Assert.That(UnicodeBidiResolver.GetMirror('A'), Is.EqualTo('A'));
        });
    }

    [Test]
    public void Unicode17BidiCharacterTest_PassesOfficialConformanceCases()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Unicode", "BidiCharacterTest.txt.gz");
        using var compressed = File.OpenRead(fixture);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var caseIndex = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var fields = line.Split(';');
            if (fields.Length != 5) continue;
            caseIndex++;
            var codePoints = ParseIntegers(fields[0], NumberStyles.HexNumber);
            var direction = (BidiParagraphDirection)int.Parse(fields[1], CultureInfo.InvariantCulture);
            var expectedParagraphLevel = byte.Parse(fields[2], CultureInfo.InvariantCulture);
            var expectedLevels = fields[3].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value == "x" ? (byte?)null : byte.Parse(value, CultureInfo.InvariantCulture))
                .ToArray();
            var expectedOrder = ParseIntegers(fields[4], NumberStyles.Integer);

            var result = UnicodeBidiResolver.Resolve(codePoints, direction);

            Assert.Multiple(() =>
            {
                Assert.That(result.ParagraphLevel, Is.EqualTo(expectedParagraphLevel), CaseMessage(caseIndex, codePoints, "paragraph level"));
                Assert.That(result.Levels, Is.EqualTo(expectedLevels), CaseMessage(caseIndex, codePoints, "levels"));
                Assert.That(result.VisualOrder, Is.EqualTo(expectedOrder), CaseMessage(caseIndex, codePoints, "visual order"));
            });
        }

        Assert.That(caseIndex, Is.EqualTo(91_707));
    }

    [Test]
    public void Unicode17BidiTest_PassesOfficialConformanceCases()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Unicode", "BidiTest.txt.gz");
        using var compressed = File.OpenRead(fixture);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        byte?[] expectedLevels = Array.Empty<byte?>();
        int[] expectedOrder = Array.Empty<int>();
        var caseIndex = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                expectedLevels = SplitWhitespace(line[8..])
                    .Select(value => value == "x" ? (byte?)null : byte.Parse(value, CultureInfo.InvariantCulture))
                    .ToArray();
                continue;
            }
            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                expectedOrder = ParseIntegers(line[9..], NumberStyles.Integer);
                continue;
            }
            if (line.Length == 0 || line[0] == '#' || line[0] == '@') continue;
            var fields = line.Split(';');
            if (fields.Length != 2) continue;
            caseIndex++;
            var bidiTypes = SplitWhitespace(fields[0]);
            var directions = int.Parse(fields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            ValidateBidiTypeCase(bidiTypes, directions, 1, BidiParagraphDirection.AutoLeftToRight, expectedLevels, expectedOrder, caseIndex);
            ValidateBidiTypeCase(bidiTypes, directions, 2, BidiParagraphDirection.LeftToRight, expectedLevels, expectedOrder, caseIndex);
            ValidateBidiTypeCase(bidiTypes, directions, 4, BidiParagraphDirection.RightToLeft, expectedLevels, expectedOrder, caseIndex);
        }

        Assert.That(caseIndex, Is.EqualTo(490_846));
    }

    private static void ValidateBidiTypeCase(string[] bidiTypes, int directions, int bit, BidiParagraphDirection direction, byte?[] expectedLevels, int[] expectedOrder, int caseIndex)
    {
        if ((directions & bit) == 0) return;
        var result = UnicodeBidiResolver.ResolveTypes(bidiTypes, direction);
        Assert.Multiple(() =>
        {
            Assert.That(result.Levels, Is.EqualTo(expectedLevels), $"Unicode 17 BidiTest case {caseIndex} {direction} levels: {string.Join(" ", bidiTypes)}");
            Assert.That(result.VisualOrder, Is.EqualTo(expectedOrder), $"Unicode 17 BidiTest case {caseIndex} {direction} visual order: {string.Join(" ", bidiTypes)}");
        });
    }

    private static int[] ParseIntegers(string field, NumberStyles style) => field
        .Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries)
        .Select(value => int.Parse(value, style, CultureInfo.InvariantCulture))
        .ToArray();

    private static string[] SplitWhitespace(string value) => value.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);

    private static string CaseMessage(int caseIndex, int[] codePoints, string field) =>
        $"Unicode 17 BidiCharacterTest case {caseIndex} {field}: {string.Join(" ", codePoints.Select(value => value.ToString("X4")))}";
}