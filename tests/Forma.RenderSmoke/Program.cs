// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using var game = new Game();
_ = new GraphicsDeviceManager(game) { GraphicsProfile = GraphicsProfile.HiDef };
var manager = (IGraphicsDeviceManager)game.Services.GetService(typeof(IGraphicsDeviceManager));
manager.CreateDevice();
var graphicsDevice = game.GraphicsDevice;
using var face = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");

ValidateAlpha8AndReset(graphicsDevice, face);
var warmDraw = ValidateWarmDrawing(graphicsDevice, face);
Console.WriteLine(RunPerformanceBenchmarks(graphicsDevice, warmDraw));
ValidateIndependentDeviceOwnership(graphicsDevice, face);
Console.WriteLine($"Dynamic render smoke passed on {graphicsDevice.Adapter.Description} ({graphicsDevice.GraphicsProfile}).");

static string RunPerformanceBenchmarks(GraphicsDevice graphicsDevice, TimeSpan warmDraw)
{
    var timer = Stopwatch.StartNew();
    using (var coldFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf")) { }
    var coldLoad = timer.Elapsed;

    using var inter = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
    timer.Restart();
    inter.Shape("Forma office", 20, TextDirection.LeftToRight, "en", "Latn");
    var firstShape = timer.Elapsed;

    using var cache = new DynamicGlyphCache(graphicsDevice, new DynamicGlyphCacheOptions(64, 64, 1, 1));
    var font = new DynamicUIFont(inter, 20);
    timer.Restart();
    cache.BeginFrame();
    cache.GetOrAdd(font, inter.GetGlyphId('A'), 1);
    cache.EndFrame();
    cache.FlushUploads();
    var firstRasterUpload = timer.Elapsed;

    var engine = new TextLayoutEngine();
    engine.Layout(font, "Warm layout lookup");
    timer.Restart();
    for (var iteration = 0; iteration < 1000; iteration++) engine.Layout(font, "Warm layout lookup");
    var warmLayout = timer.Elapsed;

    using var arabic = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/NotoSansArabic_Variable.ttf");
    var fallbackFont = new DynamicUIFont(inter, 20, UIFontHinting.Default, arabic);
    timer.Restart();
    new TextLayoutEngine().Layout(fallbackFont, "Forma مرحبا office العالم Forma مرحبا", new TextLayoutOptions(maxWidth: 180, wrapping: TextWrapping.Word, locale: "ar"));
    var fallbackLayout = timer.Elapsed;

    using var churnCache = new DynamicGlyphCache(graphicsDevice, new DynamicGlyphCacheOptions(64, 64, 1, 1));
    timer.Restart();
    for (var character = 33; character < 127; character++)
    {
        churnCache.BeginFrame();
        churnCache.GetOrAdd(font, inter.GetGlyphId(character), 1);
        churnCache.EndFrame();
        churnCache.FlushUploads();
    }
    var atlasChurn = timer.Elapsed;

    RequireBudget("cold face load", coldLoad, 1000);
    RequireBudget("first shape", firstShape, 500);
    RequireBudget("first raster/upload", firstRasterUpload, 500);
    RequireBudget("1,000 warm layout lookups", warmLayout, 500);
    RequireBudget("100 warm draws", warmDraw, 1000);
    RequireBudget("fallback-heavy layout", fallbackLayout, 500);
    RequireBudget("atlas churn", atlasChurn, 2000);
    Require(engine.Diagnostics.CacheHitRate >= .99, $"Warm layout cache hit rate was {engine.Diagnostics.CacheHitRate:P2}; budget is at least 99%.");

    return FormattableString.Invariant($"Dynamic text benchmark: load={coldLoad.TotalMilliseconds:F3}ms, shape={firstShape.TotalMilliseconds:F3}ms, rasterUpload={firstRasterUpload.TotalMilliseconds:F3}ms, warmLayout1000={warmLayout.TotalMilliseconds:F3}ms, warmDraw100={warmDraw.TotalMilliseconds:F3}ms, fallback={fallbackLayout.TotalMilliseconds:F3}ms, churn={atlasChurn.TotalMilliseconds:F3}ms");
}

static void RequireBudget(string operation, TimeSpan elapsed, double milliseconds) =>
    Require(elapsed.TotalMilliseconds <= milliseconds, $"Dynamic text {operation} took {elapsed.TotalMilliseconds:F3} ms; budget is {milliseconds:F0} ms.");

static void ValidateAlpha8AndReset(GraphicsDevice graphicsDevice, UIFontFace face)
{
    var font = new DynamicUIFont(face, 18);
    using var cache = new DynamicGlyphCache(graphicsDevice, new DynamicGlyphCacheOptions(64, 64, 2, 1));
    cache.BeginFrame();
    var first = cache.GetOrAdd(font, face.GetGlyphId('A'), 1);
    var second = cache.GetOrAdd(font, face.GetGlyphId('B'), 1);
    cache.EndFrame();
    Require(cache.Diagnostics.PendingUploads == 1, "Two cold glyphs must batch into one pending page upload.");
    cache.FlushUploads();
    var originalTexture = cache.GetTexture(first);
    var pixels = new byte[originalTexture.Width * originalTexture.Height];
    originalTexture.GetData(pixels);
    Require(originalTexture.Format == SurfaceFormat.Alpha8, "Dynamic glyph pages must use Alpha8.");
    Require(ReferenceEquals(cache.GetTexture(second), originalTexture), "Glyphs on one page must share one texture.");
    Require(cache.Diagnostics.Uploads == 1 && cache.Diagnostics.PendingUploads == 0, "The cold page must upload exactly once.");
    Require(pixels.Any(value => value > 0), "The Alpha8 page must preserve nonzero glyph coverage.");

    graphicsDevice.Reset();
    Require(cache.Diagnostics.PendingUploads == 1, "Device reset must retain CPU pages and queue them for reupload.");
    cache.FlushUploads();
    var recoveredTexture = cache.GetTexture(first);
    Require(!ReferenceEquals(recoveredTexture, originalTexture), "Device reset must replace invalid texture references.");
    Require(cache.Diagnostics.PendingUploads == 0, "Recovered pages must finish uploading.");
}

static TimeSpan ValidateWarmDrawing(GraphicsDevice graphicsDevice, UIFontFace face)
{
    var engine = new TextLayoutEngine();
    var font = new DynamicUIFont(face, 20);
    var layout = engine.Layout(font, "Atlas");
    var emptyDynamicLayout = engine.Layout(font, string.Empty);
    var glyphTint = new Color(20, 180, 140, 255);
    using var target = new RenderTarget2D(graphicsDevice, 120, 60, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    using var context = new UIRenderContext(graphicsDevice, new Theme());

    DrawFrame(graphicsDevice, context, target, layout, glyphTint);
    var firstFrame = ReadPixels(target);
    DrawFrame(graphicsDevice, context, target, layout, glyphTint);
    var populatedFrame = ReadPixels(target);
    var populated = context.DynamicGlyphDiagnostics;
    DrawFrame(graphicsDevice, context, target, layout, glyphTint);
    var warmFrame = ReadPixels(target);
    var warm = context.DynamicGlyphDiagnostics;

    Require(firstFrame.All(pixel => pixel == Color.Transparent), "Cold glyphs must wait for their batched upload.");
    Require(populatedFrame.Any(pixel => pixel != Color.Transparent), "Uploaded glyphs must render visible pixels.");
    Require(populatedFrame.Any(pixel => pixel.G > pixel.R * 4 && pixel.B > pixel.R * 3), "Dynamic glyphs must preserve the requested RGB tint.");
    Require(warmFrame.SequenceEqual(populatedFrame), "Warm drawing must preserve pixel output.");
    Require(warm.Misses == populated.Misses, "Warm drawing must not rasterize another glyph.");
    Require(warm.Uploads == populated.Uploads && warm.PendingUploads == 0, "Warm drawing must not upload another page.");
    ValidateWarmTraversalAllocations(layout);
    ValidateWarmCacheAllocations(graphicsDevice, font, face);


static void ValidateWarmTraversalAllocations(TextLayout layout)
{
    var visible = 0;
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var iteration = 0; iteration < 100; iteration++)
        for (var runIndex = 0; runIndex < layout.Runs.Count; runIndex++)
            for (var glyphIndex = 0; glyphIndex < layout.Runs[runIndex].Glyphs.Count; glyphIndex++)
                if (layout.IsVisible(layout.Runs[runIndex].Glyphs[glyphIndex])) visible++;
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    Require(visible > 0 && allocated == 0, $"Warm retained-layout traversal allocated {allocated} managed bytes.");
}

static void ValidateWarmCacheAllocations(GraphicsDevice graphicsDevice, DynamicUIFont font, UIFontFace face)
{
    using var cache = new DynamicGlyphCache(graphicsDevice, new DynamicGlyphCacheOptions(64, 64, 1, 1));
    var glyphIds = "Atlas".Select(character => face.GetGlyphId(character)).ToArray();
    cache.BeginFrame();
    foreach (var glyphId in glyphIds) cache.GetOrAdd(font, glyphId, 1);
    cache.EndFrame();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var iteration = 0; iteration < 100; iteration++)
    {
        cache.BeginFrame();
        for (var glyphIndex = 0; glyphIndex < glyphIds.Length; glyphIndex++) cache.GetOrAdd(font, glyphIds[glyphIndex], 1);
        cache.EndFrame();
    }
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    Require(allocated == 0, $"Warm atlas lookup allocated {allocated} managed bytes.");
}
    for (var frame = 0; frame < 100; frame++) DrawEmptyFrame(graphicsDevice, context, target);
    var emptyAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var frame = 0; frame < 100; frame++) DrawEmptyFrame(graphicsDevice, context, target);
    var emptyAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - emptyAllocatedBefore;
    for (var frame = 0; frame < 100; frame++) DrawFilledFrame(graphicsDevice, context, target);
    var filledAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var frame = 0; frame < 100; frame++) DrawFilledFrame(graphicsDevice, context, target);
    var filledAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - filledAllocatedBefore;
    for (var frame = 0; frame < 100; frame++) DrawFrame(graphicsDevice, context, target, emptyDynamicLayout, glyphTint);
    var effectAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    for (var frame = 0; frame < 100; frame++) DrawFrame(graphicsDevice, context, target, emptyDynamicLayout, glyphTint);
    var effectAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - effectAllocatedBefore;
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var warmDrawStarted = Stopwatch.GetTimestamp();
    for (var frame = 0; frame < 100; frame++) DrawFrame(graphicsDevice, context, target, layout, glyphTint);
    var warmDraw = Stopwatch.GetElapsedTime(warmDrawStarted);
    var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    var allocationDiagnostics = context.DynamicGlyphDiagnostics;
    Require(allocatedBytes == effectAllocatedBytes, $"One hundred unchanged warm text draws allocated {allocatedBytes} managed bytes versus {effectAllocatedBytes} for empty Alpha8 effect frames, {filledAllocatedBytes} for rendered non-text frames, and {emptyAllocatedBytes} for empty frames.");
    Require(allocationDiagnostics.Misses == warm.Misses && allocationDiagnostics.Uploads == warm.Uploads, "Warm allocation sampling must remain free of rasterization and uploads.");

    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.PushClip(new Rectangle(0, 0, 12, target.Height));
    context.Text(layout, new Vector2(4, 4), glyphTint);
    context.PopClip();
    context.End();
    graphicsDevice.SetRenderTarget(null);
    var clipped = ReadPixels(target);
    Require(clipped.Any(pixel => pixel != Color.Transparent), "A partial clip must retain covered glyph pixels.");
    for (var y = 0; y < target.Height; y++)
        for (var x = 12; x < target.Width; x++)
            Require(clipped[y * target.Width + x] == Color.Transparent, "Glyph pixels must not escape the active clip.");

    context.DisplayScale = 1.5f;
    var fractionalLayout = new TextLayoutEngine().Layout(new DynamicUIFont(face, 17.5f), "Fractional");
    DrawClippedFrame(graphicsDevice, context, target, fractionalLayout, new Vector2(3.25f, 2.5f), new Rectangle(0, 0, 10, 40), glyphTint);
    DrawClippedFrame(graphicsDevice, context, target, fractionalLayout, new Vector2(3.25f, 2.5f), new Rectangle(0, 0, 10, 40), glyphTint);
    var fractional = ReadPixels(target);
    DrawClippedFrame(graphicsDevice, context, target, fractionalLayout, new Vector2(3.25f, 2.5f), new Rectangle(0, 0, 10, 40), glyphTint);
    var fractionalWarm = ReadPixels(target);
    Require(fractional.Any(pixel => pixel != Color.Transparent), "Fractional 1.5x drawing must preserve covered glyph samples.");
    Require(fractionalWarm.SequenceEqual(fractional), "Fractional 1.5x sampling must remain stable on a warm frame.");
    for (var y = 0; y < target.Height; y++)
        for (var x = 15; x < target.Width; x++)
            Require(fractional[y * target.Width + x] == Color.Transparent, "The 1.5x physical scissor must contain fractional glyph samples.");
    ValidateLifecycleStress(graphicsDevice, context, target, face);
    return warmDraw;
}

