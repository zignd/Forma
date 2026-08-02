// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma
{
    internal static class MathHelper
    {
        public const float PiOver2 = Microsoft.Xna.Framework.MathHelper.PiOver2;
        public const float TwoPi = Microsoft.Xna.Framework.MathHelper.TwoPi;

        public static float Clamp(float value, float min, float max) =>
            Microsoft.Xna.Framework.MathHelper.Clamp(value, min, max);

        public static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);
    }

    internal static class XnaCompatibility
    {
        public static Color WithAlpha(this Color color, byte alpha) =>
            new Color(color.R, color.G, color.B, alpha);

        public static Point ToPoint(this Vector2 value) => new Point((int)value.X, (int)value.Y);
        public static Vector2 ToVector2(this Point value) => new Vector2(value.X, value.Y);
        public static Point GetPosition(this MouseState value) => new Point(value.X, value.Y);
    }
}