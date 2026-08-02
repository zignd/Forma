// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using FreeTypeSharp;
using HarfBuzzSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Buffer = HarfBuzzSharp.Buffer;

namespace Forma.DynamicTextSpike;

internal static class Program
{
    public static int Main()
    {
        try
        {
            using var game = new DynamicTextSpikeGame();
            game.Run();
            Console.WriteLine(game.Result);
            return game.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

internal sealed class DynamicTextSpikeGame : Game
{
    private const int PixelSize = 32;
    private readonly GraphicsDeviceManager _graphics;

    public DynamicTextSpikeGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 64,
            PreferredBackBufferHeight = 64,
            SynchronizeWithVerticalRetrace = false,
        };
        IsFixedTimeStep = false;
        Window.Title = "Forma Dynamic Text Spike";
    }

    public bool Succeeded { get; private set; }
    public string Result { get; private set; } = "Dynamic text spike did not complete.";

    protected override void LoadContent()
    {
        var fontDirectory = Path.Combine(AppContext.BaseDirectory, "Fonts");
        using var inter = new NativeFontFace(Path.Combine(fontDirectory, "Inter_Regular.ttf"), PixelSize);
        using var notoArabic = new NativeFontFace(Path.Combine(fontDirectory, "NotoSansArabic_Variable.ttf"), PixelSize);
        var latin = inter.Shape("Forma office");
        var arabic = notoArabic.Shape("مرحبا بالعالم");

        Require(latin.Direction == Direction.LeftToRight, "Latin shaping did not resolve left-to-right direction.");
        Require(arabic.Direction == Direction.RightToLeft, "Arabic shaping did not resolve right-to-left direction.");
        Require(latin.Glyphs.All(glyph => glyph.Codepoint != 0), "Latin shaping produced a missing glyph.");
        Require(arabic.Glyphs.All(glyph => glyph.Codepoint != 0), "Arabic shaping produced a missing glyph.");
        Require(latin.Glyphs.All(glyph => glyph.XAdvance > 0), "Latin shaping produced a non-advancing glyph.");
        Require(arabic.Glyphs[0].Cluster > arabic.Glyphs[^1].Cluster, "Arabic shaping did not produce descending right-to-left clusters.");

        var atlas = new AlphaAtlas(256, 256);
        atlas.Add(inter, latin.Glyphs);
        atlas.Add(notoArabic, arabic.Glyphs);
        using var texture = new Texture2D(GraphicsDevice, atlas.Width, atlas.Height, false, SurfaceFormat.Alpha8);
        texture.SetData(atlas.Pixels);
        var readback = new byte[atlas.Pixels.Length];
        texture.GetData(readback);
        Require(readback.AsSpan().SequenceEqual(atlas.Pixels), "Alpha8 texture upload did not round-trip exactly.");

        using var renderTarget = new RenderTarget2D(GraphicsDevice, atlas.Width, atlas.Height, false, SurfaceFormat.Color, DepthFormat.None);
        using var spriteBatch = new SpriteBatch(GraphicsDevice);
        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.Clear(Color.Transparent);
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity);
        spriteBatch.Draw(texture, Vector2.Zero, Color.White);
        spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        var renderedPixels = new Color[atlas.Width * atlas.Height];
        renderTarget.GetData(renderedPixels);
        var coverageInColor = renderedPixels.Any(pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
        var renderedCoverage = renderedPixels.Select(pixel => coverageInColor ? pixel.R : pixel.A).ToArray();
        Require(renderedCoverage.AsSpan().SequenceEqual(atlas.Pixels), "Alpha8 atlas rendering did not preserve glyph coverage.");

        var fingerprint = CreateFingerprint(latin, arabic, readback, renderedCoverage);
        Succeeded = true;
        Result = $"Dynamic text spike ({GetRuntimeName()}): Latin={latin.Glyphs.Count}, Arabic={arabic.Glyphs.Count}, AtlasGlyphs={atlas.GlyphCount}, CoverageChannel={(coverageInColor ? "red" : "alpha")}, SHA256={fingerprint}";
        Exit();
        base.LoadContent();
    }

    private static string CreateFingerprint(ShapedRun latin, ShapedRun arabic, byte[] atlasPixels, byte[] renderedCoverage)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendRun(hash, latin);
        AppendRun(hash, arabic);
        hash.AppendData(atlasPixels);
        hash.AppendData(renderedCoverage);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendRun(IncrementalHash hash, ShapedRun run)
    {
        hash.AppendData(Encoding.UTF8.GetBytes($"{run.Text}\n{run.Direction}\n"));
        foreach (var glyph in run.Glyphs)
            hash.AppendData(Encoding.UTF8.GetBytes($"{glyph.Codepoint}:{glyph.Cluster}:{glyph.XAdvance}:{glyph.XOffset}:{glyph.YOffset}\n"));
    }

    private static string GetRuntimeName() => typeof(Game).Assembly.GetName().Name ?? "unknown";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal sealed unsafe class NativeFontFace : IDisposable
{
    private readonly byte[] _bytes;
    private readonly GCHandle _pin;
    private readonly Blob _harfBuzzBlob;
    private readonly Face _harfBuzzFace;
    private readonly Font _harfBuzzFont;
    private FT_LibraryRec_* _library;
    private FT_FaceRec_* _face;

    public NativeFontFace(string path, uint pixelSize)
    {
        _bytes = File.ReadAllBytes(path);
        _pin = GCHandle.Alloc(_bytes, GCHandleType.Pinned);
        FT_LibraryRec_* library;
        ThrowIfError(FT.FT_Init_FreeType(&library), "initialize FreeType");
        _library = library;
        FT_FaceRec_* face;
        ThrowIfError(FT.FT_New_Memory_Face(_library, (byte*)_pin.AddrOfPinnedObject(), _bytes.Length, 0, &face), "load font face");
        _face = face;
        ThrowIfError(FT.FT_Set_Pixel_Sizes(_face, 0, pixelSize), "set pixel size");

        _harfBuzzBlob = new Blob(_pin.AddrOfPinnedObject(), _bytes.Length, MemoryMode.ReadOnly);
        _harfBuzzFace = new Face(_harfBuzzBlob, 0);
        _harfBuzzFont = new Font(_harfBuzzFace);
        _harfBuzzFont.SetFunctionsOpenType();
        _harfBuzzFont.SetScale((int)pixelSize * 64, (int)pixelSize * 64);
    }

    public ShapedRun Shape(string text)
    {
        using var buffer = new Buffer();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        _harfBuzzFont.Shape(buffer);
        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        var glyphs = new ShapedGlyph[infos.Length];
        for (var index = 0; index < infos.Length; index++)
        {
            glyphs[index] = new ShapedGlyph(
                infos[index].Codepoint,
                infos[index].Cluster,
                positions[index].XAdvance,
                positions[index].XOffset,
                positions[index].YOffset);
        }
        return new ShapedRun(text, buffer.Direction, glyphs);
    }

    public GlyphBitmap Rasterize(uint glyphId)
    {
        ThrowIfError(FT.FT_Load_Glyph(_face, glyphId, FT_LOAD.FT_LOAD_DEFAULT), $"load glyph {glyphId}");
        ThrowIfError(FT.FT_Render_Glyph(_face->glyph, FT_Render_Mode_.FT_RENDER_MODE_NORMAL), $"render glyph {glyphId}");
        var bitmap = _face->glyph->bitmap;
        var width = checked((int)bitmap.width);
        var height = checked((int)bitmap.rows);
        var pixels = new byte[checked(width * height)];
        var sourcePitch = Math.Abs(bitmap.pitch);
        for (var row = 0; row < height; row++)
        {
            var sourceRow = bitmap.pitch >= 0 ? row : height - row - 1;
            new ReadOnlySpan<byte>(bitmap.buffer + sourceRow * sourcePitch, width).CopyTo(pixels.AsSpan(row * width, width));
        }
        return new GlyphBitmap(width, height, pixels);
    }

    public void Dispose()
    {
        _harfBuzzFont.Dispose();
        _harfBuzzFace.Dispose();
        _harfBuzzBlob.Dispose();
        if (_face != null) FT.FT_Done_Face(_face);
        if (_library != null) FT.FT_Done_FreeType(_library);
        if (_pin.IsAllocated) _pin.Free();
    }

    private static void ThrowIfError(FT_Error error, string operation)
    {
        if (error != FT_Error.FT_Err_Ok) throw new InvalidOperationException($"Failed to {operation}: {error}.");
    }
}

internal sealed class AlphaAtlas
{
    private const int Padding = 1;
    private readonly HashSet<(NativeFontFace Font, uint GlyphId)> _glyphs = new HashSet<(NativeFontFace, uint)>();
    private int _cursorX = Padding;
    private int _cursorY = Padding;
    private int _rowHeight;

    public AlphaAtlas(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[checked(width * height)];
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public int GlyphCount => _glyphs.Count;

    public void Add(NativeFontFace font, IEnumerable<ShapedGlyph> glyphs)
    {
        foreach (var glyph in glyphs)
        {
            if (!_glyphs.Add((font, glyph.Codepoint))) continue;
            var bitmap = font.Rasterize(glyph.Codepoint);
            if (bitmap.Width == 0 || bitmap.Height == 0) continue;
            if (_cursorX + bitmap.Width + Padding > Width)
            {
                _cursorX = Padding;
                _cursorY += _rowHeight + Padding;
                _rowHeight = 0;
            }
            if (_cursorY + bitmap.Height + Padding > Height)
                throw new InvalidOperationException("The dynamic text spike exceeded its single atlas page.");
            for (var row = 0; row < bitmap.Height; row++)
                bitmap.Pixels.AsSpan(row * bitmap.Width, bitmap.Width).CopyTo(Pixels.AsSpan((_cursorY + row) * Width + _cursorX, bitmap.Width));
            _cursorX += bitmap.Width + Padding;
            _rowHeight = Math.Max(_rowHeight, bitmap.Height);
        }
    }
}

internal sealed record ShapedRun(string Text, Direction Direction, IReadOnlyList<ShapedGlyph> Glyphs);
internal readonly record struct ShapedGlyph(uint Codepoint, uint Cluster, int XAdvance, int XOffset, int YOffset);
internal readonly record struct GlyphBitmap(int Width, int Height, byte[] Pixels);