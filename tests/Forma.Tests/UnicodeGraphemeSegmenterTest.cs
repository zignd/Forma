// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;

namespace Forma.Tests;

public class UnicodeGraphemeSegmenterTest
{
    [Test]
    public void Unicode17ExtendedGraphemeClusters_PassOfficialConformanceCases()
    {
        for (var caseIndex = 0; caseIndex < UnicodeGraphemeBreakCases.All.Length; caseIndex++)
        {
            var test = UnicodeGraphemeBreakCases.All[caseIndex];
            var text = new StringBuilder();
            var scalarOffsets = new List<int> { 0 };
            foreach (var codePoint in test.CodePoints)
            {
                text.Append(new Rune(codePoint));
                scalarOffsets.Add(text.Length);
            }
            var expected = test.ScalarBoundaries.Select(boundary => scalarOffsets[boundary]).ToArray();

            Assert.That(
                UnicodeGraphemeSegmenter.GetUtf16Boundaries(text.ToString()),
                Is.EqualTo(expected),
                $"Unicode 17 GraphemeBreakTest case {caseIndex + 1}: {string.Join(" ", test.CodePoints.Select(value => value.ToString("X4")))}");
        }
    }

    [Test]
    public void MalformedUtf16_UsesStableReplacementCharacterRanges()
    {
        Assert.That(UnicodeGraphemeSegmenter.GetUtf16Boundaries("A\uD800\u0308B"), Is.EqualTo(new[] { 0, 1, 3, 4 }));
    }
}