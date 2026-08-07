using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.QuickStart;

public sealed class QuickStartGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly UIContext _ui;
    private readonly int _maximumFrames;
    private readonly string? _screenshotPath;
    private VBoxContainer? _root;
    private UIFontFace? _fontFace;
    private int _renderedFrames;

    public QuickStartGame(int maximumFrames = 0, string? screenshotPath = null)
    {
        _maximumFrames = maximumFrames;
        _screenshotPath = screenshotPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 800,
            PreferredBackBufferHeight = 480,
        };
        _ui = new UIContext();
        Components.Add(new UIComponent(this, _ui));
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
#if FORMA_QUICKSTART_FNA
        Window.Title = "Forma Quick Start [FNA]";
#else
        Window.Title = "Forma Quick Start [MonoGame]";
#endif
    }

    protected override void LoadContent()
    {
        _fontFace = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
        var font = new DynamicUIFont(_fontFace, 20);
        _ui.Theme.FontFamily = new UIFontFamily(new[] { font });
        _ui.TooltipUIFont = font;

        _root = QuickStartView.Create();
        _ui.Add(_root);

        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_root is not null)
        {
            var viewport = GraphicsDevice.Viewport;
            _root.Position = new Vector2(40, 40);
            _root.Size = new Vector2(
                Math.Max(0, viewport.Width - 80),
                Math.Max(0, viewport.Height - 80));
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(24, 28, 36));
        base.Draw(gameTime);

        _renderedFrames++;
        if (_screenshotPath is not null && _renderedFrames == Math.Max(1, _maximumFrames))
            SaveScreenshot(_screenshotPath);
        if (_maximumFrames > 0 && _renderedFrames >= _maximumFrames)
            Exit();
    }

    private void SaveScreenshot(string path)
    {
        var viewport = GraphicsDevice.Viewport;
        var pixels = new Color[viewport.Width * viewport.Height];
        GraphicsDevice.GetBackBufferData(pixels);
        using var screenshot = new Texture2D(GraphicsDevice, viewport.Width, viewport.Height);
        screenshot.SetData(pixels);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        screenshot.SaveAsPng(stream, viewport.Width, viewport.Height);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _fontFace?.Dispose();
    }
}
