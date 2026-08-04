// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// Control APIs and behavior are adapted from Godot Engine's video_stream_player.cpp;
// see THIRD-PARTY-NOTICES.md.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Forma
{
    [Flags]
    public enum VideoPlaybackCapabilities
    {
        None = 0,
        BuiltInPlayback = 1,
        LocalFileLoading = 2,
        Seeking = 4,
        Looping = 8,
        Audio = 16,
    }

    /// <summary>Playback operations consumed by <see cref="VideoStreamPlayer"/>.</summary>
    public interface IVideoPlaybackBackend : IDisposable
    {
        MediaState State { get; }
        TimeSpan PlayPosition { get; }
        bool IsLooped { get; set; }
        float Volume { get; set; }
        void Play(Video stream);
        void Pause();
        void Resume();
        void Stop();
        Texture2D GetTexture();
        bool TrySetPlayPosition(TimeSpan position);
    }

    /// <summary>XNA-compatible VideoPlayer-backed UI control for video streams.</summary>
    public sealed class VideoStreamPlayer : Control, IDisposable
    {
        private readonly Func<IVideoPlaybackBackend> _backendFactory;
        private IVideoPlaybackBackend _backend;
        private bool _backendUnavailable;
        private string _backendUnavailableReason;
        private Video _stream;
        private bool _autoplay;
        private bool _loop;
        private bool _paused;
        private bool _expand;
        private bool _playRequested;
        private float _volume = 1;
        private float _speedScale = 1;
        private int _bufferingMsec = 500;
        private int _audioTrack;
        private string _bus = "Master";

        public VideoStreamPlayer() : this(() => new BuiltInVideoPlaybackBackend()) { }
        public VideoStreamPlayer(IVideoPlaybackBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _backendFactory = () => backend;
            _backend.IsLooped = Loop;
            _backend.Volume = Volume;
        }
        private VideoStreamPlayer(Func<IVideoPlaybackBackend> backendFactory) =>
            _backendFactory = backendFactory ?? throw new ArgumentNullException(nameof(backendFactory));
        public Video Stream { get => _stream; set { if (_stream == value) return; Stop(); _stream = value; QueueLayout(); if (Autoplay && value != null) Play(); } }
        public bool Autoplay { get => _autoplay; set => _autoplay = value; }
        public bool Loop { get => _loop; set { _loop = value; if (_backend != null) _backend.IsLooped = value; } }
        public bool Paused { get => _paused; set { if (_paused == value) return; _paused = value; if (_stream == null || _backend == null) return; if (value) _backend.Pause(); else _backend.Resume(); } }
        public float Volume { get => _volume; set { _volume = value; if (_backend != null) _backend.Volume = MathHelper.Clamp(value, 0, 1); } }
        public float SpeedScale { get => _speedScale; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); _speedScale = value; } }
        public bool Expand { get => _expand; set { if (_expand == value) return; _expand = value; QueueLayout(); } }
        public int BufferingMsec { get => _bufferingMsec; set => _bufferingMsec = value; }
        public int AudioTrack { get => _audioTrack; set => _audioTrack = value; }
        public string Bus { get => _bus; set => _bus = string.IsNullOrEmpty(value) ? "Master" : value; }
        public MediaState PlaybackState => _backend?.State ?? MediaState.Stopped;
        public static VideoPlaybackCapabilities RuntimeCapabilities =>
            RuntimeVideoLoader.Capabilities |
            (RuntimeVideoPlaybackAdapter.SupportsSeeking ? VideoPlaybackCapabilities.Seeking : VideoPlaybackCapabilities.None);
        public bool IsPlaybackAvailable => !_backendUnavailable;
        public string PlaybackUnavailableReason => _backendUnavailableReason;
        public event EventHandler Finished;

        public static Video LoadLocalFile(string path, GraphicsDevice graphicsDevice)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A video path is required.", nameof(path));
            if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("The video file was not found.", fullPath);
            return RuntimeVideoLoader.LoadLocalFile(fullPath, graphicsDevice);
        }

        public override Vector2 GetMinimumSize()
        {
            if (Expand || Stream == null) return CustomMinimumSize;
            return Vector2.Max(CustomMinimumSize, new Vector2(Stream.Width, Stream.Height));
        }
        public void Play()
        {
            if (_stream == null || !EnsureBackend()) return;
            try
            {
                _backend.Play(_stream);
                if (Paused) _backend.Pause();
                _playRequested = true;
            }
            catch (NotImplementedException exception) { MarkBackendUnavailable(exception); }
            catch (PlatformNotSupportedException exception) { MarkBackendUnavailable(exception); }
        }
        public void Stop() { _backend?.Stop(); _playRequested = false; }
        public bool IsPlaying() => _backend?.State == MediaState.Playing;
        public void SetStream(Video stream) => Stream = stream;
        public Video GetStream() => Stream;
        public void SetPaused(bool paused) => Paused = paused;
        public bool IsPaused() => Paused;
        public void SetLoop(bool loop) => Loop = loop;
        public bool HasLoop() => Loop;
        public void SetVolume(float volume) => Volume = volume;
        public float GetVolume() => Volume;
        public void SetVolumeDb(float db) => Volume = db < -79 ? 0 : MathF.Pow(10, db / 20);
        public float GetVolumeDb() => Volume == 0 ? -80 : 20 * MathF.Log10(Volume);
        public void SetSpeedScale(float speedScale) => SpeedScale = speedScale;
        public float GetSpeedScale() => SpeedScale;
        public string GetStreamName() => RuntimeVideoMetadata.GetStreamName(Stream);
        public double GetStreamLength() => Stream?.Duration.TotalSeconds ?? 0;
        public double GetStreamPosition() => _backend?.PlayPosition.TotalSeconds ?? 0;
        public void SetStreamPosition(double position) =>
            _backend?.TrySetPlayPosition(TimeSpan.FromSeconds(Math.Max(0, position)));
        public void SetAutoplay(bool enabled) => Autoplay = enabled;
        public bool HasAutoplay() => Autoplay;
        public void SetExpand(bool enable) => Expand = enable;
        public bool HasExpand() => Expand;
        public void SetAudioTrack(int track) => AudioTrack = track;
        public int GetAudioTrack() => AudioTrack;
        public void SetBufferingMsec(int msec) => BufferingMsec = msec;
        public int GetBufferingMsec() => BufferingMsec;
        public void SetBus(string bus) => Bus = bus;
        public string GetBus() => Bus;
        public Texture2D GetVideoTexture()
        {
            if (Stream == null || _backend == null || _backend.State == MediaState.Stopped) return null;
            try { return _backend.GetTexture(); }
            catch (InvalidOperationException) { return null; }
        }
        public void Dispose() => _backend?.Dispose();
        internal override void Process(GameTime gameTime)
        {
            if (_playRequested && PlaybackState == MediaState.Stopped && !Loop)
            {
                _playRequested = false;
                Finished?.Invoke(this, EventArgs.Empty);
            }
            base.Process(gameTime);
        }
        internal override void Draw(UIRenderContext context)
        {
            var texture = GetVideoTexture();
            if (texture != null)
            {
                var destination = Bounds;
                if (!Expand) { destination.Width = texture.Width; destination.Height = texture.Height; }
                context.SpriteBatch.Draw(texture, destination, Color.White);
            }
            base.Draw(context);
        }
        private bool EnsureBackend()
        {
            if (_backend != null) return true;
            if (_backendUnavailable) return false;
            try
            {
                _backend = _backendFactory();
                _backend.IsLooped = Loop;
                _backend.Volume = MathHelper.Clamp(Volume, 0, 1);
                return true;
            }
            catch (NotImplementedException exception) { MarkBackendUnavailable(exception); return false; }
            catch (PlatformNotSupportedException exception) { MarkBackendUnavailable(exception); return false; }
        }
        private void MarkBackendUnavailable(Exception exception)
        {
            _backendUnavailable = true;
            _backendUnavailableReason = exception.Message;
            _playRequested = false;
            _backend?.Dispose();
            _backend = null;
        }
    }

    internal sealed class BuiltInVideoPlaybackBackend : IVideoPlaybackBackend
    {
        private readonly VideoPlayer _player = new VideoPlayer();

        public MediaState State => _player.State;
        public TimeSpan PlayPosition => _player.PlayPosition;
        public bool IsLooped { get => _player.IsLooped; set => _player.IsLooped = value; }
        public float Volume { get => _player.Volume; set => _player.Volume = value; }
        public void Play(Video stream) => _player.Play(stream);
        public void Pause() => _player.Pause();
        public void Resume() => _player.Resume();
        public void Stop() => _player.Stop();
        public Texture2D GetTexture() => _player.GetTexture();
        public bool TrySetPlayPosition(TimeSpan position) => RuntimeVideoPlaybackAdapter.TrySetPlayPosition(_player, position);
        public void Dispose() => _player.Dispose();
    }
}