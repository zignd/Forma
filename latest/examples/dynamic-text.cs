// SPDX-License-Identifier: MIT

namespace Forma.QuickStart;

public static class DynamicTextExampleView
{
    public static Control Create(UIFontFace face)
    {
        ArgumentNullException.ThrowIfNull(face);

        var light = new DynamicUIFont(face, 18, UIFontHinting.Light);
        var display = new DynamicUIFont(face, 32, UIFontHinting.Default);
        var title = new Label { Text = "Dynamic text", UIFont = display };
        title.SetOpenTypeFeatures(new[]
        {
            new UIFontOpenTypeFeature("kern", 1),
            new UIFontOpenTypeFeature("liga", 1),
        });

        var column = new VBoxContainer { Separation = 12 };
        column.AddChild(title);
        column.AddChild(new Label
        {
            Text = "Runtime-loaded Inter with light hinting at 18 logical pixels.",
            UIFont = light,
            AutowrapMode = LabelAutowrapMode.Word,
            CustomMinimumSize = new Microsoft.Xna.Framework.Vector2(620, 34),
        });
        column.AddChild(new Label
        {
            Text = $"Face: {face.FamilyName} | scale-independent layout",
            UIFont = light,
        });
        return column;
    }
}