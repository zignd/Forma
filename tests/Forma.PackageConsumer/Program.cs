// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;
using Forma.PackageConsumer;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using var context = new UIContext();
var foundation = new Border
{
    Background = new RadialGradientBrush
    {
        GradientStops = new[] { new GradientStop(0, Color.White), new GradientStop(1, Color.Transparent) },
    },
    CornerRadius = new CornerRadius(4),
};
var foundationalShape = new PathShape
{
    Data = new PathGeometry(DrawingPath.Parse("M0 0 H24 V16 H0 Z")),
    Fill = new ConicGradientBrush
    {
        GradientStops = new[] { new GradientStop(0, Color.Red), new GradientStop(1, Color.Blue) },
    },
    Stroke = new SolidColorBrush(Color.White),
    StrokeThickness = 2,
    StrokeDashArray = new[] { 3f, 2f },
    GeometryTransform = new TransformGroup(),
};
((TransformGroup)foundationalShape.GeometryTransform).Children.Add(new TranslateTransform { X = 2, Y = 3 });
foundation.AddChild(foundationalShape);
var foundationDrawing = new DrawingImage
{
    IntrinsicSize = new Vector2(24, 16),
    Drawing = new DrawingGroup
    {
        Effect = new EffectGroup(),
        Children =
        {
            new GeometryDrawing
            {
                Geometry = new RectangleGeometry { CornerRadius = new CornerRadius(2) },
                Fill = new LinearGradientBrush
                {
                    GradientStops = new[] { new GradientStop(0, Color.Lime), new GradientStop(1, Color.Blue) },
                },
            },
        },
    },
};
((EffectGroup)foundationDrawing.Drawing.Effect).Add(new ColorMatrixEffect());
var foundationText = new TextBlock { Text = "foundation", FontSize = 18, FontWeight = UIFontWeight.SemiBold, LetterSpacing = 1 };
foundationText.Inlines.Add(new Run("retained"));
if (!foundationalShape.ContainsPoint(new Point(12, 8)) || foundationDrawing.IntrinsicSize != new Vector2(24, 16) || foundationText.Inlines.Count != 1)
    throw new InvalidOperationException("Foundational package vocabulary did not survive trimming.");
var compiledModel = new ConsumerViewModel { Message = "Compiled package view" };
var compiledView = new ConsumerView { DataContext = compiledModel };
var compiledScope = NameScope.GetNameScope(compiledView) ?? throw new InvalidOperationException("Compiled package view has no namescope.");
var compiledLabel = compiledScope.Find<Label>("Message");
var compiledEditor = compiledScope.Find<LineEdit>("Editor");
var staticTarget = compiledScope.Find<ConsumerTarget>("StaticTarget");
var dynamicTarget = compiledScope.Find<ConsumerTarget>("DynamicTarget");
var styleTarget = compiledScope.Find<ConsumerTarget>("StyleTarget");
var packageGrid = compiledScope.Find<DataGrid>("PackageGrid");
var packageTreeGrid = compiledScope.Find<DataGrid>("PackageTreeGrid");
if (compiledLabel.Text != compiledModel.Message || compiledEditor.Text != compiledModel.Message) throw new InvalidOperationException("Packed typed bindings did not initialize.");
if (staticTarget.Value.Name != "Static" || dynamicTarget.Value.Name != "Dynamic") throw new InvalidOperationException("Packed resource references did not initialize.");
if (!compiledView.Resources.ContainsKey("LocalPalette") || !compiledView.Resources.TryFind("MergedPalette", out _)) throw new InvalidOperationException("Packed local or merged resources were not populated.");
var winner = (ResourceDictionary)compiledView.Resources["Winner"];
if (!winner.ContainsKey("LocalMarker") || winner.ContainsKey("MergedMarker")) throw new InvalidOperationException("Packed merged-resource precedence was not preserved.");
if (styleTarget.TooltipText != "Styled" || styleTarget.Value.Name != "Static") throw new InvalidOperationException("Packed selector style did not apply.");
compiledModel.Message = "One-way update";
if (compiledLabel.Text != compiledModel.Message) throw new InvalidOperationException("Packed one-way binding did not update.");
compiledEditor.Text = "Two-way update";
if (compiledModel.Message != compiledEditor.Text) throw new InvalidOperationException("Packed two-way binding did not update.");
context.Add(compiledView);
context.Layout();
packageGrid.ActivateColumnHeader(1);
packageGrid.SelectCell(new CellIndex(packageGrid.GetRowPath(0), 0));
var packageTreeRoot = packageTreeGrid.GetRowPath(0);
compiledModel.TreeRows[0].Children.Add(new ConsumerTreeRow { Name = "Observable child" });
context.Layout();
if (packageGrid.Columns.Count != 2 || packageGrid.Columns[0].CellTemplate == null || packageGrid.SortDescriptions.Count != 1 || packageGrid.SelectedCells.Count != 1)
    throw new InvalidOperationException("Packed flat DataGrid columns, templates, sorting, or selection failed.");
