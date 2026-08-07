// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma.QuickStart;

internal static class QuickStartView
{
    public static VBoxContainer Create()
    {
        var name = new LineEdit
        {
            Name = "Name",
            PlaceholderText = "Your name",
            Text = "Player",
            CustomMinimumSize = new Vector2(320, 44),
        };
        var status = new Label { Text = "Ready." };
        var greet = new Button
        {
            Text = "Greet",
            CustomMinimumSize = new Vector2(120, 44),
        };
        greet.Pressed += (_, _) => status.Text = $"Hello, {name.Text.Trim()}!";

        var root = new VBoxContainer
        {
            Name = "QuickStartRoot",
            Separation = 12,
        };
        root.AddChild(new Label { Text = "Your first Forma UI" });
        root.AddChild(name);
        root.AddChild(greet);
        root.AddChild(status);
        return root;
    }
}
