// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma
{
    /// <summary>SpriteBatch-backed drawing surface used by controls. Applications can subclass controls to draw custom content.</summary>
    public sealed class UIRenderContext : IDisposable
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _pixel;
        private readonly BasicEffect _basicEffect;
        private readonly Effect _alpha8CoverageEffect;
        private readonly RasterizerState _scissorRasterizer;
        private readonly DynamicGlyphCache _dynamicGlyphCache;
        private readonly Stack<Rectangle?> _clipStack = new Stack<Rectangle?>();
        private readonly Stack<ThemeScope> _themeStack = new Stack<ThemeScope>();
        private TextLayoutEngine _textLayoutEngine = new TextLayoutEngine();
        private bool _begun;
        private Rectangle? _currentClip;
        internal UIRenderContext(GraphicsDevice graphicsDevice, Theme theme)
        {
            GraphicsDevice = graphicsDevice;
            Theme = theme;
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _basicEffect = new BasicEffect(graphicsDevice) { TextureEnabled = true, VertexColorEnabled = true };
            _alpha8CoverageEffect = Alpha8CoverageEffect.Create(graphicsDevice);
            _scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
            _dynamicGlyphCache = new DynamicGlyphCache(graphicsDevice);
        }
        public GraphicsDevice GraphicsDevice { get; }
        public Theme Theme { get; internal set; }
        internal float DisplayScale { get; set; } = 1f;
        internal Func<SpriteFont, float, SpriteFont> DisplayFontResolver { get; set; }
        internal TextLayoutEngine TextLayoutEngine { get => _textLayoutEngine; set => _textLayoutEngine = value ?? throw new ArgumentNullException(nameof(value)); }
        public SpriteBatch SpriteBatch => _spriteBatch;
        internal Texture2D Pixel => _pixel;
        public void Begin()
        {
            if (_begun) return;
            _currentClip = null;
            _dynamicGlyphCache.FlushUploads();
            _dynamicGlyphCache.BeginFrame();
            BeginBatch();
        }
        public void End()
        {
            if (!_begun) return;
            _spriteBatch.End();
            _begun = false;
            _clipStack.Clear();
            _themeStack.Clear();
            _currentClip = null;
            _dynamicGlyphCache.EndFrame();
        }
        /// <summary>Clips subsequent drawing to the intersection of this rectangle and all active parent clips.</summary>
        public void PushClip(Rectangle rectangle)
        {
            if (!_begun) throw new InvalidOperationException("Begin must be called before pushing a clip.");
            _clipStack.Push(_currentClip);
            var viewport = GraphicsDevice.Viewport;
            var logicalViewport = Math.Abs(DisplayScale - 1f) < .0001f
                ? viewport.Bounds
                : new Rectangle(0, 0, (int)MathF.Ceiling(viewport.Width / DisplayScale), (int)MathF.Ceiling(viewport.Height / DisplayScale));
            _currentClip = _currentClip.HasValue ? Rectangle.Intersect(_currentClip.Value, rectangle) : Rectangle.Intersect(logicalViewport, rectangle);
            RestartBatch();
        }
        /// <summary>Restores the parent clip previously installed with <see cref="PushClip"/>.</summary>
        public void PopClip()
        {
            if (_clipStack.Count == 0) throw new InvalidOperationException("No clip is active.");
            _currentClip = _clipStack.Pop();
            RestartBatch();
        }
        internal void PushTheme(Theme themeOverride)
        {
            var inheritedParent = false;
            if (themeOverride != null && !ReferenceEquals(themeOverride, Theme) && themeOverride.Parent == null)
            {
                themeOverride.SetInheritedParent(Theme);
                inheritedParent = true;
            }
            _themeStack.Push(new ThemeScope(Theme, themeOverride, inheritedParent));
            if (themeOverride != null) Theme = themeOverride;
        }
        internal void PopTheme()
        {
            if (_themeStack.Count == 0) throw new InvalidOperationException("No theme scope is active.");
            var scope = _themeStack.Pop();
            if (scope.InheritedParent) scope.Override.SetInheritedParent(null);
            Theme = scope.Previous;
        }
        public void Fill(Rectangle rectangle, Color color) { if (rectangle.Width > 0 && rectangle.Height > 0) _spriteBatch.Draw(_pixel, rectangle, color); }
        /// <summary>Draws a theme-owned atlas region into a pixel-rounded logical rectangle.</summary>
        public void Icon(ThemeIcon icon, Rectangle destination, Color color)
        {
            if (icon.Texture == null || destination.Width <= 0 || destination.Height <= 0) return;
            _spriteBatch.Draw(icon.Texture, destination, icon.SourceRectangle, color);
        }
        /// <summary>Draws a theme-owned atlas region at its stable logical size.</summary>
        public void Icon(ThemeIcon icon, Vector2 position, Color color)
        {
            var destination = new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                icon.LogicalSize.X,
                icon.LogicalSize.Y);
            Icon(icon, destination, color);
        }
        /// <summary>Fills a deterministic pixel-rounded rectangle without requiring an external texture asset.</summary>
        public void FillRounded(Rectangle rectangle, Color color, int radius)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0) return;
            radius = Math.Max(0, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2));
            if (radius == 0) { Fill(rectangle, color); return; }
            for (var y = 0; y < rectangle.Height; y++)
            {
                var distance = y < radius ? radius - y - .5f : y >= rectangle.Height - radius ? y - (rectangle.Height - radius) + .5f : 0;
                var inset = distance == 0 ? 0 : (int)Math.Ceiling(radius - Math.Sqrt(radius * radius - distance * distance));
                Fill(new Rectangle(rectangle.X + inset, rectangle.Y + y, Math.Max(0, rectangle.Width - inset * 2), 1), color);
            }
        }
        public void Border(Rectangle rectangle, Color color, int width = 1)
        {
            if (width <= 0 || rectangle.Width <= 0 || rectangle.Height <= 0) return;
            Fill(new Rectangle(rectangle.Left, rectangle.Top, rectangle.Width, width), color);
            Fill(new Rectangle(rectangle.Left, rectangle.Bottom - width, rectangle.Width, width), color);
            Fill(new Rectangle(rectangle.Left, rectangle.Top, width, rectangle.Height), color);
            Fill(new Rectangle(rectangle.Right - width, rectangle.Top, width, rectangle.Height), color);
        }
        public void Text(SpriteFont font, string text, Vector2 position, Color color)
        {
            Text(font, text, position, color, 1f);
        }
        public void Text(UIFont font, string text, Vector2 position, Color color)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;
            Text(TextLayoutEngine.Layout(font, text), position, color);
        }
        /// <summary>Draws SpriteFont text at a deterministic uniform scale for controls with a font-size override.</summary>
        public void Text(SpriteFont font, string text, Vector2 position, Color color, float scale)
        {
            if (font == null || string.IsNullOrEmpty(text) || scale <= 0) return;
            var adapter = new SpriteFontAdapter(font, font.LineSpacing * scale);
            Text(TextLayoutEngine.Layout(adapter, text), position, color);
        }
        /// <summary>Draws an immutable layout without repeating measurement or line breaking.</summary>
        public void Text(TextLayout layout, Vector2 position, Color color)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            layout.Font.Draw(this, layout, position, color);
        }
        internal void DrawSpriteFont(SpriteFont font, string text, Vector2 position, Color color, float scale)
        {
            if (font == null || string.IsNullOrEmpty(text) || scale <= 0) return;
            var displayFont = DisplayFontResolver?.Invoke(font, DisplayScale);
            if (displayFont != null && !ReferenceEquals(displayFont, font) && displayFont.LineSpacing > 0)
            {
                scale *= font.LineSpacing / (float)displayFont.LineSpacing;
                font = displayFont;
            }
            if (Math.Abs(scale - 1f) < .0001f) { _spriteBatch.DrawString(font, text, position, color); return; }
            _spriteBatch.DrawString(font, text, position, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        internal void DrawDynamicGlyph(UIFont font, uint glyphId, Vector2 baselinePosition, Color color)
        {
            var glyph = _dynamicGlyphCache.GetOrAdd(font, glyphId, DisplayScale);
            if (glyph.PageIndex < 0 || !glyph.Uploaded) return;
            var texture = _dynamicGlyphCache.GetTexture(glyph);
            var topLeft = SnapDynamicGlyphPosition(baselinePosition, glyph.BearingX, glyph.BearingY, DisplayScale);
            _spriteBatch.Draw(texture, topLeft, glyph.Bounds, color, 0, Vector2.Zero, 1f / DisplayScale, SpriteEffects.None, 0);
        }
        internal static Vector2 SnapDynamicGlyphPosition(Vector2 baselinePosition, int bearingX, int bearingY, float displayScale)
        {
            return new Vector2(
                baselinePosition.X + bearingX / displayScale,
                (MathF.Round(baselinePosition.Y * displayScale) - bearingY) / displayScale);
        }
        internal void BeginDynamicGlyphs()
        {
            if (!_begun) throw new InvalidOperationException("Begin must be called before drawing dynamic glyphs.");
            _spriteBatch.End();
            var transform = Math.Abs(DisplayScale - 1f) < .0001f ? Matrix.Identity : Matrix.CreateScale(DisplayScale, DisplayScale, 1f);
            var viewport = GraphicsDevice.Viewport;
            var projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, -1);
            _alpha8CoverageEffect.Parameters["MatrixTransform"].SetValue(transform * projection);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, _currentClip.HasValue ? _scissorRasterizer : RasterizerState.CullNone, null, transform);
            _alpha8CoverageEffect.CurrentTechnique.Passes[0].Apply();
        }
        internal void EndDynamicGlyphs()
        {
            _spriteBatch.End();
            BeginBatch();
        }
        internal DynamicGlyphCacheDiagnostics DynamicGlyphDiagnostics => _dynamicGlyphCache.Diagnostics;
        internal IReadOnlyList<DynamicGlyphAtlasPageSnapshot> DynamicGlyphPages => _dynamicGlyphCache.GetDebugPages();
        internal void ClearDynamicGlyphCache() => _dynamicGlyphCache.Clear();
        internal void RenderToTarget(RenderTarget2D target, Color clearColor, Action draw)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            if (!_begun) throw new InvalidOperationException("Begin must be called before rendering a nested target.");
            _spriteBatch.End();
            _begun = false;
            var previousTargets = GraphicsDevice.GetRenderTargets();
            var previousViewport = GraphicsDevice.Viewport;
            try
            {
                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(clearColor);
                draw();
            }
            finally
            {
                if (previousTargets.Length == 0) GraphicsDevice.SetRenderTarget(null);
                else GraphicsDevice.SetRenderTargets(previousTargets);
                GraphicsDevice.Viewport = previousViewport;
                BeginBatch();
            }
        }
        /// <summary>Draws a texture-clipped triangle fan while preserving the active UI clip and batch state.</summary>
        public void TexturedFan(Texture2D texture, Vector2 center, Vector2 centerUv, IReadOnlyList<Vector2> boundary, IReadOnlyList<Vector2> uvs, Color color)
        {
            if (texture == null || boundary == null || uvs == null || boundary.Count < 2 || boundary.Count != uvs.Count) return;
            _spriteBatch.End();
            var vertices = new VertexPositionColorTexture[(boundary.Count - 1) * 3];
            centerUv = Vector2.Clamp(centerUv, Vector2.Zero, Vector2.One);
            for (var i = 0; i < boundary.Count - 1; i++)
            {
                var index = i * 3;
                vertices[index] = new VertexPositionColorTexture(new Vector3(center, 0), color, centerUv);
                vertices[index + 1] = new VertexPositionColorTexture(new Vector3(boundary[i], 0), color, uvs[i]);
                vertices[index + 2] = new VertexPositionColorTexture(new Vector3(boundary[i + 1], 0), color, uvs[i + 1]);
            }
            _basicEffect.Texture = texture;
            _basicEffect.World = Matrix.Identity;
            _basicEffect.View = Matrix.Identity;
            _basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width / DisplayScale, GraphicsDevice.Viewport.Height / DisplayScale, 0, 0, 1);
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = _currentClip.HasValue ? _scissorRasterizer : RasterizerState.CullNone;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, boundary.Count - 1);
            }
            BeginBatch();
        }
        private void RestartBatch()
        {
            _spriteBatch.End();
            BeginBatch();
        }
        private void BeginBatch(Effect effect = null)
        {
            var transform = Math.Abs(DisplayScale - 1f) < .0001f ? Matrix.Identity : Matrix.CreateScale(DisplayScale, DisplayScale, 1f);
            if (effect != null)
            {
                var viewport = GraphicsDevice.Viewport;
                var projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, -1);
                effect.Parameters["MatrixTransform"].SetValue(transform * projection);
            }
            if (_currentClip.HasValue)
            {
                var clip = ToPhysicalRectangle(_currentClip.Value);
                if (clip.Width <= 0 || clip.Height <= 0) clip = new Rectangle(0, 0, 1, 1);
                GraphicsDevice.ScissorRectangle = clip;
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorRasterizer, effect, transform);
            }
            else _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, effect, transform);
            _begun = true;
        }
        private Rectangle ToPhysicalRectangle(Rectangle rectangle)
        {
            if (Math.Abs(DisplayScale - 1f) < .0001f) return rectangle;
            var viewport = GraphicsDevice.Viewport;
            var left = viewport.X + (int)MathF.Floor(rectangle.Left * DisplayScale);
            var top = viewport.Y + (int)MathF.Floor(rectangle.Top * DisplayScale);
            var right = viewport.X + (int)MathF.Ceiling(rectangle.Right * DisplayScale);
            var bottom = viewport.Y + (int)MathF.Ceiling(rectangle.Bottom * DisplayScale);
            return Rectangle.Intersect(viewport.Bounds, new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
        }
        private readonly struct ThemeScope
        {
            public ThemeScope(Theme previous, Theme themeOverride, bool inheritedParent) { Previous = previous; Override = themeOverride; InheritedParent = inheritedParent; }
            public Theme Previous { get; }
            public Theme Override { get; }
            public bool InheritedParent { get; }
        }
        public void Dispose() { _dynamicGlyphCache.Dispose(); _spriteBatch.Dispose(); _pixel.Dispose(); _basicEffect.Dispose(); _alpha8CoverageEffect.Dispose(); _scissorRasterizer.Dispose(); }
    }
}