if (!packageTreeGrid.HierarchySource.IsExpanded(packageTreeRoot) || packageTreeGrid.HierarchySource.IndexOfPath(new IndexPath(0, 1)) < 0 || packageTreeGrid.RealizedCount > 16)
    throw new InvalidOperationException("Packed hierarchical DataGrid expansion, observable deltas, or virtualization failed.");
if (compiledView.AttachedHandlerCalls != 1 || styleTarget.CustomMinimumSize != new Vector2(2, 3)) throw new InvalidOperationException("Packed event hookup or event trigger did not run.");
context.Update(new GameTime(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)));
if (styleTarget.CustomMinimumSize != new Vector2(8, 9)) throw new InvalidOperationException("Packed event storyboard did not advance.");
compiledModel.IsActive = true;
if (dynamicTarget.CustomMinimumSize != new Vector2(1, 2)) throw new InvalidOperationException("Packed property trigger did not start.");
context.Update(new GameTime(TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(50)));
if (dynamicTarget.CustomMinimumSize != new Vector2(1, 2)) throw new InvalidOperationException("Packed storyboard repeat behavior was not preserved.");
dynamicTarget.RaiseStopRequested();
if (dynamicTarget.CustomMinimumSize != Vector2.Zero) throw new InvalidOperationException("Packed StopStoryboard did not restore its target.");
compiledModel.IsActive = false;
compiledView.Resources["DynamicValue"] = new ConsumerResourceValue { Name = "Replaced" };
if (dynamicTarget.Value.Name != "Replaced") throw new InvalidOperationException("Packed dynamic resource did not observe replacement.");
context.Remove(compiledView);
if (styleTarget.CustomMinimumSize != Vector2.Zero || dynamicTarget.StopRequestedSubscriberCount != 0) throw new InvalidOperationException("Packed clocks or event triggers remained attached.");
if (styleTarget.TooltipText != "Underlying" || styleTarget.Value.Name != "Underlying" || dynamicTarget.Value.Name != "Underlying") throw new InvalidOperationException("Packed style or resource values were not restored.");
var detachedText = compiledLabel.Text;
compiledModel.Message = "After detach";
compiledModel.IsActive = true;
if (compiledLabel.Text != detachedText || dynamicTarget.CustomMinimumSize != Vector2.Zero) throw new InvalidOperationException("Packed binding or property trigger remained attached.");
compiledEditor.Text = "Detached edit";
if (compiledModel.Message == compiledEditor.Text) throw new InvalidOperationException("Packed two-way binding remained attached.");
var inheritedThemeStyle = new StyleBoxEmpty();
context.Theme.SetStyleBox("consumer", inheritedThemeStyle, nameof(BaseButton));
var derivedButton = new ConsumerButton();
context.Add(derivedButton);
if (!ReferenceEquals(derivedButton.GetThemeStyleBox("consumer"), inheritedThemeStyle)) throw new InvalidOperationException("Packed theme inheritance failed.");
var tooltipProperty = new XamlProperty<string>(
    nameof(Control.TooltipText),
    target => ((Control)target).TooltipText,
    (target, value) => ((Control)target).TooltipText = value);
