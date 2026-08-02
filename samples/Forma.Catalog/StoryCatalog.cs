// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

public sealed class ComponentStory
{
    public ComponentStory(string category, string name, string description, Func<Control> factory, Action<Control> attached = null)
    {
        Category = category;
        Name = name;
        Description = description;
        Factory = factory;
        Attached = attached;
    }

    public string Category { get; }
    public string Name { get; }
    public string Description { get; }
    public Func<Control> Factory { get; }
    public Action<Control> Attached { get; }
}

public static class StoryCatalog
{
    public static IReadOnlyList<ComponentStory> Create(Texture2D texture, Action<float> setDisplayScale = null, Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont = null)
    {
        var stories = new List<ComponentStory>();
        var controlType = typeof(Control);
        var explicitFactories = new Dictionary<Type, Func<Control>>
        {
            [typeof(BoxContainer)] = () => new BoxContainer(Orientation.Horizontal),
            [typeof(Slider)] = () => new Slider(Orientation.Horizontal),
            [typeof(ScrollBar)] = () => new ScrollBar(Orientation.Horizontal),
            [typeof(SplitContainer)] = () => new SplitContainer(Orientation.Horizontal),
            [typeof(FlowContainer)] = () => new FlowContainer(Orientation.Horizontal),
        };
        foreach (var type in new[] { controlType.Assembly, typeof(VideoStreamPlayer).Assembly }
                     .SelectMany(assembly => assembly.GetTypes())
                     .Where(type => type.IsPublic && !type.IsAbstract && controlType.IsAssignableFrom(type))
                     .Where(type => type.GetConstructor(Type.EmptyTypes) != null || explicitFactories.ContainsKey(type))
                     .OrderBy(type => GetCategory(type))
                     .ThenBy(type => type.Name))
        {
            stories.Add(new ComponentStory(
                GetCategory(type),
                type.Name,
                type == typeof(VideoStreamPlayer)
                    ? "Optional [color=#30b9a4]Forma.Media[/color] video control configured for autoplay, looping, and responsive expansion. Assign a content-loaded Video to begin playback."
                    : $"Interactive example of [color=#30b9a4]{type.FullName}[/color]. Use the property inspector to change its public writable values at runtime.",
                () => CreateExample(type, texture, explicitFactories)));
        }
        stories.Add(CreateIconInventoryStory());
        stories.Add(CreateIconCustomizationStory(texture));
        stories.Add(CreateIconDiagnosticsStory(setDisplayScale));
        stories.Add(CreateDynamicSizesStory(createDynamicFont));
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
        () =>
        {
            var column = new VBoxContainer { Separation = 12, CustomMinimumSize = new Vector2(640, 300) };
            var controls = new HBoxContainer { Separation = 10 };
            var input = new LineEdit { Name = "liveText", Text = "Forma office 你好", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(280, 36) };
            var family = new OptionButton { Name = "fontFamily", CustomMinimumSize = new Vector2(150, 36) };
            family.AddItem("Inter");
            family.AddItem("Noto Sans SC");
            var size = new Slider(Orientation.Horizontal) { Name = "fontSize", MinValue = 8, MaxValue = 96, Step = 1, Value = 28, CustomMinimumSize = new Vector2(180, 36) };
            controls.AddChild(input);
            controls.AddChild(family);
            controls.AddChild(size);
            column.AddChild(controls);
            column.AddChild(new Label { Name = "sizeStatus", Text = "28 px", FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(0, 22) });
            column.AddChild(new Label { Name = "preview", Text = input.Text, CustomMinimumSize = new Vector2(620, 110), AutowrapMode = LabelAutowrapMode.Word });
            column.AddChild(new Label { Name = "smallPreview", Text = input.Text, CustomMinimumSize = new Vector2(620, 48) });
            return column;
        },
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
                status.Text = $"{size.Value:0} px logical size";
                if (createDynamicFont == null) return;
                var familyName = family.Selected == 1 ? "Noto Sans SC" : "Inter";
                preview.UIFont = createDynamicFont(familyName, size.Value, null);
                smallPreview.UIFont = createDynamicFont(familyName, 12, null);
            }
            input.TextChanged += (_, _) => Refresh();
            family.ItemSelected += (_, _) => Refresh();
            size.ValueChanged += (_, _) => Refresh();
            Refresh();
        });

    private static ComponentStory CreateDisplayDensityStory(Action<float> setDisplayScale, Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Display Density",
        "Compare stable 24 px logical bounds while switching physical rasterization between 1x, 1.5x, and 2x.",
        () =>
        {
            var column = new VBoxContainer { Separation = 12, CustomMinimumSize = new Vector2(660, 260) };
            var buttons = new HBoxContainer { Separation = 8 };
            foreach (var density in new[] { "1x", "1.5x", "2x" }) buttons.AddChild(new Button { Name = density, Text = density, CustomMinimumSize = new Vector2(84, 34) });
            column.AddChild(buttons);
            column.AddChild(new Label { Name = "densityStatus", CustomMinimumSize = new Vector2(0, 24) });
            var samples = new HBoxContainer { Separation = 18 };
            foreach (var density in new[] { 1f, 1.5f, 2f })
                samples.AddChild(new Label { Name = $"sample{density:0.0}", Text = $"Forma\n24 px logical\n{24 * density:0} px raster", CustomMinimumSize = new Vector2(190, 120), VerticalAlignment = VerticalAlignment.Center });
            column.AddChild(samples);
            return column;
        },
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
        });

    private static ComponentStory CreateFallbackChainStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Fallback Chain",
        "Shape mixed scripts through the real fallback family and report the selected face for every retained run.",
        () =>
        {
            var column = new VBoxContainer { Separation = 10, CustomMinimumSize = new Vector2(660, 260) };
            column.AddChild(new LineEdit { Name = "fallbackText", Text = "Forma Ελληνικά Кириллица مرحبا क्ष 你好 ★ 👩🏽‍💻", CustomMinimumSize = new Vector2(640, 38) });
            column.AddChild(new Label { Name = "fallbackPreview", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 76) });
            column.AddChild(new Label { Name = "fallbackDiagnostics", AutowrapMode = LabelAutowrapMode.Word, FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(640, 100) });
            return column;
        },
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
        });

    private static ComponentStory CreateShapingFeaturesStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Shaping and Features",
        "Toggle standard ligatures and kerning, then vary the Noto Arabic weight axis through real HarfBuzz layouts.",
        () =>
        {
            var column = new VBoxContainer { Separation = 10, CustomMinimumSize = new Vector2(660, 280) };
            var controls = new HBoxContainer { Separation = 12 };
            controls.AddChild(new CheckBox { Name = "ligatures", Text = "Standard ligatures", ButtonPressed = true });
            controls.AddChild(new CheckBox { Name = "kerning", Text = "Kerning", ButtonPressed = true });
            controls.AddChild(new Slider(Orientation.Horizontal) { Name = "weight", MinValue = 100, MaxValue = 900, Step = 100, Value = 400, CustomMinimumSize = new Vector2(180, 34) });
            column.AddChild(controls);
            column.AddChild(new Label { Name = "latinFeatures", Text = "office AV é", CustomMinimumSize = new Vector2(640, 62) });
            column.AddChild(new Label { Name = "arabicFeatures", Text = "مرحبا بالعالم", TextDirection = TextDirection.RightToLeft, Language = "ar", CustomMinimumSize = new Vector2(640, 62) });
            column.AddChild(new Label { Name = "featureDiagnostics", FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(640, 40) });
            return column;
        },
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
        });

    private static ComponentStory CreateBidirectionalStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Bidirectional Text",
        "Edit mixed Hebrew, Arabic, numbers, and Latin text; force paragraph direction and inspect logical versus visual run order.",
        () =>
        {
            var column = new VBoxContainer { Separation = 10, CustomMinimumSize = new Vector2(660, 270) };
            var controls = new HBoxContainer { Separation = 10 };
            controls.AddChild(new LineEdit { Name = "bidiText", Text = "Forma שלום 123 مرحبا", CustomMinimumSize = new Vector2(470, 38) });
            var direction = new OptionButton { Name = "bidiDirection", CustomMinimumSize = new Vector2(150, 38) };
            direction.AddItem("Auto");
            direction.AddItem("LTR");
            direction.AddItem("RTL");
            controls.AddChild(direction);
            column.AddChild(controls);
            column.AddChild(new Label { Name = "bidiPreview", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 76) });
            column.AddChild(new Label { Name = "bidiDiagnostics", AutowrapMode = LabelAutowrapMode.Word, FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(640, 96) });
            return column;
        },
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
        });

    private static ComponentStory CreateWrappingSelectionStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Wrapping and Selection",
        "Edit multilingual paragraphs, select with the mouse, and inspect caret, range, wrapping, ellipsis, and visual movement on one retained layout.",
        () =>
        {
            var column = new VBoxContainer { Separation = 9, CustomMinimumSize = new Vector2(680, 560) };
            column.AddChild(new TextEdit
            {
                Name = "wrappingText",
                Text = "Forma élan office שלום مرحبا\nनमस्ते สวัสดี 你好 — select this text with the mouse.",
                LineWrappingMode = TextEditLineWrappingMode.Boundary,
                CustomMinimumSize = new Vector2(660, 105),
            });
            var layoutControls = new HBoxContainer { Separation = 8 };
            layoutControls.AddChild(new Slider(Orientation.Horizontal) { Name = "wrappingWidth", MinValue = 240, MaxValue = 640, Step = 20, Value = 460, CustomMinimumSize = new Vector2(170, 34) });
            var wrapping = new OptionButton { Name = "wrappingMode", CustomMinimumSize = new Vector2(130, 34) };
            wrapping.AddItem("No wrap");
            wrapping.AddItem("Character");
            wrapping.AddItem("Word");
            wrapping.Select(2);
            layoutControls.AddChild(wrapping);
            layoutControls.AddChild(new CheckBox { Name = "wrappingEllipsis", Text = "Ellipsis" });
            var direction = new OptionButton { Name = "wrappingDirection", CustomMinimumSize = new Vector2(110, 34) };
            direction.AddItem("Auto");
            direction.AddItem("LTR");
            direction.AddItem("RTL");
            layoutControls.AddChild(direction);
            column.AddChild(layoutControls);
            var movementControls = new HBoxContainer { Separation = 8 };
            var movement = new OptionButton { Name = "caretMovement", CustomMinimumSize = new Vector2(130, 34) };
            movement.AddItem("Grapheme");
            movement.AddItem("Word");
            movement.AddItem("Visual");
            movementControls.AddChild(movement);
            movementControls.AddChild(new Button { Name = "caretPrevious", Text = "Previous", CustomMinimumSize = new Vector2(100, 34) });
            movementControls.AddChild(new Button { Name = "caretNext", Text = "Next", CustomMinimumSize = new Vector2(100, 34) });
            movementControls.AddChild(new Button { Name = "inspectSelection", Text = "Inspect selection", CustomMinimumSize = new Vector2(150, 34) });
            column.AddChild(movementControls);
            var preview = new Label { Name = "wrappingPreview", ClipText = true, CustomMinimumSize = new Vector2(460, 190), CustomMaximumSize = new Vector2(460, 190) };
            for (var index = 0; index < 12; index++)
                preview.AddChild(new ColorRect { Name = $"selectionOverlay{index}", Color = new Color(48, 185, 164, 82), MouseFilter = MouseFilter.Ignore, Visible = false, ZIndex = 1 });
            preview.AddChild(new ColorRect { Name = "caretOverlay", Color = new Color(246, 185, 73), MouseFilter = MouseFilter.Ignore, Visible = false, ZIndex = 2 });
            column.AddChild(preview);
            column.AddChild(new Label { Name = "wrappingDiagnostics", AutowrapMode = LabelAutowrapMode.Word, FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(660, 58) });
            return column;
        },
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
        });

    private static ComponentStory CreateSpriteFontCompatibilityStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "SpriteFont Compatibility",
        "Compare retained dynamic text with the native-free SpriteFontAdapter path. Metric differences are expected because the source fonts and rasterizers differ.",
        () =>
        {
            var column = new VBoxContainer { Separation = 12, CustomMinimumSize = new Vector2(660, 250) };
            column.AddChild(new LineEdit { Name = "compatibilityText", Text = "Forma office AV", CustomMinimumSize = new Vector2(640, 38) });
            column.AddChild(new Label { Name = "dynamicCompatibility", CustomMinimumSize = new Vector2(640, 62) });
            column.AddChild(new Label { Name = "spriteCompatibility", CustomMinimumSize = new Vector2(640, 62) });
            column.AddChild(new Label { Name = "compatibilityDiagnostics", FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(640, 42) });
            return column;
        },
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
        });

    private static ComponentStory CreateAtlasInspectorStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Atlas Inspector",
        "Inspect immutable page previews, occupancy, cache activity, uploads, evictions, and bounded memory while stressing or clearing the device cache.",
        () =>
        {
            var column = new VBoxContainer { Separation = 9, CustomMinimumSize = new Vector2(680, 390) };
            var controls = new HBoxContainer { Separation = 8 };
            controls.AddChild(new LineEdit { Name = "atlasStress", Text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789 مرحبا 你好 👩🏽‍💻", CustomMinimumSize = new Vector2(450, 38) });
            controls.AddChild(new Button { Name = "atlasRefresh", Text = "Refresh", CustomMinimumSize = new Vector2(96, 38) });
            controls.AddChild(new Button { Name = "atlasClear", Text = "Clear", CustomMinimumSize = new Vector2(86, 38) });
            column.AddChild(controls);
            column.AddChild(new Label { Name = "atlasPreviewText", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(660, 58) });
            column.AddChild(new Label { Name = "atlasStatus", AutowrapMode = LabelAutowrapMode.Word, FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(660, 48) });
            var pages = new HBoxContainer { Name = "atlasPages", Separation = 8 };
            for (var index = 0; index < 4; index++) pages.AddChild(new DynamicGlyphAtlasView { Name = $"atlasPage{index}", CustomMinimumSize = new Vector2(150, 150) });
            column.AddChild(pages);
            return column;
        },
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
        });

    private static ComponentStory CreateFailureStatesStory(Func<string, float, IReadOnlyList<UIFontVariationCoordinate>, UIFont> createDynamicFont) => new(
        "Typography",
        "Failure States",
        "Run bounded font, fallback, and atlas failure probes. Expected failures remain visible diagnostics and never terminate the catalog.",
        () =>
        {
            var column = new VBoxContainer { Separation = 9, CustomMinimumSize = new Vector2(660, 300) };
            column.AddChild(new Button { Name = "runFailureProbes", Text = "Run failure probes", CustomMinimumSize = new Vector2(190, 36) });
            column.AddChild(new Label { Name = "missingFaceFailure", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 38) });
            column.AddChild(new Label { Name = "malformedFontFailure", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 38) });
            column.AddChild(new Label { Name = "fallbackFailure", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 38) });
            column.AddChild(new Label { Name = "atlasFailure", AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(640, 54) });
            return column;
        },
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
        });

    private static ComponentStory CreateIconInventoryStory() => new(
        "Theme icons",
        "Complete icon inventory",
        "Every imported logical icon, resolved through a real control binding from the embedded manifest.",
        () =>
        {
            var flow = new FlowContainer(Orientation.Horizontal) { Separation = 10, CustomMinimumSize = new Vector2(540, 0) };
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(580, 420), HorizontalScrollMode = ScrollBarVisibility.Never };
            scroll.AddChild(flow);
            return scroll;
        },
        PopulateIconInventory);

    private static void PopulateIconInventory(Control root)
    {
        var flow = root.Children.OfType<FlowContainer>().Single();
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
        () =>
        {
            var column = new VBoxContainer { Separation = 12, CustomMinimumSize = new Vector2(620, 150) };
            var direction = new HBoxContainer { Separation = 8 };
            direction.AddChild(new Button { Name = "ltr", Text = "LTR", CustomMinimumSize = new Vector2(90, 32) });
            direction.AddChild(new Button { Name = "rtl", Text = "RTL", CustomMinimumSize = new Vector2(90, 32) });
            var row = new HBoxContainer { Separation = 18, CustomMinimumSize = new Vector2(620, 100) };
            row.AddChild(LabeledOption("Default", "default"));
            row.AddChild(LabeledOption("Overridden", "override"));
            row.AddChild(LabeledOption("Suppressed", "suppressed"));
            var explicitIcon = new Button { Text = "Content", Icon = texture, CustomMinimumSize = new Vector2(120, 64) };
            row.AddChild(explicitIcon);
            column.AddChild(direction);
            column.AddChild(row);
            return column;
        },
        root =>
        {
            var width = Math.Min(16, texture.Width);
            var height = Math.Min(16, texture.Height);
            var custom = new ThemeIcon(texture, new Rectangle(0, 0, width, height), new Point(width, height));
            var direction = root.Children[0];
            var row = root.Children[1];
            ((OptionButton)row.Children[1].Children[1]).AddThemeIconOverride("arrow", custom);
            ((OptionButton)row.Children[2].Children[1]).SuppressThemeIcon("arrow");
            ((Button)direction.Children[0]).Pressed += (_, _) => root.LayoutDirection = LayoutDirection.LeftToRight;
            ((Button)direction.Children[1]).Pressed += (_, _) => root.LayoutDirection = LayoutDirection.RightToLeft;
        });

    private static Control LabeledOption(string label, string name)
    {
        var column = new VBoxContainer { Separation = 6, CustomMinimumSize = new Vector2(164, 72) };
        column.AddChild(new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center });
        var option = new OptionButton { Name = name, CustomMinimumSize = new Vector2(164, 36) };
        option.AddItem("Density");
        column.AddChild(option);
        return column;
    }

    private static ComponentStory CreateIconDiagnosticsStory(Action<float> setDisplayScale) => new(
        "Theme icons",
        "Atlas diagnostics",
        "Inspect active density, loaded atlas count, decoded texture memory, cache generation, and missing optional lookups.",
        () =>
        {
            var column = new VBoxContainer { Separation = 10, CustomMinimumSize = new Vector2(480, 180) };
            column.AddChild(new Label { Name = "status", CustomMinimumSize = new Vector2(480, 48) });
            var row = new HBoxContainer { Separation = 8 };
            row.AddChild(new Button { Name = "one", Text = "Use 1x", CustomMinimumSize = new Vector2(110, 34) });
            row.AddChild(new Button { Name = "two", Text = "Use 2x", CustomMinimumSize = new Vector2(110, 34) });
            row.AddChild(new Button { Name = "refresh", Text = "Refresh", CustomMinimumSize = new Vector2(110, 34) });
            column.AddChild(row);
            return column;
        },
        root =>
        {
            var status = (Label)root.Children[0];
            var row = root.Children[1];
            void Refresh()
            {
                var diagnostics = root.Context.ThemeIconDiagnostics;
                status.Text = $"Density: {diagnostics.ActiveDensity}x   Atlases: {diagnostics.AtlasCount}   Texture: {diagnostics.TextureBytes / 1024f:0.0} KB\nGeneration: {diagnostics.Generation}   Missing optional lookups: {diagnostics.MissingIconCount}";
            }
            ((Button)row.Children[0]).Pressed += (_, _) => { (setDisplayScale ?? (scale => root.Context.DisplayScale = scale))(1f); Refresh(); };
            ((Button)row.Children[1]).Pressed += (_, _) => { (setDisplayScale ?? (scale => root.Context.DisplayScale = scale))(2f); Refresh(); };
            ((Button)row.Children[2]).Pressed += (_, _) => Refresh();
            Refresh();
        });

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

    private static Control CreateExample(Type type, Texture2D texture, IReadOnlyDictionary<Type, Func<Control>> explicitFactories)
    {
        var control = explicitFactories.TryGetValue(type, out var factory) ? factory() : (Control)Activator.CreateInstance(type);
        control.Name = type.Name;
        control.TooltipText = type.FullName;
        control.CustomMinimumSize = IsLargeSurface(type) ? new Vector2(560, 360) : new Vector2(300, 64);
        SeedExample(control, type.Name, texture);
        return control;
    }

    private static void SeedExample(Control control, string name, Texture2D texture)
    {
        if (control is VideoStreamPlayer videoPlayer)
        {
            videoPlayer.Autoplay = true;
            videoPlayer.Loop = true;
            videoPlayer.Expand = true;
            videoPlayer.Volume = .75f;
            videoPlayer.AddChild(new ColorRect
            {
                Size = new Vector2(560, 315),
                Color = new Color(16, 20, 27),
            });
            videoPlayer.AddChild(new Label
            {
                Position = new Vector2(180, 124),
                Size = new Vector2(200, 68),
                Text = "VIDEO\nNO STREAM",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }
        if (control is Popup embeddedPopup)
        {
            embeddedPopup.Modal = false;
            embeddedPopup.HideOnOutsideClick = false;
            embeddedPopup.Visible = true;
        }
        if (control is FileDialog fileDialog)
        {
            fileDialog.Title = "Open project asset";
            fileDialog.DialogText = "Select a file from the project";
            fileDialog.AddFilter("*.cs;*.png;*.json");
            fileDialog.Visible = true;
            return;
        }
        if (control is AcceptDialog dialog)
        {
            dialog.Title = name;
            dialog.DialogText = control is ConfirmationDialog ? "Continue with this action?" : "This is a modal message.";
            dialog.Visible = true;
            return;
        }
        if (control is PopupMenu popupMenu)
        {
            popupMenu.AddItem("New scene", 1);
            popupMenu.AddCheckItem("Show grid", 2);
            popupMenu.SetItemChecked(1, true);
            popupMenu.AddSeparator();
            popupMenu.AddItem("Close", 3);
            popupMenu.Visible = true;
            return;
        }
        if (control is MenuBar menuBar)
        {
            var file = menuBar.AddMenu("File");
            file.Menu.AddItem("New");
            file.Menu.AddItem("Open");
            file.Menu.AddSeparator();
            file.Menu.AddItem("Save");
            var edit = menuBar.AddMenu("Edit");
            edit.Menu.AddItem("Undo");
            edit.Menu.AddItem("Redo");
            return;
        }
        if (control is MenuButton menuButton)
        {
            menuButton.Text = "Build";
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
        if (control is TabContainer tabContainer)
        {
            tabContainer.AddChild(CreatePage("Scene", new Color(37, 70, 70)));
            tabContainer.AddChild(CreatePage("Inspector", new Color(64, 55, 37)));
            tabContainer.AddChild(CreatePage("Output", new Color(43, 49, 65)));
            return;
        }
        if (control is TabBar tabBar)
        {
            tabBar.AddTab("Scene");
            tabBar.AddTab("Inspector");
            tabBar.AddTab("Output");
            tabBar.AddTab("Debugger");
            tabBar.CloseDisplayPolicy = TabCloseDisplayPolicy.ActiveOnly;
            return;
        }
        if (control is Tree tree)
        {
            tree.Columns = 2;
            tree.ColumnTitlesVisible = true;
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
            itemList.MaxColumns = 2;
            return;
        }
        if (control is GraphEdit graphEdit)
        {
            graphEdit.HorizontalSizeFlags = SizeFlags.Fill | SizeFlags.Expand;
            graphEdit.VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand;
            var source = new GraphNode { Name = "Input", Title = "Input", Position = new Vector2(32, 54), Size = new Vector2(150, 84) };
            source.AddOutputPort("value", 1, new Color(48, 185, 164));
            var output = new GraphNode { Name = "Output", Title = "Output", Position = new Vector2(310, 180), Size = new Vector2(150, 84) };
            output.AddInputPort("value", 1, new Color(48, 185, 164));
            graphEdit.AddChild(source);
            graphEdit.AddChild(output);
            graphEdit.ConnectNode("Input", 0, "Output", 0);
            return;
        }
        if (control is GraphNode graphNode)
        {
            graphNode.Title = name;
            graphNode.AddInputPort("input", 1, new Color(246, 185, 73));
            graphNode.AddOutputPort("result", 1, new Color(48, 185, 164));
            graphNode.AddChild(new Label { Text = "Process value", CustomMinimumSize = new Vector2(180, 32) });
            return;
        }
        if (control is CodeEdit codeEdit)
        {
            codeEdit.Text = "using Forma;\n\nvar button = new Button\n{\n    Text = \"Run game\",\n};";
            codeEdit.DrawLineNumbers = true;
            codeEdit.DrawMinimap = true;
            codeEdit.SetLineWrappingMode(TextEditLineWrappingMode.Boundary);
            return;
        }
        if (control is RichTextLabel richText)
        {
            richText.AppendBbcode("[color=#30b9a4][b]Forma[/b][/color][br]Rich text supports [i]formatting[/i], links, lists, tables, and selection.");
            richText.SelectionEnabled = true;
            richText.ScrollActive = true;
            return;
        }
        if (control is TextEdit textEdit)
        {
            textEdit.Text = "A multiline editor built with Forma.\n\nSelect text, move the caret, and edit this document.";
            textEdit.SetLineWrappingMode(TextEditLineWrappingMode.Boundary);
            return;
        }
        if (control is SpinBox spinBox)
        {
            spinBox.MinValue = 8;
            spinBox.MaxValue = 96;
            spinBox.Value = 24;
            spinBox.Step = 1;
            spinBox.Prefix = "Font size: ";
            return;
        }
        if (control is LineEdit lineEdit)
        {
            lineEdit.Text = "Editable component value";
            lineEdit.PlaceholderText = "Type here";
            lineEdit.ClearButtonEnabled = true;
            return;
        }
        if (control is ColorPicker colorPicker)
        {
            colorPicker.Color = new Color(48, 185, 164);
            colorPicker.AddPreset(new Color(246, 185, 73));
            colorPicker.AddPreset(new Color(91, 126, 246));
            return;
        }
        if (control is ColorPickerButton colorPickerButton)
        {
            colorPickerButton.Color = new Color(48, 185, 164);
            colorPickerButton.Picker.AddPreset(new Color(246, 185, 73));
            return;
        }
        if (control is ColorPresetButton presetButton)
        {
            presetButton.Color = new Color(246, 185, 73);
            return;
        }
        if (control is TextureProgressBar textureProgress)
        {
            textureProgress.Under = texture;
            textureProgress.Progress = texture;
            textureProgress.Value = 68;
            textureProgress.TintUnder = new Color(80, 86, 98);
            textureProgress.TintProgress = new Color(48, 185, 164);
            return;
        }
        if (control is TextureButton textureButton)
        {
            textureButton.TextureNormal = texture;
            textureButton.StretchMode = TextureButtonStretchMode.KeepAspectCentered;
            return;
        }
        if (control is NinePatchRect ninePatch)
        {
            ninePatch.Texture = texture;
            ninePatch.PatchMargin = new Thickness(8);
            return;
        }
        if (control is TextureRect textureRect)
        {
            textureRect.Texture = texture;
            textureRect.StretchMode = TextureStretchMode.Tile;
            textureRect.ExpandMode = TextureRectExpandMode.IgnoreSize;
            return;
        }
        if (control is SubViewportContainer subViewport)
        {
            subViewport.Stretch = true;
            subViewport.StretchShrink = 2;
            subViewport.ViewportClearColor = new Color(17, 21, 27);
            subViewport.ViewportContext.Add(new ColorRect { Position = new Vector2(16, 16), Size = new Vector2(180, 100), Color = new Color(48, 185, 164) });
            return;
        }
        if (control is VirtualJoystick joystick)
        {
            joystick.CustomMinimumSize = new Vector2(180, 180);
            joystick.BackgroundColor = new Color(43, 52, 66);
            joystick.KnobColor = new Color(48, 185, 164);
            return;
        }
        if (control is FoldableContainer foldable)
        {
            foldable.Title = "Transform";
            foldable.AddChild(new Label { Text = "Position   120, 64", CustomMinimumSize = new Vector2(280, 30) });
            foldable.AddChild(new Label { Text = "Rotation   0 degrees", CustomMinimumSize = new Vector2(280, 30) });
            return;
        }
        if (control is ScrollContainer scroll)
        {
            var content = new VBoxContainer { Separation = 6, CustomMinimumSize = new Vector2(500, 600) };
            for (var index = 1; index <= 14; index++) content.AddChild(new Button { Text = $"Scrollable row {index}", CustomMinimumSize = new Vector2(480, 32) });
            scroll.AddChild(content);
            return;
        }
        if (control is AspectRatioContainer aspectRatio)
        {
            aspectRatio.Ratio = 16f / 9;
            aspectRatio.AddChild(new ColorRect { Color = new Color(48, 185, 164), CustomMinimumSize = new Vector2(240, 135), HorizontalSizeFlags = SizeFlags.Fill, VerticalSizeFlags = SizeFlags.Fill });
            return;
        }
        if (control is SplitContainer split)
        {
            split.AddChild(CreatePage("Primary pane", new Color(37, 70, 70)));
            split.AddChild(CreatePage("Secondary pane", new Color(64, 55, 37)));
            split.SplitOffset = 260;
            return;
        }
        if (control is FlowContainer flow)
        {
            for (var index = 1; index <= 9; index++) flow.AddChild(new Button { Text = $"Item {index}", CustomMinimumSize = new Vector2(92, 34) });
            return;
        }
        if (control is GridContainer grid)
        {
            grid.Columns = 3;
            for (var index = 1; index <= 9; index++) grid.AddChild(new Button { Text = index.ToString(CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(84, 44) });
            return;
        }
        if (control is BoxContainer box)
        {
            box.Separation = 8;
            box.AddChild(new Button { Text = "One", CustomMinimumSize = new Vector2(96, 38) });
            box.AddChild(new Button { Text = "Two", CustomMinimumSize = new Vector2(96, 38) });
            box.AddChild(new Button { Text = "Three", CustomMinimumSize = new Vector2(96, 38) });
            return;
        }
        if (control is Container container)
        {
            container.AddChild(new ColorRect { Position = new Vector2(24, 24), Size = new Vector2(180, 80), Color = new Color(48, 185, 164) });
            container.AddChild(new Label { Position = new Vector2(42, 50), Size = new Vector2(140, 28), Text = name });
            return;
        }
        if (control is Label label)
        {
            label.Text = $"{name}\nForma";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AutowrapMode = LabelAutowrapMode.Word;
            return;
        }
        if (control is BaseButton button)
        {
            button.Text = name;
            if (control is CheckBox checkBox) checkBox.ButtonPressed = true;
            return;
        }
        if (control is ProgressBar progressBar)
        {
            progressBar.Value = 62;
            return;
        }
        if (control is Slider slider)
        {
            slider.Value = 62;
            slider.TickCount = 6;
            return;
        }
        if (control is Forma.Range range)
        {
            range.Value = 62;
            return;
        }
        if (control is ColorRect colorRect)
        {
            colorRect.Color = new Color(48, 185, 164);
            return;
        }
        if (control is ReferenceRect referenceRect)
        {
            referenceRect.BorderColor = new Color(246, 185, 73);
            referenceRect.BorderWidth = 3;
            return;
        }
        if (control is Panel panel) panel.BackgroundColor = new Color(43, 52, 66);
    }

    private static Panel CreatePage(string name, Color color)
    {
        var page = new Panel { Name = name, BackgroundColor = color, CustomMinimumSize = new Vector2(240, 120) };
        page.AddChild(new Label { Text = name, Position = new Vector2(18, 18), Size = new Vector2(180, 28) });
        return page;
    }

    private static bool IsLargeSurface(Type type) =>
        typeof(Container).IsAssignableFrom(type) ||
        type == typeof(Tree) || type == typeof(ItemList) || type == typeof(TextEdit) ||
        type == typeof(CodeEdit) || type == typeof(RichTextLabel) || type == typeof(GraphEdit) ||
        type == typeof(ColorPicker) || type == typeof(VideoStreamPlayer) ||
        typeof(Popup).IsAssignableFrom(type);

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