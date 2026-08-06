// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;

namespace Forma.Tests
{
    [TestFixture]
    public sealed class SvgRasterCacheTest
    {
        [SetUp]
        public void InstallBackend()
        {
    #if THORVG
            SvgThorvgBackendDefaults.Install();
    #else
            SvgSkiaBackendDefaults.Install();
    #endif
        }

        [Test]
        public void StoreParsesOnceReusesExactSizeAndPreservesTransparentPadding()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(16, 16, 2, 1));
            var source = Source("#80c040");
            store.BeginFrame();
            var first = store.GetOrAdd(source, 4, 3);
            var warm = store.GetOrAdd(source, 4, 3);
            var secondSize = store.GetOrAdd(source, 3, 2);
            store.EndFrame();
            var diagnostics = store.GetDiagnostics();
            var pixels = store.GetPagePixels(first.PageIndex).ToArray();
            var paddingOffset = ((first.Bounds.Y - 1) * 16 + first.Bounds.X - 1) * 4;
            var paddingPixel = pixels.AsSpan(paddingOffset, 4).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(warm, Is.SameAs(first));
                Assert.That(secondSize.PageIndex, Is.EqualTo(first.PageIndex));
                Assert.That(diagnostics.Parses, Is.EqualTo(1));
                Assert.That(diagnostics.Rasterizations, Is.EqualTo(2));
                Assert.That(diagnostics.Misses, Is.EqualTo(2));
                Assert.That(diagnostics.Hits, Is.EqualTo(1));
                Assert.That(diagnostics.DocumentCount, Is.EqualTo(1));
                Assert.That(diagnostics.BackendId, Is.EqualTo(SvgRuntime.Health.BackendId));
                Assert.That(diagnostics.BackendVersion, Is.EqualTo(SvgRuntime.Health.Version));
                Assert.That(diagnostics.ProfileVersion, Is.EqualTo("1"));
                Assert.That(paddingPixel, Is.EqualTo(new byte[4]));
                Assert.That(pixels[(first.Bounds.Y * 16 + first.Bounds.X) * 4 + 3], Is.GreaterThan((byte)0));
            });
        }

        [Test]
        public void NestedFrameScopesShareTheActiveDeviceFrame()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(16, 16, 1, 1));
            var source = Source("#4080c0");
            store.BeginFrame();
            store.BeginFrame();
            store.GetOrAdd(source, 4, 4);
            store.EndFrame();

            Assert.That(() => store.GetOrAdd(source, 4, 4), Throws.Nothing);
            store.EndFrame();
            Assert.That(() => store.GetOrAdd(source, 4, 4), Throws.InvalidOperationException);
        }

        [Test]
        public void StoreEvictsOldestInactivePageWithoutInvalidatingCurrentFrame()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(8, 8, 2, 1));
            var first = Source("#ff0000");
            var second = Source("#00ff00");
            var third = Source("#0000ff");
            store.BeginFrame();
            var firstEntry = store.GetOrAdd(first, 6, 6);
            var secondEntry = store.GetOrAdd(second, 6, 6);
            var deferred = store.GetOrAdd(third, 6, 6);
            store.EndFrame();

            store.BeginFrame();
            store.GetOrAdd(second, 6, 6);
            var thirdEntry = store.GetOrAdd(third, 6, 6);
            store.EndFrame();

            Assert.Multiple(() =>
            {
                Assert.That(firstEntry.IsAvailable, Is.True);
                Assert.That(secondEntry.IsAvailable, Is.True);
                Assert.That(deferred.IsAvailable, Is.False);
                Assert.That(thirdEntry.IsAvailable, Is.True);
                Assert.That(store.Contains(firstEntry.Key), Is.False);
                Assert.That(store.Contains(secondEntry.Key), Is.True);
                Assert.That(store.Contains(thirdEntry.Key), Is.True);
                Assert.That(store.GetDiagnostics().Evictions, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().Failures, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().Bytes, Is.EqualTo(8 * 8 * 2 * 4));
            });
        }

        [Test]
        public void ResetRetainsRastersAndOversizedRequestsFailWithinBudget()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(8, 8, 1, 1));
            var source = Source("#ffffff");
            store.BeginFrame();
            var entry = store.GetOrAdd(source, 4, 4);
            var oversized = store.GetOrAdd(source, 7, 7);
            store.EndFrame();
            store.MarkPageUploaded(entry.PageIndex);
            var beforeReset = store.GetDiagnostics();
            store.MarkAllPagesDirty();

            Assert.Multiple(() =>
            {
                Assert.That(entry.IsAvailable, Is.True);
                Assert.That(oversized.IsAvailable, Is.False);
                Assert.That(beforeReset.Uploads, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().PendingUploads, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().Rasterizations, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().Failures, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().LastDiagnostic, Does.Contain("does not fit"));
                Assert.That(() => new SvgRasterCacheOptions(4096, 4096, 2), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => new SvgRasterCacheOptions(16, 16, 1, 1, 512), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void ParsedDocumentLruStaysBoundedAndReparsesOnlyWhenAnotherSizeNeedsIt()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(16, 16, 1, 1, 1024, 1));
            var first = Source("#ff0000");
            var second = Source("#00ff00");
            store.BeginFrame();
            store.GetOrAdd(first, 2, 2);
            store.GetOrAdd(second, 2, 2);
            store.GetOrAdd(first, 2, 2);
            store.GetOrAdd(first, 3, 3);
            store.EndFrame();

            Assert.Multiple(() =>
            {
                Assert.That(store.GetDiagnostics().DocumentCount, Is.EqualTo(1));
                Assert.That(store.GetDiagnostics().DocumentEvictions, Is.EqualTo(2));
                Assert.That(store.GetDiagnostics().Parses, Is.EqualTo(3));
                Assert.That(store.GetDiagnostics().Rasterizations, Is.EqualTo(3));
                Assert.That(store.GetDiagnostics().Hits, Is.EqualTo(1));
            });
        }

        [Test]
        public void WarmExactSizeLookupAllocatesNoManagedMemoryOrColdWork()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(16, 16, 1, 1));
            var source = Source("#40c080");
            store.BeginFrame();
            store.GetOrAdd(source, 4, 4);
            store.EndFrame();
            var before = store.GetDiagnostics();

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 100; iteration++)
            {
                store.BeginFrame();
                store.GetOrAdd(source, 4, 4);
                store.EndFrame();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var after = store.GetDiagnostics();

            Assert.Multiple(() =>
            {
                Assert.That(allocated, Is.Zero);
                Assert.That(after.Parses, Is.EqualTo(before.Parses));
                Assert.That(after.Rasterizations, Is.EqualTo(before.Rasterizations));
                Assert.That(after.PageCount, Is.EqualTo(before.PageCount));
                Assert.That(after.PendingUploads, Is.EqualTo(before.PendingUploads));
                Assert.That(after.Hits - before.Hits, Is.EqualTo(100));
            });
        }

        private static SvgImageSource Source(string color) => SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
            $"<svg xmlns='http://www.w3.org/2000/svg' width='6' height='6'><rect width='6' height='6' fill='{color}'/></svg>"));

        [Test]
        public void FractionalScalesBoundCacheCardinalityToDistinctSizes()
        {
            using var store = new SvgRasterAtlasStore(new SvgRasterCacheOptions(256, 256, 4, 1));
            var source = Source("#204060");
            var scales = new[] { 1f, 1.25f, 1.5f, 2f };
            store.BeginFrame();
            foreach (var scale in scales)
            {
                var width = (int)MathF.Ceiling(16 * scale);
                var height = (int)MathF.Ceiling(16 * scale);
                store.GetOrAdd(source, width, height);
            }
            store.EndFrame();
            var diag = store.GetDiagnostics();
            Assert.Multiple(() =>
            {
                Assert.That(diag.EntryCount, Is.EqualTo(scales.Length), "One entry must exist per distinct rasterization size.");
                Assert.That(diag.Parses, Is.EqualTo(1), "A single source must parse exactly once regardless of scale count.");
                Assert.That(diag.Rasterizations, Is.EqualTo(scales.Length), "Each distinct size must rasterize exactly once.");
            });
        }
    }
}