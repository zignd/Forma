// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma
{
    internal sealed class RuntimeClipboard : IClipboard
    {
        public static RuntimeClipboard Instance { get; } = new RuntimeClipboard();

        private static bool UsesSdl3 => Environment.GetEnvironmentVariable("FNA_PLATFORM_BACKEND") != "SDL2";

        private RuntimeClipboard() { }

        public string GetText()
        {
            try
            {
                return UsesSdl3 ? SDL3.SDL.SDL_GetClipboardText() : SDL2.SDL.SDL_GetClipboardText();
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                return null;
            }
        }

        public bool SetText(string text)
        {
            try
            {
                return UsesSdl3
                    ? SDL3.SDL.SDL_SetClipboardText(text ?? string.Empty)
                    : SDL2.SDL.SDL_SetClipboardText(text ?? string.Empty) == 0;
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                return false;
            }
        }

        private static bool IsUnavailable(Exception exception) =>
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException;
    }
}