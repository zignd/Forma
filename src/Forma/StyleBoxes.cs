// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// API concepts are inspired by Godot Engine's scene/resources/style_box*.cpp;
// see THIRD-PARTY-NOTICES.md.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma
{
    /// <summary>Drawable theme surface inspired by Godot's StyleBox resource family.</summary>
    public abstract class StyleBox
    {
        public Thickness ContentMargin { get; set; }
        public abstract void Draw(UIRenderContext context, Rectangle bounds);
    }

    /// <summary>A no-op style box used to reserve content margins without drawing a surface.</summary>
    public sealed class StyleBoxEmpty : StyleBox
    {
        public override void Draw(UIRenderContext context, Rectangle bounds) { }
    }

    /// <summary>Color-filled style box with configurable border and rounded corners.</summary>
    public sealed class StyleBoxFlat : StyleBox
    {
        public Color BackgroundColor { get; set; } = Color.White;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderWidth { get; set; }
        public int CornerRadius { get; set; }
        public override void Draw(UIRenderContext context, Rectangle bounds)
        {
            var radius = System.Math.Max(0, System.Math.Min(CornerRadius, System.Math.Min(bounds.Width, bounds.Height) / 2));
            if (BorderWidth <= 0 || BorderColor.A == 0)
            {
                context.FillRounded(bounds, BackgroundColor, radius);
                return;
            }
            context.FillRounded(bounds, BorderColor, radius);
            var border = System.Math.Min(BorderWidth, System.Math.Min(bounds.Width, bounds.Height) / 2);
            var content = new Rectangle(bounds.X + border, bounds.Y + border, System.Math.Max(0, bounds.Width - border * 2), System.Math.Max(0, bounds.Height - border * 2));
            context.FillRounded(content, BackgroundColor, System.Math.Max(0, radius - border));
        }
    }

    /// <summary>Texture-backed style surface, optionally rendered as a nine-patch.</summary>
    public sealed class StyleBoxTexture : StyleBox
    {
        public Texture2D Texture { get; set; }
        public Thickness PatchMargin { get; set; }
        public Color Modulate { get; set; } = Color.White;
        public override void Draw(UIRenderContext context, Rectangle bounds)
        {
            if (Texture == null) return;
            var left = System.Math.Max(0, System.Math.Min(Texture.Width, (int)PatchMargin.Left));
            var top = System.Math.Max(0, System.Math.Min(Texture.Height, (int)PatchMargin.Top));
            var right = System.Math.Max(0, System.Math.Min(Texture.Width - left, (int)PatchMargin.Right));
            var bottom = System.Math.Max(0, System.Math.Min(Texture.Height - top, (int)PatchMargin.Bottom));
            if (left == 0 && top == 0 && right == 0 && bottom == 0)
            {
                context.SpriteBatch.Draw(Texture, bounds, Modulate);
                return;
            }
            var middleSourceWidth = System.Math.Max(0, Texture.Width - left - right);
            var middleSourceHeight = System.Math.Max(0, Texture.Height - top - bottom);
            var middleDestinationWidth = System.Math.Max(0, bounds.Width - left - right);
            var middleDestinationHeight = System.Math.Max(0, bounds.Height - top - bottom);
            DrawPatch(context, new Rectangle(0, 0, left, top), new Rectangle(bounds.Left, bounds.Top, left, top));
            DrawPatch(context, new Rectangle(left, 0, middleSourceWidth, top), new Rectangle(bounds.Left + left, bounds.Top, middleDestinationWidth, top));
            DrawPatch(context, new Rectangle(Texture.Width - right, 0, right, top), new Rectangle(bounds.Right - right, bounds.Top, right, top));
            DrawPatch(context, new Rectangle(0, top, left, middleSourceHeight), new Rectangle(bounds.Left, bounds.Top + top, left, middleDestinationHeight));
            DrawPatch(context, new Rectangle(left, top, middleSourceWidth, middleSourceHeight), new Rectangle(bounds.Left + left, bounds.Top + top, middleDestinationWidth, middleDestinationHeight));
            DrawPatch(context, new Rectangle(Texture.Width - right, top, right, middleSourceHeight), new Rectangle(bounds.Right - right, bounds.Top + top, right, middleDestinationHeight));
            DrawPatch(context, new Rectangle(0, Texture.Height - bottom, left, bottom), new Rectangle(bounds.Left, bounds.Bottom - bottom, left, bottom));
            DrawPatch(context, new Rectangle(left, Texture.Height - bottom, middleSourceWidth, bottom), new Rectangle(bounds.Left + left, bounds.Bottom - bottom, middleDestinationWidth, bottom));
            DrawPatch(context, new Rectangle(Texture.Width - right, Texture.Height - bottom, right, bottom), new Rectangle(bounds.Right - right, bounds.Bottom - bottom, right, bottom));
        }
        private void DrawPatch(UIRenderContext context, Rectangle source, Rectangle destination)
        {
            if (source.Width > 0 && source.Height > 0 && destination.Width > 0 && destination.Height > 0)
                context.SpriteBatch.Draw(Texture, destination, source, Modulate);
        }
    }
}
