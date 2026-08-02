// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma
{
    /// <summary>Reports the active default icon-atlas selection and resource usage.</summary>
    public readonly struct ThemeIconDiagnostics
    {
        internal ThemeIconDiagnostics(int density, int atlasCount, long textureBytes, int generation, int missingIconCount)
        {
            ActiveDensity = density;
            AtlasCount = atlasCount;
            TextureBytes = textureBytes;
            Generation = generation;
            MissingIconCount = missingIconCount;
        }
        public int ActiveDensity { get; }
        public int AtlasCount { get; }
        public long TextureBytes { get; }
        public int Generation { get; }
        public int MissingIconCount { get; }
    }

    internal sealed class DefaultThemeIconResources : IDisposable
    {
        private static readonly ConditionalWeakTable<GraphicsDevice, DeviceCache> DeviceCaches = new ConditionalWeakTable<GraphicsDevice, DeviceCache>();
        private static readonly ThemeIconManifest Manifest = LoadManifest();
        private readonly HashSet<string> _missingNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly DeviceCache _cache;
        private int _density;
        private bool _disposed;

        internal DefaultThemeIconResources(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _cache = DeviceCaches.GetValue(graphicsDevice, _ => new DeviceCache());
            _cache.ReferenceCount++;
        }

        internal GraphicsDevice GraphicsDevice { get; }
        internal Theme Theme { get; } = new Theme();
        internal ThemeIconDiagnostics Diagnostics => new ThemeIconDiagnostics(_density, _cache.AtlasCount, _cache.TextureBytes, _cache.Generation, _missingNames.Count);
        internal static int ManifestIconCount => Manifest.Icons.Count / 2;

        internal void Ensure(float displayScale)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DefaultThemeIconResources));
            var density = SelectDensity(displayScale);
            var texture = _cache.GetTexture(GraphicsDevice, density);
            if (_density == density && Theme.GetIcon("arrow", "OptionButton")?.Texture == texture) return;
            _density = density;
            foreach (var entry in Manifest.Icons.Where(icon => icon.Density == density))
            {
                var icon = new ThemeIcon(texture, new Rectangle(entry.X, entry.Y, entry.Width, entry.Height), new Point(entry.LogicalWidth, entry.LogicalHeight), density);
                foreach (var binding in entry.Bindings)
                {
                    var separator = binding.IndexOf(':');
                    if (separator <= 0 || separator == binding.Length - 1) throw new InvalidDataException($"Invalid theme icon binding: {binding}");
                    Theme.SetIcon(binding.Substring(separator + 1), icon, binding.Substring(0, separator));
                }
            }
        }

        internal void RecordMissing(string itemName)
        {
            if (!string.IsNullOrWhiteSpace(itemName)) _missingNames.Add(itemName);
        }

        internal static int SelectDensity(float displayScale) => float.IsFinite(displayScale) && displayScale >= 1.5f ? 2 : 1;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cache.ReferenceCount--;
            if (_cache.ReferenceCount == 0)
            {
                _cache.Dispose();
                DeviceCaches.Remove(GraphicsDevice);
            }
        }

        private static ThemeIconManifest LoadManifest()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Forma.ThemeIcons.theme-icons.json")
                ?? throw new InvalidDataException("Embedded theme icon manifest is missing.");
            return JsonSerializer.Deserialize(stream, ThemeIconManifestJsonContext.Default.ThemeIconManifest)
                ?? throw new InvalidDataException("Embedded theme icon manifest is invalid.");
        }

        private sealed class DeviceCache : IDisposable
        {
            private readonly Dictionary<int, Texture2D> _textures = new Dictionary<int, Texture2D>();
            internal int ReferenceCount { get; set; }
            internal int Generation { get; private set; }
            internal int AtlasCount => _textures.Count;
            internal long TextureBytes => _textures.Values.Where(texture => !texture.IsDisposed).Sum(texture => (long)texture.Width * texture.Height * 4);

            internal Texture2D GetTexture(GraphicsDevice graphicsDevice, int density)
            {
                if (_textures.TryGetValue(density, out var texture) && !texture.IsDisposed) return texture;
                texture?.Dispose();
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Forma.ThemeIcons.theme-icons-{density}x.png")
                    ?? throw new InvalidDataException($"Embedded {density}x theme icon atlas is missing.");
                texture = Texture2D.FromStream(graphicsDevice, stream);
                _textures[density] = texture;
                Generation++;
                return texture;
            }

            public void Dispose()
            {
                foreach (var texture in _textures.Values) texture.Dispose();
                _textures.Clear();
            }
        }

    }

    internal sealed class ThemeIconManifest
    {
        public List<ThemeIconManifestEntry> Icons { get; set; } = new List<ThemeIconManifestEntry>();
    }

    internal sealed class ThemeIconManifestEntry
    {
        public int Density { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int LogicalWidth { get; set; }
        public int LogicalHeight { get; set; }
        public List<string> Bindings { get; set; } = new List<string>();
    }

    [JsonSerializable(typeof(ThemeIconManifest))]
    internal partial class ThemeIconManifestJsonContext : JsonSerializerContext
    {
    }
}