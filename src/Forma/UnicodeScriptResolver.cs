// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Forma;

internal static class UnicodeScriptResolver
{
    internal static string[] ResolveGraphemeScripts(string text, int[] boundaries)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (boundaries == null) throw new ArgumentNullException(nameof(boundaries));
        if (boundaries.Length == 0 || boundaries[0] != 0 || boundaries[^1] != text.Length)
            throw new ArgumentException("Grapheme boundaries must span the complete UTF-16 input.", nameof(boundaries));

        var candidates = new HashSet<string>[boundaries.Length - 1];
        var scripts = new string[boundaries.Length - 1];
        for (var index = 0; index < scripts.Length; index++)
        {
            candidates[index] = GetCandidates(text.AsSpan(boundaries[index], boundaries[index + 1] - boundaries[index]));
            if (candidates[index].Count == 1)
                foreach (var candidate in candidates[index]) scripts[index] = candidate;
        }

        string previous = null;
        for (var index = 0; index < scripts.Length; index++)
        {
            if (previous != null && (candidates[index].Count == 0 || candidates[index].Contains(previous))) scripts[index] = previous;
            if (scripts[index] != null) previous = scripts[index];
        }

        string next = null;
        for (var index = scripts.Length - 1; index >= 0; index--)
        {
            if (scripts[index] == null && next != null && (candidates[index].Count == 0 || candidates[index].Contains(next))) scripts[index] = next;
            if (scripts[index] != null) next = scripts[index];
        }

        previous = null;
        for (var index = 0; index < scripts.Length; index++)
        {
            scripts[index] ??= previous ?? "Zyyy";
            previous = scripts[index];
        }
        return scripts;
    }

    private static HashSet<string> GetCandidates(ReadOnlySpan<char> cluster)
    {
        HashSet<string> candidates = null;
        while (!cluster.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(cluster, out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }
            var values = GetScriptExtensions(rune.Value);
            if (values.Count == 0)
            {
                var script = Lookup(UnicodeGraphemeData.ScriptRanges, rune.Value);
                if (script is not ("Zyyy" or "Zinh" or "Zzzz") && script.Length != 0) values.Add(script);
            }
            if (values.Count != 0)
            {
                if (candidates == null) candidates = values;
                else candidates.IntersectWith(values);
            }
            cluster = cluster.Slice(consumed);
        }
        return candidates ?? new HashSet<string>(StringComparer.Ordinal);
    }

    private static HashSet<string> GetScriptExtensions(int value)
    {
        var property = Lookup(UnicodeGraphemeData.ScriptExtensionRanges, value);
        return property.Length == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(property.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
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
}