using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
#if FORMA_XAML_HOT_RELOAD
using Forma.Xaml.HotReload;
#endif

namespace Forma.Xaml.Game;

public sealed class XamlGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly UIContext _ui;
    private RuntimeGameTextInputAdapter _textInput;
    private GameScreen _screen;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private KeyboardState _previousKeyboard;
#if FORMA_XAML_HOT_RELOAD
    private FormaXamlHotReloadService _hotReload;
#endif

    public XamlGame()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 960, PreferredBackBufferHeight = 640 };
        _graphics.GetType().GetProperty("AllowHighDpi")?.SetValue(_graphics, true);
        _ui = new UIContext { Theme = CreateTheme() };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Forma XAML - Signal Run";
        _textInput = new RuntimeGameTextInputAdapter(this, _ui.TextInput);
    }

    public GameScreen Screen => _screen;

    protected override void LoadContent()
    {
        var font = Content.Load<SpriteFont>("Fonts/Catalog");
        _ui.Theme.FontFamily = new UIFontFamily(new[] { new SpriteFontAdapter(font) });
        _ui.TooltipFont = font;
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _screen = new GameScreen();
        _ui.Add(_screen);
#if FORMA_XAML_HOT_RELOAD
        if (RuntimeFeature.IsDynamicCodeSupported && RuntimeFeature.IsDynamicCodeCompiled)
        {
            var sourceRoot = typeof(XamlGame).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(metadata => metadata.Key == "FormaXamlGameSourceRoot").Value;
            _hotReload = new FormaXamlHotReloadService(_ui, sourceRoot);
            _hotReload.DiagnosticsChanged += diagnostics => { foreach (var diagnostic in diagnostics) Console.Error.WriteLine(diagnostic); };
            _screen.EnableHotReload(_hotReload);
        }
#endif
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();
        var movement = new Vector2(
            Axis(keyboard, Keys.D, Keys.Right) - Axis(keyboard, Keys.A, Keys.Left),
            Axis(keyboard, Keys.S, Keys.Down) - Axis(keyboard, Keys.W, Keys.Up));
        var input = new GameInput(
            movement,
            Pressed(keyboard, Keys.Enter),
            Pressed(keyboard, Keys.P),
            Pressed(keyboard, Keys.R));
        _screen.Update(gameTime.ElapsedGameTime, input);
        var viewport = GraphicsDevice.Viewport;
        _ui.ViewportSize = new Vector2(viewport.Width, viewport.Height);
        _screen.Arrange(_ui.ViewportSize);
        _ui.Update(gameTime);
        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(18, 24, 30));
        var session = _screen.Presenter.Session;
        _spriteBatch.Begin();
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(24, 34, 42));
        _spriteBatch.Draw(_pixel, CenteredRectangle(session.TargetPosition, 26), new Color(246, 185, 73));
        _spriteBatch.Draw(_pixel, CenteredRectangle(session.PlayerPosition, 30), new Color(48, 185, 164));
        _spriteBatch.End();
        _ui.Draw(GraphicsDevice);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screen?.Dispose();
#if FORMA_XAML_HOT_RELOAD
            _hotReload?.Dispose();
#endif
            _textInput?.Dispose();
            _ui.Dispose();
            _pixel?.Dispose();
            _spriteBatch?.Dispose();
        }
        base.Dispose(disposing);
    }

    private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    private static int Axis(KeyboardState keyboard, Keys first, Keys second) => keyboard.IsKeyDown(first) || keyboard.IsKeyDown(second) ? 1 : 0;
    private static Rectangle CenteredRectangle(Vector2 center, int size) => new((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

    private static Theme CreateTheme() => new()
    {
        BackgroundColor = new Color(18, 24, 30),
        PanelColor = new Color(38, 49, 59),
        TextColor = new Color(225, 232, 237),
        AccentColor = new Color(48, 185, 164),
    };
}