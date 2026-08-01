// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text.Json;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Forma.Catalog;

public sealed class CatalogGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly UIContext _ui;
    private CatalogShell _catalog;
    private SpriteFont _font;
    private SpriteFont _displayFont;
    private SpriteFont _codeFont;
    private SpriteFont _displayCodeFont;
    private Texture2D _catalogTexture;
    private readonly CatalogMetricsOptions _metricsOptions;
    private readonly VertexPositionColorTexture[] _hotReloadVertices =
    {
        new(new Vector3(0.82f, -0.92f, 0), Color.White, new Vector2(0, 1)),
        new(new Vector3(0.9f, -0.72f, 0), Color.White, new Vector2(0.5f, 0)),
        new(new Vector3(0.98f, -0.92f, 0), Color.White, new Vector2(1, 1)),
    };
    private CatalogEffectHotReloadService _hotReload;
    private float _displayScale = 1;
    private int _storyCount;
    private int _renderedFrames;

    public CatalogGame(CatalogMetricsOptions metricsOptions = null)
    {
        _metricsOptions = metricsOptions;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1440,
            PreferredBackBufferHeight = 900,
        };
        _ui = new UIContext { Theme = CreateTheme() };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Forma Catalog";
        Window.TextInput += OnTextInput;
    }

    protected override void LoadContent()
    {
        _font = Content.Load<SpriteFont>("Fonts/Catalog");
        _displayFont = Content.Load<SpriteFont>("Fonts/Catalog@2x");
        _codeFont = Content.Load<SpriteFont>("Fonts/CatalogCode");
        _displayCodeFont = Content.Load<SpriteFont>("Fonts/CatalogCode@2x");
        _catalogTexture = CreateCatalogTexture();
        Window.Title = $"Forma Catalog - {CatalogBackend.Name}";
        _ui.TooltipFont = _font;
        _ui.DisplayFontResolver = (font, scale) => scale > 1f
            ? ReferenceEquals(font, _font) ? _displayFont
            : ReferenceEquals(font, _codeFont) ? _displayCodeFont
            : null
            : null;
        var stories = StoryCatalog.Create(_catalogTexture);
        _storyCount = stories.Count;
        _catalog = new CatalogShell(stories, _font, _codeFont);
        _ui.Add(_catalog);
        if (_metricsOptions?.WatchedEffectPath != null)
        {
            _hotReload = new CatalogEffectHotReloadService(
                GraphicsDevice,
                Path.GetFullPath(_metricsOptions.WatchedEffectPath));
        }
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        var viewport = GraphicsDevice.Viewport;
        _displayScale = GetDisplayScale(viewport);
        _ui.DisplayScale = _displayScale;
        _ui.ViewportSize = new Vector2(viewport.Width / _displayScale, viewport.Height / _displayScale);
        if (_catalog != null) _catalog.Size = _ui.ViewportSize;
        _ui.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_ui.Theme.BackgroundColor);
        _ui.Draw(GraphicsDevice);
        if (_hotReload != null)
        {
            _hotReload.Update();
            var effect = _hotReload.Current;
            effect.Parameters["MatrixTransform"]?.SetValue(Matrix.Identity);
            effect.Parameters["Texture"]?.SetValue(_catalogTexture);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _hotReloadVertices,
                    0,
                    1);
            }
        }
        base.Draw(gameTime);

        if (_metricsOptions?.OutputPath != null && ++_renderedFrames >= _metricsOptions.FrameCount)
        {
            WriteMetrics();
            Exit();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var window = Window;
            if (window != null) window.TextInput -= OnTextInput;
            _hotReload?.Dispose();
            _ui.Dispose();
            _catalogTexture?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnTextInput(object sender, TextInputEventArgs args) => _ui.TextInput(args.Character);

    private float GetDisplayScale(Viewport viewport)
    {
        if (_metricsOptions?.DisplayScale is float displayScale) return displayScale;
        var clientBounds = Window.ClientBounds;
        if (clientBounds.Width <= 0 || viewport.Width <= 0) return 1f;
        var scale = viewport.Width / (float)clientBounds.Width;
        return float.IsFinite(scale) && scale > 0 ? scale : 1f;
    }

    private void WriteMetrics()
    {
        var report = new
        {
            schemaVersion = 2,
            backend = CatalogBackend.Name,
            renderedFrames = _renderedFrames,
            storyCount = _storyCount,
            displayScale = _displayScale,
            logicalViewportWidth = _ui.ViewportSize.X,
            logicalViewportHeight = _ui.ViewportSize.Y,
            densityFontSelected = _displayFont != null && _displayScale > 1,
            hotReloadEnabled = _hotReload != null,
            hotReloadSucceeded = _hotReload?.LastReloadSucceeded,
            hotReloadMilliseconds = _hotReload?.LastReloadMilliseconds,
            hotReloadMessage = _hotReload?.LastReloadMessage,
        };
        var outputPath = Path.GetFullPath(_metricsOptions.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    private Texture2D CreateCatalogTexture()
    {
        const int size = 48;
        var texture = new Texture2D(GraphicsDevice, size, size);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var checker = (x / 8 + y / 8) % 2 == 0;
            pixels[y * size + x] = checker ? new Color(48, 185, 164) : new Color(246, 185, 73);
        }
        texture.SetData(pixels);
        return texture;
    }

    private static Theme CreateTheme() => new Theme
    {
        BackgroundColor = new Color(20, 24, 31),
        PanelColor = new Color(29, 35, 45),
        PanelBorderColor = new Color(56, 66, 82),
        TextColor = new Color(235, 239, 246),
        DisabledTextColor = new Color(143, 153, 170),
        AccentColor = new Color(48, 185, 164),
        HoverColor = new Color(43, 52, 66),
        PressedColor = new Color(23, 28, 36),
        FocusColor = new Color(246, 185, 73),
    };
}

public sealed class CatalogMetricsOptions
{
    public string OutputPath { get; }
    public int FrameCount { get; }
    public string WatchedEffectPath { get; }
    public float? DisplayScale { get; }

    private CatalogMetricsOptions(
        string outputPath,
        int frameCount,
        string watchedEffectPath,
        float? displayScale)
    {
        OutputPath = outputPath;
        FrameCount = frameCount;
        WatchedEffectPath = watchedEffectPath;
        DisplayScale = displayScale;
    }

    public static CatalogMetricsOptions Parse(string[] args)
    {
        string outputPath = null;
        string watchedEffectPath = null;
        float? displayScale = null;
        var frameCount = 120;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--metrics" when index + 1 < args.Length:
                    outputPath = args[++index];
                    break;
                case "--frames" when index + 1 < args.Length && int.TryParse(args[++index], out frameCount) && frameCount > 0:
                    break;
                case "--watch-effect" when index + 1 < args.Length:
                    watchedEffectPath = args[++index];
                    break;
                case "--display-scale" when index + 1 < args.Length &&
                    float.TryParse(args[++index], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsedScale) &&
                    float.IsFinite(parsedScale) && parsedScale > 0:
                    displayScale = parsedScale;
                    break;
                default:
                    throw new ArgumentException($"Unknown or invalid catalog argument: {args[index]}");
            }
        }

        if (watchedEffectPath != null && !File.Exists(watchedEffectPath))
            throw new ArgumentException($"The watched effect does not exist: {watchedEffectPath}");
        return outputPath == null && watchedEffectPath == null && displayScale == null
            ? null
            : new CatalogMetricsOptions(outputPath, frameCount, watchedEffectPath, displayScale);
    }
}