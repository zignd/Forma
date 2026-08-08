# Release Notes

## 0.1.0-alpha.2

- Added explicit `Forma.Svg.Skia.*` and `Forma.Svg.ThorVG.*` backend packages with one
  immutable process-wide selection contract. Legacy `Forma.Svg.*` packages retain Skia behavior for
  the `0.x` migration window, emit a migration warning, and are scheduled for removal in `1.0.0`.
- Added ThorVG 1.1.0 through a nine-symbol Forma C ABI, source-generated managed interop, `SafeHandle`
  documents, dynamic desktop assets, and a dead-stripped static reference host. macOS arm64 and
  Linux x64 source builds pass Profile v1 and the 67-icon corpus; Windows and consoles are untested.
- Added Runtime SVG Profile v1, process-isolated backend tests, package isolation guards, sanitizer/
  leak checks, provenance, migration guidance, and explicit console-ready versus console-qualified
  terminology. No console support is claimed.
- Validated ThorVG dynamic and static NativeAOT on macOS arm64 and Linux x64. ThorVG is now the
  default for Catalog and SVG validation hosts on those platforms; Windows explicitly retains Skia.
  See `docs/svg-backend-rollout.md` for measured tradeoffs, release gates, and rollback.

- Added matching optional `Forma.Svg.MonoGame` and `Forma.Svg.FNA` companions using Svg.Skia 5.2.0
  (SkiaSharp 4.148.0) for bounded runtime SVG rendering without exposing backend-native types from
  core. Core packages remain free of Svg.Skia, SkiaSharp, and native Skia assets.
- Added immutable validated SVG sources, exact-physical-size CPU/GPU caches, compiled-XAML SVG
  assets, hot reload, deterministic build diagnostics, and MonoGame/FNA render parity checks.
- Added runtime SVG / PNG default-theme policies with per-icon fallback, diagnostics, and a Catalog
  `Runtime SVG` story covering embedded/file sources, resizing, tint, RTL, cache state, and rejected
  external input. See [runtime SVG deployment and security guidance](docs/runtime-svg.md).
- Measured macOS arm64 Catalog baseline: 67/67 theme SVG icons with zero bitmap fallbacks on both
  peers across 1x through 2.5x, RTL, and narrow cells; SVG versus PNG aggregate metrics remain within
  3%, and MonoGame versus FNA runtime SVG output remains within 1%.

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
- Added a direct-rendered visual foundation with brushes, geometries, transforms, clips, masks,
  bounded effects, flex/wrap, explicit-track grid layout, and presenter projection.
- Added typed `ControlTemplate`, `DataTemplate`, and `ItemsPanelTemplate` factories; explicit/theme/
  packaged-default lookup; local namescopes; typed relative sources; visual-tree selectors with the
  explicit `>>` template-boundary combinator; and adaptive viewport/scale/theme/input conditions.
- Added observable `ItemsControl`, source-occurrence `ListBox` selection, bounded vertical,
  horizontal, and uniform-grid virtualization, versioned recycling pools, and interaction/focus
  anchoring without warm-frame source enumeration.
- Added flat and hierarchical `DataGrid` modes with explicit typed columns, sorting/filtering,
  row/cell selection, expansion, and virtualized rows. Visible columns are intentionally
  non-virtualized and bounded to 256.
- Migrated the existing Catalog and Signal Run applications to template-first compiled XAML while
  preserving their workflows, and added visible hot-reload diagnostics plus template/items/
  virtualization stories on both runtime peers.
- This is a breaking visual-tree release: semantic widgets retain behavior but no longer draw
  unconditional outer chrome. See the
  [template, items, and visual-tree migration guide](docs/xaml-templates-migration.md).

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
