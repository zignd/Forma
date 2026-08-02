// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma
{
    public sealed class DynamicGlyphCacheOptions
    {
        public const int MaximumPageDimension = 2048;
        public const int MaximumPageCount = 8;
        public const int MaximumBytes = 32 * 1024 * 1024;

        public DynamicGlyphCacheOptions(int pageWidth = 2048, int pageHeight = 2048, int maximumPages = 8, int padding = 1)
        {
            if (pageWidth <= 0 || pageWidth > MaximumPageDimension) throw new ArgumentOutOfRangeException(nameof(pageWidth));
            if (pageHeight <= 0 || pageHeight > MaximumPageDimension) throw new ArgumentOutOfRangeException(nameof(pageHeight));
            if (maximumPages <= 0 || maximumPages > MaximumPageCount) throw new ArgumentOutOfRangeException(nameof(maximumPages));
            if (padding < 0 || padding * 2 >= pageWidth || padding * 2 >= pageHeight) throw new ArgumentOutOfRangeException(nameof(padding));
            var bytes = checked((long)pageWidth * pageHeight * maximumPages);
            if (bytes > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumPages), $"Atlas memory exceeds the {MaximumBytes}-byte limit.");
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            MaximumPages = maximumPages;
            Padding = padding;
        }

        public int PageWidth { get; }
        public int PageHeight { get; }
        public int MaximumPages { get; }
        public int Padding { get; }
    }

    public readonly struct DynamicGlyphCacheDiagnostics
    {
        internal DynamicGlyphCacheDiagnostics(int pageCount, int capacity, int usedArea, int glyphCount, long hits, long misses, long uploads, long evictions, long failures, string lastFailure, string lastEviction, long bytes, int pendingUploads, int queuedGlyphs, long uploadBytes, long rasterTicks)
        {
            PageCount = pageCount;
            Capacity = capacity;
            UsedArea = usedArea;
            GlyphCount = glyphCount;
            Hits = hits;
            Misses = misses;
            Uploads = uploads;
            Evictions = evictions;
            Failures = failures;
            LastFailure = lastFailure ?? string.Empty;
            LastEviction = lastEviction ?? string.Empty;
            Bytes = bytes;
            PendingUploads = pendingUploads;
            QueuedGlyphs = queuedGlyphs;
            UploadBytes = uploadBytes;
            RasterTime = TimeSpan.FromSeconds((double)rasterTicks / Stopwatch.Frequency);
        }

        public int PageCount { get; }
        public int Capacity { get; }
        public int UsedArea { get; }
        public int GlyphCount { get; }
        public long Hits { get; }
        public long Misses { get; }
        public long Uploads { get; }
        public long Evictions { get; }
        public long Failures { get; }
        public string LastFailure { get; }
        public string LastEviction { get; }
        public long Bytes { get; }
        public int PendingUploads { get; }
        public int QueuedGlyphs { get; }
        public long UploadBytes { get; }
        public TimeSpan RasterTime { get; }
    }

    public sealed class DynamicGlyphAtlasPageSnapshot
    {
        internal DynamicGlyphAtlasPageSnapshot(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = new ReadOnlyMemory<byte>(pixels);
        }

        public int Width { get; }
        public int Height { get; }
        public ReadOnlyMemory<byte> Pixels { get; }
    }

    internal readonly struct DynamicGlyphKey : IEquatable<DynamicGlyphKey>
    {
        public DynamicGlyphKey(UIFontIdentity faceIdentity, uint glyphId, int physicalSize, UIFontHinting hinting)
        {
            FaceIdentity = faceIdentity;
            GlyphId = glyphId;
            PhysicalSize = physicalSize;
            Hinting = hinting;
        }

        public UIFontIdentity FaceIdentity { get; }
        public uint GlyphId { get; }
        public int PhysicalSize { get; }
        public UIFontHinting Hinting { get; }
        public bool Equals(DynamicGlyphKey other) => FaceIdentity == other.FaceIdentity && GlyphId == other.GlyphId && PhysicalSize == other.PhysicalSize && Hinting == other.Hinting;
        public override bool Equals(object obj) => obj is DynamicGlyphKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(FaceIdentity, GlyphId, PhysicalSize, Hinting);
    }

    internal sealed class DynamicGlyphAtlasEntry
    {
        public DynamicGlyphAtlasEntry(DynamicGlyphKey key, int pageIndex, Rectangle bounds, int bearingX, int bearingY, float advanceX)
        {
            Key = key;
            PageIndex = pageIndex;
            Bounds = bounds;
            BearingX = bearingX;
            BearingY = bearingY;
            AdvanceX = advanceX;
        }

        public DynamicGlyphKey Key { get; }
        public int PageIndex { get; }
        public Rectangle Bounds { get; }
        public int BearingX { get; }
        public int BearingY { get; }
        public float AdvanceX { get; }
        internal long LastUsedFrame { get; set; }
        internal bool Uploaded { get; set; }
    }

    internal sealed class DynamicGlyphAtlasStore
    {
        private readonly DynamicGlyphCacheOptions _options;
        private readonly Dictionary<DynamicGlyphKey, DynamicGlyphAtlasEntry> _entries = new Dictionary<DynamicGlyphKey, DynamicGlyphAtlasEntry>();
        private readonly List<AtlasPage> _pages = new List<AtlasPage>();
        private long _frame;
        private bool _frameActive;
        private long _hits;
        private long _misses;
        private long _uploads;
        private long _evictions;
        private long _failures;
        private long _rasterTicks;
        private long _uploadBytes;
        private string _lastFailure = string.Empty;
        private string _lastEviction = string.Empty;

        public DynamicGlyphAtlasStore(DynamicGlyphCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public int PageCount => _pages.Count;

        public void BeginFrame()
        {
            if (_frameActive) throw new InvalidOperationException("An atlas frame is already active.");
            _frame = checked(_frame + 1);
            _frameActive = true;
        }

        public void EndFrame()
        {
            if (!_frameActive) throw new InvalidOperationException("No atlas frame is active.");
            _frameActive = false;
        }

        public DynamicGlyphAtlasEntry GetOrAdd(DynamicGlyphKey key, Func<UIFontGlyphBitmap> rasterize)
        {
            if (!_frameActive) throw new InvalidOperationException("BeginFrame must be called before requesting glyphs.");
            if (TryGet(key, out var existing)) return existing;

            if (rasterize == null) throw new ArgumentNullException(nameof(rasterize));
            _misses++;
            var rasterStarted = Stopwatch.GetTimestamp();
            UIFontGlyphBitmap bitmap;
            try { bitmap = rasterize(); }
            finally { _rasterTicks = checked(_rasterTicks + Stopwatch.GetTimestamp() - rasterStarted); }
            if (bitmap == null) throw new InvalidOperationException("Glyph rasterization returned no bitmap.");
            if (bitmap.Width == 0 || bitmap.Height == 0)
            {
                var empty = new DynamicGlyphAtlasEntry(key, -1, Rectangle.Empty, bitmap.BearingX, bitmap.BearingY, bitmap.AdvanceX) { LastUsedFrame = _frame };
                empty.Uploaded = true;
                _entries.Add(key, empty);
                return empty;
            }

            int pageIndex;
            Rectangle bounds;
            try { pageIndex = FindPage(bitmap.Width, bitmap.Height, out bounds); }
            catch (InvalidOperationException error)
            {
                _failures++;
                _lastFailure = error.Message;
                return new DynamicGlyphAtlasEntry(key, -1, Rectangle.Empty, bitmap.BearingX, bitmap.BearingY, bitmap.AdvanceX) { LastUsedFrame = _frame, Uploaded = true };
            }
            var page = _pages[pageIndex];
            CopyBitmap(bitmap, page.Pixels, bounds, _options.PageWidth);
            page.Dirty = true;
            page.LastUsedFrame = _frame;
            var entry = new DynamicGlyphAtlasEntry(key, pageIndex, bounds, bitmap.BearingX, bitmap.BearingY, bitmap.AdvanceX) { LastUsedFrame = _frame };
            page.Keys.Add(key);
            _entries.Add(key, entry);
            return entry;
        }

        public bool TryGet(DynamicGlyphKey key, out DynamicGlyphAtlasEntry entry)
        {
            if (!_frameActive) throw new InvalidOperationException("BeginFrame must be called before requesting glyphs.");
            if (!_entries.TryGetValue(key, out entry)) return false;
            _hits++;
            Touch(entry);
            return true;
        }

        public bool Contains(DynamicGlyphKey key) => _entries.ContainsKey(key);

        public ReadOnlyMemory<byte> GetPagePixels(int pageIndex) => _pages[pageIndex].Pixels;

        public bool IsPageDirty(int pageIndex) => _pages[pageIndex].Dirty;

        public void MarkPageUploaded(int pageIndex)
        {
            var page = _pages[pageIndex];
            if (!page.Dirty) return;
            page.Dirty = false;
            foreach (var key in page.Keys)
                if (_entries.TryGetValue(key, out var entry)) entry.Uploaded = true;
            _uploads++;
            _uploadBytes = checked(_uploadBytes + page.Pixels.Length);
        }

        public void MarkAllPagesDirty()
        {
            foreach (var page in _pages)
            {
                page.Dirty = true;
                foreach (var key in page.Keys)
                    if (_entries.TryGetValue(key, out var entry)) entry.Uploaded = false;
            }
        }

        public DynamicGlyphCacheDiagnostics GetDiagnostics()
        {
            var usedArea = 0;
            var pending = 0;
            var queuedGlyphs = 0;
            foreach (var page in _pages)
            {
                usedArea = checked(usedArea + page.Allocator.UsedArea);
                if (page.Dirty) pending++;
            }
            foreach (var entry in _entries.Values) if (!entry.Uploaded) queuedGlyphs++;
            return new DynamicGlyphCacheDiagnostics(
                _pages.Count,
                checked(_pages.Count * _options.PageWidth * _options.PageHeight),
                usedArea,
                _entries.Count,
                _hits,
                _misses,
                _uploads,
                _evictions,
                _failures,
                _lastFailure,
                _lastEviction,
                checked((long)_pages.Count * _options.PageWidth * _options.PageHeight),
                pending,
                queuedGlyphs,
                _uploadBytes,
                _rasterTicks);
        }

        public IReadOnlyList<DynamicGlyphAtlasPageSnapshot> GetDebugPages()
        {
            var snapshots = new List<DynamicGlyphAtlasPageSnapshot>(_pages.Count);
            foreach (var page in _pages) snapshots.Add(new DynamicGlyphAtlasPageSnapshot(_options.PageWidth, _options.PageHeight, (byte[])page.Pixels.Clone()));
            return new ReadOnlyCollection<DynamicGlyphAtlasPageSnapshot>(snapshots);
        }

        public void Clear()
        {
            if (_frameActive) throw new InvalidOperationException("The glyph atlas cannot be cleared during an active frame.");
            _entries.Clear();
            _pages.Clear();
        }

        private int FindPage(int width, int height, out Rectangle bounds)
        {
            for (var index = 0; index < _pages.Count; index++)
                if (_pages[index].Allocator.TryAllocate(width, height, _options.Padding, out bounds)) return index;

            if (_pages.Count < _options.MaximumPages)
            {
                var page = new AtlasPage(_options.PageWidth, _options.PageHeight);
                _pages.Add(page);
                if (page.Allocator.TryAllocate(width, height, _options.Padding, out bounds)) return _pages.Count - 1;
                _pages.RemoveAt(_pages.Count - 1);
                throw new InvalidOperationException("Glyph bitmap does not fit an empty atlas page.");
            }

            var evictionIndex = -1;
            var oldestFrame = long.MaxValue;
            for (var index = 0; index < _pages.Count; index++)
            {
                var page = _pages[index];
                if (page.LastUsedFrame == _frame || page.LastUsedFrame >= oldestFrame) continue;
                evictionIndex = index;
                oldestFrame = page.LastUsedFrame;
            }
            if (evictionIndex < 0) throw new InvalidOperationException("The glyph atlas budget is full and every page is active in the current frame.");

            var evicted = _pages[evictionIndex];
            foreach (var key in evicted.Keys) _entries.Remove(key);
            _evictions += evicted.Keys.Count;
            _lastEviction = $"Evicted {evicted.Keys.Count} glyphs from the least-recently-used inactive atlas page.";
            evicted.Reset();
            if (!evicted.Allocator.TryAllocate(width, height, _options.Padding, out bounds))
                throw new InvalidOperationException("Glyph bitmap does not fit an empty atlas page.");
            return evictionIndex;
        }

        private void Touch(DynamicGlyphAtlasEntry entry)
        {
            entry.LastUsedFrame = _frame;
            if (entry.PageIndex >= 0) _pages[entry.PageIndex].LastUsedFrame = _frame;
        }

        private static void CopyBitmap(UIFontGlyphBitmap bitmap, byte[] destination, Rectangle bounds, int destinationWidth)
        {
            var source = bitmap.Pixels.Span;
            for (var row = 0; row < bounds.Height; row++)
                source.Slice(row * bounds.Width, bounds.Width).CopyTo(destination.AsSpan((bounds.Y + row) * destinationWidth + bounds.X, bounds.Width));
        }

        private sealed class AtlasPage
        {
            public AtlasPage(int width, int height)
            {
                Allocator = new GlyphRectangleAllocator(width, height);
                Pixels = new byte[checked(width * height)];
            }

            public GlyphRectangleAllocator Allocator { get; }
            public byte[] Pixels { get; }
            public List<DynamicGlyphKey> Keys { get; } = new List<DynamicGlyphKey>();
            public bool Dirty { get; set; }
            public long LastUsedFrame { get; set; }

            public void Reset()
            {
                Allocator.Reset();
                Array.Clear(Pixels, 0, Pixels.Length);
                Keys.Clear();
                Dirty = true;
                LastUsedFrame = 0;
            }
        }
    }

    internal sealed class DynamicGlyphCache : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly DynamicGlyphCacheOptions _options;
        private readonly DynamicGlyphAtlasStore _store;
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private bool _disposed;

        public DynamicGlyphCache(GraphicsDevice graphicsDevice, DynamicGlyphCacheOptions options = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _options = options ?? new DynamicGlyphCacheOptions();
            _store = new DynamicGlyphAtlasStore(_options);
            _graphicsDevice.DeviceReset += OnDeviceReset;
            _graphicsDevice.Disposing += OnGraphicsDeviceDisposing;
        }

        public DynamicGlyphCacheDiagnostics Diagnostics
        {
            get { ThrowIfDisposed(); return _store.GetDiagnostics(); }
        }

        public void BeginFrame()
        {
            ThrowIfDisposed();
            _store.BeginFrame();
        }

        public DynamicGlyphAtlasEntry GetOrAdd(UIFont font, uint glyphId, float displayScale)
        {
            ThrowIfDisposed();
            if (font == null) throw new ArgumentNullException(nameof(font));
            if (!float.IsFinite(displayScale) || displayScale <= 0) throw new ArgumentOutOfRangeException(nameof(displayScale));
            var physicalSize = checked((int)MathF.Round(font.Size * displayScale * 64));
            var key = new DynamicGlyphKey(font.Identity, glyphId, physicalSize, font.RasterHinting);
            if (_store.TryGet(key, out var existing)) return existing;
            return AddCold(font, glyphId, displayScale, key);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private DynamicGlyphAtlasEntry AddCold(UIFont font, uint glyphId, float displayScale, DynamicGlyphKey key) =>
            _store.GetOrAdd(key, () => font.RasterizeGlyph(glyphId, displayScale));

        public void EndFrame()
        {
            ThrowIfDisposed();
            _store.EndFrame();
        }

        public void FlushUploads()
        {
            ThrowIfDisposed();
            while (_textures.Count < _store.PageCount)
                _textures.Add(new Texture2D(_graphicsDevice, _options.PageWidth, _options.PageHeight, false, SurfaceFormat.Alpha8));
            for (var pageIndex = 0; pageIndex < _store.PageCount; pageIndex++)
            {
                if (!_store.IsPageDirty(pageIndex)) continue;
                var pixels = _store.GetPagePixels(pageIndex).ToArray();
                for (var pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
                    if (pixels[pixelIndex] == byte.MaxValue) pixels[pixelIndex]--;
                _textures[pageIndex].SetData(pixels);
                _store.MarkPageUploaded(pageIndex);
            }
        }

        public Texture2D GetTexture(DynamicGlyphAtlasEntry entry)
        {
            ThrowIfDisposed();
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.PageIndex < 0) return null;
            if (entry.PageIndex >= _textures.Count) throw new InvalidOperationException("FlushUploads must be called before drawing glyphs.");
            return _textures[entry.PageIndex];
        }

        public IReadOnlyList<DynamicGlyphAtlasPageSnapshot> GetDebugPages()
        {
            ThrowIfDisposed();
            return _store.GetDebugPages();
        }

        public void Clear()
        {
            ThrowIfDisposed();
            _store.Clear();
            DisposeTextures();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _graphicsDevice.DeviceReset -= OnDeviceReset;
            _graphicsDevice.Disposing -= OnGraphicsDeviceDisposing;
            DisposeTextures();
        }

        private void OnDeviceReset(object sender, EventArgs args)
        {
            if (_disposed) return;
            DisposeTextures();
            _store.MarkAllPagesDirty();
        }

        private void OnGraphicsDeviceDisposing(object sender, EventArgs args) => Dispose();

        private void DisposeTextures()
        {
            foreach (var texture in _textures) texture.Dispose();
            _textures.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DynamicGlyphCache));
        }
    }

    internal sealed class GlyphRectangleAllocator
    {
        private readonly int _width;
        private readonly int _height;
        private readonly List<SkylineNode> _skyline = new List<SkylineNode>();

        public GlyphRectangleAllocator(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            _width = width;
            _height = height;
            Reset();
        }

        public int UsedArea { get; private set; }

        public bool TryAllocate(int width, int height, int padding, out Rectangle rectangle)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (padding < 0) throw new ArgumentOutOfRangeException(nameof(padding));
            if (width == 0 || height == 0)
            {
                rectangle = Rectangle.Empty;
                return true;
            }

            var paddedWidth = checked(width + padding * 2);
            var paddedHeight = checked(height + padding * 2);
            var bestIndex = -1;
            var bestX = 0;
            var bestY = int.MaxValue;
            for (var index = 0; index < _skyline.Count; index++)
            {
                var y = FindY(index, paddedWidth);
                if (y < 0 || y + paddedHeight > _height) continue;
                var x = _skyline[index].X;
                if (y < bestY || y == bestY && x < bestX)
                {
                    bestIndex = index;
                    bestX = x;
                    bestY = y;
                }
            }

            if (bestIndex < 0)
            {
                rectangle = Rectangle.Empty;
                return false;
            }

            AddLevel(bestIndex, bestX, bestY, paddedWidth, paddedHeight);
            UsedArea = checked(UsedArea + paddedWidth * paddedHeight);
            rectangle = new Rectangle(bestX + padding, bestY + padding, width, height);
            return true;
        }

        public void Reset()
        {
            _skyline.Clear();
            _skyline.Add(new SkylineNode(0, 0, _width));
            UsedArea = 0;
        }

        private int FindY(int index, int width)
        {
            var x = _skyline[index].X;
            if (x > _width - width) return -1;
            var widthLeft = width;
            var y = _skyline[index].Y;
            while (widthLeft > 0)
            {
                if (index >= _skyline.Count) return -1;
                y = Math.Max(y, _skyline[index].Y);
                widthLeft -= _skyline[index].Width;
                index++;
            }
            return y;
        }

        private void AddLevel(int index, int x, int y, int width, int height)
        {
            _skyline.Insert(index, new SkylineNode(x, checked(y + height), width));
            for (var nodeIndex = index + 1; nodeIndex < _skyline.Count; nodeIndex++)
            {
                var previous = _skyline[nodeIndex - 1];
                var current = _skyline[nodeIndex];
                var overlap = previous.X + previous.Width - current.X;
                if (overlap <= 0) break;
                if (overlap >= current.Width)
                {
                    _skyline.RemoveAt(nodeIndex);
                    nodeIndex--;
                    continue;
                }
                _skyline[nodeIndex] = new SkylineNode(current.X + overlap, current.Y, current.Width - overlap);
                break;
            }

            for (var nodeIndex = 0; nodeIndex < _skyline.Count - 1; nodeIndex++)
            {
                if (_skyline[nodeIndex].Y != _skyline[nodeIndex + 1].Y) continue;
                var current = _skyline[nodeIndex];
                _skyline[nodeIndex] = new SkylineNode(current.X, current.Y, current.Width + _skyline[nodeIndex + 1].Width);
                _skyline.RemoveAt(nodeIndex + 1);
                nodeIndex--;
            }
        }

        private readonly struct SkylineNode
        {
            public SkylineNode(int x, int y, int width) { X = x; Y = y; Width = width; }
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
        }
    }
}