static void ValidateLifecycleStress(GraphicsDevice graphicsDevice, UIRenderContext context, RenderTarget2D target, UIFontFace inter)
{
    using var arabic = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/NotoSansArabic_Variable.ttf");
    var engine = new TextLayoutEngine();
    for (var iteration = 0; iteration < 48; iteration++)
    {
        using var recreated = iteration % 8 == 0 ? UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf") : null;
        var primary = recreated ?? inter;
        var font = iteration % 2 == 0
            ? new DynamicUIFont(primary, 12 + iteration % 17, UIFontHinting.Default, arabic)
            : new DynamicUIFont(arabic, 12 + iteration % 17, UIFontHinting.Default, primary);
        var arabicIteration = iteration % 3 == 0;
        var text = arabicIteration ? $"تحرير سريع {iteration} Forma" : $"Rapid edit {iteration} مرحبا";
        var options = new TextLayoutOptions(
            maxWidth: 80 + iteration % 5 * 12,
            wrapping: TextWrapping.Word,
            direction: arabicIteration ? TextDirection.RightToLeft : TextDirection.LeftToRight,
            locale: arabicIteration ? "ar" : "en");
        context.DisplayScale = iteration % 3 == 0 ? 1f : iteration % 3 == 1 ? 1.5f : 2f;
        var layout = engine.Layout(font, text, options);
        DrawFrame(graphicsDevice, context, target, layout, Color.White);
        DrawFrame(graphicsDevice, context, target, layout, Color.White);
        if (iteration % 12 == 11) engine.Clear();
    }
    var diagnostics = context.DynamicGlyphDiagnostics;
    Require(diagnostics.Bytes <= DynamicGlyphCacheOptions.MaximumBytes, "Lifecycle stress exceeded the glyph atlas memory budget.");
    Require(diagnostics.Failures == 0, $"Lifecycle stress produced an atlas failure: {diagnostics.LastFailure}");
}

