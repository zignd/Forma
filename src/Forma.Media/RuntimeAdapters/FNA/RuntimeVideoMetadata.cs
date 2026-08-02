// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework.Media;

namespace Forma
{
    internal static class RuntimeVideoMetadata
    {
        public static string GetStreamName(Video stream) => stream == null ? "<No Stream>" : "<Video Stream>";
    }
}