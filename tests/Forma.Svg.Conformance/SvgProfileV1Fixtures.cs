// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Tests
{
    internal static class SvgProfileV1Fixtures
    {
        internal static IEnumerable<TestCaseData> All()
        {
            foreach (var fixture in SvgProfileV1Corpus.All)
                yield return new TestCaseData(fixture.Name, fixture.Svg).SetName(fixture.Name);
        }
    }
}
