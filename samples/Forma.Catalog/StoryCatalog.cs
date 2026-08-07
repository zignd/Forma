// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Forma;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

public sealed class ComponentStory
{
    public ComponentStory(string category, string name, string description, Func<Control> factory, Action<Control> attached = null, string xamlPath = null)
    {
        Category = category;
        Name = name;
        Description = description;
        Factory = factory;
        Attached = attached;
        XamlPath = xamlPath;
    }

    public string Category { get; }
    public string Name { get; }
    public string Description { get; }
    public Func<Control> Factory { get; }
    public Action<Control> Attached { get; }
    public string XamlPath { get; }
}

public static class StoryCatalog
{
    public static IReadOnlyList<ComponentStory> Create(Texture2D texture, Action<float> setDisplayScale = null, Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont = null)
    {
        var stories = new List<ComponentStory>();
        var controlType = typeof(Control);
        var xamlRootTypes = new Dictionary<Type, Type>
        {
            [typeof(BoxContainer)] = typeof(CatalogBoxContainerStoryRoot),
            [typeof(Slider)] = typeof(CatalogSliderStoryRoot),
            [typeof(ScrollBar)] = typeof(CatalogScrollBarStoryRoot),
            [typeof(SplitContainer)] = typeof(CatalogSplitContainerStoryRoot),
            [typeof(FlowContainer)] = typeof(CatalogFlowContainerStoryRoot),
        };
        foreach (var type in new[] { controlType.Assembly, typeof(VideoStreamPlayer).Assembly }
                     .SelectMany(assembly => assembly.GetTypes())
                     .Where(type => type.IsPublic && !type.IsAbstract && controlType.IsAssignableFrom(type))
                     .Where(type => type.GetConstructor(Type.EmptyTypes) != null || xamlRootTypes.ContainsKey(type))
                     .OrderBy(type => GetCategory(type))
                     .ThenBy(type => type.Name))
        {
            stories.Add(new ComponentStory(
                GetCategory(type),
                type.Name,
                type == typeof(VideoStreamPlayer)
                    ? "Optional [color=#30b9a4]Forma.Media[/color] video control configured for autoplay, looping, and responsive expansion. Assign a content-loaded Video to begin playback."
                    : $"Interactive example of [color=#30b9a4]{type.FullName}[/color]. Use the property inspector to change its public writable values at runtime.",
                () => (Control)FormaXamlLoader.Load(
                    xamlRootTypes.TryGetValue(type, out var xamlRootType) ? xamlRootType : type),
                root => AttachExample(root, texture),
                $"Stories/Controls/{type.Name}.xaml"));
        }
        stories.Add(new ComponentStory(
            "XAML",
            "Selector Styles",
            "Compare type, class, name, and hover selectors while the style engine resolves specificity and restores underlying values.",
            () => new StylesStoryView(),
            xamlPath: "StylesStoryView.xaml"));
        stories.Add(new ComponentStory(
            "XAML",
            "Template Systems",
            "Compare seven structural control redesigns authored with compiled XAML templates, styles, and resources without custom drawing.",
            () => new TemplateGalleryStoryView(),
            xamlPath: "TemplateGalleryStoryView.xaml"));
        stories.Add(new ComponentStory(
            "XAML",
            "Composition Systems",
            "Inspect compiled visual primitives, responsive flex and grid layouts, template-part selectors, relative bindings, and live theme-template replacement.",
            () => new CompositionSystemsStoryView(),
            xamlPath: "CompositionSystemsStoryView.xaml"));
        stories.Add(new ComponentStory(
            "Collections",
            "Collection Systems",
            "Exercise observable deltas, eventful rows, selection modes, and bounded vertical, horizontal, and grid virtualization over 10,000 items.",
            () => new CollectionSystemsStoryView(),
            xamlPath: "CollectionSystemsStoryView.xaml"));
        stories.Add(new ComponentStory(
            "XAML",
            "Storyboards and Triggers",
            "Run color and size timelines from an event trigger, then toggle a repeating storyboard through a typed property trigger.",
            () => new AnimationsStoryView(),
            xamlPath: "AnimationsStoryView.xaml"));
        stories.Add(new ComponentStory(
            "XAML",
            "Compiled Data Binding",
            "Edit a typed view model through two-way controls and watch one-way labels and progress update through [color=#30b9a4]INotifyPropertyChanged[/color].",
            () => new DataBindingStoryView(),
            xamlPath: "DataBindingStoryView.xaml"));
        stories.Add(new ComponentStory(
            "Collections",
            "Flat Data Grid",
            "Sort typed columns, filter 5,000 rows, and exercise cell ranges while viewport virtualization keeps realization bounded.",
            () => new FlatDataGridStoryView(),
            xamlPath: "FlatDataGridStoryView.xaml"));
        stories.Add(new ComponentStory(
            "Collections",
            "Hierarchical Data Grid",
            "Expand and collapse observable branches, insert live children, sort columns, and select rows across a 10,000-item tree.",
            () => new HierarchicalDataGridStoryView(),
            xamlPath: "HierarchicalDataGridStoryView.xaml"));
        stories.Add(CreateIconInventoryStory());
        stories.Add(CreateIconCustomizationStory(texture));
        stories.Add(CreateIconDiagnosticsStory(setDisplayScale));
        stories.Add(CreateRuntimeSvgStory(setDisplayScale));
        stories.Add(CreateDynamicSizesStory(createDynamicFont));
        stories.Add(CreateLetterSpacingStory(createDynamicFont));
        stories.Add(CreateDisplayDensityStory(setDisplayScale, createDynamicFont));
        stories.Add(CreateFallbackChainStory(createDynamicFont));
        stories.Add(CreateShapingFeaturesStory(createDynamicFont));
        stories.Add(CreateBidirectionalStory(createDynamicFont));
        stories.Add(CreateWrappingSelectionStory(createDynamicFont));
        stories.Add(CreateSpriteFontCompatibilityStory(createDynamicFont));
        stories.Add(CreateAtlasInspectorStory(createDynamicFont));
        stories.Add(CreateFailureStatesStory(createDynamicFont));
        return stories;
    }

