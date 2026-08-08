// SPDX-License-Identifier: MIT

using System.Text;
using Microsoft.Xna.Framework;

namespace Forma.QuickStart;

public static class RuntimeSvgExampleView
{
    private const string BadgeSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="240" height="120" viewBox="0 0 240 120">
          <defs>
            <linearGradient id="signal" x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stop-color="#65dcc8"/>
              <stop offset="1" stop-color="#f6b949"/>
            </linearGradient>
          </defs>
          <rect x="4" y="4" width="232" height="112" rx="18" fill="#173a49" stroke="#65dcc8" stroke-width="4"/>
          <path d="M42 80L78 42l26 26 30-34 64 64H42z" fill="url(#signal)"/>
        </svg>
        """;

    public static Control Create()
    {
        var source = SvgImageSource.FromMemory(Encoding.UTF8.GetBytes(BadgeSvg));
        var health = SvgRuntime.Health;
        var column = new VBoxContainer { Separation = 12 };
        column.AddChild(new Label { Text = "Bounded runtime SVG" });
        column.AddChild(new Image
        {
            ScalableSource = source,
            Stretch = ImageStretch.Contain,
            ExpandMode = TextureRectExpandMode.IgnoreSize,
            CustomMinimumSize = new Vector2(360, 180),
            AccessibilityLabel = "Gradient signal badge",
        });
        column.AddChild(new Label
        {
            Text = $"{health.BackendId} | profile {health.ProfileVersion} | {source.ElementCount} elements",
        });
        return column;
    }
}