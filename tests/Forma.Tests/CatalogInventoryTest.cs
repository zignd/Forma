// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using Forma.Catalog;

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

        Assert.That(storyNames, Is.Unique);
        Assert.That(storyNames, Is.EqualTo(expectedTypes));
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
                "--watch-effect", effectPath,
            ]);

            Assert.That(options.OutputPath, Is.EqualTo("catalog.json"));
            Assert.That(options.FrameCount, Is.EqualTo(3));
            Assert.That(options.DisplayScale, Is.EqualTo(2));
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