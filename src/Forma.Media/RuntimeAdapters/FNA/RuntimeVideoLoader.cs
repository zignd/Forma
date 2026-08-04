// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Forma
{
    internal static class RuntimeVideoPlaybackAdapter
    {
        public const bool SupportsSeeking = false;

        public static bool TrySetPlayPosition(VideoPlayer player, TimeSpan position) => false;
    }

    internal static class RuntimeVideoLoader
    {
        public static VideoPlaybackCapabilities Capabilities =>
            VideoPlaybackCapabilities.BuiltInPlayback |
            VideoPlaybackCapabilities.LocalFileLoading |
            VideoPlaybackCapabilities.Looping |
            VideoPlaybackCapabilities.Audio;

        public static Video LoadLocalFile(string fullPath, GraphicsDevice graphicsDevice) =>
            Video.FromUriEXT(new Uri(fullPath), graphicsDevice);
    }
}