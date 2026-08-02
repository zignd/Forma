// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Forma;

internal static class UnicodeWordBreaker
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
            var property = Lookup(UnicodeGraphemeData.WordBreakRanges, rune.Value);
            scalars.Add(new Scalar(rune.Value, offset, property.Length == 0 ? "Other" : property, IsExtendedPictographic(rune.Value)));
            offset += consumed;
        }
        return scalars;
    }

    private static bool ShouldBreak(IReadOnlyList<Scalar> values, int index)
    {
        var adjacentLeft = values[index - 1];
        var adjacentRight = values[index];
        if (adjacentLeft.Property == "CR" && adjacentRight.Property == "LF") return false;
        if (IsNewline(adjacentLeft.Property) || IsNewline(adjacentRight.Property)) return true;
        if (adjacentLeft.Property == "ZWJ" && adjacentRight.ExtendedPictographic) return false;
        if (adjacentLeft.Property == "WSegSpace" && adjacentRight.Property == "WSegSpace") return false;
        if (IsIgnored(adjacentRight.Property)) return false;

        var leftIndex = PreviousSignificant(values, index - 1);
        if (leftIndex < 0) return true;
        var left = values[leftIndex].Property;
        var right = adjacentRight.Property;
        if (IsLetter(left) && IsLetter(right)) return false;
        if (IsLetter(left) && IsMidLetter(right) && IsLetter(PropertyAt(values, NextSignificant(values, index + 1)))) return false;
        if (IsLetter(right) && IsMidLetter(left) && IsLetter(PropertyAt(values, PreviousSignificant(values, leftIndex - 1)))) return false;
        if (left == "Hebrew_Letter" && right == "Single_Quote") return false;
        if (left == "Hebrew_Letter" && right == "Double_Quote" && PropertyAt(values, NextSignificant(values, index + 1)) == "Hebrew_Letter") return false;
        if (right == "Hebrew_Letter" && left == "Double_Quote" && PropertyAt(values, PreviousSignificant(values, leftIndex - 1)) == "Hebrew_Letter") return false;
        if (left == "Numeric" && right == "Numeric") return false;
        if (IsLetter(left) && right == "Numeric" || left == "Numeric" && IsLetter(right)) return false;
        if (left == "Numeric" && IsMidNumeric(right) && PropertyAt(values, NextSignificant(values, index + 1)) == "Numeric") return false;
        if (right == "Numeric" && IsMidNumeric(left) && PropertyAt(values, PreviousSignificant(values, leftIndex - 1)) == "Numeric") return false;
        if (left == "Katakana" && right == "Katakana") return false;
        if (IsExtendNumLetBase(left) && right == "ExtendNumLet") return false;
        if (left == "ExtendNumLet" && IsExtendNumLetBase(right)) return false;
        if (left == "Regional_Indicator" && right == "Regional_Indicator")
        {
            var count = 0;
            for (var cursor = leftIndex; cursor >= 0; cursor = PreviousSignificant(values, cursor - 1))
            {
                if (values[cursor].Property != "Regional_Indicator") break;
                count++;
            }
            return count % 2 == 0;
        }
        return true;
    }

    private static int PreviousSignificant(IReadOnlyList<Scalar> values, int index)
    {
        while (index >= 0 && IsIgnored(values[index].Property)) index--;
        return index;
    }

    private static int NextSignificant(IReadOnlyList<Scalar> values, int index)
    {
        while (index < values.Count && IsIgnored(values[index].Property)) index++;
        return index;
    }

    private static string PropertyAt(IReadOnlyList<Scalar> values, int index) => index >= 0 && index < values.Count ? values[index].Property : string.Empty;
    private static bool IsIgnored(string property) => property is "Extend" or "Format" or "ZWJ";
    private static bool IsNewline(string property) => property is "Newline" or "CR" or "LF";
    private static bool IsLetter(string property) => property is "ALetter" or "Hebrew_Letter";
    private static bool IsMidLetter(string property) => property is "MidLetter" or "MidNumLet" or "Single_Quote";
    private static bool IsMidNumeric(string property) => property is "MidNum" or "MidNumLet" or "Single_Quote";
    private static bool IsExtendNumLetBase(string property) => IsLetter(property) || property is "Numeric" or "Katakana" or "ExtendNumLet";
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

    private readonly record struct Scalar(int Value, int Utf16Start, string Property, bool ExtendedPictographic);
}