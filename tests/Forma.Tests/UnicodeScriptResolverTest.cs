// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Tests;

public class UnicodeScriptResolverTest
{
    [TestCase("A.\u0627", new[] { "Latn", "Latn", "Arab" })]
    [TestCase("(\u0627", new[] { "Arab", "Arab" })]
    [TestCase("\u03B1\u00B7\u03B2", new[] { "Grek", "Grek", "Grek" })]
    public void CommonAndScriptExtensionClusters_ResolveAgainstStrongNeighbors(string text, string[] expected)
    {
        var boundaries = UnicodeGraphemeSegmenter.GetUtf16Boundaries(text);

        Assert.That(UnicodeScriptResolver.ResolveGraphemeScripts(text, boundaries), Is.EqualTo(expected));
    }

    [Test]
    public void IndicConjunct_ResolvesAsOneDevanagariCluster()
    {
        const string text = "\u0915\u094D\u0937";
        var boundaries = UnicodeGraphemeSegmenter.GetUtf16Boundaries(text);

        Assert.That(boundaries, Is.EqualTo(new[] { 0, text.Length }));
        Assert.That(UnicodeScriptResolver.ResolveGraphemeScripts(text, boundaries), Is.EqualTo(new[] { "Deva" }));
    }
}