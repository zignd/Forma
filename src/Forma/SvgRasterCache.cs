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
    /// <summary>Configures bounded SVG document, raster, upload, and atlas storage.</summary>
    public sealed class SvgRasterCacheOptions
    {
        public const int MaximumPageDimension = 4096;
        public const int MaximumPageCount = 8;
        public const int MaximumDocumentCount = 1024;
        public const int MaximumBytes = 64 * 1024 * 1024;

        public SvgRasterCacheOptions(int pageWidth = 2048, int pageHeight = 2048, int maximumPages = 4, int padding = 1, int maximumUploadBytesPerFrame = 0, int maximumDocuments = 128)
        {
            if (pageWidth <= 0 || pageWidth > MaximumPageDimension) throw new ArgumentOutOfRangeException(nameof(pageWidth));
            if (pageHeight <= 0 || pageHeight > MaximumPageDimension) throw new ArgumentOutOfRangeException(nameof(pageHeight));
            if (maximumPages <= 0 || maximumPages > MaximumPageCount) throw new ArgumentOutOfRangeException(nameof(maximumPages));
            if (padding < 1 || padding * 2 >= pageWidth || padding * 2 >= pageHeight) throw new ArgumentOutOfRangeException(nameof(padding));
            var bytes = checked((long)pageWidth * pageHeight * maximumPages * 4);
            if (bytes > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumPages), $"SVG atlas memory exceeds the {MaximumBytes}-byte limit.");
            var pageBytes = checked(pageWidth * pageHeight * 4);
            if (maximumUploadBytesPerFrame == 0) maximumUploadBytesPerFrame = pageBytes;
            if (maximumUploadBytesPerFrame < pageBytes || maximumUploadBytesPerFrame > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(maximumUploadBytesPerFrame));
            if (maximumDocuments <= 0 || maximumDocuments > MaximumDocumentCount) throw new ArgumentOutOfRangeException(nameof(maximumDocuments));
            PageWidth = pageWidth;
            PageHeight = pageHeight;
            MaximumPages = maximumPages;
            Padding = padding;
            MaximumUploadBytesPerFrame = maximumUploadBytesPerFrame;
            MaximumDocuments = maximumDocuments;
        }

        public int PageWidth { get; }
        public int PageHeight { get; }
        public int MaximumPages { get; }
        public int Padding { get; }
        public int MaximumUploadBytesPerFrame { get; }
        public int MaximumDocuments { get; }
    }

    /// <summary>Reports cumulative work and current occupancy for a device-scoped SVG raster cache.</summary>
    public readonly struct SvgRasterCacheDiagnostics
    {
        internal SvgRasterCacheDiagnostics(int pageCount, int capacity, int usedArea, int entryCount, int documentCount, long hits, long misses, long parses, long rasterizations, long uploads, long evictions, long documentEvictions, long failures, string lastDiagnostic, long bytes, int pendingUploads, long uploadBytes, long parseTicks, long rasterTicks)
        {
            PageCount = pageCount;
            Capacity = capacity;
            UsedArea = usedArea;
            EntryCount = entryCount;
            DocumentCount = documentCount;
            Hits = hits;
            Misses = misses;
            Parses = parses;
            Rasterizations = rasterizations;
            Uploads = uploads;
            Evictions = evictions;
            DocumentEvictions = documentEvictions;
            Failures = failures;
            LastDiagnostic = lastDiagnostic ?? string.Empty;
            Bytes = bytes;
            PendingUploads = pendingUploads;
            UploadBytes = uploadBytes;
            ParseTime = TimeSpan.FromSeconds((double)parseTicks / Stopwatch.Frequency);
            RasterTime = TimeSpan.FromSeconds((double)rasterTicks / Stopwatch.Frequency);
        }

        public int PageCount { get; }
        public int Capacity { get; }
        public int UsedArea { get; }
        public int EntryCount { get; }
        public int DocumentCount { get; }
        public long Hits { get; }
        public long Misses { get; }
        public long Parses { get; }
        public long Rasterizations { get; }
        public long Uploads { get; }
        public long Evictions { get; }
        public long DocumentEvictions { get; }
        public long Failures { get; }
        public string LastDiagnostic { get; }
        public long Bytes { get; }
        public int PendingUploads { get; }
        public long UploadBytes { get; }
        public TimeSpan ParseTime { get; }
        public TimeSpan RasterTime { get; }
    }

    /// <summary>Provides an immutable RGBA snapshot of an SVG atlas page for diagnostics.</summary>
    public sealed class SvgRasterAtlasPageSnapshot
    {
        internal SvgRasterAtlasPageSnapshot(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = new ReadOnlyMemory<byte>(pixels);
        }

        public int Width { get; }
        public int Height { get; }
        public ReadOnlyMemory<byte> Pixels { get; }
    }

    internal readonly struct SvgRasterKey : IEquatable<SvgRasterKey>
    {
        internal SvgRasterKey(string contentIdentity, int width, int height, int renderOptionsKey = 0)
        {
            ContentIdentity = contentIdentity ?? throw new ArgumentNullException(nameof(contentIdentity));
            Width = width;
            Height = height;
            RenderOptionsKey = renderOptionsKey;
        }

        internal string ContentIdentity { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal int RenderOptionsKey { get; }
        public bool Equals(SvgRasterKey other) => Width == other.Width && Height == other.Height && RenderOptionsKey == other.RenderOptionsKey && StringComparer.Ordinal.Equals(ContentIdentity, other.ContentIdentity);
        public override bool Equals(object obj) => obj is SvgRasterKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(ContentIdentity), Width, Height, RenderOptionsKey);
    }

    internal sealed class SvgRasterAtlasEntry
    {
        internal SvgRasterAtlasEntry(SvgRasterKey key, int pageIndex, Rectangle bounds)
        {
            Key = key;
            PageIndex = pageIndex;
            Bounds = bounds;
        }

        internal SvgRasterKey Key { get; }
        internal int PageIndex { get; }
        internal Rectangle Bounds { get; }
        internal bool IsAvailable => PageIndex >= 0;
        internal bool Uploaded { get; set; }
    }

    internal sealed class SvgRasterAtlasStore : IDisposable
    {
        private readonly SvgRasterCacheOptions _options;
        private readonly Dictionary<SvgRasterKey, SvgRasterAtlasEntry> _entries = new Dictionary<SvgRasterKey, SvgRasterAtlasEntry>();
        private readonly Dictionary<string, DocumentRecord> _documents = new Dictionary<string, DocumentRecord>(StringComparer.Ordinal);
        private readonly List<AtlasPage> _pages = new List<AtlasPage>();
        private long _frame;
        private int _frameDepth;
        private bool _disposed;
        private long _hits;
        private long _misses;
        private long _parses;
        private long _rasterizations;
        private long _uploads;
        private long _evictions;
        private long _documentEvictions;
        private long _failures;
        private long _uploadBytes;
        private long _parseTicks;
        private long _rasterTicks;
        private string _lastDiagnostic = string.Empty;

        internal SvgRasterAtlasStore(SvgRasterCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        internal int PageCount => _pages.Count;

        internal void BeginFrame()
        {
            ThrowIfDisposed();
            if (_frameDepth == 0) _frame = checked(_frame + 1);
            _frameDepth = checked(_frameDepth + 1);
        }

        internal void EndFrame()
        {
            ThrowIfDisposed();
            if (_frameDepth == 0) throw new InvalidOperationException("No SVG atlas frame is active.");
            _frameDepth--;
        }

        internal SvgRasterAtlasEntry GetOrAdd(SvgImageSource source, int width, int height, int renderOptionsKey = 0)
        {
            ThrowIfDisposed();
            if (_frameDepth == 0) throw new InvalidOperationException("BeginFrame must be called before requesting SVG rasters.");
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            var key = new SvgRasterKey(source.ContentIdentity, width, height, renderOptionsKey);
            if (_entries.TryGetValue(key, out var existing))
            {
                _hits++;
                Touch(existing);
                return existing;
            }

            _misses++;
            if (width > _options.PageWidth - _options.Padding * 2 || height > _options.PageHeight - _options.Padding * 2)
                return Failure(key, "SVG raster does not fit an empty atlas page.");

            var document = GetDocument(source);
            SvgRasterData raster;
            var rasterStarted = Stopwatch.GetTimestamp();
            try
            {
                raster = SvgBackendRegistry.Backend.Rasterize(document.Document, width, height);
                _rasterizations++;
            }
            catch (Exception exception)
            {
                _failures++;
                _lastDiagnostic = $"SVG rasterization failed: {exception.GetType().Name}.";
                throw;
            }
            finally
            {
                _rasterTicks = checked(_rasterTicks + Stopwatch.GetTimestamp() - rasterStarted);
            }
            if (raster.Width != width || raster.Height != height)
                throw new InvalidOperationException("The SVG backend returned raster dimensions that do not match the cache key.");

            if (!TryFindPage(width, height, out var pageIndex, out var bounds)) return Failure(key, _lastDiagnostic);
            var page = _pages[pageIndex];
            CopyRaster(raster.Pixels, page.Pixels, bounds, _options.PageWidth);
            page.Dirty = true;
            page.LastUsedFrame = _frame;
            var entry = new SvgRasterAtlasEntry(key, pageIndex, bounds);
            page.Keys.Add(key);
            _entries.Add(key, entry);
            return entry;
        }

        internal bool Contains(SvgRasterKey key) => _entries.ContainsKey(key);
        internal ReadOnlyMemory<byte> GetPagePixels(int pageIndex) => _pages[pageIndex].Pixels;
        internal bool IsPageDirty(int pageIndex) => _pages[pageIndex].Dirty;

        internal void MarkPageUploaded(int pageIndex)
        {
            var page = _pages[pageIndex];
            if (!page.Dirty) return;
            page.Dirty = false;
            foreach (var key in page.Keys)
                if (_entries.TryGetValue(key, out var entry)) entry.Uploaded = true;
            _uploads++;
            _uploadBytes = checked(_uploadBytes + page.Pixels.Length);
        }

        internal void MarkAllPagesDirty()
        {
            ThrowIfDisposed();
            foreach (var page in _pages)
            {
                page.Dirty = true;
                foreach (var key in page.Keys)
                    if (_entries.TryGetValue(key, out var entry)) entry.Uploaded = false;
            }
        }

        internal SvgRasterCacheDiagnostics GetDiagnostics()
        {
            var usedArea = 0;
            var pendingUploads = 0;
            foreach (var page in _pages)
            {
                usedArea = checked(usedArea + page.Allocator.UsedArea);
                if (page.Dirty) pendingUploads++;
            }
            return new SvgRasterCacheDiagnostics(
                _pages.Count,
                checked(_pages.Count * _options.PageWidth * _options.PageHeight),
                usedArea,
                _entries.Count,
                _documents.Count,
                _hits,
                _misses,
                _parses,
                _rasterizations,
                _uploads,
                _evictions,
                _documentEvictions,
                _failures,
                _lastDiagnostic,
                checked((long)_pages.Count * _options.PageWidth * _options.PageHeight * 4),
                pendingUploads,
                _uploadBytes,
                _parseTicks,
                _rasterTicks);
        }

        internal IReadOnlyList<SvgRasterAtlasPageSnapshot> GetDebugPages()
        {
            var snapshots = new List<SvgRasterAtlasPageSnapshot>(_pages.Count);
            foreach (var page in _pages) snapshots.Add(new SvgRasterAtlasPageSnapshot(_options.PageWidth, _options.PageHeight, (byte[])page.Pixels.Clone()));
            return new ReadOnlyCollection<SvgRasterAtlasPageSnapshot>(snapshots);
        }

        internal void Clear()
        {
            ThrowIfDisposed();
            if (_frameDepth > 0) throw new InvalidOperationException("The SVG atlas cannot be cleared during an active frame.");
            _entries.Clear();
            _pages.Clear();
            DisposeDocuments();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_frameDepth > 0) throw new InvalidOperationException("The SVG atlas cannot be disposed during an active frame.");
            _disposed = true;
            _entries.Clear();
            _pages.Clear();
            DisposeDocuments();
        }

        private DocumentRecord GetDocument(SvgImageSource source)
        {
            if (_documents.TryGetValue(source.ContentIdentity, out var existing))
            {
                existing.LastUsedFrame = _frame;
                return existing;
            }

            var parseStarted = Stopwatch.GetTimestamp();
            try
            {
                EnsureDocumentCapacity();
                var record = new DocumentRecord(SvgBackendRegistry.Backend.Parse(source.CopySource()), _frame);
                _documents.Add(source.ContentIdentity, record);
                _parses++;
                return record;
            }
            catch (Exception exception)
            {
                _failures++;
                _lastDiagnostic = $"SVG parsing failed: {exception.GetType().Name}.";
                throw;
            }
            finally
            {
                _parseTicks = checked(_parseTicks + Stopwatch.GetTimestamp() - parseStarted);
            }
        }

        private bool TryFindPage(int width, int height, out int pageIndex, out Rectangle bounds)
        {
            for (var index = 0; index < _pages.Count; index++)
            {
                if (!_pages[index].Allocator.TryAllocate(width, height, _options.Padding, out bounds)) continue;
                pageIndex = index;
                return true;
            }

            if (_pages.Count < _options.MaximumPages)
            {
                var page = new AtlasPage(_options.PageWidth, _options.PageHeight);
                _pages.Add(page);
                if (page.Allocator.TryAllocate(width, height, _options.Padding, out bounds))
                {
                    pageIndex = _pages.Count - 1;
                    return true;
                }
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
            if (evictionIndex < 0)
            {
                _lastDiagnostic = "The SVG atlas budget is full and every page is active in the current frame.";
                pageIndex = -1;
                bounds = Rectangle.Empty;
                return false;
            }

            var evicted = _pages[evictionIndex];
            var evictedCount = evicted.Keys.Count;
            foreach (var key in evicted.Keys) _entries.Remove(key);
            _evictions = checked(_evictions + evictedCount);
            _lastDiagnostic = $"Evicted {evictedCount} SVG rasters from the least-recently-used inactive atlas page.";
            evicted.Reset();
            PruneDocuments();
            if (!evicted.Allocator.TryAllocate(width, height, _options.Padding, out bounds))
            {
                _lastDiagnostic = "SVG raster does not fit an empty atlas page.";
                pageIndex = -1;
                return false;
            }
            pageIndex = evictionIndex;
            return true;
        }

        private SvgRasterAtlasEntry Failure(SvgRasterKey key, string diagnostic)
        {
            _failures++;
            _lastDiagnostic = diagnostic;
            return new SvgRasterAtlasEntry(key, -1, Rectangle.Empty) { Uploaded = true };
        }

        private void Touch(SvgRasterAtlasEntry entry)
        {
            if (entry.PageIndex >= 0) _pages[entry.PageIndex].LastUsedFrame = _frame;
            if (_documents.TryGetValue(entry.Key.ContentIdentity, out var document)) document.LastUsedFrame = _frame;
        }

        private void PruneDocuments()
        {
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in _entries.Keys) identities.Add(key.ContentIdentity);
            var removed = new List<string>();
            foreach (var pair in _documents)
                if (pair.Value.LastUsedFrame != _frame && !identities.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var identity in removed)
            {
                _documents[identity].Document.Dispose();
                _documents.Remove(identity);
                _documentEvictions++;
            }
        }

        private void EnsureDocumentCapacity()
        {
            if (_documents.Count < _options.MaximumDocuments) return;
            string oldestIdentity = null;
            var oldestFrame = long.MaxValue;
            foreach (var pair in _documents)
            {
                if (pair.Value.LastUsedFrame >= oldestFrame) continue;
                oldestIdentity = pair.Key;
                oldestFrame = pair.Value.LastUsedFrame;
            }
            if (oldestIdentity == null) return;
            _documents[oldestIdentity].Document.Dispose();
            _documents.Remove(oldestIdentity);
            _documentEvictions++;
        }

        private void DisposeDocuments()
        {
            foreach (var record in _documents.Values) record.Document.Dispose();
            _documents.Clear();
        }

        private static void CopyRaster(byte[] source, byte[] destination, Rectangle bounds, int destinationWidth)
        {
            var sourceStride = checked(bounds.Width * 4);
            var destinationStride = checked(destinationWidth * 4);
            for (var row = 0; row < bounds.Height; row++)
                source.AsSpan(row * sourceStride, sourceStride).CopyTo(destination.AsSpan((bounds.Y + row) * destinationStride + bounds.X * 4, sourceStride));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SvgRasterAtlasStore));
        }

        private sealed class DocumentRecord
        {
            internal DocumentRecord(ISvgBackendDocument document, long lastUsedFrame)
            {
                Document = document;
                LastUsedFrame = lastUsedFrame;
            }

            internal ISvgBackendDocument Document { get; }
            internal long LastUsedFrame { get; set; }
        }

        private sealed class AtlasPage
        {
            internal AtlasPage(int width, int height)
            {
                Allocator = new GlyphRectangleAllocator(width, height);
                Pixels = new byte[checked(width * height * 4)];
            }

            internal GlyphRectangleAllocator Allocator { get; }
            internal byte[] Pixels { get; }
            internal List<SvgRasterKey> Keys { get; } = new List<SvgRasterKey>();
            internal bool Dirty { get; set; }
            internal long LastUsedFrame { get; set; }

            internal void Reset()
            {
                Allocator.Reset();
                Array.Clear(Pixels, 0, Pixels.Length);
                Keys.Clear();
                Dirty = true;
                LastUsedFrame = 0;
            }
        }
    }

    internal sealed class SvgRasterCache : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SvgRasterCacheOptions _options;
        private readonly SvgRasterAtlasStore _store;
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly int _renderThreadId;
        private bool _disposed;

        internal bool IsDisposed => _disposed;

        internal SvgRasterCache(GraphicsDevice graphicsDevice, SvgRasterCacheOptions options = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _options = options ?? new SvgRasterCacheOptions();
            _store = new SvgRasterAtlasStore(_options);
            _renderThreadId = Environment.CurrentManagedThreadId;
            _graphicsDevice.DeviceReset += OnDeviceReset;
            _graphicsDevice.Disposing += OnGraphicsDeviceDisposing;
        }

        internal SvgRasterCacheDiagnostics Diagnostics
        {
            get { ThrowIfDisposed(); return _store.GetDiagnostics(); }
        }

        internal void BeginFrame()
        {
            ThrowIfDisposed();
            _store.BeginFrame();
        }

        internal SvgRasterAtlasEntry GetOrAdd(SvgImageSource source, int width, int height, int renderOptionsKey = 0)
        {
            ThrowIfDisposed();
            return _store.GetOrAdd(source, width, height, renderOptionsKey);
        }

        internal void EndFrame()
        {
            ThrowIfDisposed();
            _store.EndFrame();
        }

        internal void FlushUploads()
        {
            ThrowIfDisposed();
            EnsureRenderThread();
            while (_textures.Count < _store.PageCount)
                _textures.Add(new Texture2D(_graphicsDevice, _options.PageWidth, _options.PageHeight, false, SurfaceFormat.Color));
            var uploadedBytes = 0;
            var pageBytes = checked(_options.PageWidth * _options.PageHeight * 4);
            for (var pageIndex = 0; pageIndex < _store.PageCount; pageIndex++)
            {
                if (!_store.IsPageDirty(pageIndex)) continue;
                if (uploadedBytes > 0 && uploadedBytes + pageBytes > _options.MaximumUploadBytesPerFrame) break;
                _textures[pageIndex].SetData(_store.GetPagePixels(pageIndex).ToArray());
                _store.MarkPageUploaded(pageIndex);
                uploadedBytes = checked(uploadedBytes + pageBytes);
            }
        }

        internal Texture2D GetTexture(SvgRasterAtlasEntry entry)
        {
            ThrowIfDisposed();
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (!entry.IsAvailable || !entry.Uploaded) return null;
            if (entry.PageIndex >= _textures.Count) throw new InvalidOperationException("FlushUploads must be called before drawing SVG rasters.");
            return _textures[entry.PageIndex];
        }

        internal IReadOnlyList<SvgRasterAtlasPageSnapshot> GetDebugPages()
        {
            ThrowIfDisposed();
            return _store.GetDebugPages();
        }

        internal void Clear()
        {
            ThrowIfDisposed();
            EnsureRenderThread();
            _store.Clear();
            DisposeTextures();
        }

        public void Dispose()
        {
            if (_disposed) return;
            EnsureRenderThread();
            _disposed = true;
            _graphicsDevice.DeviceReset -= OnDeviceReset;
            _graphicsDevice.Disposing -= OnGraphicsDeviceDisposing;
            _store.Dispose();
            DisposeTextures();
        }

        private void OnDeviceReset(object sender, EventArgs args)
        {
            if (_disposed) return;
            EnsureRenderThread();
            DisposeTextures();
            _store.MarkAllPagesDirty();
        }

        private void OnGraphicsDeviceDisposing(object sender, EventArgs args) => Dispose();

        private void DisposeTextures()
        {
            foreach (var texture in _textures) texture.Dispose();
            _textures.Clear();
        }

        private void EnsureRenderThread()
        {
            if (Environment.CurrentManagedThreadId != _renderThreadId)
                throw new InvalidOperationException("SVG texture creation, upload, and disposal must run on the render thread.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SvgRasterCache));
        }
    }

    internal sealed class SvgRasterCacheLease : IDisposable
    {
        private static readonly ConditionalWeakTable<GraphicsDevice, SharedCache> SharedCaches = new ConditionalWeakTable<GraphicsDevice, SharedCache>();
        private SharedCache _shared;

        private SvgRasterCacheLease(SharedCache shared)
        {
            _shared = shared;
            Cache = shared.Cache;
        }

        internal SvgRasterCache Cache { get; }

        internal static SvgRasterCacheLease Acquire(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
            var shared = SharedCaches.GetValue(graphicsDevice, device => new SharedCache(device));
            lock (shared)
            {
                if (shared.Cache.IsDisposed) throw new ObjectDisposedException(nameof(GraphicsDevice));
                shared.ReferenceCount++;
            }
            return new SvgRasterCacheLease(shared);
        }

        public void Dispose()
        {
            var shared = _shared;
            if (shared == null) return;
            _shared = null;
            lock (shared)
            {
                shared.ReferenceCount--;
            }
        }

        private sealed class SharedCache
        {
            internal SharedCache(GraphicsDevice graphicsDevice) => Cache = new SvgRasterCache(graphicsDevice);
            internal SvgRasterCache Cache { get; }
            internal int ReferenceCount { get; set; }
        }
    }
}