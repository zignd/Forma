// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Forma.FnaVideoSmoke;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Forma.FnaVideoSmoke <video.ogv>");
            return 2;
        }

        try
        {
            using var game = new VideoSmokeGame(Path.GetFullPath(args[0]));
            game.Run();
            Console.WriteLine(game.Result);
            return game.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

internal sealed class VideoSmokeGame : Game
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LoopObservation = TimeSpan.FromSeconds(2);
    private readonly string _videoPath;
    private readonly GraphicsDeviceManager _graphics;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private readonly HashSet<uint> _frameHashes = new HashSet<uint>();
    private VideoStreamPlayer? _player;
    private Color[]? _pixels;
    private bool _observedPlayback;
    private bool _validatedPauseResume;
    private bool _naturalCompletionObserved;
    private int _naturalFrameCount;

    public VideoSmokeGame(string videoPath)
    {
        _videoPath = videoPath;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 160,
            PreferredBackBufferHeight = 90,
            SynchronizeWithVerticalRetrace = false,
        };
        IsFixedTimeStep = false;
        Window.Title = "Forma FNA Video Smoke";
    }

    public bool Succeeded { get; private set; }
    public string Result { get; private set; } = "FNA video smoke did not complete.";

    protected override void LoadContent()
    {
        var video = VideoStreamPlayer.LoadLocalFile(_videoPath, GraphicsDevice);
        if (video.Width <= 0 || video.Height <= 0 || video.FramesPerSecond <= 0)
            throw new InvalidDataException("The video fixture has invalid dimensions or frame rate.");

        _pixels = new Color[video.Width * video.Height];
        _player = new VideoStreamPlayer
        {
            Stream = video,
            Volume = 0,
            Loop = false,
        };
        _player.Play();
        _stopwatch.Start();
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_player == null || _pixels == null) return;

        if (_player.PlaybackState != MediaState.Stopped)
        {
            _observedPlayback = true;
            var texture = _player.GetVideoTexture();
            if (texture != null)
            {
                texture.GetData(_pixels);
                _frameHashes.Add(HashFrame(_pixels));
                if (!_validatedPauseResume)
                {
                    _player.SetPaused(true);
                    if (_player.PlaybackState != MediaState.Paused)
                        throw new InvalidOperationException("FNA video playback did not pause.");
                    _player.SetVolume(.25f);
                    if (Math.Abs(_player.GetVolume() - .25f) > .001f)
                        throw new InvalidOperationException("FNA video playback did not retain volume.");
                    _player.SetPaused(false);
                    if (_player.PlaybackState != MediaState.Playing)
                        throw new InvalidOperationException("FNA video playback did not resume.");
                    _player.SetVolume(0);
                    _validatedPauseResume = true;
                }
            }
        }

        if (_observedPlayback && _player.PlaybackState == MediaState.Stopped && !_naturalCompletionObserved)
        {
            if (_frameHashes.Count < 2 || !_validatedPauseResume)
            {
                Result = $"FNA video smoke decoded only {_frameHashes.Count} distinct frame(s) before completion.";
                Exit();
                return;
            }

            _naturalCompletionObserved = true;
            _naturalFrameCount = _frameHashes.Count;
            _frameHashes.Clear();
            _player.SetLoop(true);
            _player.Play();
            _stopwatch.Restart();
        }
        else if (_naturalCompletionObserved && _stopwatch.Elapsed >= LoopObservation)
        {
            Succeeded = _player.PlaybackState == MediaState.Playing && _frameHashes.Count >= 2;
            Result = Succeeded
                ? $"FNA video smoke: {_naturalFrameCount} completion frames, {_frameHashes.Count} looping frames, audiovisual playback passed."
                : $"FNA video smoke loop failed with state {_player.PlaybackState} and {_frameHashes.Count} distinct frame(s).";
            Exit();
        }
        else if (!_naturalCompletionObserved && _stopwatch.Elapsed > Timeout)
        {
            Result = $"FNA video smoke timed out with {_frameHashes.Count} distinct decoded frame(s).";
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _player?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static uint HashFrame(IEnumerable<Color> pixels)
    {
        var hash = 2166136261u;
        foreach (var pixel in pixels)
        {
            hash = (hash ^ pixel.PackedValue) * 16777619u;
        }
        return hash;
    }
}