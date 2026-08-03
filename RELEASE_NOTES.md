# Release Notes

## Unreleased

- Added Forma-native compiled XAML through matching `Forma.Xaml.Build.MonoGame` and
  `Forma.Xaml.Build.FNA` build-only packages. V1 includes code-behind populate semantics,
  namescopes, resources, selector styles, typed one/two-way bindings, triggers, and deterministic
  storyboards without a runtime XAML reader or reflection binding fallback.
- Added `forma-xaml` validation, schema, watch, JSON/SARIF output, and stdio LSP support for
  diagnostics, completion, hover, definitions, references, rename, and formatting.
- Added opt-in Debug hot reload with frame-boundary latest-wins replacement and rollback on invalid
  edits, plus one shared playable XAML sample with thin MonoGame and FNA hosts.
- Added deterministic build fixtures, package consumers, and macOS arm64 trim/NativeAOT gates that
  execute compiled XAML and typed two-way bindings while excluding compiler and development assets.

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
  validated on macOS arm64.
- Added [dynamic text deployment and migration guidance](docs/dynamic-text.md), including ownership,
  cache limits, rollback, and restricted-platform considerations.
