// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Forma;

internal enum BidiParagraphDirection
{
    LeftToRight,
    RightToLeft,
    AutoLeftToRight
}

internal sealed class UnicodeBidiResult
{
    private readonly byte?[] _implicitLevels;
    private readonly bool[] _l1Trailing;
    private readonly bool[] _separators;

    internal UnicodeBidiResult(byte paragraphLevel, byte?[] levels, int[] visualOrder, byte?[] implicitLevels, bool[] l1Trailing, bool[] separators)
    {
        ParagraphLevel = paragraphLevel;
        Levels = levels;
        VisualOrder = visualOrder;
        _implicitLevels = implicitLevels;
        _l1Trailing = l1Trailing;
        _separators = separators;
    }

    internal byte ParagraphLevel { get; }
    internal byte?[] Levels { get; }
    internal int[] VisualOrder { get; }

    internal byte?[] GetLineLevels(int scalarStart, int scalarLength)
    {
        if (scalarStart < 0 || scalarLength < 0 || scalarStart > _implicitLevels.Length - scalarLength)
            throw new ArgumentOutOfRangeException();
        var result = new byte?[scalarLength];
        Array.Copy(_implicitLevels, scalarStart, result, 0, scalarLength);
        for (var position = 0; position < scalarLength; position++)
        {
            if (!_separators[scalarStart + position]) continue;
            result[position] = ParagraphLevel;
            for (var cursor = position - 1; cursor >= 0 && _l1Trailing[scalarStart + cursor]; cursor--)
                result[cursor] = ParagraphLevel;
        }
        for (var cursor = scalarLength - 1; cursor >= 0 && _l1Trailing[scalarStart + cursor]; cursor--)
            result[cursor] = ParagraphLevel;
        return result;
    }
}

internal static class UnicodeBidiResolver
{
    private const int MaximumExplicitDepth = 125;

