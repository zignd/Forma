// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using var context = new UIContext();
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
    var drawRoot = new VBoxContainer { Size = new Vector2(320, 180) };
    drawRoot.AddChild(new Label { Font = spriteFont, Text = "Packed SpriteFont" });
    drawRoot.AddChild(new Button { Font = spriteFont, Text = "Continue" });
#if FORMA_DYNAMIC_TEXT
    drawRoot.AddChild(new Label { Text = "Automatic dynamic default" });
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
#if FORMA_DYNAMIC_TEXT
    spriteFontDrawSucceeded &= drawContext.DynamicGlyphDiagnostics.Misses > 0;
#endif
}
#endif
#if FORMA_DYNAMIC_TEXT
#if FORMA_DYNAMIC_TEXT_DEFAULT
if (context.Theme.FontFamily?.Primary is not DynamicUIFont) return 1;
DynamicTextDefaults.Install(Path.Combine(AppContext.BaseDirectory, "Fonts", "Inter_Regular.ttf"), 18);
using (var replacementContext = new UIContext())
    if (replacementContext.Theme.FontFamily?.Primary is not DynamicUIFont replacement || replacement.Size != 18) return 1;
#else
if (context.Theme.FontFamily is not null) return 1;
#endif
using var face = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
var dynamicLayout = new TextLayoutEngine().Layout(new DynamicUIFont(face, 18), "Forma");
if (dynamicLayout.Runs.Count == 0 || dynamicLayout.Runs.SelectMany(run => run.Glyphs).Any() == false) return 1;
if (face.RasterizeCharacter('A', 18).Pixels.Length == 0) return 1;
if (typeof(DynamicUIFont).Assembly.GetName().Name != "Forma.DynamicText") return 1;
var outputDirectory = Path.GetFullPath(AppContext.BaseDirectory);
var packagedModules = NativeModuleInspector.GetLoadedModulePaths()
    .Select(Path.GetFullPath)
    .Where(fileName => fileName.StartsWith(outputDirectory, StringComparison.Ordinal))
    .Select(fileName => Path.GetFileName(fileName) ?? string.Empty)
    .ToArray();
if (!packagedModules.Any(fileName => fileName.Contains("freetype", StringComparison.OrdinalIgnoreCase)) ||
    !packagedModules.Any(fileName => fileName.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase))) return 1;
#else
if (context.Theme.FontFamily is not null) return 1;
#endif
#if !FORMA_CORE_ONLY
using var video = new VideoStreamPlayer();
Action<UIContext, GraphicsDevice> drawingSurface = (drawingContext, graphicsDevice) => drawingContext.Draw(graphicsDevice);
_ = drawingSurface;

return context.Roots.Count == 1 && spriteFontDrawSucceeded && VideoStreamPlayer.RuntimeCapabilities != VideoPlaybackCapabilities.None ? 0 : 1;
#else
return context.Roots.Count == 1 && spriteFontDrawSucceeded ? 0 : 1;
#endif