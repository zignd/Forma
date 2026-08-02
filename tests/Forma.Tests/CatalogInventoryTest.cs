// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Forma.Catalog;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Tests;

public sealed class CatalogInventoryTest
{
    [Test]
    public void StoryCatalogIncludesEveryConstructiblePublicControl()
    {
        var explicitlyConstructedTypes = new[]
        {
            typeof(BoxContainer),
            typeof(FlowContainer),
            typeof(ScrollBar),
            typeof(Slider),
            typeof(SplitContainer),
        };
        var expectedTypes = new[] { typeof(Control).Assembly, typeof(VideoStreamPlayer).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsPublic && !type.IsAbstract && typeof(Control).IsAssignableFrom(type))
            .Where(type => type.GetConstructor(Type.EmptyTypes) != null || explicitlyConstructedTypes.Contains(type))
            .Select(type => type.Name)
            .OrderBy(name => name)
            .ToArray();

        var storyNames = StoryCatalog.Create(null)
            .Select(story => story.Name)
            .OrderBy(name => name)
            .ToArray();
        var controlStoryNames = storyNames.Intersect(expectedTypes).OrderBy(name => name).ToArray();

        Assert.That(storyNames, Is.Unique);
        Assert.That(controlStoryNames, Is.EqualTo(expectedTypes));
        Assert.That(storyNames, Does.Contain("Complete icon inventory"));
        Assert.That(storyNames, Does.Contain("Override and suppression"));
        Assert.That(storyNames, Does.Contain("Atlas diagnostics"));
        Assert.That(storyNames, Does.Contain("Dynamic Sizes"));
        Assert.That(storyNames, Does.Contain("Display Density"));
        Assert.That(storyNames, Does.Contain("Fallback Chain"));
        Assert.That(storyNames, Does.Contain("Shaping and Features"));
        Assert.That(storyNames, Does.Contain("Bidirectional Text"));
        Assert.That(storyNames, Does.Contain("Wrapping and Selection"));
        Assert.That(storyNames, Does.Contain("SpriteFont Compatibility"));
        Assert.That(storyNames, Does.Contain("Atlas Inspector"));
        Assert.That(storyNames, Does.Contain("Failure States"));
    }

    [TestCase("Display Density", "densityStatus")]
    [TestCase("Fallback Chain", "fallbackPreview")]
    [TestCase("Wrapping and Selection", "wrappingDiagnostics")]
    [TestCase("Atlas Inspector", "atlasStatus")]
    public void TypographyDiagnosticStoriesExposeTheirInteractiveSurface(string storyName, string expectedControlName)
    {
        var story = StoryCatalog.Create(null).Single(story => story.Name == storyName);
        var root = story.Factory();

        Assert.Multiple(() =>
        {
            Assert.That(story.Category, Is.EqualTo("Typography"));
            Assert.That(root.Children.SelectMany(Flatten).Select(control => control.Name), Does.Contain(expectedControlName));
        });
    }