static void ValidateIndependentDeviceOwnership(GraphicsDevice primaryDevice, UIFontFace face)
{
    using var primaryCache = new DynamicGlyphCache(primaryDevice, new DynamicGlyphCacheOptions(64, 64, 1, 1));
    var primaryEntry = Prepare(primaryCache, face);
    var primaryTexture = primaryCache.GetTexture(primaryEntry);

    var secondGame = new Game();
    _ = new GraphicsDeviceManager(secondGame) { GraphicsProfile = GraphicsProfile.HiDef };
    var secondManager = (IGraphicsDeviceManager)secondGame.Services.GetService(typeof(IGraphicsDeviceManager));
    secondManager.CreateDevice();
    var secondCache = new DynamicGlyphCache(secondGame.GraphicsDevice, new DynamicGlyphCacheOptions(64, 64, 1, 1));
    var secondEntry = Prepare(secondCache, face);
    var secondTexture = secondCache.GetTexture(secondEntry);
    Require(!ReferenceEquals(primaryTexture, secondTexture), "Each graphics device must own independent atlas textures.");

    secondGame.Dispose();
    secondCache.Dispose();
}

static DynamicGlyphAtlasEntry Prepare(DynamicGlyphCache cache, UIFontFace face)
{
    var font = new DynamicUIFont(face, 18);
    cache.BeginFrame();
    var entry = cache.GetOrAdd(font, face.GetGlyphId('A'), 1);
    cache.EndFrame();
    cache.FlushUploads();
    return entry;
}

