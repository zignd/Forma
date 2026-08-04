// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

using var face = UIFontFace.FromMemory(new byte[] { 1, 2, 3, 4 });
if (face.FamilyName != "Platform Sans" || face.GetGlyphId('A') == 0) return 1;
if (face.Shape("Forma مرحبا क्ष", 18).Glyphs.Count == 0) return 1;
if (face.RasterizeCharacter('A', 18).Pixels.Length == 0) return 1;
var diagnostics = DynamicTextNativeDiagnostics.Current;
if (diagnostics.FreeTypeLibraryName != "platform-font" || diagnostics.HarfBuzzLibraryName != "platform-shaper") return 1;
return 0;