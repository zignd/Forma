// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Forma;

internal static class UnicodeLineBreaker
{
    private static readonly HashSet<string> HardBreaks = new(StringComparer.Ordinal) { "BK", "CR", "LF", "NL" };
    private static readonly HashSet<string> Alphabetics = new(StringComparer.Ordinal) { "AL", "HL" };
    private static readonly HashSet<string> Ideographics = new(StringComparer.Ordinal) { "ID", "EB", "EM" };
    private static readonly HashSet<string> Hangul = new(StringComparer.Ordinal) { "JL", "JV", "JT", "H2", "H3" };

    internal static int[] GetUtf16BreakOpportunities(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        var scalars = Decode(text);
        if (scalars.Count == 0) return new[] { 0 };
        ResolveCombiningClasses(scalars);
        var boundaries = new List<int>();
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
            var lineBreak = Lookup(UnicodeGraphemeData.LineBreakRanges, rune.Value);
            if (lineBreak.Length == 0) lineBreak = "XX";
            var category = Lookup(UnicodeGraphemeData.GeneralCategoryRanges, rune.Value);
            if (category.Length == 0) category = "Cn";
            var eastAsianWidth = Lookup(UnicodeGraphemeData.EastAsianWidthRanges, rune.Value);
            if (eastAsianWidth.Length == 0) eastAsianWidth = "N";
            lineBreak = lineBreak switch
            {
                "AI" or "SG" or "XX" => "AL",
                "SA" when category is "Mn" or "Mc" => "CM",
                "SA" => "AL",
                "CJ" => "NS",
                _ => lineBreak
            };
            scalars.Add(new Scalar(
                rune.Value,
                offset,
                lineBreak,
                lineBreak,
                category,
                eastAsianWidth,
                Lookup(UnicodeGraphemeData.ExtendedPictographicRanges, rune.Value).Length != 0));
            offset += consumed;
        }
        return scalars;
    }

    private static void ResolveCombiningClasses(List<Scalar> scalars)
    {
        for (var index = 0; index < scalars.Count; index++)
        {
            if (scalars[index].OriginalClass is not ("CM" or "ZWJ")) continue;
            if (index > 0 && scalars[index - 1].Class is not ("BK" or "CR" or "LF" or "NL" or "SP" or "ZW"))
            {
                scalars[index] = scalars[index] with { Class = scalars[index - 1].Class, InheritedClass = true };
                continue;
            }
            scalars[index] = scalars[index] with { Class = "AL" };
        }
    }

    private static bool ShouldBreak(IReadOnlyList<Scalar> values, int index)
    {
        var adjacentLeft = values[index - 1];
        var right = values[index];

        if (adjacentLeft.OriginalClass == "CR" && right.OriginalClass == "LF") return false;
        if (HardBreaks.Contains(adjacentLeft.OriginalClass)) return true;
        if (HardBreaks.Contains(right.OriginalClass)) return false;
        if (right.OriginalClass is "SP" or "ZW") return false;
        if (HasZeroWidthSpaceBefore(values, index)) return true;
        if (adjacentLeft.OriginalClass == "ZWJ") return false;
        if (right.InheritedClass) return false;
        var left = values[PreviousSignificantIndex(values, index)];
        if (left.Class == "WJ" || right.Class == "WJ") return false;
        if (left.Class == "GL") return false;
        if (right.Class == "GL" && left.Class is not ("SP" or "BA" or "HY" or "HH")) return false;
        if (right.Class is "CL" or "CP" or "EX" or "SY") return false;
        if (PreviousNonSpaceClass(values, index) == "OP") return false;
        if (IsInitialQuote(values, index)) return false;
        if (IsFinalQuote(values, index)) return false;
        if (left.Class == "SP" && right.Class == "IS" && index + 1 < values.Count && values[index + 1].Class == "NU") return true;
        if (right.Class == "IS") return false;
        if (right.Class == "NS" && PreviousNonSpaceClass(values, index) is "CL" or "CP") return false;
        if (right.Class == "B2" && PreviousNonSpaceClass(values, index) == "B2") return false;
        if (left.Class == "SP") return true;
        if (IsNonInitialQuote(values, index) || IsNonFinalQuote(values, index) || IsNonEastAsianQuoteContext(values, index)) return false;
        if (left.Class == "CB" || right.Class == "CB") return true;
        if (IsWordInitialHyphen(values, index)) return false;
        if (right.Class is "BA" or "HH" or "HY" or "NS" || left.Class == "BB") return false;
        if (IsHebrewHyphen(values, index)) return false;
        if (left.Class == "SY" && right.Class == "HL") return false;
        if (right.Class == "IN") return false;
        if (Alphabetics.Contains(left.Class) && right.Class == "NU" || left.Class == "NU" && Alphabetics.Contains(right.Class)) return false;
        if (left.Class == "PR" && Ideographics.Contains(right.Class) || Ideographics.Contains(left.Class) && right.Class == "PO") return false;
        if ((left.Class is "PR" or "PO") && Alphabetics.Contains(right.Class) || Alphabetics.Contains(left.Class) && right.Class is "PR" or "PO") return false;
        if (IsNumericSequence(values, index)) return false;
        if (left.Class == "JL" && right.Class is "JL" or "JV" or "H2" or "H3") return false;
        if (left.Class is "JV" or "H2" && right.Class is "JV" or "JT") return false;
        if (left.Class is "JT" or "H3" && right.Class == "JT") return false;
        if (Hangul.Contains(left.Class) && right.Class == "PO" || left.Class == "PR" && Hangul.Contains(right.Class)) return false;
        if (Alphabetics.Contains(left.Class) && Alphabetics.Contains(right.Class)) return false;
        if (IsBrahmicSyllable(values, index)) return false;
        if (left.Class == "IS" && Alphabetics.Contains(right.Class)) return false;
        if ((Alphabetics.Contains(left.Class) || left.Class == "NU") && right.Class == "OP" && !right.IsEastAsian) return false;
        if (left.Class == "CP" && !left.IsEastAsian && (Alphabetics.Contains(right.Class) || right.Class == "NU")) return false;
        if (left.Class == "RI" && right.Class == "RI")
        {
            var preceding = 0;
            for (var cursor = PreviousSignificantIndex(values, index); cursor >= 0 && values[cursor].Class == "RI"; cursor = PreviousSignificantIndex(values, cursor)) preceding++;
            return preceding % 2 == 0;
        }
        if ((left.Class == "EB" || left.ExtendedPictographic && left.GeneralCategory == "Cn") && right.Class == "EM") return false;
        return true;
    }

    private static bool HasZeroWidthSpaceBefore(IReadOnlyList<Scalar> values, int index)
    {
        var cursor = index - 1;
        while (cursor >= 0 && values[cursor].OriginalClass == "SP") cursor--;
        return cursor >= 0 && values[cursor].OriginalClass == "ZW";
    }

    private static string PreviousNonSpaceClass(IReadOnlyList<Scalar> values, int index)
    {
        var cursor = index - 1;
        while (cursor >= 0 && values[cursor].Class == "SP") cursor--;
        return cursor < 0 ? string.Empty : values[cursor].Class;
    }

    private static bool IsInitialQuote(IReadOnlyList<Scalar> values, int index)
    {
        var cursor = index - 1;
        while (cursor >= 0 && values[cursor].Class == "SP") cursor--;
        while (cursor >= 0 && values[cursor].InheritedClass) cursor--;
        if (cursor < 0 || values[cursor].Class != "QU" || values[cursor].GeneralCategory != "Pi") return false;
        cursor = PreviousSignificantIndex(values, cursor);
        return cursor < 0 || values[cursor].Class is "BK" or "CR" or "LF" or "NL" or "OP" or "QU" or "GL" or "SP" or "ZW";
    }

    private static bool IsFinalQuote(IReadOnlyList<Scalar> values, int index)
    {
        if (values[index].Class != "QU" || values[index].GeneralCategory != "Pf") return false;
        if (index + 1 == values.Count) return true;
        return values[index + 1].Class is "SP" or "GL" or "WJ" or "CL" or "QU" or "CP" or "EX" or "IS" or "SY" or "BK" or "CR" or "LF" or "NL" or "ZW";
    }

    private static bool IsNonInitialQuote(IReadOnlyList<Scalar> values, int index) =>
        values[index].Class == "QU" && values[index].GeneralCategory != "Pi";

    private static bool IsNonFinalQuote(IReadOnlyList<Scalar> values, int index) =>
        values[index - 1].Class == "QU" && values[index - 1].GeneralCategory != "Pf";

    private static bool IsNonEastAsianQuoteContext(IReadOnlyList<Scalar> values, int index)
    {
        var left = values[index - 1];
        var right = values[index];
        if (right.Class == "QU" && !left.IsEastAsian) return true;
        if (right.Class == "QU" && (index + 1 == values.Count || !values[index + 1].IsEastAsian)) return true;
        if (left.Class == "QU" && !right.IsEastAsian) return true;
        return left.Class == "QU" && (index < 2 || !values[index - 2].IsEastAsian);
    }

    private static bool IsWordInitialHyphen(IReadOnlyList<Scalar> values, int index)
    {
        var hyphenIndex = PreviousSignificantIndex(values, index);
        if (values[hyphenIndex].Class is not ("HY" or "HH") || !Alphabetics.Contains(values[index].Class)) return false;
        var contextIndex = PreviousSignificantIndex(values, hyphenIndex);
        return contextIndex < 0 || values[contextIndex].Class is "BK" or "CR" or "LF" or "NL" or "SP" or "ZW" or "CB" or "GL";
    }

    private static bool IsHebrewHyphen(IReadOnlyList<Scalar> values, int index)
    {
        var hyphenIndex = PreviousSignificantIndex(values, index);
        var hebrewIndex = PreviousSignificantIndex(values, hyphenIndex);
        return hebrewIndex >= 0 && values[hebrewIndex].Class == "HL" && values[hyphenIndex].Class is "HY" or "HH" && values[index].Class != "HL";
    }

    private static bool IsNumericSequence(IReadOnlyList<Scalar> values, int index)
    {
        var left = values[index - 1].Class;
        var right = values[index].Class;
        if (left is "HY" or "IS" && right == "NU") return true;
        if (left is "PO" or "PR")
        {
            if (right == "NU") return true;
            if (right == "OP" && index + 1 < values.Count && (values[index + 1].Class == "NU" || values[index + 1].Class == "IS" && index + 2 < values.Count && values[index + 2].Class == "NU")) return true;
        }
        if (right is "PO" or "PR")
        {
            var cursor = index - 1;
            if (values[cursor].Class is "CL" or "CP") cursor--;
            while (cursor >= 0 && values[cursor].Class is "SY" or "IS") cursor--;
            if (cursor >= 0 && values[cursor].Class == "NU") return true;
        }
        if (right == "NU")
        {
            var cursor = index - 1;
            while (cursor >= 0 && values[cursor].Class is "SY" or "IS") cursor--;
            if (cursor >= 0 && values[cursor].Class == "NU") return true;
        }
        return false;
    }

    private static bool IsBrahmicSyllable(IReadOnlyList<Scalar> values, int index)
    {
        static bool IsBase(Scalar value) => value.Class is "AK" or "AS" || value.Value == 0x25CC;
        var leftIndex = index - 1;
        while (leftIndex > 0 && values[leftIndex].InheritedClass) leftIndex--;
        var left = values[leftIndex];
        var right = values[index];
        if (left.Class == "AP" && IsBase(right)) return true;
        if (IsBase(left) && right.Class is "VF" or "VI") return true;
        if (left.Class == "VI" && IsBase(right))
        {
            var baseIndex = PreviousSignificantIndex(values, leftIndex);
            if (baseIndex >= 0 && IsBase(values[baseIndex])) return true;
        }
        return IsBase(left) && IsBase(right) && index + 1 < values.Count && values[index + 1].Class == "VF";
    }

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

    private static int PreviousSignificantIndex(IReadOnlyList<Scalar> values, int index)
    {
        var cursor = index - 1;
        while (cursor >= 0 && values[cursor].InheritedClass) cursor--;
        return cursor;
    }

    private readonly record struct Scalar(
        int Value,
        int Utf16Start,
        string OriginalClass,
        string Class,
        string GeneralCategory,
        string EastAsianWidth,
        bool ExtendedPictographic,
        bool InheritedClass = false)
    {
        internal bool IsEastAsian => EastAsianWidth is "F" or "W" or "H";
    }
}