static void DrawFrame(GraphicsDevice graphicsDevice, UIRenderContext context, RenderTarget2D target, TextLayout layout, Color color)
{
    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.PushClip(new Rectangle(0, 0, target.Width, target.Height));
    context.Text(layout, new Vector2(4, 4), color);
    context.PopClip();
    context.End();
    graphicsDevice.SetRenderTarget(null);
}

static void DrawEmptyFrame(GraphicsDevice graphicsDevice, UIRenderContext context, RenderTarget2D target)
{
    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.PushClip(new Rectangle(0, 0, target.Width, target.Height));
    context.PopClip();
    context.End();
    graphicsDevice.SetRenderTarget(null);
}

static void DrawFilledFrame(GraphicsDevice graphicsDevice, UIRenderContext context, RenderTarget2D target)
{
    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.PushClip(new Rectangle(0, 0, target.Width, target.Height));
    context.Fill(new Rectangle(4, 4, 20, 20), Color.White);
    context.PopClip();
    context.End();
    graphicsDevice.SetRenderTarget(null);
}

static void DrawClippedFrame(GraphicsDevice graphicsDevice, UIRenderContext context, RenderTarget2D target, TextLayout layout, Vector2 position, Rectangle clip, Color color)
{
    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.PushClip(clip);
    context.Text(layout, position, color);
    context.PopClip();
    context.End();
    graphicsDevice.SetRenderTarget(null);
}

static Color[] ReadPixels(RenderTarget2D target)
{
    var pixels = new Color[target.Width * target.Height];
    target.GetData(pixels);
    return pixels;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}