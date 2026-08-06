// Copyright (c) 2026 Igor Hipolito Vieira
// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using var game = new Game();
_ = new GraphicsDeviceManager(game) { GraphicsProfile = GraphicsProfile.HiDef };
var manager = (IGraphicsDeviceManager)game.Services.GetService(typeof(IGraphicsDeviceManager));
manager.CreateDevice();
var graphicsDevice = game.GraphicsDevice;
#if THORVG
SvgThorvgBackendDefaults.Verify();
#else
SvgSkiaBackendDefaults.Verify();
#endif
var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
    "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='14' color='#ffffff'><rect width='20' height='14' rx='3' fill='currentColor'/></svg>"));
using var target = new RenderTarget2D(graphicsDevice, 50, 35, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
var hashes = new List<string>();

using (var context = new UIRenderContext(graphicsDevice, new Theme()))
{
    foreach (var scale in new[] { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f })
    {
        var width = (int)MathF.Ceiling(source.IntrinsicSize.X * scale);
        var height = (int)MathF.Ceiling(source.IntrinsicSize.Y * scale);
        context.DisplayScale = scale;
        Draw(context, new Rectangle(0, 0, width, height));
        Draw(context, new Rectangle(0, 0, width, height));
        var pixels = ReadPixels(target);
        Require(pixels.Any(pixel => pixel.A != 0), $"SVG output at {scale}x must be visible.");
        hashes.Add(Hash(pixels, width, height));
    }
    var before = context.SvgRasterDiagnostics;
    Draw(context, new Rectangle(0, 0, 50, 35));
    var after = context.SvgRasterDiagnostics;
    Require(after.Rasterizations == before.Rasterizations && after.Uploads == before.Uploads, "Warm SVG lookup must not rasterize or upload.");
}

using (var shared = new UIRenderContext(graphicsDevice, new Theme()) { DisplayScale = 2.5f })
{
    var before = shared.SvgRasterDiagnostics;
    Draw(shared, new Rectangle(0, 0, 50, 35));
    Require(shared.SvgRasterDiagnostics.Hits > before.Hits, "A second context must reuse the device SVG cache.");
}

graphicsDevice.Reset();
using (var reset = new UIRenderContext(graphicsDevice, new Theme()) { DisplayScale = 2.5f })
{
    Draw(reset, new Rectangle(0, 0, 50, 35));
    Draw(reset, new Rectangle(0, 0, 50, 35));
    Require(ReadPixels(target).Any(pixel => pixel.A != 0), "SVG output must recover after device reset.");
}

Console.WriteLine($"{SvgRuntime.Health.BackendId} SVG graphics lifecycle passed with hashes: {string.Join(' ', hashes)}");

// Contain mode: a wide SVG in a square container leaves top/bottom letterbox rows transparent.
// 40x8 SVG in 20x20 Contain box → scale=min(20/40,20/8)=0.5 → size=20x4 centered at y=8..11.
var wideSource = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(
    "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='8'><rect width='40' height='8' fill='#ff0000'/></svg>"));
using var containTarget = new RenderTarget2D(graphicsDevice, 20, 20, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
using (var containContext = new UIRenderContext(graphicsDevice, new Theme()))
{
    for (var frame = 0; frame < 2; frame++)
    {
        var img = new Image { Size = new Vector2(20, 20), ExpandMode = TextureRectExpandMode.IgnoreSize, Stretch = ImageStretch.Contain, ScalableSource = wideSource };
        graphicsDevice.SetRenderTarget(containTarget);
        graphicsDevice.Clear(Color.Transparent);
        containContext.Begin();
        img.DrawTree(containContext);
        containContext.End();
        graphicsDevice.SetRenderTarget(null);
    }
}
var containPixels = new Color[containTarget.Width * containTarget.Height];
containTarget.GetData(containPixels);
Require(
    containPixels.Select(pixel => pixel.PackedValue).Distinct().Skip(1).Any(),
    "Contain mode must produce both covered and uncovered target regions.");

Console.WriteLine("SVG contain-mode letterbox verified.");

void Draw(UIRenderContext context, Rectangle destination)
{
    graphicsDevice.SetRenderTarget(target);
    graphicsDevice.Clear(Color.Transparent);
    context.Begin();
    context.DrawScalableImage(source, destination, new Color(48, 185, 164));
    context.End();
    graphicsDevice.SetRenderTarget(null);
}

Color[] ReadPixels(RenderTarget2D renderTarget)
{
    var pixels = new Color[renderTarget.Width * renderTarget.Height];
    renderTarget.GetData(pixels);
    return pixels;
}

static string Hash(Color[] pixels, int width, int height)
{
    var bytes = new byte[width * height * sizeof(uint)];
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
        BitConverter.TryWriteBytes(bytes.AsSpan((y * width + x) * sizeof(uint), sizeof(uint)), pixels[y * 50 + x].PackedValue);
    return Convert.ToHexString(SHA256.HashData(bytes));
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
