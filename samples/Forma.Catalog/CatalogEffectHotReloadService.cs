// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

internal sealed class CatalogEffectHotReloadService : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly string _effectPath;
    private DateTime _lastWriteTimeUtc;
    private long _lastLength;

    public CatalogEffectHotReloadService(GraphicsDevice graphicsDevice, string effectPath)
    {
        _graphicsDevice = graphicsDevice;
        _effectPath = effectPath;
        Reload(throwOnFailure: true);
    }

    public Effect Current { get; private set; }
    public bool? LastReloadSucceeded { get; private set; }
    public double? LastReloadMilliseconds { get; private set; }
    public string LastReloadMessage { get; private set; }

    public void Update()
    {
        var file = new FileInfo(_effectPath);
        if (file.LastWriteTimeUtc == _lastWriteTimeUtc && file.Length == _lastLength) return;
        Reload(throwOnFailure: false);
    }

    public void Dispose() => Current?.Dispose();

    private void Reload(bool throwOnFailure)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var file = new FileInfo(_effectPath);
            var replacement = new Effect(_graphicsDevice, File.ReadAllBytes(_effectPath));
            var previous = Current;
            Current = replacement;
            previous?.Dispose();
            _lastWriteTimeUtc = file.LastWriteTimeUtc;
            _lastLength = file.Length;
            LastReloadSucceeded = true;
            LastReloadMessage = "Effect reloaded.";
        }
        catch (Exception exception) when (!throwOnFailure)
        {
            LastReloadSucceeded = false;
            LastReloadMessage = exception.Message;
        }
        finally
        {
            stopwatch.Stop();
            LastReloadMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}