    private static ComponentStory CreateDynamicSizesStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Dynamic Sizes",
        "Edit live text, switch runtime font families, and inspect arbitrary logical sizes without separate SpriteFont assets.",
        () => new DynamicSizesStoryView(),
        root =>
        {
            var controls = root.Children[0];
            var input = (LineEdit)controls.Children[0];
            var family = (OptionButton)controls.Children[1];
            var size = (Slider)controls.Children[2];
            var status = (Label)root.Children[1];
            var preview = (Label)root.Children[2];
            var smallPreview = (Label)root.Children[3];
            void Refresh()
            {
                preview.Text = input.Text;
                smallPreview.Text = input.Text;
                ((CatalogStoryViewModel)root.DataContext).Status = $"{size.Value:0} px logical size";
                if (createDynamicFont == null) return;
                var familyName = family.Selected == 1 ? "Noto Sans SC" : "Inter";
                preview.UIFont = createDynamicFont(familyName, size.Value, null);
                smallPreview.UIFont = createDynamicFont(familyName, 12, null);
            }
            input.TextChanged += (_, _) => Refresh();
            family.ItemSelected += (_, _) => Refresh();
            size.ValueChanged += (_, _) => Refresh();
            Refresh();
        },
        "DynamicSizesStoryView.xaml");

    private static ComponentStory CreateLetterSpacingStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Letter Spacing",
        "Edit the sample and compare natural shaping with adjustable tracking from -1 to 2 logical pixels.",
        () => new LetterSpacingStoryView(),
        root =>
        {
            var scope = NameScope.GetNameScope(root);
            var input = scope.Find<LineEdit>("letterSpacingText");
            var spacing = scope.Find<Slider>("letterSpacing");
            var reset = scope.Find<Button>("resetLetterSpacing");
            var status = scope.Find<Label>("letterSpacingStatus");
            var natural = scope.Find<RichTextLabel>("naturalSpacingPreview");
            var tracked = scope.Find<RichTextLabel>("trackedSpacingPreview");
            if (createDynamicFont != null)
            {
                natural.UIFont = createDynamicFont("Inter", 28, null);
                tracked.UIFont = createDynamicFont("Inter", 28, null);
            }
            void Refresh()
            {
                natural.Text = input.Text;
                tracked.Text = input.Text;
                tracked.LetterSpacing = spacing.Value;
                var naturalWidth = MeasureUnwrappedWidth(natural);
                var trackedWidth = MeasureUnwrappedWidth(tracked);
                status.Text = string.Create(CultureInfo.InvariantCulture,
                    $"Tracking {spacing.Value:+0.00;-0.00;0.00} px   Delta {trackedWidth - naturalWidth:+0.00;-0.00;0.00} px\nNatural {naturalWidth:0.00} px   Tracked {trackedWidth:0.00} px");
            }
            static float MeasureUnwrappedWidth(RichTextLabel preview)
            {
                var autowrapMode = preview.AutowrapMode;
                preview.AutowrapMode = LabelAutowrapMode.Off;
                var width = preview.GetMinimumSize().X;
                preview.AutowrapMode = autowrapMode;
                return width;
            }
            input.TextChanged += (_, _) => Refresh();
            spacing.ValueChanged += (_, _) => Refresh();
            reset.Pressed += (_, _) => spacing.Value = 0;
            Refresh();
        },
        "LetterSpacingStoryView.xaml");

    private static ComponentStory CreateDisplayDensityStory(Action<float> setDisplayScale, Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Display Density",
        "Compare stable 24 px logical bounds while switching physical rasterization between 1x, 1.5x, and 2x.",
        () => new DisplayDensityStoryView(),
        root =>
        {
            var buttons = root.Children[0];
            var status = (Label)root.Children[1];
            var samples = root.Children[2].Children.Cast<Label>().ToArray();
            if (createDynamicFont != null)
                foreach (var sample in samples) sample.UIFont = createDynamicFont("Inter", 24, null);
            void Select(float density)
            {
                (setDisplayScale ?? (value => root.Context.DisplayScale = value))(density);
                var widths = samples.Select(sample => sample.GetMinimumSize().X).ToArray();
                status.Text = $"{density:0.0}x active · logical widths {string.Join(" / ", widths.Select(width => width.ToString("0", CultureInfo.InvariantCulture)))}";
            }
            ((Button)buttons.Children[0]).Pressed += (_, _) => Select(1);
            ((Button)buttons.Children[1]).Pressed += (_, _) => Select(1.5f);
            ((Button)buttons.Children[2]).Pressed += (_, _) => Select(2);
            Select(1);
        },
        "DisplayDensityStoryView.xaml");

    private static ComponentStory CreateFallbackChainStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Fallback Chain",
        "Shape mixed scripts through the real fallback family and report the selected face for every retained run.",
        () => new FallbackChainStoryView(),
        root =>
        {
            var input = (LineEdit)root.Children[0];
            var preview = (Label)root.Children[1];
            var diagnostics = (Label)root.Children[2];
            void Refresh()
            {
                preview.Text = input.Text;
                if (createDynamicFont == null) { diagnostics.Text = "Runtime font diagnostics unavailable."; return; }
                var font = createDynamicFont("Inter", 24, null);
                preview.UIFont = font;
                var layout = root.Context.TextLayoutEngine.Layout(font, input.Text);
                diagnostics.Text = string.Join("  ·  ", layout.Runs.Select(run =>
                {
                    var face = run.Font is DynamicUIFont dynamicFont ? dynamicFont.Face.FamilyName : run.Font.Identity.Value;
                    return $"U+{char.ConvertToUtf32(input.Text, run.Start):X4} {face}";
                }));
            }
            input.TextChanged += (_, _) => Refresh();
            Refresh();
        },
        "FallbackChainStoryView.xaml");

    private static ComponentStory CreateShapingFeaturesStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Shaping and Features",
        "Toggle standard ligatures and kerning, then vary the Noto Arabic weight axis through real HarfBuzz layouts.",
        () => new ShapingFeaturesStoryView(),
        root =>
        {
            var controls = root.Children[0];
            var ligatures = (CheckBox)controls.Children[0];
            var kerning = (CheckBox)controls.Children[1];
            var weight = (Slider)controls.Children[2];
            var latin = (Label)root.Children[1];
            var arabic = (Label)root.Children[2];
            var diagnostics = (Label)root.Children[3];
            void Refresh()
            {
                if (createDynamicFont == null) { diagnostics.Text = "Runtime shaping diagnostics unavailable."; return; }
                var features = new[] { new UIFontOpenTypeFeature("liga", ligatures.ButtonPressed ? 1u : 0u), new UIFontOpenTypeFeature("kern", kerning.ButtonPressed ? 1u : 0u) };
                var latinFont = createDynamicFont("Noto Sans SC", 30, null);
                var arabicFont = createDynamicFont("Noto Sans Arabic", 30, new[] { new UIFontVariationCoordinate("wght", weight.Value) });
                latin.UIFont = latinFont;
                latin.SetOpenTypeFeatures(features);
                arabic.UIFont = arabicFont;
                arabic.SetOpenTypeFeatures(features);
                var latinLayout = root.Context.TextLayoutEngine.Layout(latinFont, latin.Text, new TextLayoutOptions(openTypeFeatures: features));
                var arabicLayout = root.Context.TextLayoutEngine.Layout(arabicFont, arabic.Text, new TextLayoutOptions(direction: TextDirection.RightToLeft, locale: "ar", openTypeFeatures: features));
                diagnostics.Text = $"Latin: {latinLayout.VisibleGlyphs.Count} glyphs, {latinLayout.Size.X:0.0} px · Arabic: {arabicLayout.VisibleGlyphs.Count} glyphs, {arabicLayout.Size.X:0.0} px · wght {weight.Value:0}";
            }
            ligatures.Toggled += (_, _) => Refresh();
            kerning.Toggled += (_, _) => Refresh();
            weight.ValueChanged += (_, _) => Refresh();
            Refresh();
        },
        "ShapingFeaturesStoryView.xaml");

    private static ComponentStory CreateBidirectionalStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Bidirectional Text",
        "Edit mixed Hebrew, Arabic, numbers, and Latin text; force paragraph direction and inspect logical versus visual run order.",
        () => new BidirectionalStoryView(),
        root =>
        {
            var controls = root.Children[0];
            var input = (LineEdit)controls.Children[0];
            var direction = (OptionButton)controls.Children[1];
            var preview = (Label)root.Children[1];
            var diagnostics = (Label)root.Children[2];
            void Refresh()
            {
                preview.Text = input.Text;
                preview.TextDirection = direction.Selected switch { 1 => TextDirection.LeftToRight, 2 => TextDirection.RightToLeft, _ => TextDirection.Auto };
                if (createDynamicFont == null) { diagnostics.Text = "Runtime bidi diagnostics unavailable."; return; }
                var font = createDynamicFont("Inter", 26, null);
                preview.UIFont = font;
                var layout = root.Context.TextLayoutEngine.Layout(font, input.Text, new TextLayoutOptions(direction: preview.TextDirection));
                string Describe(TextLayoutRun run) => $"{run.Start}:{run.Direction}:L{run.BidiLevel}";
                diagnostics.Text = $"Logical  {string.Join("  ·  ", layout.Runs.OrderBy(run => run.Start).Select(Describe))}\nVisual   {string.Join("  ·  ", layout.Runs.Select(Describe))}";
            }
            input.TextChanged += (_, _) => Refresh();
            direction.ItemSelected += (_, _) => Refresh();
            Refresh();
        },
        "BidirectionalStoryView.xaml");

    private static ComponentStory CreateWrappingSelectionStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Wrapping and Selection",
        "Edit multilingual paragraphs, select with the mouse, and inspect caret, range, wrapping, ellipsis, and visual movement on one retained layout.",
        () => new WrappingSelectionStoryView(),
        root =>
        {
            var editor = (TextEdit)root.Children[0];
            var layoutControls = root.Children[1];
            var width = (Slider)layoutControls.Children[0];
            var wrapping = (OptionButton)layoutControls.Children[1];
            var ellipsis = (CheckBox)layoutControls.Children[2];
            var direction = (OptionButton)layoutControls.Children[3];
            var movementControls = root.Children[2];
            var movement = (OptionButton)movementControls.Children[0];
            var previous = (Button)movementControls.Children[1];
            var next = (Button)movementControls.Children[2];
            var inspect = (Button)movementControls.Children[3];
            var preview = (Label)root.Children[3];
            var diagnostics = (Label)root.Children[4];
            var selectionOverlays = preview.Children.Where(child => child.Name.StartsWith("selectionOverlay", StringComparison.Ordinal)).Cast<ColorRect>().ToArray();
            var caretOverlay = (ColorRect)preview.Children.Single(child => child.Name == "caretOverlay");
            TextLayout retainedLayout = null;
            var caret = 0;

            void Refresh()
            {
                preview.Text = editor.Text;
                var layoutWidth = (float)width.Value;
                preview.CustomMinimumSize = new Vector2(layoutWidth, 190);
                preview.CustomMaximumSize = new Vector2(layoutWidth, 190);
                preview.AutowrapMode = wrapping.Selected switch { 1 => LabelAutowrapMode.Arbitrary, 2 => LabelAutowrapMode.Word, _ => LabelAutowrapMode.Off };
                preview.TextOverrunBehavior = ellipsis.ButtonPressed ? LabelTextOverrunBehavior.WordEllipsis : LabelTextOverrunBehavior.NoTrimming;
                preview.TextDirection = direction.Selected switch { 1 => TextDirection.LeftToRight, 2 => TextDirection.RightToLeft, _ => TextDirection.Auto };
                if (createDynamicFont == null) { diagnostics.Text = "Runtime selection diagnostics unavailable."; return; }
                var font = createDynamicFont("Inter", 22, null);
                preview.UIFont = font;
                retainedLayout = root.Context.TextLayoutEngine.Layout(font, editor.Text, new TextLayoutOptions(
                    layoutWidth - preview.Padding.Horizontal,
                    wrapping.Selected switch { 1 => TextWrapping.Character, 2 => TextWrapping.Word, _ => TextWrapping.NoWrap },
                    direction: preview.TextDirection,
                    trimming: ellipsis.ButtonPressed ? TextTrimming.WordEllipsis : TextTrimming.None));
                caret = editor.HasSelection ? editor.SelectionTo : Math.Clamp(caret, 0, editor.Text.Length);
                var selectionStart = editor.HasSelection ? editor.SelectionFrom : caret;
                var selectionLength = editor.HasSelection ? editor.SelectionTo - editor.SelectionFrom : 0;
                var rectangles = retainedLayout.GetSelectionRectangles(selectionStart, selectionLength);
                for (var index = 0; index < selectionOverlays.Length; index++)
                {
                    var overlay = selectionOverlays[index];
                    overlay.Visible = index < rectangles.Count;
                    if (!overlay.Visible) continue;
                    var rectangle = rectangles[index];
                    overlay.Position = new Vector2(preview.Padding.Left + rectangle.X, preview.Padding.Top + rectangle.Y);
                    overlay.Size = new Vector2(rectangle.Width, rectangle.Height);
                }
                var caretPosition = retainedLayout.GetCaretPosition(caret);
                var caretLine = retainedLayout.Lines.LastOrDefault(line => caret >= line.Start) ?? retainedLayout.Lines.FirstOrDefault();
                caretOverlay.Visible = caretLine != null;
                if (caretOverlay.Visible)
                {
                    caretOverlay.Position = new Vector2(preview.Padding.Left + caretPosition.X, preview.Padding.Top + caretPosition.Y);
                    caretOverlay.Size = new Vector2(2, caretLine.Size.Y);
                }
                var bounds = retainedLayout.GetRangeBounds(selectionStart, selectionLength);
                diagnostics.Text = $"Width {layoutWidth:0} · {retainedLayout.Lines.Count} lines · caret UTF-16 {caret} at {caretPosition.X:0.0},{caretPosition.Y:0.0} · selection {selectionStart}..{selectionStart + selectionLength} · range {bounds.X:0.0},{bounds.Y:0.0} {bounds.Width:0.0}×{bounds.Height:0.0}";
            }

            void MoveCaret(int delta)
            {
                if (retainedLayout == null) return;
                if (movement.Selected == 0)
                    caret = delta < 0 ? retainedLayout.GetPreviousGraphemeBoundary(caret) : retainedLayout.GetNextGraphemeBoundary(caret);
                else if (movement.Selected == 1)
                    caret = delta < 0 ? retainedLayout.GetPreviousWordBoundary(caret) : retainedLayout.GetNextWordBoundary(caret);
                else if (retainedLayout.VisualClusters.Count > 0)
                {
                    var logicalIndex = Math.Min(retainedLayout.GetClusterIndex(caret), retainedLayout.Clusters.Count - 1);
                    var visualIndex = retainedLayout.Clusters[logicalIndex].VisualIndex;
                    var target = retainedLayout.VisualClusters[Math.Clamp(visualIndex + delta, 0, retainedLayout.VisualClusters.Count - 1)];
                    caret = delta < 0 ? target.Start : target.Start + target.Length;
                }
                editor.Select(caret, caret);
                Refresh();
            }

            editor.TextChanged += (_, _) => { caret = Math.Min(caret, editor.Text.Length); Refresh(); };
            width.ValueChanged += (_, _) => Refresh();
            wrapping.ItemSelected += (_, _) => Refresh();
            ellipsis.Toggled += (_, _) => Refresh();
            direction.ItemSelected += (_, _) => Refresh();
            previous.Pressed += (_, _) => MoveCaret(-1);
            next.Pressed += (_, _) => MoveCaret(1);
            inspect.Pressed += (_, _) => Refresh();
            Refresh();
        },
        "WrappingSelectionStoryView.xaml");

    private static ComponentStory CreateSpriteFontCompatibilityStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "SpriteFont Compatibility",
        "Compare retained dynamic text with the native-free SpriteFontAdapter path. Metric differences are expected because the source fonts and rasterizers differ.",
        () => new SpriteFontCompatibilityStoryView(),
        root =>
        {
            var input = (LineEdit)root.Children[0];
            var dynamicPreview = (Label)root.Children[1];
            var spritePreview = (Label)root.Children[2];
            var diagnostics = (Label)root.Children[3];
            void Refresh()
            {
                dynamicPreview.Text = $"Dynamic  ·  {input.Text}";
                spritePreview.Text = $"SpriteFontAdapter  ·  {input.Text}";
                if (createDynamicFont == null || spritePreview.Font == null) { diagnostics.Text = "Compatibility diagnostics unavailable."; return; }
                var dynamicFont = createDynamicFont("Inter", 24, null);
                dynamicPreview.UIFont = dynamicFont;
                var dynamicLayout = root.Context.TextLayoutEngine.Layout(dynamicFont, input.Text);
                var compatibilityLayout = root.Context.TextLayoutEngine.Layout(new SpriteFontAdapter(spritePreview.Font), input.Text);
                diagnostics.Text = $"Dynamic {dynamicLayout.Size.X:0.0} × {dynamicLayout.Size.Y:0.0} logical px  ·  SpriteFont {compatibilityLayout.Size.X:0.0} × {compatibilityLayout.Size.Y:0.0} logical px  ·  metric differences intentional";
            }
            input.TextChanged += (_, _) => Refresh();
            Refresh();
        },
        "SpriteFontCompatibilityStoryView.xaml");

    private static ComponentStory CreateAtlasInspectorStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Atlas Inspector",
        "Inspect immutable page previews, occupancy, cache activity, uploads, evictions, and bounded memory while stressing or clearing the device cache.",
        () => new AtlasInspectorStoryView(),
        root =>
        {
            var controls = root.Children[0];
            var stress = (LineEdit)controls.Children[0];
            var refresh = (Button)controls.Children[1];
            var clear = (Button)controls.Children[2];
            var preview = (Label)root.Children[1];
            var status = (Label)root.Children[2];
            var views = root.Children[3].Children.Cast<DynamicGlyphAtlasView>().ToArray();
            if (createDynamicFont != null) preview.UIFont = createDynamicFont("Inter", 26, null);
            void Refresh()
            {
                preview.Text = stress.Text;
                var diagnostics = root.Context.DynamicGlyphDiagnostics;
                var pages = root.Context.GetDynamicGlyphAtlasPages();
                var occupancy = diagnostics.Capacity == 0 ? 0 : diagnostics.UsedArea * 100f / diagnostics.Capacity;
                var failure = diagnostics.Failures == 0 ? string.Empty : $" · Failures {diagnostics.Failures}: {diagnostics.LastFailure}";
                status.Text = $"Pages {diagnostics.PageCount} · Glyphs {diagnostics.GlyphCount} · Occupancy {occupancy:0.0}% · Hits {diagnostics.Hits} · Misses {diagnostics.Misses} · Uploads {diagnostics.Uploads} · Evictions {diagnostics.Evictions} · Memory {diagnostics.Bytes / 1024f:0.0} KB / {DynamicGlyphCacheOptions.MaximumBytes / 1048576} MB{failure}";
                for (var index = 0; index < views.Length; index++) views[index].Snapshot = index < pages.Count ? pages[index] : null;
            }
            stress.TextChanged += (_, _) => Refresh();
            refresh.Pressed += (_, _) => Refresh();
            clear.Pressed += (_, _) => { root.Context.ClearDynamicGlyphCache(); Refresh(); };
            Refresh();
        },
        "AtlasInspectorStoryView.xaml");

    private static ComponentStory CreateFailureStatesStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Failure States",
        "Run bounded font, fallback, and atlas failure probes. Expected failures remain visible diagnostics and never terminate the catalog.",
        () => new FailureStatesStoryView(),
        root =>
        {
            var run = (Button)root.Children[0];
            var missing = (Label)root.Children[1];
            var malformed = (Label)root.Children[2];
            var fallback = (Label)root.Children[3];
            var atlas = (Label)root.Children[4];
            void Probe()
            {
                try { using var _ = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/does-not-exist.ttf"); }
                catch (Exception error) { missing.Text = $"Missing face · {error.GetType().Name}: {error.Message}"; }
                try { using var _ = UIFontFace.FromMemory(new byte[12]); }
                catch (FontLoadException error) { malformed.Text = $"Malformed asset · {error.ErrorCode}: {error.Message}"; }
                if (createDynamicFont == null) fallback.Text = "Fallback exhaustion diagnostics unavailable.";
                else
                {
                    var layout = root.Context.TextLayoutEngine.Layout(createDynamicFont("Inter", 24, null), "\u0378");
                    var glyph = layout.Runs.SelectMany(item => item.Glyphs).Single();
                    fallback.Text = $"Unsupported U+0378 · final fallback glyph {glyph.GlyphId} (.notdef) · process continues";
                }
                var diagnostics = root.Context.DynamicGlyphDiagnostics;
                atlas.Text = diagnostics.Failures == 0
                    ? $"Atlas budget · ready ({DynamicGlyphCacheOptions.MaximumBytes / 1048576} MB hard limit); active-frame exhaustion is skipped and recorded"
                    : $"Atlas exhaustion · recovered after {diagnostics.Failures} failure(s): {diagnostics.LastFailure}";
            }
            run.Pressed += (_, _) => Probe();
            Probe();
        },
        "FailureStatesStoryView.xaml");

    private static ComponentStory CreateIconInventoryStory() => new(
        "Theme icons",
        "Complete icon inventory",
        "Every imported logical icon, resolved through a real control binding from the embedded manifest.",
        () => new IconInventoryStoryView(),
        PopulateIconInventory,
        "IconInventoryStoryView.xaml");

    private static ComponentStory CreateRuntimeSvgStory(Action<float> setDisplayScale) => new(
        "Theme icons",
        "Runtime SVG",
        "Compare embedded and file SVGs, exact scaling, tint, RTL, stretch modes, cache diagnostics, and SVG/PNG theme policy.",
        () => new RuntimeSvgStoryView(),
        root =>
        {
            var scope = NameScope.GetNameScope(root);
            var status = scope.Find<Label>("svgStatus");
            var invalid = scope.Find<Label>("invalidStatus");
            var atlasPreview = scope.Find<SvgAtlasPreview>("atlasPreview");
            var compiledSvg = scope.Find<Image>("compiledSvg");
            var fileSvg = scope.Find<Image>("fileSvg");
            scope.Find<OptionButton>("themeArrow").AddItem("Default arrow");
            fileSvg.ScalableSource = SvgImageSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "runtime-catalog.svg"));
            scope.Find<Image>("drawingImage").VectorSource = new DrawingImage
            {
                IntrinsicSize = new Vector2(126, 72),
                Drawing = new GeometryDrawing
                {
                    Geometry = new RectangleGeometry { CornerRadius = new CornerRadius(10) },
                    Fill = new LinearGradientBrush
                    {
                        GradientStops = new[] { new GradientStop(0, new Color(48, 185, 164)), new GradientStop(1, new Color(246, 185, 73)) },
                    },
                },
            };
            var tree = scope.Find<Tree>("svgTree");
            var treeRoot = tree.CreateItem();
            treeRoot.SetText(0, "Source SVG");
            treeRoot.CreateChild().SetText(0, "Exact scale");
            treeRoot.CreateChild().SetText(0, "RTL");
            var gridRoot = new CatalogTreeRow { Name = "Source SVG", Kind = "Source", IsExpanded = true };
            gridRoot.Children.Add(new CatalogTreeRow { Name = "Exact scale", Kind = "Raster" });
            gridRoot.Children.Add(new CatalogTreeRow { Name = "PNG fallback", Kind = "Bitmap" });
            scope.Find<DataGrid>("svgGrid").ItemsSource = new[] { gridRoot };
            void Refresh()
            {
                var health = SvgRuntime.Health;
                var raster = root.Context.SvgRasterDiagnostics;
                var icons = root.Context.ThemeIconDiagnostics;
                status.Text = $"{health.BackendId} {health.Version.Split('+')[0]} · profile {health.ProfileVersion} · {health.LinkMode}/{health.NativeAvailability}\n{root.Context.ThemeIconRenderingPolicy} · SVG icons {icons.RuntimeSvgIconCount} · PNG fallbacks {icons.BitmapFallbackCount}\nRasters {raster.EntryCount} · pages {raster.PageCount} · {raster.Bytes / 1024f:0.0} KB · hits {raster.Hits} · misses {raster.Misses} · {atlasPreview.Summary}";
            }
            scope.Find<Button>("runtimePolicy").Pressed += (_, _) => { root.Context.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.RuntimeSvg; Refresh(); };
            scope.Find<Button>("bitmapPolicy").Pressed += (_, _) => { root.Context.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.BitmapAtlas; Refresh(); };
            scope.Find<Button>("autoPolicy").Pressed += (_, _) => { root.Context.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.Auto; Refresh(); };
            scope.Find<Button>("ltr").Pressed += (_, _) => root.LayoutDirection = LayoutDirection.LeftToRight;
            scope.Find<Button>("rtl").Pressed += (_, _) => root.LayoutDirection = LayoutDirection.RightToLeft;
            scope.Find<Button>("stretchContain").Pressed += (_, _) => { compiledSvg.Stretch = ImageStretch.Contain; fileSvg.Stretch = ImageStretch.Contain; Refresh(); };
            scope.Find<Button>("stretchCover").Pressed += (_, _) => { compiledSvg.Stretch = ImageStretch.Cover; fileSvg.Stretch = ImageStretch.Cover; Refresh(); };
            scope.Find<Button>("stretchFill").Pressed += (_, _) => { compiledSvg.Stretch = ImageStretch.Fill; fileSvg.Stretch = ImageStretch.Fill; Refresh(); };
            void SetScale(float scale) => (setDisplayScale ?? (s => root.Context.DisplayScale = s))(scale);
            scope.Find<Button>("scale100").Pressed += (_, _) => SetScale(1f);
            scope.Find<Button>("scale125").Pressed += (_, _) => SetScale(1.25f);
            scope.Find<Button>("scale150").Pressed += (_, _) => SetScale(1.5f);
            scope.Find<Button>("scale175").Pressed += (_, _) => SetScale(1.75f);
            scope.Find<Button>("scale200").Pressed += (_, _) => SetScale(2f);
            scope.Find<Button>("scale250").Pressed += (_, _) => SetScale(2.5f);
            {
                var health = SvgRuntime.Health;
                var rejectionLine = string.Empty;
                try
                {
                    _ = SvgImageSource.FromMemory(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.com/external.png'/></svg>"));
                }
                catch (SvgLoadException exception)
                {
                    rejectionLine = $"Rejected external ref · {exception.Code}: {exception.Message}";
                }
                var backendLine = health.IsAvailable
                    ? $"Backend: {health.Diagnostic}"
                    : $"Backend absent: {health.Diagnostic}";
                invalid.Text = $"{rejectionLine}\n{backendLine}";
            }
            IDisposable refreshRegistration = null;
            refreshRegistration = root.Context.RegisterFrameBoundaryCallback(_ =>
            {
                if (root.Context == null) refreshRegistration.Dispose();
                else
                {
                    Refresh();
                    if (root.Context.SvgRasterDiagnostics.EntryCount > 0 && root.Context.ThemeIconDiagnostics.RuntimeSvgIconCount > 0)
                    {
                        atlasPreview.RefreshSnapshot();
                        Refresh();
                        refreshRegistration.Dispose();
                    }
                }
            });
            Refresh();
        },
        "RuntimeSvgStoryView.xaml");

    private static void PopulateIconInventory(Control root)
    {
        var flow = NameScope.GetNameScope(root).Find<FlowContainer>("iconFlow");
        using var stream = typeof(Control).Assembly.GetManifestResourceStream("Forma.ThemeIcons.theme-icons.json")
            ?? throw new InvalidDataException("Embedded theme icon manifest is missing.");
        var manifest = JsonSerializer.Deserialize<IconInventoryManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Embedded theme icon manifest is invalid.");
        foreach (var entry in manifest.Icons.Where(icon => icon.Density == 1).OrderBy(icon => icon.Name, StringComparer.Ordinal))
        {
            var binding = entry.Bindings[0].Split(':', 2);
            var tile = new VBoxContainer { Separation = 3, CustomMinimumSize = new Vector2(164, 120) };
            tile.AddChild(new ThemeIconRect { ThemeTypeName = binding[0], ThemeItemName = binding[1], CustomMinimumSize = new Vector2(32, 28) });
            tile.AddChild(new Label { Text = entry.Name, HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(164, 20) });
            tile.AddChild(new Label { Text = $"{entry.LogicalWidth}x{entry.LogicalHeight} @ 1x/2x", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(164, 18) });
            tile.AddChild(new Label { Text = entry.Bindings[0], HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(164, 18) });
            tile.AddChild(new Label { Text = entry.Source, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = LabelAutowrapMode.Arbitrary, CustomMinimumSize = new Vector2(164, 42) });
            flow.AddChild(tile);
        }
    }

    private static ComponentStory CreateIconCustomizationStory(Texture2D texture) => new(
        "Theme icons",
        "Override and suppression",
        "Compare default lookup with a per-control override and intentional suppression. Existing content icons still take precedence.",
        () => new IconCustomizationStoryView(),
        root =>
        {
            var width = Math.Min(16, texture.Width);
            var height = Math.Min(16, texture.Height);
            var custom = new ThemeIcon(texture, new Rectangle(0, 0, width, height), new Point(width, height));
            var direction = root.Children[0];
            var row = root.Children[1];
            ((Button)row.Children[3]).Icon = texture;
            ((OptionButton)row.Children[1].Children[1]).AddThemeIconOverride("arrow", custom);
            ((OptionButton)row.Children[2].Children[1]).SuppressThemeIcon("arrow");
            ((Button)direction.Children[0]).Pressed += (_, _) => root.LayoutDirection = LayoutDirection.LeftToRight;
            ((Button)direction.Children[1]).Pressed += (_, _) => root.LayoutDirection = LayoutDirection.RightToLeft;
        },
        "IconCustomizationStoryView.xaml");

    private static ComponentStory CreateIconDiagnosticsStory(Action<float> setDisplayScale) => new(
        "Theme icons",
        "Atlas diagnostics",
        "Inspect active density, loaded atlas count, decoded texture memory, cache generation, and missing optional lookups.",
        () => new IconDiagnosticsStoryView(),
        root =>
        {
            var status = (Label)root.Children[0];
            var row = root.Children[1];
            void Refresh()
            {
                var diagnostics = root.Context.ThemeIconDiagnostics;
                ((CatalogStoryViewModel)root.DataContext).Status = $"Density: {diagnostics.ActiveDensity}x   Atlases: {diagnostics.AtlasCount}   Texture: {diagnostics.TextureBytes / 1024f:0.0} KB\nGeneration: {diagnostics.Generation}   Missing optional lookups: {diagnostics.MissingIconCount}";
            }
            ((Button)row.Children[0]).Pressed += (_, _) => { (setDisplayScale ?? (scale => root.Context.DisplayScale = scale))(1f); Refresh(); };
            ((Button)row.Children[1]).Pressed += (_, _) => { (setDisplayScale ?? (scale => root.Context.DisplayScale = scale))(2f); Refresh(); };
            ((Button)row.Children[2]).Pressed += (_, _) => Refresh();
            Refresh();
        },
        "IconDiagnosticsStoryView.xaml");

    private sealed class IconInventoryManifest
    {
        public List<IconInventoryEntry> Icons { get; set; } = new();
    }

    private sealed class IconInventoryEntry
    {
        public string Name { get; set; } = string.Empty;
        public int Density { get; set; }
        public int LogicalWidth { get; set; }
        public int LogicalHeight { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<string> Bindings { get; set; } = new();
    }

    private sealed class CatalogFileDialogFileSystem : IFileDialogFileSystem
    {
        private readonly HashSet<string> _directories;
        private readonly HashSet<string> _files;

        public CatalogFileDialogFileSystem()
        {
            RootPath = Path.GetFullPath(Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? string.Empty, "Forma Project"));
            _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                RootPath,
                Path.Combine(RootPath, "Assets"),
                Path.Combine(RootPath, "Scenes"),
                Path.Combine(RootPath, "Scripts"),
            };
            _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(RootPath, "project.json"),
                Path.Combine(RootPath, "Assets", "forma-mark.png"),
                Path.Combine(RootPath, "Scenes", "main.json"),
                Path.Combine(RootPath, "Scripts", "PlayerController.cs"),
                Path.Combine(RootPath, "Scripts", "WorldLoader.cs"),
            };
        }

        public string RootPath { get; }
        public bool IsAvailable => true;
        public string GetCurrentDirectory() => RootPath;
        public bool FileExists(string path) => _files.Contains(Path.GetFullPath(path));
        public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path));
        public IEnumerable<string> EnumerateEntries(string path)
        {
            var directory = Path.GetFullPath(path);
            return _directories.Concat(_files)
                .Where(entry => !string.Equals(entry, directory, StringComparison.OrdinalIgnoreCase))
                .Where(entry => string.Equals(Path.GetDirectoryName(entry), directory, StringComparison.OrdinalIgnoreCase));
        }
        public string GetParentDirectory(string path)
        {
            var directory = Path.GetFullPath(path);
            if (string.Equals(directory, RootPath, StringComparison.OrdinalIgnoreCase)) return null;
            var parent = Path.GetDirectoryName(directory);
            return parent != null && DirectoryExists(parent) ? parent : null;
        }
        public void CreateDirectory(string path) => _directories.Add(Path.GetFullPath(path));
        public DateTime GetLastWriteTimeUtc(string path) => new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    private static void AttachExample(Control control, Texture2D texture)
    {
        if (control is FileDialog fileDialog)
        {
            var fileSystem = new CatalogFileDialogFileSystem();
            fileDialog.FileSystem = fileSystem;
            fileDialog.AddFilter("*.cs,*.png,*.json;Project assets");
            fileDialog.SetCurrentDir(fileSystem.RootPath);
            return;
        }
        if (control is PopupMenu popupMenu)
        {
            popupMenu.AddItem("New scene", 1);
            popupMenu.AddCheckItem("Show grid", 2);
            popupMenu.SetItemChecked(1, true);
            popupMenu.AddSeparator();
            popupMenu.AddItem("Close", 3);
            return;
        }
        if (control is MenuBar menuBar)
        {
            var file = (MenuButton)menuBar.Children.Single(child => child.Name == "FileMenu");
            file.Menu.AddItem("New");
            file.Menu.AddItem("Open");
            file.Menu.AddSeparator();
            file.Menu.AddItem("Save");
            var edit = (MenuButton)menuBar.Children.Single(child => child.Name == "EditMenu");
            edit.Menu.AddItem("Undo");
            edit.Menu.AddItem("Redo");
            file.Pressed += (_, _) => edit.Menu.Hide();
            edit.Pressed += (_, _) => file.Menu.Hide();
            return;
        }
        if (control is MenuButton menuButton)
        {
            menuButton.Menu.AddItem("Build solution");
            menuButton.Menu.AddItem("Rebuild");
            menuButton.Menu.AddItem("Clean");
            return;
        }
        if (control is OptionButton optionButton)
        {
            optionButton.AddItem("DesktopGL");
            optionButton.AddItem("WindowsDX");
            optionButton.AddItem("Android");
            optionButton.Select(0);
            return;
        }
        if (control is TabBar tabBar)
        {
            tabBar.AddTab("Scene");
            tabBar.AddTab("Inspector");
            tabBar.AddTab("Output");
            tabBar.AddTab("Debugger");
            return;
        }
        if (control is Tree tree)
        {
            tree.SetColumnTitle(0, "Node");
            tree.SetColumnTitle(1, "Visible");
            var root = tree.CreateItem();
            root.SetText(0, "World");
            var player = root.CreateChild();
            player.SetText(0, "Player");
            player.SetCellMode(1, TreeCellMode.Check);
            player.SetChecked(1, true);
            var camera = player.CreateChild();
            camera.SetText(0, "Camera2D");
            camera.SetCellMode(1, TreeCellMode.Check);
            camera.SetChecked(1, true);
            return;
        }
        if (control is ItemList itemList)
        {
            itemList.AddItem("Player.tscn", texture);
            itemList.AddItem("World.cs", texture);
            itemList.AddItem("palette.png", texture);
            itemList.Select(0);
            return;
        }
        if (control is GraphEdit graphEdit)
        {
            var source = (GraphNode)graphEdit.Children.Single(child => child.Name == "Input");
            source.AddOutputPort("value", 1, new Color(48, 185, 164));
            var output = (GraphNode)graphEdit.Children.Single(child => child.Name == "Output");
            output.AddInputPort("value", 1, new Color(48, 185, 164));
            graphEdit.ConnectNode("Input", 0, "Output", 0);
            return;
        }
        if (control is GraphNode graphNode)
        {
            graphNode.AddInputPort("input", 1, new Color(246, 185, 73));
            graphNode.AddOutputPort("result", 1, new Color(48, 185, 164));
            return;
        }
        if (control is RichTextLabel richText)
        {
            richText.AppendBbcode("[color=#30b9a4][b]Forma[/b][/color][br]Rich text supports [i]formatting[/i], links, lists, tables, and selection.");
            return;
        }
        if (control is ColorPicker colorPicker)
        {
            colorPicker.AddPreset(new Color(246, 185, 73));
            colorPicker.AddPreset(new Color(91, 126, 246));
            return;
        }
        if (control is ColorPickerButton colorPickerButton)
        {
            colorPickerButton.Picker.AddPreset(new Color(246, 185, 73));
            return;
        }
        if (control is TextureProgressBar textureProgress)
        {
            textureProgress.Under = texture;
            textureProgress.Progress = texture;
            return;
        }
        if (control is TextureButton textureButton)
        {
            textureButton.TextureNormal = texture;
            return;
        }
        if (control is NinePatchRect ninePatch)
        {
            ninePatch.Texture = texture;
            return;
        }
        if (control is TextureRect textureRect)
        {
            textureRect.Texture = texture;
            return;
        }
        if (control is SubViewportContainer subViewport)
        {
            subViewport.ViewportContext.Add(new ColorRect { Position = new Vector2(16, 16), Size = new Vector2(180, 100), Color = new Color(48, 185, 164) });
        }
    }

    private static string GetCategory(Type type)
    {
        if (type == typeof(VideoStreamPlayer)) return "Media";
        if (typeof(Popup).IsAssignableFrom(type) || type.Name.Contains("Dialog", StringComparison.Ordinal)) return "Overlays";
        if (type.Name.Contains("Text", StringComparison.Ordinal) || type == typeof(Label) || type == typeof(LineEdit) || type == typeof(CodeEdit)) return "Text";
        if (type.Name.Contains("Graph", StringComparison.Ordinal) || type == typeof(Tree) || type == typeof(ItemList) || type.Name.Contains("Tab", StringComparison.Ordinal) || type.Name.Contains("Menu", StringComparison.Ordinal)) return "Data & navigation";
        if (type.Name.Contains("Texture", StringComparison.Ordinal) || type == typeof(ColorRect) || type.Name.Contains("Color", StringComparison.Ordinal)) return "Visuals";
        if (typeof(BaseButton).IsAssignableFrom(type) || typeof(Forma.Range).IsAssignableFrom(type)) return "Inputs";
        if (typeof(Container).IsAssignableFrom(type)) return "Layout";
        return "Specialized";
    }
}