    [Test]
    public void GraphEditStoryFillsTheAvailablePreviewSurface()
    {
        var font = CreateTestFont();
        var stories = StoryCatalog.Create(null);
        var shell = new CatalogShell(stories, font, font) { Size = new Vector2(1600, 900) };
        using var context = new UIContext();
        context.Add(shell);
        Assert.That(shell.SelectStory(nameof(GraphEdit)), Is.True);

        context.Layout();

        var graphEdit = Flatten(shell).OfType<GraphEdit>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(graphEdit.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(graphEdit.Size, Is.EqualTo(graphEdit.Parent.Size));
            Assert.That(graphEdit.Size.X, Is.GreaterThan(graphEdit.CustomMinimumSize.X));
            Assert.That(graphEdit.Size.Y, Is.GreaterThan(graphEdit.CustomMinimumSize.Y));
        });
    }

    [Test]
    public void TypographyStoriesUseRealFallbackRunsAndKeepLogicalBoundsAcrossDensityChanges()
    {
        using var interFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
        using var arabicFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
        using var devanagariFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansDevanagari_Subset.ttf");
        using var hebrewFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansHebrew_Subset.ttf");
        using var cjkFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansCJK_Subset.ttf");
        using var emojiFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoEmoji_Subset.ttf");
        using var ui = new UIContext();
        var selectedDensity = 1f;
        UIFont CreateFont(string family, float size, System.Collections.Generic.IReadOnlyList<UIFontVariationCoordinate> variations)
        {
            variations ??= Array.Empty<UIFontVariationCoordinate>();
            if (family == "Noto Sans Arabic") return new DynamicUIFont(arabicFace, size, UIFontHinting.Default, variations, interFace, hebrewFace, devanagariFace, cjkFace, emojiFace);
            if (family == "Noto Sans SC") return new DynamicUIFont(cjkFace, size, UIFontHinting.Default, variations, interFace, arabicFace, hebrewFace, devanagariFace, emojiFace);
            return new DynamicUIFont(interFace, size, UIFontHinting.Default, variations, arabicFace, hebrewFace, devanagariFace, cjkFace, emojiFace);
        }
        var stories = StoryCatalog.Create(null, scale => { selectedDensity = scale; ui.DisplayScale = scale; }, CreateFont);
        var fallbackStory = stories.Single(story => story.Name == "Fallback Chain");
        var fallbackRoot = fallbackStory.Factory();
        ui.Add(fallbackRoot);
        fallbackStory.Attached(fallbackRoot);
        var diagnostics = (Label)fallbackRoot.Children[2];
        var densityStory = stories.Single(story => story.Name == "Display Density");
        var densityRoot = densityStory.Factory();
        ui.Add(densityRoot);
        densityStory.Attached(densityRoot);
        var sample = (Label)densityRoot.Children[2].Children[0];
        var logicalSizeAtOneX = sample.GetMinimumSize();
        ui.DisplayScale = 2;
        selectedDensity = 2;
        var shapingStory = stories.Single(story => story.Name == "Shaping and Features");
        var shapingRoot = shapingStory.Factory();
        ui.Add(shapingRoot);
        shapingStory.Attached(shapingRoot);
        var shapingControls = shapingRoot.Children[0];
        var shapingDiagnostics = (Label)shapingRoot.Children[3];
        var enabledFeatureDiagnostics = shapingDiagnostics.Text;
        ((CheckBox)shapingControls.Children[0]).SetPressed(false);
        ((Slider)shapingControls.Children[2]).Value = 800;
        var bidiStory = stories.Single(story => story.Name == "Bidirectional Text");
        var bidiRoot = bidiStory.Factory();
        ui.Add(bidiRoot);
        bidiStory.Attached(bidiRoot);
        var bidiDiagnostics = (Label)bidiRoot.Children[2];
        var compatibilityStory = stories.Single(story => story.Name == "SpriteFont Compatibility");
        var compatibilityRoot = compatibilityStory.Factory();
        ui.Add(compatibilityRoot);
        var compatibilityFont = CreateTestFont();
        FontApplicator.Apply(compatibilityRoot, compatibilityFont, compatibilityFont);
        compatibilityStory.Attached(compatibilityRoot);
        var compatibilityDiagnostics = (Label)compatibilityRoot.Children[3];
        var failureStory = stories.Single(story => story.Name == "Failure States");
        var failureRoot = failureStory.Factory();
        ui.Add(failureRoot);
        failureStory.Attached(failureRoot);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Text, Does.Contain("Noto Sans Arabic"));
            Assert.That(diagnostics.Text, Does.Contain("Noto Sans Devanagari"));
            Assert.That(diagnostics.Text, Does.Contain("Noto Sans SC"));
            Assert.That(selectedDensity, Is.EqualTo(2));
            Assert.That(sample.GetMinimumSize(), Is.EqualTo(logicalSizeAtOneX));
            Assert.That(shapingDiagnostics.Text, Is.Not.EqualTo(enabledFeatureDiagnostics));
            Assert.That(shapingDiagnostics.Text, Does.Contain("wght 800"));
            Assert.That(bidiDiagnostics.Text, Does.Contain("Logical"));
            Assert.That(bidiDiagnostics.Text, Does.Contain("Visual"));
            Assert.That(bidiDiagnostics.Text, Does.Contain("RightToLeft"));
            Assert.That(compatibilityDiagnostics.Text, Does.Contain("metric differences intentional"));
            Assert.That(((Label)failureRoot.Children[1]).Text, Does.Contain("Missing face"));
            Assert.That(((Label)failureRoot.Children[2]).Text, Does.Contain("Malformed asset"));
            Assert.That(((Label)failureRoot.Children[3]).Text, Does.Contain(".notdef"));
            Assert.That(((Label)failureRoot.Children[4]).Text, Does.Contain("Atlas budget"));
        });
    }

    [Test]
    public void EveryTypographyStoryIsFocusableAndStableAcrossResizeAndDisplayScale()
    {
        using var interFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
        using var arabicFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
        using var devanagariFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansDevanagari_Subset.ttf");
        using var hebrewFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansHebrew_Subset.ttf");
        using var cjkFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansCJK_Subset.ttf");
        using var emojiFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoEmoji_Subset.ttf");
        var compatibilityFont = CreateTestFont();
        UIFont CreateFont(string family, float size, System.Collections.Generic.IReadOnlyList<UIFontVariationCoordinate> variations)
        {
            variations ??= Array.Empty<UIFontVariationCoordinate>();
            if (family == "Noto Sans Arabic") return new DynamicUIFont(arabicFace, size, UIFontHinting.Default, variations, interFace, hebrewFace, devanagariFace, cjkFace, emojiFace);
            if (family == "Noto Sans SC") return new DynamicUIFont(cjkFace, size, UIFontHinting.Default, variations, interFace, arabicFace, hebrewFace, devanagariFace, emojiFace);
            return new DynamicUIFont(interFace, size, UIFontHinting.Default, variations, arabicFace, hebrewFace, devanagariFace, cjkFace, emojiFace);
        }
        var stories = StoryCatalog.Create(null, createDynamicFont: CreateFont).Where(story => story.Category == "Typography").ToArray();

        Assert.That(stories, Has.Length.EqualTo(9));
        foreach (var story in stories)
        {
            using var context = new UIContext { ViewportSize = new Vector2(720, 450), DisplayScale = 1 };
            var root = story.Factory();
            FontApplicator.Apply(root, CreateFont("Inter", 16, null), CreateFont("Inter", 15, null), compatibilityFont);
            context.Add(root);
            story.Attached(root);
            context.Layout();
            var minimumAtOneX = root.GetMinimumSize();

            context.ViewportSize = new Vector2(420, 720);
            root.Size = context.ViewportSize;
            context.DisplayScale = 2;
            context.Layout();

            Assert.Multiple(() =>
            {
                Assert.That(Flatten(root).Any(control => control.FocusMode != FocusMode.None), Is.True, $"{story.Name} must expose keyboard-focusable interaction.");
                Assert.That(root.GetMinimumSize(), Is.EqualTo(minimumAtOneX), $"{story.Name} logical minimum size must not depend on display scale.");
                Assert.That(root.Size.X, Is.GreaterThan(0), $"{story.Name} must remain laid out in a narrow viewport.");
                Assert.That(root.Size.Y, Is.GreaterThan(0), $"{story.Name} must remain laid out in a narrow viewport.");
            });
        }
    }

    private static System.Collections.Generic.IEnumerable<Control> Flatten(Control control)
    {
        yield return control;
        foreach (var child in control.Children.SelectMany(Flatten)) yield return child;
    }

    private static SpriteFont CreateTestFont()
    {
        var characters = Enumerable.Range(32, 95).Select(value => (char)value).ToList();
        characters.Add('·');
        var bounds = characters.Select(_ => new Rectangle(0, 0, 8, 16)).ToList();
        var cropping = characters.Select(_ => new Rectangle(0, 0, 8, 16)).ToList();
        var kerning = characters.Select(_ => new Vector3(0, 8, 0)).ToList();
        return (SpriteFont)Activator.CreateInstance(
            typeof(SpriteFont),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object[] { null, bounds, cropping, characters, 16, 0f, kerning, null },
            null);
    }

    [Test]
    public void DynamicSizesStoryExposesLiveTextFamilyAndSizeControls()
    {
        var story = StoryCatalog.Create(null).Single(story => story.Name == "Dynamic Sizes");
        var root = story.Factory();
        story.Attached(root);
        var controls = root.Children[0];

        Assert.Multiple(() =>
        {
            Assert.That(story.Category, Is.EqualTo("Typography"));
            Assert.That(controls.Children.Select(control => control.Name), Is.EqualTo(new[] { "liveText", "fontFamily", "fontSize" }));
            Assert.That(((Slider)controls.Children[2]).MinValue, Is.EqualTo(8));
            Assert.That(((Slider)controls.Children[2]).MaxValue, Is.EqualTo(96));
            Assert.That(root.Children[2].Name, Is.EqualTo("preview"));
        });
    }

    [Test]
    public void WrappingSelectionStoryProjectsEditorSelectionThroughRetainedLayout()
    {
        using var interFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/Inter_Regular.ttf");
        using var arabicFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansArabic_Variable.ttf");
        using var devanagariFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansDevanagari_Subset.ttf");
        using var hebrewFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansHebrew_Subset.ttf");
        using var cjkFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoSansCJK_Subset.ttf");
        using var emojiFace = UIFontFace.FromProjectFile(TestContext.CurrentContext.TestDirectory, "Fonts/NotoEmoji_Subset.ttf");
        using var ui = new UIContext();
        UIFont CreateFont(string _, float size, System.Collections.Generic.IReadOnlyList<UIFontVariationCoordinate> variations) =>
            new DynamicUIFont(interFace, size, UIFontHinting.Default, variations ?? Array.Empty<UIFontVariationCoordinate>(), arabicFace, hebrewFace, devanagariFace, cjkFace, emojiFace);
        var story = StoryCatalog.Create(null, createDynamicFont: CreateFont).Single(item => item.Name == "Wrapping and Selection");
        var root = story.Factory();
        ui.Add(root);
        story.Attached(root);
        var editor = (TextEdit)root.Children[0];
        editor.Select(0, 5);
        ((Slider)root.Children[1].Children[0]).Value = 440;
        var preview = (Label)root.Children[3];
        var diagnostics = (Label)root.Children[4];

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Text, Does.Contain("selection 0..5"));
            Assert.That(diagnostics.Text, Does.Contain("range"));
            Assert.That(preview.Children.OfType<ColorRect>().Count(overlay => overlay.Name.StartsWith("selectionOverlay") && overlay.Visible), Is.GreaterThan(0));
            Assert.That(preview.Children.Single(overlay => overlay.Name == "caretOverlay").Visible, Is.True);
        });
    }

    [Test]
    public void CatalogShellModeToggleSelectsDynamicAndCompatibilityServices()
    {
        var selected = true;
        var font = CreateTestFont();
        var shell = new CatalogShell(
            new[] { new ComponentStory("Test", "Mode", "Mode", () => new Label { Text = "Mode" }) },
            font,
            font,
            enabled => selected = enabled);
        var toggle = (CheckBox)shell.Children[0].Children.Single(control => control.Name == "dynamicTextMode");

        toggle.SetPressed(false);

        Assert.Multiple(() =>
        {
            Assert.That(shell.DynamicTextEnabled, Is.False);
            Assert.That(selected, Is.False);
        });
    }

    [Test]
    public void VideoStreamPlayerStoryUsesOptionalMediaControl()
    {
        var story = StoryCatalog.Create(null).Single(story => story.Name == nameof(VideoStreamPlayer));

        using var player = (VideoStreamPlayer)story.Factory();

        Assert.That(story.Category, Is.EqualTo("Media"));
        Assert.That(player.Autoplay, Is.True);
        Assert.That(player.Loop, Is.True);
        Assert.That(player.Expand, Is.True);
        Assert.That(player.Volume, Is.EqualTo(.75f));
        Assert.That(player.Stream, Is.Null);
    }

    [Test]
    public void MetricsOptionsPreserveScaleAndWatchedEffectArguments()
    {
        var effectPath = Path.GetTempFileName();
        try
        {
            var options = CatalogMetricsOptions.Parse([
                "--metrics", "catalog.json",
                "--frames", "3",
                "--display-scale", "2",
                "--story", "Complete icon inventory",
                "--viewport-width", "720",
                "--viewport-height", "480",
                "--watch-effect", effectPath,
            ]);

            Assert.That(options.OutputPath, Is.EqualTo("catalog.json"));
            Assert.That(options.FrameCount, Is.EqualTo(3));
            Assert.That(options.DisplayScale, Is.EqualTo(2));
            Assert.That(options.StoryName, Is.EqualTo("Complete icon inventory"));
            Assert.That(options.ViewportWidth, Is.EqualTo(720));
            Assert.That(options.ViewportHeight, Is.EqualTo(480));
            Assert.That(options.WatchedEffectPath, Is.EqualTo(effectPath));
        }
        finally
        {
            File.Delete(effectPath);
        }
    }

    [Test]
    public void MetricsOptionsRejectMissingWatchedEffect()
    {
        Assert.Throws<ArgumentException>(() =>
            CatalogMetricsOptions.Parse(["--watch-effect", Path.GetRandomFileName()]));
    }
}