// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Collections.ObjectModel;

namespace Forma
{
    internal static class UIFontDefaultRegistry
    {
        private static UIFontFamily _fontFamily;

        internal static UIFontFamily FontFamily => Volatile.Read(ref _fontFamily);
        internal static void SetFontFamily(UIFontFamily fontFamily) => Volatile.Write(ref _fontFamily, fontFamily);
    }

    public enum FontLoadErrorCode
    {
        InvalidData,
        SourceTooLarge,
        FaceIndexOutOfRange,
        UnsupportedFormat,
        NativeFailure,
        RasterLimitExceeded,
        RasterTimeout,
        ShapingTimeout
    }

    public sealed class FontLoadException : Exception
    {
        internal FontLoadException(FontLoadErrorCode errorCode, string message) : base(message) => ErrorCode = errorCode;
        internal FontLoadException(FontLoadErrorCode errorCode, string message, Exception innerException) : base(message, innerException) => ErrorCode = errorCode;
        public FontLoadErrorCode ErrorCode { get; }
    }

    public enum UIFontHinting
    {
        Default,
        None,
        Auto
    }

    public readonly struct UIFontShapedGlyph
    {
        internal UIFontShapedGlyph(uint glyphId, int utf16Cluster, float advanceX, float advanceY, float offsetX, float offsetY)
        {
            GlyphId = glyphId;
            Utf16Cluster = utf16Cluster;
            AdvanceX = advanceX;
            AdvanceY = advanceY;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        public uint GlyphId { get; }
        public int Utf16Cluster { get; }
        public float AdvanceX { get; }
        public float AdvanceY { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
    }

    public sealed class UIFontShapedRun
    {
        private readonly ReadOnlyCollection<UIFontShapedGlyph> _glyphs;
        internal UIFontShapedRun(string text, TextDirection direction, List<UIFontShapedGlyph> glyphs) { Text = text; Direction = direction; _glyphs = glyphs.AsReadOnly(); }
        public string Text { get; }
        public TextDirection Direction { get; }
        public IReadOnlyList<UIFontShapedGlyph> Glyphs => _glyphs;
    }

    public readonly struct UIFontVariationAxis
    {
        internal UIFontVariationAxis(string tag, float minimum, float defaultValue, float maximum, ushort nameId) { Tag = tag; Minimum = minimum; Default = defaultValue; Maximum = maximum; NameId = nameId; }
        public string Tag { get; }
        public float Minimum { get; }
        public float Default { get; }
        public float Maximum { get; }
        public ushort NameId { get; }
    }

    public readonly struct UIFontVariationCoordinate : IEquatable<UIFontVariationCoordinate>
    {
        public UIFontVariationCoordinate(string tag, float value)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (tag.Length != 4) throw new ArgumentException("A variation axis tag must contain exactly four characters.", nameof(tag));
            for (var index = 0; index < tag.Length; index++) if (tag[index] < 0x20 || tag[index] > 0x7E) throw new ArgumentException("A variation axis tag must contain printable ASCII characters.", nameof(tag));
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            Tag = tag;
            Value = value;
        }

        public string Tag { get; }
        public float Value { get; }
        public bool Equals(UIFontVariationCoordinate other) => string.Equals(Tag, other.Tag, StringComparison.Ordinal) && Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is UIFontVariationCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(Tag ?? string.Empty), Value);
        public static bool operator ==(UIFontVariationCoordinate left, UIFontVariationCoordinate right) => left.Equals(right);
        public static bool operator !=(UIFontVariationCoordinate left, UIFontVariationCoordinate right) => !left.Equals(right);
    }

    public readonly struct UIFontFaceMetrics
    {
        internal UIFontFaceMetrics(float ascender, float descender, float lineGap, float lineHeight, float underlinePosition, float underlineThickness) { Ascender = ascender; Descender = descender; LineGap = lineGap; LineHeight = lineHeight; UnderlinePosition = underlinePosition; UnderlineThickness = underlineThickness; }
        public float Ascender { get; }
        public float Descender { get; }
        public float LineGap { get; }
        public float LineHeight { get; }
        public float UnderlinePosition { get; }
        public float UnderlineThickness { get; }
    }

    public readonly struct UIFontGlyphMetrics
    {
        internal UIFontGlyphMetrics(float width, float height, float bearingX, float bearingY, float advanceX, float advanceY) { Width = width; Height = height; BearingX = bearingX; BearingY = bearingY; AdvanceX = advanceX; AdvanceY = advanceY; }
        public float Width { get; }
        public float Height { get; }
        public float BearingX { get; }
        public float BearingY { get; }
        public float AdvanceX { get; }
        public float AdvanceY { get; }
    }

    public sealed class UIFontGlyphBitmap
    {
        internal UIFontGlyphBitmap(uint glyphId, int width, int height, int bearingX, int bearingY, float advanceX, byte[] pixels) { GlyphId = glyphId; Width = width; Height = height; BearingX = bearingX; BearingY = bearingY; AdvanceX = advanceX; Pixels = new ReadOnlyMemory<byte>(pixels); }
        public uint GlyphId { get; }
        public int Width { get; }
        public int Height { get; }
        public int BearingX { get; }
        public int BearingY { get; }
        public float AdvanceX { get; }
        public ReadOnlyMemory<byte> Pixels { get; }
    }
}