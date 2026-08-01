// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;

namespace Forma.RenderTests
{
    [NonParallelizable]
    [Platform(Exclude = "MacOsX", Reason = "SDL graphics-device creation must run on the macOS main thread; compilation remains validated here.")]
    internal sealed class UIRenderTest : GraphicsDeviceTestFixtureBase
    {
        [Test]
        public void SubViewportContainer_RendersHostedUiIntoAnIsolatedTarget()
        {
            using var parentTarget = new RenderTarget2D(gd, 64, 64, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var ui = new UIContext();
            using var viewport = new SubViewportContainer
            {
                Position = new Vector2(8, 8),
                Size = new Vector2(32, 32),
                Stretch = true,
                ViewportClearColor = Color.Transparent
            };
            viewport.ViewportContext.Add(new ColorRect { Size = new Vector2(32, 32), Color = Color.Red });
            ui.Add(viewport);

            gd.SetRenderTarget(parentTarget);
            gd.Clear(Color.Blue);
            ui.Draw(gd);
            gd.SetRenderTarget(null);
            var pixels = new Color[64 * 64];
            parentTarget.GetData(pixels);

            Assert.That(pixels[16 + 16 * 64], Is.EqualTo(Color.Red));
            Assert.That(pixels[48 + 48 * 64], Is.EqualTo(Color.Blue));
        }

        [Test]
        public void UIContext_DisplayScaleRendersLogicalCoordinatesInPhysicalPixels()
        {
            using var renderTarget = new RenderTarget2D(gd, 64, 64, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var ui = new UIContext { DisplayScale = 2f, ViewportSize = new Vector2(32, 32) };
            var root = new ColorRect { Size = new Vector2(20, 20), Color = Color.Red, ClipContents = true };
            root.AddChild(new ColorRect { Position = new Vector2(16, 16), Size = new Vector2(16, 16), Color = Color.Lime });
            ui.Add(root);

            gd.SetRenderTarget(renderTarget);
            gd.Clear(Color.Blue);
            ui.Draw(gd);
            gd.SetRenderTarget(null);
            var pixels = new Color[64 * 64];
            renderTarget.GetData(pixels);

            Assert.That(pixels[30 + 30 * 64], Is.EqualTo(Color.Red));
            Assert.That(pixels[36 + 36 * 64], Is.EqualTo(Color.Lime));
            Assert.That(pixels[50 + 36 * 64], Is.EqualTo(Color.Blue));
        }

        [Test]
        public void UIContext_DisplayFontResolverUsesDensityAtlasAtLogicalSize()
        {
            using var logicalTexture = new Texture2D(gd, 1, 1);
            logicalTexture.SetData(new[] { Color.Red });
            using var densityTexture = new Texture2D(gd, 2, 2);
            densityTexture.SetData(new[] { Color.Lime, Color.Lime, Color.Lime, Color.Lime });
            var logicalFont = CreateSingleGlyphFont(logicalTexture, 1);
            var densityFont = CreateSingleGlyphFont(densityTexture, 2);
            using var renderTarget = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var ui = new UIContext
            {
                DisplayScale = 2f,
                ViewportSize = new Vector2(4, 4),
                DisplayFontResolver = (font, scale) => font == logicalFont && scale == 2f ? densityFont : null
            };
            ui.Add(new Label
            {
                Position = Vector2.One,
                Size = new Vector2(2, 2),
                Padding = new Thickness(0),
                Font = logicalFont,
                FontColor = Color.White,
                Text = "A"
            });

            gd.SetRenderTarget(renderTarget);
            gd.Clear(Color.Blue);
            ui.Draw(gd);
            gd.SetRenderTarget(null);
            var pixels = new Color[8 * 8];
            renderTarget.GetData(pixels);

            Assert.That(pixels[2 + 2 * 8], Is.EqualTo(Color.Lime));
            Assert.That(pixels[3 + 3 * 8], Is.EqualTo(Color.Lime));
            Assert.That(pixels[4 + 2 * 8], Is.EqualTo(Color.Blue));
        }

        [Test]
        public void CatalogDensityFontProvidesDoubleResolutionAtlas()
        {
            var logicalFont = content.Load<SpriteFont>("Fonts/Catalog");
            var densityFont = content.Load<SpriteFont>("Fonts/Catalog@2x");

            Assert.That(densityFont.LineSpacing, Is.InRange(logicalFont.LineSpacing * 1.9f, logicalFont.LineSpacing * 2.1f));
            Assert.That(densityFont.Texture.Width, Is.GreaterThan(logicalFont.Texture.Width));
            Assert.That(densityFont.Texture.Height, Is.GreaterThan(logicalFont.Texture.Height));
            Assert.That(logicalFont.DefaultCharacter, Is.EqualTo('?'));
            Assert.That(densityFont.DefaultCharacter, Is.EqualTo('?'));
            Assert.DoesNotThrow(() => logicalFont.MeasureString("café 🔎"));
            Assert.DoesNotThrow(() => densityFont.MeasureString("café 🔎"));

            using var renderTarget = new RenderTarget2D(gd, 256, 64);
            using var ui = new UIContext
            {
                DisplayScale = 2f,
                ViewportSize = new Vector2(128, 32),
                DisplayFontResolver = (font, scale) => font == logicalFont && scale > 1f ? densityFont : null
            };
            ui.Add(new LineEdit { Font = logicalFont, Text = "café 🔎", Size = new Vector2(128, 32) });

            gd.SetRenderTarget(renderTarget);
            Assert.DoesNotThrow(() => ui.Draw(gd));
            gd.SetRenderTarget(null);
        }

        private static SpriteFont CreateSingleGlyphFont(Texture2D texture, int glyphSize)
        {
            return new SpriteFont(
                texture,
                new List<Rectangle> { new Rectangle(0, 0, glyphSize, glyphSize) },
                new List<Rectangle> { new Rectangle(0, 0, glyphSize, glyphSize) },
                new List<char> { 'A' },
                glyphSize,
                0,
                new List<Vector3> { new Vector3(0, glyphSize, 0) },
                null);
        }

        [Test]
        [Ignore("Requires a drawable desktop graphics context; run from the interactive visual test runner.")]
        public void ClipContents_PreventsChildRenderingOutsideItsParent()
        {
            using var renderTarget = new RenderTarget2D(gd, 32, 32);
            using var ui = new UIContext();
            var root = new Panel
            {
                Size = new Vector2(20, 20),
                BackgroundColor = Color.Red,
                BorderWidth = 0,
                ClipContents = true
            };
            root.AddChild(new ColorRect
            {
                Position = new Vector2(16, 16),
                Size = new Vector2(16, 16),
                Color = Color.Lime
            });
            ui.Add(root);

            gd.SetRenderTarget(renderTarget);
            gd.Clear(Color.Blue);
            ui.Draw(gd);
            gd.SetRenderTarget(null);
            var pixels = new Color[32 * 32];
            renderTarget.GetData(pixels);

            Assert.That(pixels[18 + 18 * 32], Is.EqualTo(Color.Lime));
            Assert.That(pixels[25 + 18 * 32], Is.EqualTo(Color.Blue));
        }
    }
}
