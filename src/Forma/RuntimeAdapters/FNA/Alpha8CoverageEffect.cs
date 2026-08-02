// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Forma
{
    internal static class Alpha8CoverageEffect
    {
        public static Effect Create(GraphicsDevice graphicsDevice)
        {
            using var stream = typeof(Alpha8CoverageEffect).Assembly.GetManifestResourceStream("Forma.Alpha8Coverage.fxb.b64")
                ?? throw new InvalidOperationException("The embedded FNA Alpha8 coverage effect is missing.");
            using var reader = new StreamReader(stream);
            return new Effect(graphicsDevice, Convert.FromBase64String(reader.ReadToEnd()));
        }
    }
}