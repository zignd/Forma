// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#if FORMA_XAML_HOT_RELOAD
using System.Reflection;
using Forma.Xaml.HotReload;
#endif

namespace Forma.QuickStart;

public enum QuickStartViewKind
{
    CSharp,
    Xaml,
    SettingsForm,
}

public sealed class QuickStartGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly UIContext _ui;
    private readonly int _maximumFrames;
    private readonly string? _screenshotPath;
    private readonly QuickStartViewKind _viewKind;
    private Control? _root;
    private UIFontFace? _fontFace;
    private int _renderedFrames;
#if FORMA_XAML_HOT_RELOAD
    private FormaXamlHotReloadService? _xamlHotReload;
    private IDisposable? _xamlHotReloadRegistration;
#endif

    public QuickStartGame(
        int maximumFrames = 0,
        string? screenshotPath = null,
        QuickStartViewKind viewKind = QuickStartViewKind.CSharp)
    {
        _maximumFrames = maximumFrames;
        _screenshotPath = screenshotPath;
        _viewKind = viewKind;
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

        _root = _viewKind switch
        {
            QuickStartViewKind.Xaml => new FirstView(),
            QuickStartViewKind.SettingsForm => new SettingsFormView(),
            _ => QuickStartView.Create(),
        };
        _ui.Add(_root);
    #if FORMA_XAML_HOT_RELOAD
        if (_viewKind == QuickStartViewKind.Xaml) StartXamlHotReload();
    #endif

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

#if FORMA_XAML_HOT_RELOAD
    private void StartXamlHotReload()
    {
        var sourceRoot = typeof(QuickStartGame).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(metadata => metadata.Key == "FormaQuickStartXamlRoot")
            .Value ?? throw new InvalidOperationException("Debug XAML source metadata is empty.");
        _xamlHotReload = new FormaXamlHotReloadService(_ui, sourceRoot);
        _xamlHotReloadRegistration = _xamlHotReload.Register<Control>(
            "FirstView.xaml",
            () => _root ?? throw new InvalidOperationException("The XAML root is not loaded."),
            (oldRoot, replacement) =>
            {
                if (replacement is not BoxContainer root)
                    throw new InvalidOperationException("FirstView must have a BoxContainer root.");
                _ui.Remove(oldRoot);
                _root = root;
                _ui.Add(root);
            });
    }
#endif

    protected override void Dispose(bool disposing)
    {
#if FORMA_XAML_HOT_RELOAD
        if (disposing)
        {
            _xamlHotReloadRegistration?.Dispose();
            _xamlHotReload?.Dispose();
        }
#endif
        base.Dispose(disposing);
        if (disposing) _fontFace?.Dispose();
    }
}
