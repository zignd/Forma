// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace Forma.Tests
{
    [TestFixture]
    public sealed class DynamicFontTests
    {
        [Test]
        public void LoadsMemoryStreamAndProjectRelativeFacesWithStableMetadata()
        {
            var path = FontPath("Inter_Regular.ttf");
            var bytes = File.ReadAllBytes(path);
            using var memoryFace = UIFontFace.FromMemory(bytes);
            using var streamFace = UIFontFace.FromStream(new MemoryStream(bytes));
            using var fileFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");

            Assert.Multiple(() =>
            {
                Assert.That(memoryFace.FamilyName, Is.EqualTo("Inter"));
                Assert.That(streamFace.FamilyName, Is.EqualTo(memoryFace.FamilyName));
                Assert.That(fileFace.StyleName, Is.EqualTo(memoryFace.StyleName));
                Assert.That(memoryFace.FaceCount, Is.EqualTo(1));
                Assert.That(memoryFace.FaceIndex, Is.EqualTo(0));
                Assert.That(memoryFace.GlyphCount, Is.GreaterThan(0));
                Assert.That(memoryFace.UnitsPerEm, Is.GreaterThan(0));
                Assert.That(memoryFace.SupportsCharacter('A'), Is.True);
                Assert.That(memoryFace.GetGlyphId('A'), Is.GreaterThan(0));
                Assert.That(memoryFace.GetSupportedCodePoints(), Does.Contain((int)'A'));
            });
        }

        [Test]
        public void LayoutMetricsUseLogicalSizeWhileRasterizationUsesDisplayScale()
        {
            using var face = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
            var glyphId = face.GetGlyphId('M');
            var metrics = face.GetMetrics(17.5f);
            var glyphMetrics = face.GetGlyphMetrics(glyphId, 17.5f);
            var oneX = face.RasterizeGlyph(glyphId, 17.5f, 1, UIFontHinting.None);
            var twoX = face.RasterizeGlyph(glyphId, 17.5f, 2, UIFontHinting.None);

            Assert.Multiple(() =>
            {
                Assert.That(metrics.LineHeight, Is.GreaterThan(0));
                Assert.That(glyphMetrics.AdvanceX, Is.GreaterThan(0));
                Assert.That(twoX.Width, Is.GreaterThan(oneX.Width));
                Assert.That(twoX.Height, Is.GreaterThan(oneX.Height));
                Assert.That(oneX.Pixels.Span.ToArray(), Has.Some.GreaterThan((byte)0));
                Assert.That(twoX.AdvanceX, Is.EqualTo(oneX.AdvanceX).Within(0.1f));
            });
        }

        [Test]
        public void LightHintingRasterizesWithoutChangingShapedHorizontalAdvances()
        {
            using var face = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
            var shaped = face.Shape("te ti", 16, TextDirection.LeftToRight, "en", "Latn");
            var advances = shaped.Glyphs.Select(glyph => glyph.AdvanceX).ToArray();
            var bitmaps = shaped.Glyphs.Select(glyph => face.RasterizeGlyph(glyph.GlyphId, 16, 1, UIFontHinting.Light)).ToArray();
            var visibleBitmaps = bitmaps.Where(bitmap => bitmap.Pixels.Length > 0).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(visibleBitmaps, Has.Length.EqualTo(4));
                Assert.That(visibleBitmaps, Has.All.Property(nameof(UIFontGlyphBitmap.Width)).GreaterThan(0));
                Assert.That(visibleBitmaps, Has.All.Property(nameof(UIFontGlyphBitmap.Height)).GreaterThan(0));
                Assert.That(shaped.Glyphs.Select(glyph => glyph.AdvanceX), Is.EqualTo(advances));
            });
        }

        [Test]
        public void ReadsVariableAxesAndRasterizesCombiningMarks()
        {
            using var face = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
            var weight = face.VariationAxes.Single(axis => axis.Tag == "wght");
            var combiningMark = face.RasterizeCharacter(0x0651, 24.25f, 1.5f, UIFontHinting.Auto);

            Assert.Multiple(() =>
            {
                Assert.That(weight.Minimum, Is.LessThan(weight.Default));
                Assert.That(weight.Default, Is.LessThan(weight.Maximum));
                Assert.That(combiningMark.GlyphId, Is.GreaterThan(0));
                Assert.That(combiningMark.Pixels.Length, Is.GreaterThan(0));
            });
        }

        [Test]
        public void AppliesVariationCoordinatesToShapingMetricsAndRasterization()
        {
            using var face = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
            var lightCoordinates = new[] { new UIFontVariationCoordinate("wght", 300) };
            var heavyCoordinates = new[] { new UIFontVariationCoordinate("wght", 800) };
            var lightFont = new DynamicUIFont(face, 32, UIFontHinting.None, lightCoordinates);
            var heavyFont = new DynamicUIFont(face, 32, UIFontHinting.None, heavyCoordinates);
            var lightLayout = new TextLayoutEngine().Layout(lightFont, "مرحبا", new TextLayoutOptions(locale: "ar"));
            var heavyLayout = new TextLayoutEngine().Layout(heavyFont, "مرحبا", new TextLayoutOptions(locale: "ar"));
            var glyphId = face.GetGlyphId(0x0645);
            var lightBitmap = face.RasterizeGlyph(glyphId, 32, 1, UIFontHinting.None, lightCoordinates);
            var heavyBitmap = face.RasterizeGlyph(glyphId, 32, 1, UIFontHinting.None, heavyCoordinates);
            var defaultAfterHeavy = face.RasterizeGlyph(glyphId, 32, 1, UIFontHinting.None);

            Assert.Multiple(() =>
            {
                Assert.That(lightFont.Identity, Is.Not.EqualTo(heavyFont.Identity));
                Assert.That(lightLayout.Runs.SelectMany(run => run.Glyphs), Is.Not.Empty);
                Assert.That(heavyLayout.Runs.SelectMany(run => run.Glyphs), Is.Not.Empty);
                Assert.That(lightBitmap.Pixels.Span.ToArray(), Is.Not.EqualTo(heavyBitmap.Pixels.Span.ToArray()));
                Assert.That(defaultAfterHeavy.Pixels.Span.ToArray(), Is.Not.EqualTo(heavyBitmap.Pixels.Span.ToArray()));
                Assert.That(() => new DynamicUIFont(face, 32, UIFontHinting.None, new[] { new UIFontVariationCoordinate("wght", 9999) }), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void RejectsMalformedDataInvalidFacesAndEscapingProjectPaths()
        {
            var malformed = Assert.Throws<FontLoadException>(() => UIFontFace.FromMemory(new byte[12]));
            var invalidFace = Assert.Throws<FontLoadException>(() => UIFontFace.FromMemory(File.ReadAllBytes(FontPath("Inter_Regular.ttf")), 1));

            Assert.Multiple(() =>
            {
                Assert.That(malformed.ErrorCode, Is.EqualTo(FontLoadErrorCode.UnsupportedFormat));
                Assert.That(invalidFace.ErrorCode, Is.EqualTo(FontLoadErrorCode.FaceIndexOutOfRange));
                Assert.That(() => UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "../Inter_Regular.ttf"), Throws.ArgumentException);
            });
        }

        [Test]
        public void ClassifiesNativeLoaderFailuresWithoutLeakingOtherErrors()
        {
            Assert.Multiple(() =>
            {
                Assert.That(UIFontFace.IsNativeDependencyFailure(new DllNotFoundException()), Is.True);
                Assert.That(UIFontFace.IsNativeDependencyFailure(new FileLoadException()), Is.True);
                Assert.That(UIFontFace.IsNativeDependencyFailure(new BadImageFormatException()), Is.True);
                Assert.That(UIFontFace.IsNativeDependencyFailure(new EntryPointNotFoundException()), Is.True);
                Assert.That(UIFontFace.IsNativeDependencyFailure(
                    new TypeInitializationException("NativeFont", new DllNotFoundException())), Is.True);
                Assert.That(UIFontFace.IsNativeDependencyFailure(new InvalidDataException()), Is.False);
            });
        }

        [Test]
        [CancelAfter(15000)]
        public void BoundedMalformedFontAndTextFuzzFailsSafelyWithoutHandleDrift()
        {
            var random = new Random(0xF07A);
            var baselineHandles = UIFontFace.NativeHandleCounts;
            for (var iteration = 0; iteration < 128; iteration++)
            {
                var source = new byte[random.Next(0, 4097)];
                random.NextBytes(source);
                try
                {
                    using var unexpectedFace = UIFontFace.FromMemory(source);
                    Assert.That(unexpectedFace.GlyphCount, Is.GreaterThanOrEqualTo(0));
                }
                catch (FontLoadException error)
                {
                    Assert.That(Enum.IsDefined(typeof(FontLoadErrorCode), error.ErrorCode), Is.True);
                }
                Assert.That(UIFontFace.NativeHandleCounts, Is.EqualTo(baselineHandles));
            }

            using var face = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
            var activeHandles = UIFontFace.NativeHandleCounts;
            for (var iteration = 0; iteration < 128; iteration++)
            {
                var characters = new char[random.Next(0, 257)];
                for (var index = 0; index < characters.Length; index++) characters[index] = (char)random.Next(char.MaxValue + 1);
                var shaped = face.Shape(new string(characters), 14);

                Assert.Multiple(() =>
                {
                    Assert.That(shaped.Text.Length, Is.LessThanOrEqualTo(characters.Length));
                    Assert.That(shaped.Glyphs.Count, Is.LessThanOrEqualTo(Math.Max(1, characters.Length * 4)));
                    Assert.That(shaped.Glyphs.All(glyph => glyph.Utf16Cluster >= 0 && glyph.Utf16Cluster <= shaped.Text.Length), Is.True);
                    Assert.That(UIFontFace.NativeHandleCounts, Is.EqualTo(activeHandles));
                });
            }
        }

        [Test]
        public void RepeatedCreateAndDisposeIsIdempotent()
        {
            var bytes = File.ReadAllBytes(FontPath("Inter_Regular.ttf"));
            var baselineHandles = UIFontFace.NativeHandleCounts;
            for (var index = 0; index < 32; index++)
            {
                var face = UIFontFace.FromMemory(bytes);
                Assert.That(UIFontFace.NativeHandleCounts, Is.EqualTo((baselineHandles.PinnedMemories + 1, baselineHandles.FreeTypeLibraries + 1, baselineHandles.FreeTypeFaces + 1)));
                Assert.That(face.RasterizeCharacter('A', 16).Pixels.Length, Is.GreaterThan(0));
                face.Dispose();
                face.Dispose();
                Assert.That(UIFontFace.NativeHandleCounts, Is.EqualTo(baselineHandles));
                Assert.That(() => face.GetGlyphId('A'), Throws.TypeOf<ObjectDisposedException>());
            }

            Assert.That(() => UIFontFace.FromMemory(new byte[12]), Throws.TypeOf<FontLoadException>());
            Assert.That(UIFontFace.NativeHandleCounts, Is.EqualTo(baselineHandles));
        }

        [Test]
        public void AssetPreservesOriginalBytesAndCreatesIndependentFaces()
        {
            var bytes = File.ReadAllBytes(FontPath("Inter_Regular.ttf"));
            var asset = UIFontAsset.FromStream(new MemoryStream(bytes));
            using var stream = asset.OpenStream();
            using var face = asset.CreateFace();

            Assert.Multiple(() =>
            {
                Assert.That(asset.FamilyName, Is.EqualTo("Inter"));
                Assert.That(stream.Length, Is.EqualTo(bytes.Length));
                Assert.That(face.FamilyName, Is.EqualTo(asset.FamilyName));
                Assert.That(face.RasterizeCharacter('A', 16).Pixels.Length, Is.GreaterThan(0));
            });
        }

        [Test]
        public void RejectsOversizedSeekableStreamBeforeReading()
        {
            using var stream = new LengthOnlyStream(UIFontFace.MaximumSourceBytes + 1L);
            var error = Assert.Throws<FontLoadException>(() => UIFontFace.FromStream(stream));
            Assert.That(error.ErrorCode, Is.EqualTo(FontLoadErrorCode.SourceTooLarge));
        }

        [Test]
        public void SupportsCollectionFaceIndicesNotdefAndLargeSizeLimits()
        {
            var source = File.ReadAllBytes(FontPath("Inter_Regular.ttf"));
            var collection = CreateCollection(source, source);
            using var face = UIFontFace.FromMemory(collection, 1);
            var notdef = face.RasterizeCharacter(0x10FFFF, 19);

            Assert.Multiple(() =>
            {
                Assert.That(face.FaceCount, Is.EqualTo(2));
                Assert.That(face.FaceIndex, Is.EqualTo(1));
                Assert.That(face.GetMetrics(4096).LineHeight, Is.GreaterThan(0));
                Assert.That(notdef.GlyphId, Is.EqualTo(0));
                Assert.That(() => face.RasterizeCharacter('A', 4097), Throws.TypeOf<FontLoadException>());
            });
        }

        [Test]
        public void HandlesPathologicalGlyphsColorTablesAndInvalidTableOffsets()
        {
            var source = File.ReadAllBytes(FontPath("Inter_Regular.ttf"));
            var colorSource = AddColorTables(source);
            using var face = UIFontFace.FromMemory(colorSource);
            var zeroContour = face.RasterizeCharacter(0, 32);
            var negativeBearing = face.RasterizeCharacter('j', 32);
            var largeMetrics = face.GetMetrics(4096);
            var invalidSource = (byte[])source.Clone();
            CorruptTableOffset(invalidSource, 0x676C7966);
            var invalid = Assert.Throws<FontLoadException>(() => UIFontFace.FromMemory(invalidSource));

            Assert.Multiple(() =>
            {
                Assert.That(zeroContour.Width, Is.Zero);
                Assert.That(zeroContour.Height, Is.Zero);
                Assert.That(negativeBearing.BearingX, Is.LessThan(0));
                Assert.That(largeMetrics.LineHeight, Is.GreaterThan(0));
                Assert.That(face.RasterizeCharacter('A', 32).Pixels.Length, Is.GreaterThan(0));
                Assert.That(invalid.ErrorCode, Is.EqualTo(FontLoadErrorCode.InvalidData));
            });
        }

        [Test]
        public void ShapesLatinArabicAndMalformedUtf16WithStableClusters()
        {
            using var latinFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
            using var arabicFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
            var latin = latinFace.Shape("office", 24, TextDirection.LeftToRight, "en");
            var arabic = arabicFace.Shape("مرحبا", 24, TextDirection.Auto, "ar");
            var malformed = latinFace.Shape("A\uD800B", 24);

            Assert.Multiple(() =>
            {
                Assert.That(latin.Direction, Is.EqualTo(TextDirection.LeftToRight));
                Assert.That(latin.Glyphs, Is.Not.Empty);
                Assert.That(latin.Glyphs, Has.All.Property(nameof(UIFontShapedGlyph.GlyphId)).GreaterThan(0));
                Assert.That(arabic.Direction, Is.EqualTo(TextDirection.RightToLeft));
                Assert.That(arabic.Glyphs[0].Utf16Cluster, Is.GreaterThan(arabic.Glyphs[^1].Utf16Cluster));
                Assert.That(arabic.Glyphs, Has.All.Property(nameof(UIFontShapedGlyph.GlyphId)).GreaterThan(0));
                Assert.That(arabic.Glyphs.Sum(glyph => glyph.AdvanceX), Is.GreaterThan(0));
                Assert.That(malformed.Text, Is.EqualTo("A\uFFFDB"));
                Assert.That(malformed.Glyphs.Any(glyph => glyph.Utf16Cluster == 1), Is.True);
            });
        }

        [Test]
        public void MatchesHarfBuzzReferenceGlyphsClustersAndPositions()
        {
            using var latinFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
            using var arabicFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
            var arabic = arabicFace.Shape("مرحبا", 24, TextDirection.RightToLeft, "ar", "Arab");
            var kerned = latinFace.Shape("AV", 24, TextDirection.LeftToRight, "en", "Latn");
            var unkerned = latinFace.Shape("AV", 24, TextDirection.LeftToRight, "en", "Latn", new[] { new UIFontOpenTypeFeature("kern", 0) });

            Assert.Multiple(() =>
            {
                Assert.That(arabic.Glyphs.Select(glyph => glyph.GlyphId), Is.EqualTo(new uint[] { 9, 317, 16, 27, 31, 79 }));
                Assert.That(arabic.Glyphs.Select(glyph => glyph.Utf16Cluster), Is.EqualTo(new[] { 4, 3, 3, 2, 1, 0 }));
                Assert.That(arabic.Glyphs.Select(glyph => glyph.AdvanceX), Is.EqualTo(new[] { 6.984375f, 0, 8.234375f, 14.140625f, 8.546875f, 12.59375f }));
                Assert.That(arabic.Glyphs.Select(glyph => glyph.OffsetX), Is.EqualTo(new[] { 0, 2.25f, 0, 0, -0.953125f, 0 }));
                Assert.That(arabic.Glyphs.Select(glyph => glyph.OffsetY), Is.EqualTo(new[] { 0, -0.078125f, 0, 0, 0, 0 }));
                Assert.That(kerned.Glyphs.Select(glyph => glyph.GlyphId), Is.EqualTo(new uint[] { 2, 456 }));
                Assert.That(kerned.Glyphs.Select(glyph => glyph.AdvanceX), Is.EqualTo(new[] { 14.921875f, 16.5625f }));
                Assert.That(unkerned.Glyphs.Select(glyph => glyph.AdvanceX), Is.EqualTo(new[] { 16.5625f, 16.5625f }));
            });
        }

        [Test]
        public void DefaultBackendReportsBoundedDiagnosticsWithoutNativeTypesInPublicApi()
        {
            var diagnostics = DynamicTextNativeDiagnostics.Current;
            var publicApi = string.Join("\n", typeof(UIFontFace).Assembly.GetExportedTypes()
                .SelectMany(type => type.GetMembers())
                .Select(member => member.ToString()));

            Assert.Multiple(() =>
            {
                Assert.That(DynamicTextBackendRegistry.Backend.Name, Is.EqualTo("FreeTypeSharp/HarfBuzzSharp"));
                Assert.That(diagnostics.FreeTypeLibraryName, Is.EqualTo("freetype"));
                Assert.That(diagnostics.FreeTypePackageId, Is.EqualTo("FreeTypeSharp"));
                Assert.That(diagnostics.HarfBuzzLibraryName, Is.EqualTo("libHarfBuzzSharp"));
                Assert.That(diagnostics.HarfBuzzPackageId, Is.EqualTo("HarfBuzzSharp.NativeAssets"));
                Assert.That(publicApi, Does.Not.Contain("FreeTypeSharp"));
                Assert.That(publicApi, Does.Not.Contain("HarfBuzzSharp"));
            });
        }

        private static string FontPath(string fileName) => Path.Combine(TestContext.CurrentContext.TestDirectory, "Fonts", fileName);

        private static byte[] CreateCollection(params byte[][] faces)
        {
            var headerLength = 12 + faces.Length * 4;
            var offsets = new int[faces.Length];
            var length = Align4(headerLength);
            for (var index = 0; index < faces.Length; index++)
            {
                offsets[index] = length;
                length = Align4(checked(length + faces[index].Length));
            }

            var collection = new byte[length];
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(0, 4), 0x74746366);
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(4, 4), 0x00010000);
            BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(8, 4), checked((uint)faces.Length));
            for (var index = 0; index < faces.Length; index++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(12 + index * 4, 4), checked((uint)offsets[index]));
                faces[index].CopyTo(collection, offsets[index]);
                var tableCount = BinaryPrimitives.ReadUInt16BigEndian(collection.AsSpan(offsets[index] + 4, 2));
                for (var tableIndex = 0; tableIndex < tableCount; tableIndex++)
                {
                    var tableOffsetPosition = offsets[index] + 12 + tableIndex * 16 + 8;
                    var tableOffset = BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(tableOffsetPosition, 4));
                    BinaryPrimitives.WriteUInt32BigEndian(collection.AsSpan(tableOffsetPosition, 4), checked(tableOffset + (uint)offsets[index]));
                }
            }
            return collection;
        }

        private static byte[] AddColorTables(byte[] source)
        {
            var tableCount = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(4, 2));
            var tables = new List<(uint Tag, byte[] Data)>();
            for (var index = 0; index < tableCount; index++)
            {
                var record = 12 + index * 16;
                var tag = BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(record, 4));
                var offset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(record + 8, 4)));
                var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(record + 12, 4)));
                tables.Add((tag, source.AsSpan(offset, length).ToArray()));
            }

            var colr = new byte[14];
            BinaryPrimitives.WriteUInt32BigEndian(colr.AsSpan(4, 4), 14);
            BinaryPrimitives.WriteUInt32BigEndian(colr.AsSpan(8, 4), 14);
            var cpal = new byte[18];
            BinaryPrimitives.WriteUInt16BigEndian(cpal.AsSpan(2, 2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(cpal.AsSpan(4, 2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(cpal.AsSpan(6, 2), 1);
            BinaryPrimitives.WriteUInt32BigEndian(cpal.AsSpan(8, 4), 14);
            cpal[14] = cpal[15] = cpal[16] = cpal[17] = byte.MaxValue;
            tables.Add((0x434F4C52, colr));
            tables.Add((0x4350414C, cpal));
            tables.Sort((left, right) => left.Tag.CompareTo(right.Tag));

            var outputLength = 12 + tables.Count * 16;
            foreach (var table in tables) outputLength = Align4(checked(outputLength + table.Data.Length));
            var output = new byte[outputLength];
            source.AsSpan(0, 4).CopyTo(output);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4, 2), checked((ushort)tables.Count));
            var maximumPower = 1;
            var entrySelector = 0;
            while (maximumPower * 2 <= tables.Count) { maximumPower *= 2; entrySelector++; }
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6, 2), checked((ushort)(maximumPower * 16)));
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8, 2), checked((ushort)entrySelector));
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10, 2), checked((ushort)(tables.Count * 16 - maximumPower * 16)));
            var outputOffset = 12 + tables.Count * 16;
            var headOffset = -1;
            for (var index = 0; index < tables.Count; index++)
            {
                var table = tables[index];
                var record = 12 + index * 16;
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record, 4), table.Tag);
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 4, 4), CalculateTableChecksum(table.Data));
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 8, 4), checked((uint)outputOffset));
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 12, 4), checked((uint)table.Data.Length));
                table.Data.CopyTo(output, outputOffset);
                if (table.Tag == 0x68656164) headOffset = outputOffset;
                outputOffset = Align4(checked(outputOffset + table.Data.Length));
            }
            if (headOffset >= 0)
            {
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8, 4), 0);
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headOffset + 8, 4), unchecked(0xB1B0AFBAu - CalculateTableChecksum(output)));
            }
            return output;
        }

        private static uint CalculateTableChecksum(ReadOnlySpan<byte> data)
        {
            uint checksum = 0;
            Span<byte> word = stackalloc byte[4];
            for (var offset = 0; offset < data.Length; offset += 4)
            {
                word.Clear();
                data.Slice(offset, Math.Min(4, data.Length - offset)).CopyTo(word);
                checksum = unchecked(checksum + BinaryPrimitives.ReadUInt32BigEndian(word));
            }
            return checksum;
        }

        private static void CorruptTableOffset(byte[] source, uint tableTag)
        {
            var tableCount = BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(4, 2));
            for (var index = 0; index < tableCount; index++)
            {
                var record = 12 + index * 16;
                if (BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(record, 4)) != tableTag) continue;
                BinaryPrimitives.WriteUInt32BigEndian(source.AsSpan(record + 8, 4), checked((uint)source.Length + 4));
                return;
            }
            throw new AssertionException("Expected SFNT table was not found.");
        }

        private static int Align4(int value) => checked((value + 3) & ~3);

        private sealed class LengthOnlyStream : Stream
        {
            private readonly long _length;
            public LengthOnlyStream(long length) { _length = length; }
            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get; set; }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new AssertionException("Oversized stream should be rejected before reading.");
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}