public static class FontApplicator
{
    public static void Apply(Control control, SpriteFont font, SpriteFont codeFont) =>
        Apply(control, new SpriteFontAdapter(font), new SpriteFontAdapter(codeFont), font);

    public static void Apply(Control control, UIFont font, UIFont codeFont, SpriteFont compatibilityFont)
    {
        switch (control)
        {
            case CodeEdit codeEdit: codeEdit.UIFont = codeFont; break;
            case TextEdit textEdit: textEdit.UIFont = font; break;
            case RichTextLabel richTextLabel: richTextLabel.UIFont = font; break;
            case Label label when label.Name == "spriteCompatibility": label.Font = compatibilityFont; break;
            case Label label: label.UIFont = font; break;
            case MenuButton menuButton: menuButton.UIFont = font; Apply(menuButton.Menu, font, codeFont, compatibilityFont); break;
            case BaseButton button: button.UIFont = font; break;
            case PopupMenu popupMenu: popupMenu.UIFont = font; break;
            case LineEdit lineEdit: lineEdit.UIFont = font; break;
            case SpinBox spinBox: spinBox.UIFont = font; break;
            case ProgressBar progressBar: progressBar.UIFont = font; break;
            case AcceptDialog dialog: dialog.UIFont = font; break;
            case Tree tree: tree.UIFont = font; break;
            case TabContainer tabs: tabs.UIFont = font; break;
            case GraphNode graphNode: graphNode.UIFont = font; break;
            case ItemList itemList: itemList.UIFont = font; break;
            case TabBar tabBar: tabBar.UIFont = font; break;
        }
        foreach (var child in control.Children) Apply(child, font, codeFont, compatibilityFont);
    }
}