var inheritedTypeStyle = new Style(nameof(BaseButton));
inheritedTypeStyle.Setters.Add(new StyleSetter<string>(tooltipProperty, "Inherited type style"));
using var styleAttachment = StyleEngine.Attach(derivedButton, new[] { inheritedTypeStyle });
if (derivedButton.TooltipText != "Inherited type style") throw new InvalidOperationException("Packed type selector inheritance failed.");
var root = new VBoxContainer
{
    Size = new Vector2(320, 180),
};
root.AddChild(new Label { Text = "Forma package consumer" });
root.AddChild(new Button { Text = "Continue" });
context.Add(root);
context.Layout();
context.Update(new GameTime(), new MouseState(), new KeyboardState());
var spriteFontDrawSucceeded = true;
var dynamicTextDiagnostics = string.Empty;
#if FORMA_SPRITEFONT_DRAW
using (var game = new Game())
{
#if FORMA_MONOGAME
    ContentTypeReaderManager.AddTypeCreator(
        "Microsoft.Xna.Framework.Content.ListReader`1[[System.Char, System.Private.CoreLib, Version=8.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]",
        () => new ListReader<char>());
#endif
    _ = new GraphicsDeviceManager(game) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 180 };
    ((IGraphicsDeviceManager)game.Services.GetService(typeof(IGraphicsDeviceManager))).CreateDevice();
    using var content = new ContentManager(game.Services, Path.Combine(AppContext.BaseDirectory, "Content"));
    var spriteFont = content.Load<SpriteFont>("Fonts/Catalog");
    using var drawContext = new UIContext();
    drawContext.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.RuntimeSvg;
    var drawRoot = new VBoxContainer { Size = new Vector2(320, 180) };
    drawRoot.AddChild(new Label { Font = spriteFont, Text = "Packed SpriteFont" });
    drawRoot.AddChild(new Button { Font = spriteFont, Text = "Continue" });
    var packagedOption = new OptionButton { Font = spriteFont };
    packagedOption.AddItem("One");
    packagedOption.AddItem("Two");
    packagedOption.Select(0);
    drawRoot.AddChild(packagedOption);
