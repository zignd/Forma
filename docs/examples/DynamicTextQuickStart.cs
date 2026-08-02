using Forma;
using Microsoft.Xna.Framework;

namespace Forma.Examples;

public sealed class DynamicTextQuickStart : IDisposable
{
    private readonly UIFontFace _face;

    public DynamicTextQuickStart(string contentDirectory)
    {
        _face = UIFontFace.FromProjectFile(contentDirectory, "Fonts/Inter_Regular.ttf");
        Context = new UIContext();
        Context.Theme.FontFamily = new UIFontFamily([new DynamicUIFont(_face, 16)]);

        var root = new VBoxContainer { Size = new Vector2(480, 240) };
        root.AddChild(new Label { Text = "Runtime-shaped text: مرحبا · こんにちは" });
        root.AddChild(new LineEdit { Text = "Editable Unicode" });
        Context.Add(root);
    }

    public UIContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _face.Dispose();
    }
}