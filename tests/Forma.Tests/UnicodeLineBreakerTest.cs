// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;

namespace Forma.Tests;

public class UnicodeLineBreakerTest
{
    [Test]
    public void Unicode17DefaultLineBreaking_PassesOfficialConformanceCases()
    {
        for (var caseIndex = 0; caseIndex < UnicodeLineBreakCases.All.Length; caseIndex++)
        {
            var test = UnicodeLineBreakCases.All[caseIndex];
            var text = new StringBuilder();
            var scalarOffsets = new List<int> { 0 };
            foreach (var codePoint in test.CodePoints)
            {
                text.Append(new Rune(codePoint));
                scalarOffsets.Add(text.Length);
            }
            var expected = test.ScalarBoundaries.Select(boundary => scalarOffsets[boundary]).ToArray();

            Assert.That(
                UnicodeLineBreaker.GetUtf16BreakOpportunities(text.ToString()),
                Is.EqualTo(expected),
                $"Unicode 17 LineBreakTest case {caseIndex + 1}: {string.Join(" ", test.CodePoints.Select(value => value.ToString("X4")))}");
        }
    }
}