#if FORMA_DYNAMIC_TEXT
    drawRoot.AddChild(new Label { Text = "Automatic dynamic default" });
    using var latinFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
    using var arabicFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/NotoSansArabic_Variable.ttf");
    using var devanagariFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/NotoSansDevanagari_Subset.ttf");
    using var hebrewFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/NotoSansHebrew_Subset.ttf");
    var multilingualFont = new DynamicUIFont(latinFace, 18, UIFontHinting.Default, arabicFace, devanagariFace, hebrewFace);
    var corpus = new[]
    {
        (Name: "latin", Text: "Forma", Locale: "en"),
        (Name: "arabic", Text: "مرحبا", Locale: "ar"),
        (Name: "indic", Text: "क्ष", Locale: "hi"),
        (Name: "bidi", Text: "abc שלום 123", Locale: "he"),
        (Name: "fallback", Text: "Forma مرحبا क्ष", Locale: "ar"),
        (Name: "missing", Text: "\u0378", Locale: "en"),
    };
    var diagnostics = new StringBuilder();
    foreach (var item in corpus)
    {
        var layout = new TextLayoutEngine().Layout(multilingualFont, item.Text, new TextLayoutOptions(locale: item.Locale));
        if (layout.Runs.Count == 0 || layout.Runs.SelectMany(run => run.Glyphs).Any() == false) return 1;
        if (item.Name == "missing" && layout.Runs.SelectMany(run => run.Glyphs).Single().GlyphId != 0) return 1;
        drawRoot.AddChild(new Label { Text = item.Text, UIFont = multilingualFont });
        diagnostics.Append(item.Name).Append('|')
            .Append(layout.Size.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
            .Append(layout.Size.Y.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        foreach (var run in layout.Runs)
        {
            diagnostics.Append(run.Start).Append(':').Append(run.Length).Append(':').Append((int)run.Direction).Append(':');
            foreach (var glyph in run.Glyphs) diagnostics.Append(glyph.GlyphId).Append('@').Append(glyph.Utf16Cluster).Append(',');
            diagnostics.Append(';');
        }
        diagnostics.AppendLine();
    }
#endif
    drawContext.Add(drawRoot);
    drawContext.Layout();
    using var target = new RenderTarget2D(game.GraphicsDevice, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
    game.GraphicsDevice.SetRenderTarget(target);
    game.GraphicsDevice.Clear(Color.Transparent);
    drawContext.Draw(game.GraphicsDevice);
    game.GraphicsDevice.SetRenderTarget(null);
    var pixels = new Color[target.Width * target.Height];
    target.GetData(pixels);
    spriteFontDrawSucceeded = pixels.Any(pixel => pixel != Color.Transparent);
#if FORMA_SVG
    spriteFontDrawSucceeded &= drawContext.ThemeIconDiagnostics.RuntimeSvgIconCount > 0;
#else
    spriteFontDrawSucceeded &= drawContext.ThemeIconDiagnostics.RuntimeSvgIconCount == 0 && drawContext.ThemeIconDiagnostics.AtlasCount > 0;
#endif
#if FORMA_DYNAMIC_TEXT
    spriteFontDrawSucceeded &= drawContext.DynamicGlyphDiagnostics.Misses > 0;
    var pixelBytes = new byte[pixels.Length * sizeof(uint)];
    for (var index = 0; index < pixels.Length; index++)
    {
        var packed = pixels[index].PackedValue;
        BitConverter.TryWriteBytes(pixelBytes.AsSpan(index * sizeof(uint), sizeof(uint)), packed);
    }
    diagnostics.Append("render|").Append(Convert.ToHexString(SHA256.HashData(pixelBytes))).Append('|')
        .Append(drawContext.DynamicGlyphDiagnostics.GlyphCount).Append('|')
        .Append(drawContext.DynamicGlyphDiagnostics.Misses).AppendLine();
    dynamicTextDiagnostics = diagnostics.ToString();
#endif
}
#endif
#if FORMA_DYNAMIC_TEXT
#if FORMA_DYNAMIC_TEXT_DEFAULT
if (context.Theme.FontFamily?.Primary is not DynamicUIFont) throw new InvalidOperationException("Dynamic text was not installed as the default font.");
DynamicTextDefaults.Install(Path.Combine(AppContext.BaseDirectory, "Fonts", "Inter_Regular.ttf"), 18);
using (var replacementContext = new UIContext())
    if (replacementContext.Theme.FontFamily?.Primary is not DynamicUIFont replacement || replacement.Size != 18) throw new InvalidOperationException("The replacement dynamic text default was not installed.");
#else
if (context.Theme.FontFamily is not null) throw new InvalidOperationException("Dynamic text defaults were not disabled.");
#endif
using var face = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
var dynamicLayout = new TextLayoutEngine().Layout(new DynamicUIFont(face, 18), "Forma");
if (dynamicLayout.Runs.Count == 0 || dynamicLayout.Runs.SelectMany(run => run.Glyphs).Any() == false) throw new InvalidOperationException("Dynamic text shaping produced no glyphs.");
if (face.RasterizeCharacter('A', 18).Pixels.Length == 0) throw new InvalidOperationException("Dynamic text rasterization produced no pixels.");
if (typeof(DynamicUIFont).Assembly.GetName().Name != "Forma.DynamicText") throw new InvalidOperationException("DynamicUIFont was not loaded from Forma.DynamicText.");
var nativeText = DynamicTextNativeDiagnostics.Current;
if (string.IsNullOrWhiteSpace(nativeText.RuntimeIdentifier) ||
    nativeText.FreeTypeLibraryName != "freetype" ||
    nativeText.FreeTypePackageId != "FreeTypeSharp" ||
    nativeText.HarfBuzzLibraryName != "libHarfBuzzSharp" ||
    nativeText.HarfBuzzPackageId != "HarfBuzzSharp.NativeAssets" ||
    nativeText.UsesRuntimeGeneratedMarshalling ||
    nativeText.RegistersUnmanagedCallbacks) throw new InvalidOperationException("Dynamic text native diagnostics did not match the packaged backend contract.");
var outputDirectory = Path.GetFullPath(AppContext.BaseDirectory);
var packagedModules = NativeModuleInspector.GetLoadedModulePaths()
    .Select(Path.GetFullPath)
    .Where(fileName => fileName.StartsWith(outputDirectory, StringComparison.Ordinal))
    .Select(fileName => Path.GetFileName(fileName) ?? string.Empty)
    .ToArray();
var freeTypeModuleName = OperatingSystem.IsWindows() ? "freetype.dll" : OperatingSystem.IsLinux() ? "libfreetype.so" : "libfreetype.dylib";
var harfBuzzModuleName = OperatingSystem.IsWindows() ? "libHarfBuzzSharp.dll" : OperatingSystem.IsLinux() ? "libHarfBuzzSharp.so" : "libHarfBuzzSharp.dylib";
if (packagedModules.Count(fileName => string.Equals(fileName, freeTypeModuleName, StringComparison.OrdinalIgnoreCase)) != 1 ||
    packagedModules.Count(fileName => string.Equals(fileName, harfBuzzModuleName, StringComparison.OrdinalIgnoreCase)) != 1)
    throw new InvalidOperationException($"Expected one packaged FreeType and HarfBuzz module; loaded: {string.Join(", ", packagedModules)}");
var diagnosticsPath = Environment.GetEnvironmentVariable("FORMA_DYNAMIC_TEXT_DIAGNOSTICS");
if (!string.IsNullOrWhiteSpace(diagnosticsPath)) File.WriteAllText(diagnosticsPath, dynamicTextDiagnostics);
#else
if (context.Theme.FontFamily is not null) return 1;
#endif
#if FORMA_SVG
var svgHealth = SvgSkiaBackendDefaults.Verify();
if (!svgHealth.IsAvailable || svgHealth.Name != "Svg.Skia" || !svgHealth.Version.StartsWith("5.2.0", StringComparison.Ordinal))
    throw new InvalidOperationException($"SVG backend health did not match the packaged contract: {svgHealth.Diagnostic}");
if (typeof(SvgSkiaBackendDefaults).Assembly.GetName().Name != "Forma.Svg.Skia")
    throw new InvalidOperationException("SVG backend was not loaded from Forma.Svg.Skia.");
var svgOutputDirectory = Path.GetFullPath(AppContext.BaseDirectory);
var svgModules = NativeModuleInspector.GetLoadedModulePaths()
    .Select(Path.GetFullPath)
    .Where(fileName => fileName.StartsWith(svgOutputDirectory, StringComparison.Ordinal))
    .Select(fileName => Path.GetFileName(fileName) ?? string.Empty)
    .ToArray();
var skiaModuleName = OperatingSystem.IsWindows() ? "libSkiaSharp.dll" : OperatingSystem.IsLinux() ? "libSkiaSharp.so" : "libSkiaSharp.dylib";
if (svgModules.Count(fileName => string.Equals(fileName, skiaModuleName, StringComparison.OrdinalIgnoreCase)) != 1)
    throw new InvalidOperationException($"Expected one packaged SkiaSharp module; loaded: {string.Join(", ", svgModules)}");
#else
if (SvgRuntime.Health.IsRegistered || !SvgRuntime.Health.Diagnostic.Contains("runtime-matched Forma SVG backend package", StringComparison.Ordinal))
    throw new InvalidOperationException("Core-only consumer did not report the actionable missing SVG backend setup diagnostic.");
#endif
#if !FORMA_CORE_ONLY
using var video = new VideoStreamPlayer();
if (typeof(VideoStreamPlayer).Assembly.GetName().Name != "Forma.Media") return 1;
if (VideoStreamPlayer.RuntimeCapabilities == VideoPlaybackCapabilities.None) return 1;
if (VideoStreamPlayer.RuntimeCapabilities.HasFlag(VideoPlaybackCapabilities.Seeking)) return 1;
if (video.GetStreamPosition() != 0 || video.IsPlaying()) return 1;
video.SetStreamPosition(12);
Action<UIContext, GraphicsDevice> drawingSurface = (drawingContext, graphicsDevice) => drawingContext.Draw(graphicsDevice);
_ = drawingSurface;

return context.Roots.Count == 2 && spriteFontDrawSucceeded ? 0 : 1;
#else
return context.Roots.Count == 2 && spriteFontDrawSucceeded ? 0 : 1;
#endif