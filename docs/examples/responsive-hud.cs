// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma.QuickStart;

public static class ResponsiveHudView
{
    public static Control Create(float displayScale)
    {
        var root = new Control();
        var canvas = new CanvasPanel();
        canvas.SetAnchorsAndOffsets(0, 0, 1, 1);
        root.AddChild(canvas);

        var score = Stack("SCORE", "12,480");
        CanvasPanel.SetLeft(score, 0);
        CanvasPanel.SetTop(score, 0);
        canvas.AddChild(score);

        var wave = Stack("WAVE", "04 / 10");
        CanvasPanel.SetRight(wave, 0);
        CanvasPanel.SetTop(wave, 0);
        canvas.AddChild(wave);

        var objective = Stack("OBJECTIVE", "Hold the relay");
        CanvasPanel.SetLeft(objective, 0);
        CanvasPanel.SetBottom(objective, 0);
        canvas.AddChild(objective);

        var density = Stack("DISPLAY", $"{displayScale:0.##}x scale");
        CanvasPanel.SetRight(density, 0);
        CanvasPanel.SetBottom(density, 0);
        canvas.AddChild(density);

        return root;
    }

    private static BoxContainer Stack(string heading, string value)
    {
        var stack = new BoxContainer { Separation = 4 };
        stack.AddChild(new Label { Text = heading });
        stack.AddChild(new Label
        {
            Text = value,
            CustomMinimumSize = new Vector2(140, 32),
        });
        return stack;
    }
}