    internal static UnicodeBidiResult Resolve(string text, BidiParagraphDirection direction = BidiParagraphDirection.AutoLeftToRight)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        var codePoints = new List<int>(text.Length);
        var offset = 0;
        while (offset < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }
            codePoints.Add(rune.Value);
            offset += consumed;
        }
        return Resolve(codePoints.ToArray(), direction);
    }

    internal static UnicodeBidiResult Resolve(int[] codePoints, BidiParagraphDirection direction = BidiParagraphDirection.AutoLeftToRight)
    {
        if (codePoints == null) throw new ArgumentNullException(nameof(codePoints));
        var originalTypes = new BidiType[codePoints.Length];
        for (var index = 0; index < codePoints.Length; index++)
            originalTypes[index] = ParseType(Lookup(UnicodeGraphemeData.BidiClassRanges, codePoints[index]));
        return Resolve(codePoints, originalTypes, direction);
    }

    internal static UnicodeBidiResult ResolveTypes(string[] bidiTypes, BidiParagraphDirection direction)
    {
        if (bidiTypes == null) throw new ArgumentNullException(nameof(bidiTypes));
        var originalTypes = new BidiType[bidiTypes.Length];
        for (var index = 0; index < bidiTypes.Length; index++) originalTypes[index] = ParseType(bidiTypes[index]);
        var codePoints = new int[bidiTypes.Length];
        Array.Fill(codePoints, -1);
        return Resolve(codePoints, originalTypes, direction);
    }

    private static UnicodeBidiResult Resolve(int[] codePoints, BidiType[] originalTypes, BidiParagraphDirection direction)
    {
        var matchingPdi = FindMatchingPdis(originalTypes);
        var paragraphLevel = direction switch
        {
            BidiParagraphDirection.LeftToRight => (byte)0,
            BidiParagraphDirection.RightToLeft => (byte)1,
            _ => DetermineParagraphLevel(originalTypes, matchingPdi, 0, originalTypes.Length)
        };
        var types = (BidiType[])originalTypes.Clone();
        var levels = new byte?[codePoints.Length];
        ResolveExplicitLevels(types, levels, matchingPdi, paragraphLevel);

        var active = new List<int>(codePoints.Length);
        for (var index = 0; index < codePoints.Length; index++)
            if (!IsRemoved(originalTypes[index])) active.Add(index);

        var explicitLevels = (byte?[])levels.Clone();
        var sequences = BuildIsolatingRunSequences(active, explicitLevels, originalTypes, matchingPdi);
        foreach (var sequence in sequences)
            ResolveSequence(sequence, codePoints, originalTypes, types, levels, explicitLevels, active, matchingPdi, paragraphLevel);

        var implicitLevels = (byte?[])levels.Clone();
        ApplyL1(active, originalTypes, levels, paragraphLevel);
        var l1Trailing = new bool[originalTypes.Length];
        var separators = new bool[originalTypes.Length];
        for (var index = 0; index < originalTypes.Length; index++)
        {
            l1Trailing[index] = IsL1Trailing(originalTypes[index]);
            separators[index] = originalTypes[index] is BidiType.S or BidiType.B;
        }
        return new UnicodeBidiResult(paragraphLevel, levels, Reorder(active, levels), implicitLevels, l1Trailing, separators);
    }

    internal static int GetMirror(int codePoint)
    {
        var maps = UnicodeGraphemeData.BidiMirrors;
        var low = 0;
        var high = maps.Length - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (codePoint < maps[middle].CodePoint) high = middle - 1;
            else if (codePoint > maps[middle].CodePoint) low = middle + 1;
            else return maps[middle].Value;
        }
        return codePoint;
    }

    private static void ResolveExplicitLevels(BidiType[] types, byte?[] levels, int[] matchingPdi, byte paragraphLevel)
    {
        var stack = new List<Status> { new(paragraphLevel, Override.Neutral, false) };
        var overflowIsolates = 0;
        var overflowEmbeddings = 0;
        var validIsolates = 0;

        for (var index = 0; index < types.Length; index++)
        {
            var type = types[index];
            var current = stack[^1];
            switch (type)
            {
                case BidiType.RLE:
                case BidiType.LRE:
                case BidiType.RLO:
                case BidiType.LRO:
                    var embeddingLevel = type is BidiType.RLE or BidiType.RLO
                        ? NextOdd(current.Level)
                        : NextEven(current.Level);
                    if (embeddingLevel <= MaximumExplicitDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        var directionalOverride = type switch
                        {
                            BidiType.RLO => Override.RightToLeft,
                            BidiType.LRO => Override.LeftToRight,
                            _ => Override.Neutral
                        };
                        stack.Add(new Status((byte)embeddingLevel, directionalOverride, false));
                    }
                    else if (overflowIsolates == 0)
                    {
                        overflowEmbeddings++;
                    }
                    break;

                case BidiType.RLI:
                case BidiType.LRI:
                case BidiType.FSI:
                    levels[index] = current.Level;
                    ApplyOverride(types, index, current.DirectionalOverride);
                    var isolateDirection = type == BidiType.FSI
                        ? DetermineParagraphLevel(types, matchingPdi, index + 1, matchingPdi[index] >= 0 ? matchingPdi[index] : types.Length)
                        : type == BidiType.RLI ? (byte)1 : (byte)0;
                    var isolateLevel = isolateDirection == 1 ? NextOdd(current.Level) : NextEven(current.Level);
                    if (isolateLevel <= MaximumExplicitDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                    {
                        validIsolates++;
                        stack.Add(new Status((byte)isolateLevel, Override.Neutral, true));
                    }
                    else
                    {
                        overflowIsolates++;
                    }
                    break;

                case BidiType.PDI:
                    if (overflowIsolates > 0)
                    {
                        overflowIsolates--;
                    }
                    else if (validIsolates > 0)
                    {
                        overflowEmbeddings = 0;
                        while (!stack[^1].Isolate) stack.RemoveAt(stack.Count - 1);
                        stack.RemoveAt(stack.Count - 1);
                        validIsolates--;
                    }
                    current = stack[^1];
                    levels[index] = current.Level;
                    ApplyOverride(types, index, current.DirectionalOverride);
                    break;

                case BidiType.PDF:
                    if (overflowIsolates == 0)
                    {
                        if (overflowEmbeddings > 0) overflowEmbeddings--;
                        else if (stack.Count > 1 && !stack[^1].Isolate) stack.RemoveAt(stack.Count - 1);
                    }
                    break;

                case BidiType.B:
                    levels[index] = paragraphLevel;
                    break;

                case BidiType.BN:
                    break;

                default:
                    levels[index] = current.Level;
                    ApplyOverride(types, index, current.DirectionalOverride);
                    break;
            }
        }
    }

    private static List<Sequence> BuildIsolatingRunSequences(List<int> active, byte?[] levels, BidiType[] originalTypes, int[] matchingPdi)
    {
        var runs = new List<LevelRun>();
        var characterToRun = new Dictionary<int, int>();
        for (var activeIndex = 0; activeIndex < active.Count;)
        {
            var start = activeIndex;
            var level = levels[active[start]]!.Value;
            while (activeIndex + 1 < active.Count && levels[active[activeIndex + 1]] == level) activeIndex++;
            var characters = active.GetRange(start, activeIndex - start + 1);
            var runIndex = runs.Count;
            foreach (var character in characters) characterToRun[character] = runIndex;
            runs.Add(new LevelRun(characters));
            activeIndex++;
        }

        var sequences = new List<Sequence>();
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            var first = runs[runIndex].Characters[0];
            if (originalTypes[first] == BidiType.PDI && HasMatchingInitiator(matchingPdi, first)) continue;

            var characters = new List<int>();
            var currentRun = runIndex;
            while (true)
            {
                characters.AddRange(runs[currentRun].Characters);
                var last = runs[currentRun].Characters[^1];
                if (!IsIsolate(originalTypes[last]) || matchingPdi[last] < 0) break;
                currentRun = characterToRun[matchingPdi[last]];
            }
            sequences.Add(new Sequence(characters));
        }
        return sequences;
    }

    private static void ResolveSequence(Sequence sequence, int[] codePoints, BidiType[] originalTypes, BidiType[] types, byte?[] levels, byte?[] explicitLevels, List<int> active, int[] matchingPdi, byte paragraphLevel)
    {
        var indices = sequence.Characters;
        var first = indices[0];
        var last = indices[^1];
        var firstActive = active.IndexOf(first);
        var lastActive = active.IndexOf(last);
        var previousLevel = firstActive > 0 ? explicitLevels[active[firstActive - 1]]!.Value : paragraphLevel;
        var followingLevel = lastActive + 1 < active.Count && !(IsIsolate(originalTypes[last]) && matchingPdi[last] < 0)
            ? explicitLevels[active[lastActive + 1]]!.Value
            : paragraphLevel;
        var sos = StrongType(Math.Max(explicitLevels[first]!.Value, previousLevel));
        var eos = StrongType(Math.Max(explicitLevels[last]!.Value, followingLevel));

        ResolveWeakTypes(indices, originalTypes, types, sos);
        ResolveBrackets(indices, codePoints, originalTypes, types, levels, sos);
        ResolveNeutralTypes(indices, types, levels, sos, eos);
        ResolveImplicitLevels(indices, types, levels);
    }

    private static void ResolveWeakTypes(List<int> indices, BidiType[] originalTypes, BidiType[] types, BidiType sos)
    {
        var previous = sos;
        foreach (var index in indices)
        {
            if (types[index] == BidiType.NSM)
                types[index] = IsIsolate(previous) || previous == BidiType.PDI ? BidiType.ON : previous;
            previous = types[index];
        }

        var previousStrong = sos;
        foreach (var index in indices)
        {
            if (types[index] == BidiType.EN && previousStrong == BidiType.AL) types[index] = BidiType.AN;
            if (types[index] is BidiType.R or BidiType.L or BidiType.AL) previousStrong = types[index];
        }
        foreach (var index in indices)
            if (types[index] == BidiType.AL) types[index] = BidiType.R;

        for (var position = 1; position + 1 < indices.Count; position++)
        {
            var left = types[indices[position - 1]];
            var currentIndex = indices[position];
            var right = types[indices[position + 1]];
            if (types[currentIndex] == BidiType.ES && left == BidiType.EN && right == BidiType.EN) types[currentIndex] = BidiType.EN;
            else if (types[currentIndex] == BidiType.CS && left == right && left is BidiType.EN or BidiType.AN) types[currentIndex] = left;
        }

        for (var position = 0; position < indices.Count;)
        {
            if (types[indices[position]] != BidiType.ET)
            {
                position++;
                continue;
            }
            var start = position;
            while (position < indices.Count && types[indices[position]] == BidiType.ET) position++;
            var adjacentToNumber = start > 0 && types[indices[start - 1]] == BidiType.EN
                || position < indices.Count && types[indices[position]] == BidiType.EN;
            if (adjacentToNumber)
                for (var cursor = start; cursor < position; cursor++) types[indices[cursor]] = BidiType.EN;
        }

        foreach (var index in indices)
            if (types[index] is BidiType.ES or BidiType.ET or BidiType.CS) types[index] = BidiType.ON;

        previousStrong = sos;
        foreach (var index in indices)
        {
            if (types[index] == BidiType.EN && previousStrong == BidiType.L) types[index] = BidiType.L;
            if (types[index] is BidiType.R or BidiType.L) previousStrong = types[index];
        }
    }

    private static void ResolveBrackets(List<int> indices, int[] codePoints, BidiType[] originalTypes, BidiType[] types, byte?[] levels, BidiType sos)
    {
        var pairs = FindBracketPairs(indices, codePoints, types);
        foreach (var pair in pairs)
        {
            var embedding = StrongType(levels[indices[pair.Open]]!.Value);
            var opposite = embedding == BidiType.L ? BidiType.R : BidiType.L;
            var hasEmbedding = false;
            var hasOpposite = false;
            for (var position = pair.Open + 1; position < pair.Close; position++)
            {
                var strong = ToStrong(types[indices[position]]);
                hasEmbedding |= strong == embedding;
                hasOpposite |= strong == opposite;
            }

            BidiType? resolved = null;
            if (hasEmbedding) resolved = embedding;
            else if (hasOpposite)
            {
                var preceding = sos;
                for (var position = pair.Open - 1; position >= 0; position--)
                {
                    var strong = ToStrong(types[indices[position]]);
                    if (strong is BidiType.L or BidiType.R)
                    {
                        preceding = strong;
                        break;
                    }
                }
                resolved = preceding == opposite ? opposite : embedding;
            }

            if (resolved == null) continue;
            types[indices[pair.Open]] = resolved.Value;
            types[indices[pair.Close]] = resolved.Value;
            PropagateBracketNsm(indices, pair.Open, originalTypes, types, resolved.Value);
            PropagateBracketNsm(indices, pair.Close, originalTypes, types, resolved.Value);
        }
    }

    private static List<BracketPair> FindBracketPairs(List<int> indices, int[] codePoints, BidiType[] types)
    {
        var stack = new List<BracketEntry>(63);
        var pairs = new List<BracketPair>();
        for (var position = 0; position < indices.Count; position++)
        {
            var index = indices[position];
            if (types[index] != BidiType.ON || !TryGetBracket(codePoints[index], out var bracket)) continue;
            if (bracket.IsOpening)
            {
                if (stack.Count == 63) return new List<BracketPair>();
                stack.Add(new BracketEntry(bracket.PairedCodePoint, position));
                continue;
            }
            for (var stackIndex = stack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                if (!BracketsMatch(stack[stackIndex].ClosingCodePoint, codePoints[index])) continue;
                pairs.Add(new BracketPair(stack[stackIndex].Position, position));
                stack.RemoveRange(stackIndex, stack.Count - stackIndex);
                break;
            }
        }
        pairs.Sort((left, right) => left.Open.CompareTo(right.Open));
        return pairs;
    }

    private static void ResolveNeutralTypes(List<int> indices, BidiType[] types, byte?[] levels, BidiType sos, BidiType eos)
    {
        for (var position = 0; position < indices.Count;)
        {
            if (!IsNeutral(types[indices[position]]))
            {
                position++;
                continue;
            }
            var start = position;
            while (position < indices.Count && IsNeutral(types[indices[position]])) position++;
            var before = start == 0 ? sos : ToStrong(types[indices[start - 1]]);
            var after = position == indices.Count ? eos : ToStrong(types[indices[position]]);
            for (var cursor = start; cursor < position; cursor++)
                types[indices[cursor]] = before == after ? before : StrongType(levels[indices[cursor]]!.Value);
        }
    }

    private static void ResolveImplicitLevels(List<int> indices, BidiType[] types, byte?[] levels)
    {
        foreach (var index in indices)
        {
            var level = levels[index]!.Value;
            if ((level & 1) == 0)
            {
                if (types[index] == BidiType.R) levels[index] = (byte)(level + 1);
                else if (types[index] is BidiType.EN or BidiType.AN) levels[index] = (byte)(level + 2);
            }
            else if (types[index] is BidiType.L or BidiType.EN or BidiType.AN)
            {
                levels[index] = (byte)(level + 1);
            }
        }
    }

    private static void ApplyL1(List<int> active, BidiType[] originalTypes, byte?[] levels, byte paragraphLevel)
    {
        for (var position = 0; position < active.Count; position++)
        {
            if (originalTypes[active[position]] is not (BidiType.S or BidiType.B)) continue;
            levels[active[position]] = paragraphLevel;
            for (var cursor = position - 1; cursor >= 0 && IsL1Trailing(originalTypes[active[cursor]]); cursor--)
                levels[active[cursor]] = paragraphLevel;
        }
        for (var cursor = active.Count - 1; cursor >= 0 && IsL1Trailing(originalTypes[active[cursor]]); cursor--)
            levels[active[cursor]] = paragraphLevel;
    }

    private static int[] Reorder(List<int> active, byte?[] levels)
    {
        var order = active.ToArray();
        var highest = 0;
        var lowestOdd = int.MaxValue;
        foreach (var index in active)
        {
            var level = levels[index]!.Value;
            highest = Math.Max(highest, level);
            if ((level & 1) != 0) lowestOdd = Math.Min(lowestOdd, level);
        }
        for (var level = highest; level >= lowestOdd; level--)
        {
            for (var position = 0; position < order.Length;)
            {
                if (levels[order[position]] < level)
                {
                    position++;
                    continue;
                }
                var start = position;
                while (position < order.Length && levels[order[position]] >= level) position++;
                Array.Reverse(order, start, position - start);
            }
        }
        return order;
    }

    private static int[] FindMatchingPdis(BidiType[] types)
    {
        var result = new int[types.Length];
        Array.Fill(result, -1);
        var stack = new Stack<int>();
        for (var index = 0; index < types.Length; index++)
        {
            if (IsIsolate(types[index])) stack.Push(index);
            else if (types[index] == BidiType.PDI && stack.Count > 0)
            {
                var initiator = stack.Pop();
                result[initiator] = index;
                result[index] = initiator;
            }
        }
        return result;
    }

    private static byte DetermineParagraphLevel(BidiType[] types, int[] matchingPdi, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (IsIsolate(types[index]))
            {
                index = matchingPdi[index] >= 0 && matchingPdi[index] < end ? matchingPdi[index] : end;
                continue;
            }
            if (types[index] == BidiType.L) return 0;
            if (types[index] is BidiType.R or BidiType.AL) return 1;
        }
        return 0;
    }

    private static void ApplyOverride(BidiType[] types, int index, Override directionalOverride)
    {
        if (directionalOverride == Override.LeftToRight) types[index] = BidiType.L;
        else if (directionalOverride == Override.RightToLeft) types[index] = BidiType.R;
    }

    private static void PropagateBracketNsm(List<int> indices, int position, BidiType[] originalTypes, BidiType[] types, BidiType resolved)
    {
        for (var cursor = position + 1; cursor < indices.Count && originalTypes[indices[cursor]] == BidiType.NSM; cursor++)
            types[indices[cursor]] = resolved;
    }

    private static bool TryGetBracket(int codePoint, out UnicodeBidiBracket bracket)
    {
        var brackets = UnicodeGraphemeData.BidiBrackets;
        var low = 0;
        var high = brackets.Length - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (codePoint < brackets[middle].CodePoint) high = middle - 1;
            else if (codePoint > brackets[middle].CodePoint) low = middle + 1;
            else
            {
                bracket = brackets[middle];
                return true;
            }
        }
        bracket = default;
        return false;
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
        return "L";
    }

    private static BidiType ParseType(string value) => Enum.Parse<BidiType>(value);
    private static int NextOdd(int level) => (level + 1) | 1;
    private static int NextEven(int level) => (level + 2) & ~1;
    private static BidiType StrongType(int level) => (level & 1) == 0 ? BidiType.L : BidiType.R;
    private static BidiType ToStrong(BidiType type) => type is BidiType.EN or BidiType.AN ? BidiType.R : type;
    private static bool IsRemoved(BidiType type) => type is BidiType.RLE or BidiType.LRE or BidiType.RLO or BidiType.LRO or BidiType.PDF or BidiType.BN;
    private static bool IsIsolate(BidiType type) => type is BidiType.LRI or BidiType.RLI or BidiType.FSI;
    private static bool IsNeutral(BidiType type) => type is BidiType.B or BidiType.S or BidiType.WS or BidiType.ON or BidiType.FSI or BidiType.LRI or BidiType.RLI or BidiType.PDI;
    private static bool IsL1Trailing(BidiType type) => type is BidiType.WS or BidiType.FSI or BidiType.LRI or BidiType.RLI or BidiType.PDI;
    private static bool HasMatchingInitiator(int[] matchingPdi, int pdi) => matchingPdi[pdi] >= 0 && matchingPdi[pdi] < pdi;
    private static bool BracketsMatch(int expected, int actual) => expected == actual || expected == 0x3009 && actual == 0x232A || expected == 0x232A && actual == 0x3009;

    private enum BidiType { L, R, AL, EN, ES, ET, AN, CS, NSM, BN, B, S, WS, ON, LRE, LRO, RLE, RLO, PDF, LRI, RLI, FSI, PDI }
    private enum Override { Neutral, LeftToRight, RightToLeft }
    private readonly record struct Status(byte Level, Override DirectionalOverride, bool Isolate);
    private sealed record LevelRun(List<int> Characters);
    private sealed record Sequence(List<int> Characters);
    private readonly record struct BracketEntry(int ClosingCodePoint, int Position);
    private readonly record struct BracketPair(int Open, int Close);
}