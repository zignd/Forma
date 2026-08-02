using System;
using System.Collections.Generic;
using System.IO;

namespace Forma
{
    /// <summary>Configures the process-wide dynamic font used by new UI contexts.</summary>
    public static class DynamicTextDefaults
    {
        private static readonly object Sync = new object();
        private static readonly List<UIFontFace> Faces = new List<UIFontFace>();
        private static UIFontFamily _fontFamily;

        /// <summary>Gets the installed default family, or null before installation.</summary>
        public static UIFontFamily FontFamily => _fontFamily;

        /// <summary>Installs a packaged font as the default for subsequently created UI contexts.</summary>
        public static void Install(string fontPath, float size = 16)
        {
            if (string.IsNullOrWhiteSpace(fontPath)) throw new ArgumentException("A font path is required.", nameof(fontPath));
            if (!float.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            lock (Sync)
            {
                using var stream = File.OpenRead(fontPath);
                var face = UIFontFace.FromStream(stream);
                Faces.Add(face);
                _fontFamily = new UIFontFamily(new[] { new DynamicUIFont(face, size) });
                UIFontDefaultRegistry.SetFontFamily(_fontFamily);
            }
        }
    }
}