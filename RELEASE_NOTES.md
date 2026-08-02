# Release Notes

## Unreleased

- Added matching optional `Forma.DynamicText.MonoGame` and `Forma.DynamicText.FNA` package surfaces
  for bounded runtime font loading, FreeType rasterization, HarfBuzz shaping, Unicode layout,
  fallback, variable fonts, and device-scoped Alpha8 glyph caches.
- Added immutable `UIFont`, `DynamicUIFont`, `SpriteFontAdapter`, `TextLayoutOptions`, `TextLayout`,
  and `TextLayoutEngine` contracts. Existing control `Font` properties remain supported; parallel
  `UIFont` properties are additive and last assignment wins.
- Added a searchable catalog Typography section with dynamic-size, density, fallback, shaping,
  bidirectional, atlas, failure, and SpriteFont compatibility diagnostics shared by MonoGame and
  FNA hosts.
- Added explicit native-free package verification for core SpriteFont consumers across the declared
  desktop runtime/RID matrix. Dynamic native dependencies remain opt-in; trimming and NativeAOT are
  not yet supported.
- Added [dynamic text deployment and migration guidance](docs/dynamic-text.md), including ownership,
  cache limits, rollback, and restricted-platform considerations.
