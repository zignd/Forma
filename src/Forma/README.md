# Forma

Forma is a retained-mode UI layer for MonoGame inspired by Godot's `Control` tree.

```csharp
using Forma;

var ui = new UIContext();
var root = new VBoxContainer { Size = new Vector2(800, 480) };
root.AddChild(new Button { Text = "Save", CustomMinimumSize = new Vector2(120, 32) });
ui.Add(root);

// Game.Update
ui.ViewportSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
ui.Update(gameTime);

// Game.Draw, after clearing the backbuffer
ui.Draw(GraphicsDevice);
```

For a normal `Game`, add `new UIComponent(this, ui)` to `Game.Components`. It forwards mouse/keyboard state, renders the control tree, and forwards `GameWindow.TextInput` into the focused `LineEdit` or `TextEdit`.

The subsystem uses logical pixels, a `Control` tree, anchors plus offsets, minimum sizes, box/grid/flow layout, focus traversal, pointer capture, delayed `TooltipText`, and a deterministic `Theme`. Rendering is SpriteBatch-based and does not require a content pipeline asset unless a control displays text or a texture. Set `UIContext.TooltipFont` to the font used by the rest of the interface when tooltips should be rendered.

Text can use an offline `SpriteFont` through existing `Font` properties or a runtime-loaded
`DynamicUIFont` through parallel `UIFont` properties. Dynamic layout, fallback, DPI behavior,
deployment, ownership, migration, and rollback are documented in
[Dynamic Text](../../docs/dynamic-text.md).
