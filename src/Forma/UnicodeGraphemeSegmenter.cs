// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Forma;

internal static class UnicodeGraphemeSegmenter
{
    internal static int[] GetUtf16Boundaries(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (text.Length == 0) return new[] { 0 };

        var scalars = Decode(text);
        var boundaries = new List<int> { 0 };
        for (var index = 1; index < scalars.Count; index++)
            if (ShouldBreak(scalars, index)) boundaries.Add(scalars[index].Utf16Start);
        boundaries.Add(text.Length);
        return boundaries.ToArray();
    }

    private static List<Scalar> Decode(string text)
    {
        var scalars = new List<Scalar>(text.Length);
        var offset = 0;
        while (offset < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }
            scalars.Add(new Scalar(rune.Value, offset, GetGraphemeProperty(rune.Value), GetIndicProperty(rune.Value), IsExtendedPictographic(rune.Value)));
            offset += consumed;
        }
        return scalars;
    }

    private static bool ShouldBreak(IReadOnlyList<Scalar> scalars, int index)
    {
        var left = scalars[index - 1];
        var right = scalars[index];
        if (left.Grapheme == "CR" && right.Grapheme == "LF") return false;
        if (IsControl(left.Grapheme) || IsControl(right.Grapheme)) return true;
        if (left.Grapheme == "L" && right.Grapheme is "L" or "V" or "LV" or "LVT") return false;
        if (left.Grapheme is "LV" or "V" && right.Grapheme is "V" or "T") return false;
        if (left.Grapheme is "LVT" or "T" && right.Grapheme == "T") return false;
        if (right.Grapheme is "Extend" or "ZWJ") return false;
        if (right.Grapheme == "SpacingMark") return false;
        if (left.Grapheme == "Prepend") return false;
        if (IsIndicConjunct(scalars, index)) return false;
        if (IsEmojiZwjSequence(scalars, index)) return false;
        if (left.Grapheme == "Regional_Indicator" && right.Grapheme == "Regional_Indicator")
        {
            var preceding = 0;
            for (var cursor = index - 1; cursor >= 0 && scalars[cursor].Grapheme == "Regional_Indicator"; cursor--) preceding++;
            return preceding % 2 == 0;
        }
        return true;
    }

    private static bool IsIndicConjunct(IReadOnlyList<Scalar> scalars, int index)
    {
        if (scalars[index].Indic != "Consonant") return false;
        var cursor = index - 1;
        var hasLinker = false;
        while (cursor >= 0 && scalars[cursor].Indic is "Extend" or "Linker")
        {
            hasLinker |= scalars[cursor].Indic == "Linker";
            cursor--;
        }
        return hasLinker && cursor >= 0 && scalars[cursor].Indic == "Consonant";
    }

    private static bool IsEmojiZwjSequence(IReadOnlyList<Scalar> scalars, int index)
    {
        if (!scalars[index].ExtendedPictographic || scalars[index - 1].Grapheme != "ZWJ") return false;
        var cursor = index - 2;
        while (cursor >= 0 && scalars[cursor].Grapheme == "Extend") cursor--;
        return cursor >= 0 && scalars[cursor].ExtendedPictographic;
    }

    private static bool IsControl(string property) => property is "Control" or "CR" or "LF";
    private static string GetGraphemeProperty(int value) => Lookup(UnicodeGraphemeData.GraphemeRanges, value);
    private static string GetIndicProperty(int value) => Lookup(UnicodeGraphemeData.IndicConjunctRanges, value);
    private static bool IsExtendedPictographic(int value) => Lookup(UnicodeGraphemeData.ExtendedPictographicRanges, value).Length != 0;

    private static string Lookup(UnicodePropertyRange[] ranges, int value)
    {
        var low = 0;
        var high = ranges.Length - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var range = ranges[middle];
            if (value < range.Start) high = middle - 1;
            else if (value > range.End) low = middle + 1;
            else return range.Property;
        }
        return string.Empty;
    }

    private readonly record struct Scalar(int Value, int Utf16Start, string Grapheme, string Indic, bool ExtendedPictographic);
}