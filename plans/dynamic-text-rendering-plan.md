# Dynamic Text Rendering Implementation Plan

## Why This Matters to Forma Users

Once implemented, Forma text should look and behave like modern UI text rather than a collection
of pre-baked bitmap glyphs. Users will get sharper text on high-DPI displays, arbitrary runtime
font sizes without preparing separate assets, and reliable layout as windows move between display
densities. Multilingual interfaces will handle shaping, bidirectional text, combining marks,
fallback fonts, and emoji sequences correctly instead of treating each UTF-16 character as an
independent glyph.

The same retained layout will drive measurement, wrapping, drawing, hit testing, selection, and
caret movement, so text fields and other controls will agree about where text is and how it can be
edited. Applications can load TTF or OTF data directly from files, streams, or bytes without
depending on MGCB or an FNA-specific content compiler. Existing `SpriteFont` and XNB applications
remain supported for pixel-art, deterministic bitmap fonts, and deployments that prefer a
native-free compatibility profile.

## Objective

Add a modern runtime text system to Forma that can load TTF/OTF fonts, shape multilingual
text, select fallback fonts, rasterize glyphs at the physical display density, and cache glyphs in
GPU atlases on demand. Dynamic fonts should become the default path for the UI catalog while the
existing `SpriteFont` and XNB workflow remains supported for compatibility, deterministic bitmap
fonts, and pixel-art games.

Update the complete `Forma` namespace to consume the new text-layout and
dynamic-rendering services. Every text-bearing control must use the shared implementation for
measurement, wrapping, drawing, hit testing, caret movement, and selection; the catalog is the
demonstration and validation host, not the only consumer.

Update `Forma.Catalog` to use the new dynamic text path throughout its application chrome,
component stories, and custom typography stories. The catalog must provide interactive examples of
the supported shaping, fallback, density, editing, layout, diagnostics, and compatibility behavior.

The target follows the architectural direction of Godot's text servers without copying Godot's API:

- FreeType-class rasterization for scalable font outlines and metrics.
- HarfBuzz-class shaping for glyph selection, positioning, kerning, and OpenType features.
- Unicode-aware script, grapheme, line-break, and bidirectional processing.
- Density-aware glyph caches populated as text is used.
- A retained text layout shared by measurement, drawing, hit testing, selection, and caret movement.

The implementation must remain natural to both MonoGame and FNA: use their shared XNA-compatible
`Texture2D`, graphics-device, and `SpriteFont` surfaces, the render thread, and .NET streams and
spans. Dynamic text must not depend on either runtime's content toolchain. MonoGame's MGCB/XNB font
workflow remains a compatibility path; FNA has no built-in content compiler and must be able to use
the same dynamic-font APIs from raw font bytes.

Runtime support and package symmetry follow the
[MonoGame and FNA compatibility plan](monogame-fna-compatibility-plan.md). This plan owns text
layout, shaping, rasterization, atlas, and control integration; the compatibility plan owns peer
runtime builds, hosts, package selection, and the broader platform matrix.

## Decision Summary

- **Target default:** runtime TTF/OTF fonts for retained UI on targets where the dynamic backend is
  packaged and validated; otherwise use the native-free `SpriteFont` profile.
- **UI integration:** `Forma` uses the shared dynamic text layout and renderer
  by default across display, editing, selection, menu, tree, dialog, tooltip, and rich-text controls.
- **Compatibility path:** preserve `SpriteFont`; expose it through a UI font adapter and keep it
  independently deployable without FreeType, HarfBuzz, or their managed wrappers.
- **Packaging boundary:** keep the shared text contracts, retained layout, controls, and
  `SpriteFontAdapter` in the core runtime packages. Ship runtime font loading, shaping,
  rasterization, and native dependencies through runtime-matched dynamic-text companion packages.
  Referencing `Forma.MonoGame` or `Forma.FNA` alone must not resolve or copy dynamic native assets.
- **Runtime parity:** compile the same text contracts and implementation for MonoGame and FNA; do
  not add runtime-branded public font namespaces or make one runtime the reference behavior.
- **Content independence:** direct file, stream, and byte loading is the required dynamic-font path.
  Pipeline integration is optional and must not be required by either runtime package.
- **First renderer:** hinted grayscale alpha glyphs in `SurfaceFormat.Alpha8` atlases.
- **First shaping target:** production-quality shaping and fallback, not character-by-character
  drawing presented as Unicode support.
- **DPI model:** layout remains in logical UI units; rasterization uses physical pixel density.
- **Threading model:** shaping and rasterization may run on workers after correctness is established;
  `Texture2D` creation and uploads remain on the graphics/render thread.
- **Migration model:** keep the catalog's 1x/2x XNB pair until the dynamic path passes the visual,
  platform, memory, and compatibility gates in this plan.
- **Deferred features:** MSDF/MTSDF, color emoji, SVG glyphs, and advanced font effects follow the
  grayscale implementation and must not delay a correct first release.

## Progress Dashboard

- [ ] Phase 0: Contracts, Dependency Spike, and Baselines
- [x] Phase 1: UI Font Abstraction and SpriteFont Adapter
- [ ] Phase 2: Runtime Font Loading and FreeType Rasterization
- [ ] Phase 3: Dynamic Glyph Atlas and Renderer
- [x] Phase 4: Unicode Shaping, Fallback, and Text Layout
- [x] Phase 5: Retained Control Integration
- [x] Phase 6: Catalog Typography Stories and Diagnostics
- [ ] Phase 7: Performance, Platform, and Resilience Gates
- [ ] Phase 8: Default Rollout, Compatibility, and Documentation

Check a phase only after all implementation tasks and exit criteria in that phase are complete.

Current evidence boundary: 153/166 checks pass. The remaining checks require execution on the
selected Windows/Linux Direct3D and Vulkan cells, completion of the reproducible Linux arm64
FreeType asset and runtime gate, or completion of the broader supported-platform matrix. Cross-RID
restore/publish success is not a substitute for those graphics, startup, reset, disposal, or
native-load runtime gates.

### Progress Tracking Workflow

Use `scripts/track-plan.sh` at the start and end of each implementation session:

```sh
bash scripts/track-plan.sh plans/dynamic-text-rendering-plan.md
```

Update task boxes only when the implementation and its focused validation are complete. Add newly
discovered required work to this document rather than tracking it only in issue comments or session
notes. A phase dashboard entry may be checked only when every task and exit criterion in that phase
is checked.

## Success Criteria

- [x] A TTF or OTF can be loaded at runtime from a file, stream, or byte array without MGCB, XNB,
  or an FNA-specific content compiler.
- [x] The same font remains crisp when a window moves between 1x and Retina/high-DPI displays.
- [x] Arbitrary logical font sizes do not require separate `.spritefont` or `.xnb` assets.
- [x] Measurement and drawing consume the same shaped layout and cannot disagree about advances.
- [x] Font fallback renders mixed-script text without replacing supported characters with `?`.
- [x] Arabic joining, Indic shaping, combining marks, ligatures, emoji sequences, and bidirectional
  text have explicit automated or approved visual coverage.
- [x] Caret movement, hit testing, wrapping, selection, ellipsis, and visible-character behavior use
  grapheme/glyph mappings rather than assuming one UTF-16 code unit equals one glyph.
- [x] Glyph atlas memory is bounded, observable, and recoverable after graphics-device reset.
- [x] Warm text rendering performs no font-file parsing, glyph rasterization, or atlas allocation per
  frame when the text, font, size, and display scale are unchanged.
- [x] `SpriteFont` UI applications continue to compile and render through a documented adapter path.
- [x] A packed `SpriteFont`-only MonoGame and FNA consumer publishes and executes with trimming and
  NativeAOT where its runtime supports those modes, without resolving, loading, or shipping
  FreeTypeSharp, HarfBuzzSharp, FreeType, or HarfBuzz.
- [x] Every text-bearing control in `Forma` uses the shared text-layout service;
  no control retains a private raw-string measurement or drawing path.
- [x] The catalog demonstrates dynamic sizing, DPI behavior, fallback, shaping, bidi, wrapping,
  atlas behavior, and `SpriteFont` compatibility.
- [x] `Forma.Catalog` uses dynamic text throughout its application chrome and existing
  component stories, without catalog-specific font-resolution wiring.
- [x] MonoGame and FNA expose matching public text contracts and produce equivalent layout results
  for the same font bytes, text, locale, direction, features, and logical constraints.
- [ ] Direct3D, OpenGL, Vulkan, and Metal runtime/backend combinations selected in Phase 0 use the
  same public text contracts and layout results.
- [ ] Native dependency packaging works for every supported MonoGame and FNA target selected in
  Phase 0, including trimming and AOT configurations where applicable.

## Non-Goals

- Remove `SpriteFont`, `.spritefont`, XNB font assets, or `SpriteBatch.DrawString`.
- Make the first milestone a complete clone of Godot's `TextServerAdvanced`.
- Depend on operating-system text APIs whose output or availability differs by backend.
- Perform graphics-resource creation or `Texture2D.SetData` from arbitrary worker threads.
- Normalize or alter application text silently.
- Guarantee that every Unicode character exists without an application-supplied fallback family.
- Ship MSDF, color emoji, SVG glyphs, vertical writing, and every OpenType feature in the first MVP.
- Change MonoGame's or FNA's general-purpose `SpriteFont` public API as part of the UI migration.
- Require MGCB, the XNA Content Pipeline, or a new FNA content compiler for dynamic fonts.
- Require the dynamic-text backend on a target that supports only prebuilt `SpriteFont` content.
- Make FreeType or HarfBuzz a transitive dependency of SpriteFont-only Forma applications.

## Current State

### Existing Framework Capabilities

- MonoGame and FNA both provide XNA-compatible `SpriteFont` and `SpriteBatch` APIs. `SpriteFont`
  stores a prebuilt bitmap atlas and metrics; `SpriteBatch.DrawString` draws one textured quad per
  character using that data.
- `.spritefont` XML selects a font, size, style, character ranges, and optional default character.
- MGCB uses FreeType in `MonoGame.Framework.Content.Pipeline` to rasterize selected glyphs offline
  and writes the result to XNB.
- `MonoGame.Library.FreeType` is currently a Content Pipeline dependency, not a guaranteed runtime
  dependency of all supported MonoGame backend packages.
- FNA deliberately provides no content compiler. It can load XNA `SpriteFont` XNBs and is mostly
  compatible with DesktopGL MGCB output, but raw TTF/OTF files do not become `SpriteFont` objects
  automatically.
- MonoGame and FNA `SpriteFont` implementations are not API-identical beyond the XNA-compatible
  surface. The compatibility adapter must not use MonoGame-only constructors, atlas properties, or
  glyph accessors.
- `Texture2D` creation, texture uploads, and textured-quad rendering are shared building blocks for
  a dynamic grayscale atlas. Format and partial-update behavior must be verified per runtime and
  graphics backend rather than inferred from matching type names.

### Existing UI Limitations

- UI controls expose `SpriteFont` directly in approximately twenty public font properties.
- Measurement is spread across controls through direct `SpriteFont.MeasureString` and
  `LineSpacing` calls.
- Wrapping, caret placement, hit testing, selection, and visible-character behavior frequently walk
  UTF-16 characters and remeasure substrings.
- `UIRenderContext.Text` centralizes drawing, but layout is not centralized.
- `DisplayFontResolver` can switch between a 1x and 2x bitmap atlas; it solves the immediate Retina
  blur but does not provide arbitrary sizes, shaping, or fallback.
- The catalog currently ships 14-point and 28-point Inter UI and JetBrains Mono code XNB atlases.

## Target Architecture

```text
TTF/OTF bytes + fallback family + style/variation settings
                         |
                         v
                    Font faces
                         |
Text + locale + direction + width + OpenType features
                         |
                         v
  grapheme/script/bidi segmentation and fallback resolution
                         |
                         v
                shaped immutable TextLayout
             / metrics / glyph IDs / clusters \
            v                                  v
 measurement, hit testing,             glyph cache lookup
 selection, wrapping, caret                    |
                                               v
                                      FreeType rasterization
                                               |
                                               v
                                render-thread Alpha8 atlas upload
                                               |
                                               v
                                      batched textured quads
```

### Proposed Public Concepts

Names are provisional until Phase 0 API review, but responsibilities must remain separate:

- `UIFontFace`: immutable font bytes, face index, family metadata, supported code points, and
  variation axes. Owns native face lifetime through safe handles.
- `UIFont`: a logical UI font configuration containing primary face, fallback family, logical size,
  style, variation coordinates, hinting mode, and rendering mode.
- `SpriteFontAdapter`: implements the UI font contract over an existing `SpriteFont` without dynamic
  rasterization.
- `TextLayoutOptions`: width, wrapping, alignment, locale, direction, line spacing, tab stops,
  OpenType features, and visible-range behavior.
- `TextLayout`: immutable shaped lines/runs with logical bounds, baselines, glyph placements,
  UTF-16 ranges, code-point ranges, and grapheme/cluster mappings.
- `TextLayoutEngine`: segmentation, fallback selection, shaping, bidi ordering, line breaking,
  trimming, and layout-cache ownership.
- `DynamicGlyphCache`: raster key lookup, atlas allocation, render-thread upload queue, eviction, and
  diagnostics.
- `ITextRenderer` or equivalent internal service: consumes `TextLayout` rather than raw strings.

Avoid an interface that exposes only `MeasureString` and `DrawString`; that would preserve the
current mismatch between layout and drawing and would be insufficient for complex scripts.

### Coordinate and Cache Rules

- Font size, advances, baselines, line heights, and layout bounds are logical UI units.
- Raster pixel size is derived from logical size, `UIContext.DisplayScale`, and an explicit
  oversampling policy.
- A layout cache key includes text content, font/fallback identity, logical size, width and layout
  options, locale, direction, features, and relevant font variation coordinates.
- A glyph raster key includes face identity, face index, glyph ID, physical pixel size, variation
  coordinates, hinting/render mode, outline parameters, and subpixel phase if supported.
- Display-scale changes invalidate or select raster entries, not application text.
- Atlas pages use bounded memory and deterministic padding. Alpha and color/MSDF glyphs use
  separate page formats.
- Missing-glyph results are cached to avoid repeatedly asking FreeType for unavailable glyphs.
- Device reset invalidates GPU pages while preserving enough CPU metadata to repopulate them.

### Thread Ownership

- Font bytes and immutable shaped layouts may be shared across threads.
- Native FreeType/HarfBuzz handles must be wrapped in safe lifetime objects and used according to
  each library's thread-safety rules; use per-worker faces or synchronization, not shared mutable
  face state.
- First implementation may shape and rasterize synchronously to establish determinism.
- Later worker rasterization produces CPU bitmaps only.
- Atlas placement decisions, texture creation, texture updates, and draw submission occur on the
  graphics thread.
- A missing glyph may render a fallback box for one frame while an asynchronous bitmap is pending,
  but measurement must already be stable and the next frame must be invalidated automatically.

## Dependency and Packaging Strategy

- Reuse lessons from MonoGame's Content Pipeline FreeType integration where useful, but do not
  reference MonoGame Content Pipeline assemblies from Forma runtime packages and do not introduce
  an FNA-only compiler dependency.
- [x] Evaluate direct native bindings, maintained .NET bindings, and a Forma-owned interop layer for
  FreeType and HarfBuzz that can be built and packaged identically for MonoGame and FNA variants.
- [x] Select a Unicode bidi/segmentation/line-break strategy. HarfBuzz shapes runs but does not by
  itself implement the full bidi or line-breaking algorithms; do not mark shaping complete without
  this decision.
- [x] Record exact versions, licenses, update ownership, vulnerability response, and upstream URLs.
- [x] Define runtime-specific RID/platform matrices for MonoGame and FNA. Include Windows x64/arm64,
  Linux x64/arm64, and macOS x64/arm64 where each runtime supports them; include Android ABIs and
  iOS architectures only for runtime/platform pairs declared in the compatibility plan.
- [x] Verify NativeAOT, trimming, sandbox, code-signing, and static-link requirements per target.
- [x] Decide whether dynamic text is in every peer framework package or symmetric optional companion
  packages. The UI public contract must not change by runtime or backend even if implementation
  packaging is optional.
- [x] Move runtime font loading, shaping, rasterization, and FreeType/HarfBuzz dependencies into
  runtime-matched optional companion packages. Keep `UIFont`, retained layout, controls, and
  `SpriteFontAdapter` in `Forma.MonoGame` and `Forma.FNA` without native text dependencies.
- [x] Add package-graph tests proving a core-only consumer neither resolves managed dynamic-font
  wrappers nor copies FreeType/HarfBuzz native assets for any declared core RID.
- [x] Add native binaries and notices through the same reproducible build/release process as other
  Forma dependencies for both runtime variants; do not depend on a developer's system installation.
- [x] Add CI verification that every MonoGame and FNA package artifact contains or resolves the
  expected native assets for each declared RID.

## Delivery Phases

### Phase 0: Contracts, Dependency Spike, and Baselines

- [x] Write an API decision record covering the provisional public concepts, ownership, disposal,
  source compatibility, binary compatibility, and error behavior.
- [x] Inventory every UI `SpriteFont` property, `MeasureString`, `LineSpacing`, glyph-width loop, and
  `UIRenderContext.Text` call; classify it as measurement, layout, hit testing, or rendering.
- [x] Decide the compatibility shape: parallel `UIFont` property, forwarding legacy property,
  implicit adapter, or a planned breaking change while the UI toolkit is experimental.
- [x] Build a desktop FreeType/HarfBuzz spike that loads Inter and an Arabic-capable Noto fallback
  from bytes, shapes one Latin and one Arabic string, rasterizes returned glyph IDs, and uploads one
  `Alpha8` atlas page.
- [x] Compile and run the spike against both MonoGame and FNA before fixing the public API around
  assumptions from one runtime.
- [x] Run the packaging spike on at least one Metal runtime/backend combination and one non-Metal
  combination for each runtime before fixing graphics upload assumptions.
- [x] Select and document bidi, grapheme, script, and line-break dependencies or data tables.
- [x] Capture baseline catalog screenshots at 1x and 2x plus startup time, frame allocations, XNB
  font size, and steady-state texture memory.
- [x] Add a representative multilingual corpus licensed for tests: Latin combining marks, Cyrillic,
  Greek, Arabic, Hebrew, Devanagari, Thai, CJK, emoji sequences, and malformed UTF-16.
- [x] Establish security limits for font size, face count, table sizes, glyph bitmap dimensions,
  atlas pages, fallback depth, layout length, and shaping/rasterization timeouts.

#### Phase 0 Exit Criteria

- [ ] Dependency choices work on the selected runtime/platform matrix and have an approved license
  and packaging path.
- [x] The spike proves identical shaped glyph data can be rasterized and rendered through both
  MonoGame and FNA graphics surfaces.
- [x] Normalized public text APIs match between the MonoGame and FNA builds.
- [x] The API decision record identifies a migration path for every current UI font call site.

### Phase 1: UI Font Abstraction and SpriteFont Adapter

- [x] Add the logical UI font and immutable text-layout contracts without adding dynamic native
  dependencies yet.
- [x] Implement `SpriteFontAdapter` with metrics, fallback behavior, layout output, and drawing that
  match the shared XNA `SpriteFont` behavior without using runtime-specific public extensions.
- [x] Centralize text measurement behind the layout service; eliminate new direct
  `SpriteFont.MeasureString` calls in UI code.
- [x] Add a UI font collection/fallback type even though the initial adapter contains one font.
- [x] Define value-based identities for font instances and layout options so caches do not rely on
  object reference equality.
- [x] Define disposal semantics: controls do not own fonts; font services and application content
  lifetime own native faces and atlas resources.
- [x] Preserve the current `DisplayFontResolver` behavior through the adapter until dynamic fonts
  supersede it.
- [x] Add unit tests proving existing ASCII measurements, wrapping, alignment, clipping, and 1x/2x
  rendering remain unchanged through the adapter in both runtime builds.

#### Phase 1 Exit Criteria

- [x] The complete retained UI suite passes using `SpriteFontAdapter` against MonoGame and FNA.
- [x] Both catalog hosts retain equivalent compatibility-font behavior and require no dynamic native
  library.

### Phase 2: Runtime Font Loading and FreeType Rasterization

- [x] Implement safe runtime initialization and shutdown for the selected FreeType integration.
- [x] Load font faces from `Stream`, `ReadOnlyMemory<byte>`, and a project-relative file API with
  deterministic ownership of copied or pinned bytes.
- [x] Support collection face indices and expose family/style metadata.
- [x] Read ascender, descender, line gap, underline, glyph advance, glyph bounds, variation axes, and
  supported-character coverage.
- [x] Convert logical UI size and display scale into FreeType sizing without baking DPI into layout
  geometry.
- [x] Rasterize grayscale glyphs with explicit hinting options and correct positive/negative pitch
  handling.
- [x] Return `.notdef` consistently when a font maps a character but cannot produce a valid glyph.
- [x] Add deterministic limits and errors for malformed, unsupported, or oversized font data.
- [x] Add an optional framework-neutral font asset wrapper that packages original font bytes and
  metadata without pre-rasterizing them. Direct runtime loading remains required; any later MGCB
  integration must be optional and have an equivalent non-pipeline path for FNA.
- [x] Test multiple faces, combining marks, large and fractional logical sizes, variable-font axes,
  malformed files, and repeated create/dispose cycles.

#### Phase 2 Exit Criteria

- [ ] Runtime metrics and grayscale glyph bitmaps are stable on every MonoGame and FNA
  runtime/platform pair selected in Phase 0.
- [x] Forma runtime packages reference neither `MonoGame.Framework.Content.Pipeline` nor any
  FNA-specific content compiler.
- [x] Native handles and font-byte ownership pass leak and repeated-disposal tests.

### Phase 3: Dynamic Glyph Atlas and Renderer

- [x] Implement a deterministic rectangle allocator with padding and configurable page dimensions.
- [ ] Store grayscale glyphs in `SurfaceFormat.Alpha8` pages and verify sampling behavior on each
  graphics backend.
- [x] Implement render-thread upload batching so multiple new glyphs do not cause one full texture
  upload each.
- [x] Render shaped glyph placements as batched quads with tint, clipping, transforms, opacity, and
  layer depth compatible with existing UI drawing.
- [x] Define bounded page budgets per graphics device and rendering mode.
- [x] Implement page/glyph usage tracking and an eviction policy that cannot invalidate glyphs while
  an active frame references them.
- [x] Recover from device loss/reset and disposal without retaining invalid `Texture2D` references.
- [x] Expose read-only diagnostics: page count, capacity, used area, glyph count, misses, uploads,
  evictions, bytes, and pending work.
- [x] Add debug atlas visualization without exposing mutable atlas internals publicly.
- [x] Test atlas edge placement, padding, fragmentation, eviction, clipping, device reset, multiple
  graphics devices, and disposal order.

#### Phase 3 Exit Criteria

- [x] A shaped Latin layout renders through dynamic atlas pages on the selected MonoGame and FNA
  Direct3D, OpenGL, Vulkan, and Metal combinations with approved pixel output.
- [x] Atlas memory never exceeds its configured budget under a randomized glyph stress test.
- [x] Warm repeated drawing performs no rasterization or texture upload.

### Phase 4: Unicode Shaping, Fallback, and Text Layout

- [x] Segment text into grapheme clusters, scripts, bidi runs, and font-fallback runs while
  preserving mappings to the original UTF-16 input.
- [x] Shape runs with locale, direction, script, language, variation coordinates, and configurable
  OpenType features.
- [x] Resolve fallback per cluster so combining sequences and emoji ZWJ sequences are not split
  across fonts incorrectly.
- [x] Implement the Unicode Bidirectional Algorithm and visual run ordering through the selected
  Phase 0 dependency/strategy.
- [x] Implement Unicode line-break opportunities, mandatory breaks, tabs, whitespace preservation,
  and application-provided paragraph separators.
- [x] Produce immutable line/run/glyph data containing advances, offsets, baselines, extents,
  clusters, and logical-to-visual mappings.
- [x] Implement hit testing, caret positions, selection rectangles, word boundaries, grapheme
  movement, range bounds, trimming, ellipsis, and visible-glyph ranges on `TextLayout`.
- [x] Define malformed UTF-16 behavior without throwing or reading outside input bounds.
- [x] Add fallback-cycle detection and a deterministic final missing-glyph policy.
- [x] Implement a bounded layout cache and invalidation when text, font settings, locale, direction,
  width, or relevant theme values change.
- [x] Validate shaping against known HarfBuzz outputs and approved reference images, not only text
  width assertions.

#### Phase 4 Exit Criteria

- [x] Arabic, Hebrew/Latin bidi, Devanagari, Thai, CJK wrapping, combining marks, standard
  ligatures, emoji sequences, and missing-glyph fallback pass focused tests.
- [x] Measurement, hit testing, selection, and drawing all consume the same `TextLayout`.
- [x] No UI API equates UTF-16 indices, Unicode scalar values, graphemes, and glyph indices.

### Phase 5: Retained Control Integration

- [x] Route the complete `Forma` namespace through the shared `UIFont`,
  `TextLayout`, layout engine, and renderer contracts; the dynamic implementation becomes the
  normal path and `SpriteFontAdapter` remains the compatibility path.
- [x] Migrate `Label` first, including wrapping, justification, ellipsis, visible characters,
  paragraph spacing, language, direction, and range queries.
- [x] Migrate `LineEdit`, including horizontal scrolling, grapheme-safe caret movement, selection,
  secret text, IME composition ranges, clipboard text, and fallback rendering.
- [x] Migrate `TextEdit` and `CodeEdit`, including multiline layout, line caches, gutters, tabs,
  syntax runs, code completion, and incremental invalidation for edited lines.
- [x] Migrate buttons, menus, tabs, item lists, trees, graph controls, dialogs, tooltips, and every
  remaining direct `SpriteFont` measurement identified in Phase 0.
- [x] Replace substring-measurement loops with layout hit testing and cluster maps.
- [x] Ensure theme inheritance can supply a font family, size, features, and rendering policy.
- [x] Invalidate control minimum size and text layout when font, locale, direction, width, scale, or
  theme values change.
- [x] Keep logical layout stable when moving between displays; update only density-dependent glyph
  resources unless font hinting policy explicitly requires relayout.
- [x] Preserve `SpriteFont` behavior through the adapter for every migrated control.
- [x] Update `GODOT_COMPATIBILITY.md` with the behaviors now backed by real shaping and the remaining
  differences from Godot.

#### Phase 5 Exit Criteria

- [x] Every text-bearing `Forma` control measures, lays out, renders, and maps
  interaction through the shared text system, with no control-specific bypass.
- [x] No retained UI control directly calls `SpriteFont.MeasureString`, indexes a glyph atlas, or
  derives caret positions by measuring every UTF-16 substring.
- [x] Existing retained UI tests pass through the compatibility adapter.
- [x] New multilingual editing, selection, wrapping, and display-scale tests pass through dynamic
  fonts.

### Phase 6: Catalog Typography Stories and Diagnostics

Add explicit `Typography` stories in addition to reflected component stories. Give custom stories
access to the font service, display-scale controls, and diagnostics rather than hiding these in
catalog-global state.

#### Dynamic Text Lab

Add a dedicated, searchable catalog section where people can experiment with the feature instead
of only reading about it. It should feel like a small text laboratory: each story changes one
important variable, shows the resulting text and layout, and exposes enough state to explain what
the renderer did.

- **Try a font:** choose a bundled runtime-loaded TTF/OTF face, fallback family, logical size,
  weight, variation axis, and rendering scale; update the sample immediately without generating a
  new content asset.
- **Try real text:** edit a corpus containing Latin combining marks, Arabic, Hebrew, Devanagari,
  Thai, CJK, emoji sequences, ligatures, and mixed-script fallback. Show the selected face and
  shaped run boundaries for the current selection.
- **Try layout and editing:** resize the available width, toggle wrapping and ellipsis, change
  alignment and direction, then select text and move the caret by grapheme, word, and visual
  position. Draw range and caret overlays on the same retained layout used for rendering.
- **Try density and caching:** switch between 1x, 1.5x, and 2x display density, show stable logical
  bounds beside physical raster dimensions, and expose atlas pages, cache hits/misses, uploads,
  evictions, and memory budget while entering new text.
- **Compare compatibility:** render the same sample through the dynamic font path and
  `SpriteFontAdapter`, with an explicit label for differences in shaping, sizing, and supported
  glyphs.
- **Exercise failures:** let the user select a missing face, malformed font, unsupported glyph, or
  deliberately exhausted atlas budget and show a recoverable diagnostic instead of terminating the
  catalog.

The lab must run in both MonoGame and FNA hosts, support keyboard and mouse interaction, and make
the active runtime, font source, display scale, layout options, and cache state visible in the
story. It is both the user-facing demonstration and a manual validation surface for the automated
tests and approved screenshots below.

- [x] Migrate all `Forma.Catalog` application chrome, reflected component stories, and custom
  stories to the same dynamic text services used by `Forma` in both peer runtime hosts.
- [x] Add the searchable **Dynamic Text Lab** section and keep its stories independent of
  catalog-global font state.
- [x] **Dynamic Sizes:** live text, font-family picker, and a size slider covering small UI text
  through large headings without prebuilt assets.
- [x] **Display Density:** side-by-side logical-size samples plus a 1x/1.5x/2x density simulator;
  show physical raster size and prove logical bounds remain stable.
- [x] **Fallback Chain:** mixed Latin, Greek, Cyrillic, Arabic, Devanagari, CJK, symbols, and emoji;
  identify the selected face per shaped run.
- [x] **Shaping and Features:** combining marks, `fi`/`ffi` ligatures, kerning pairs, optional
  OpenType feature toggles, and variable-font axes when the selected font supports them.
- [x] **Bidirectional Text:** editable Arabic/Hebrew and Latin content with automatic, LTR, and RTL
  direction controls; visualize logical and visual run order in diagnostics.
- [x] **Wrapping and Selection:** multilingual paragraphs with adjustable width, wrapping, ellipsis,
  mouse selection, caret movement, and range-bound overlays.
- [x] **Atlas Inspector:** page thumbnails, occupancy, cache hits/misses, uploads, evictions, memory
  budget, clear-cache command, and a glyph stress input.
- [x] **SpriteFont Compatibility:** render the same controls with dynamic font and
  `SpriteFontAdapter`; label differences that are intentional rather than implying pixel identity.
- [x] **Failure States:** missing face, malformed asset, exhausted atlas budget, unsupported glyph,
  and fallback exhaustion must produce visible diagnostics without terminating the catalog.
- [x] Add the same catalog option to switch between dynamic and compatibility fonts in the
  MonoGame and FNA hosts.
- [x] Add screenshot states for desktop 1x, Retina 2x, narrow viewport, and RTL examples.
- [x] Package test fonts and licenses explicitly; use a broad fallback font only if its redistribution
  terms and package size are acceptable.

#### Phase 6 Exit Criteria

- [x] Every catalog screen and story renders text through the dynamic path by default; the only
  `SpriteFont` rendering is inside explicit compatibility examples or runtime compatibility mode.
- [x] Every typography story is interactive, searchable, keyboard accessible, and stable under
  window resize and display-scale changes.
- [x] Automated catalog smoke for both runtime hosts visits every typography story and records no
  exception, missing native dependency, unbounded atlas growth, or graphics validation error.
- [x] Approved screenshots demonstrate crisp 1x/2x output and correct complex-script shaping.

### Phase 7: Performance, Platform, and Resilience Gates

- [x] Benchmark cold face load, first shape, first raster/upload, warm layout lookup, warm draw,
  fallback-heavy text, and atlas eviction.
- [x] Record allocations and ensure unchanged warm UI text creates no managed allocation per frame.
- [x] Add counters for shape time, raster time, queued glyphs, upload bytes, layout-cache hit rate,
  and atlas-cache hit rate.
- [x] Stress rapid text editing, locale changes, display moves, font-size animation, fallback churn,
  device reset, and repeated game creation/disposal.
- [x] Fuzz font loading and text shaping with bounded resources and malformed input.
- [x] Test fonts with very large metrics, zero contours, negative bearings, color tables, collections,
  variable axes, and unsupported table combinations.
- [x] Verify clipping and atlas sampling at fractional positions and non-integer display scales.
- [x] Verify MonoGame and FNA package artifacts in clean environments without system
  FreeType/HarfBuzz installs.
- [x] Run trimming and AOT smoke tests for every runtime/platform pair that declares those modes.
- [x] Publish and execute packed core-only MonoGame and FNA consumers that load XNB `SpriteFont`
  content, lay out and draw representative controls, and contain no dynamic-font managed or native
  dependencies.
- [x] Inspect core-only publish outputs and native import tables in CI; fail if FreeType, HarfBuzz,
  FreeTypeSharp, or HarfBuzzSharp becomes reachable or packaged.
- [x] Define and meet release budgets for binary size, font load time, first-use latency, warm frame
  cost, layout-cache memory, and atlas memory; record numbers in this document before rollout.

#### Phase 7 Exit Criteria

- [x] Performance budgets are documented with measured evidence and no unbounded cache remains.
- [ ] The supported platform matrix passes package, startup, render, device-reset, and disposal tests.
- [x] Fuzzing and malformed-font tests fail safely without process termination or uncontrolled memory
  growth.

### Phase 8: Default Rollout, Compatibility, and Documentation

- [x] Make dynamic text rendering the default implementation for `Forma` on every MonoGame and FNA
  runtime/backend combination where the dynamic companion package passes Phases 0-7. Keep
  `SpriteFontAdapter` as the default on core-only or dynamic-text-unsupported targets.
- [ ] Make dynamic fonts the default throughout both `Forma.Catalog` runtime hosts, including
  application chrome, reflected component stories, and typography examples, only after Phases 0-7
  pass.
- [x] Keep a runtime catalog toggle and CI compatibility coverage for `SpriteFontAdapter`.
- [x] Remove the catalog-specific `Catalog@2x` asset and `DisplayFontResolver` wiring only after
  dynamic DPI tests and screenshots are accepted; do not remove the general compatibility API
  solely because the catalog no longer needs it.
- [x] Document runtime font loading, fallback families, logical sizing, DPI behavior, OpenType
  features, cache budgets, disposal, and deployment requirements for both runtimes.
- [x] Document that MonoGame MGCB/XNB and FNA-compatible XNBs are optional `SpriteFont`
  compatibility routes, not requirements for dynamic text.
- [x] Document when `SpriteFont` remains preferable: fixed glyph sets, pixel art, deterministic
  offline atlases, minimal native dependencies, and legacy XNA-compatible projects.
- [x] Add migration examples converting a control tree from `SpriteFont` to dynamic fonts without
  changing layout intent.
- [x] Update package readmes, templates, API docs, third-party notices, and release notes.
- [x] Define semantic-versioning treatment for any UI font property changes and retain obsolete
  forwarding APIs for the agreed compatibility window.
- [x] Add a rollback switch that selects `SpriteFontAdapter` if a platform-specific dynamic text
  regression is found after release.
- [x] Document the native-free core package/profile as the compatibility route for restricted and
  authorized console platforms, while keeping actual console support conditional on the selected
  MonoGame/FNA port and platform-holder validation.

#### Phase 8 Exit Criteria

- [x] New `Forma` control trees use dynamic text by default without catalog-
  specific setup or a `DisplayFontResolver`.
- [ ] New MonoGame and FNA catalog runs use dynamic fonts by default on every supported backend.
- [x] Existing `SpriteFont` UI samples and tests remain functional and documented.
- [x] Core-only package consumers publish and execute without dynamic-font assemblies or native
  libraries, including every declared trimming/AOT configuration.
- [x] The dual-XNB Retina workaround is no longer required by the catalog.
- [x] Release artifacts contain all required native dependencies, licenses, docs, and examples.

## Test Matrix

| Layer | Required coverage |
| --- | --- |
| Font parsing | Valid TTF/OTF/TTC, variable fonts, malformed tables, limits, disposal |
| Unicode | Scalars, malformed UTF-16, graphemes, combining marks, ZWJ, variation selectors |
| Shaping | Latin features, Arabic, Hebrew bidi, Indic, Thai, CJK, fallback boundaries |
| Layout | Wrap, trim, ellipsis, tabs, alignment, justification, paragraph spacing, ranges |
| Interaction | Caret, word/grapheme movement, hit testing, selection, IME composition |
| Rasterization | Hinting modes, bearings, outlines, fractional/non-integer density |
| Atlas | Allocation, padding, upload, eviction, budget, reset, multiple devices |
| Compatibility | `SpriteFontAdapter`, existing UI suite, pixel-font sampling, native-free packed consumer |
| Runtimes | MonoGame and FNA API parity, behavior, package, and isolated-consumer tests |
| Content paths | Raw TTF/OTF required; MGCB/XNB and FNA-compatible XNB compatibility tests |
| Backends | Supported MonoGame/FNA Direct3D, OpenGL, Vulkan, and Metal pixel/smoke tests |
| Platforms | Per-runtime Windows, Linux, macOS, Android, and iOS package/startup/render/AOT gates |
| Catalog | Both runtime hosts; every typography story at 1x, 1.5x, 2x, narrow, and RTL states |

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Native dependency/package growth | Isolate dynamic text in companion packages and require native-free core package-graph and publish-output gates |
| Incorrect claim of Unicode support | Gate on shaping, bidi, grapheme, fallback, and interaction tests rather than glyph rendering alone |
| Layout changes after async rasterization | Derive layout from shaping metrics before raster work; raster completion only invalidates drawing |
| Atlas churn during animated sizes | Quantize physical sizes deliberately, cap pages, expose diagnostics, document animation tradeoffs |
| UI migration regressions | Land the SpriteFont adapter first, migrate one control family at a time, run focused and full suites |
| Worker-thread graphics access | Separate CPU glyph jobs from an explicit render-thread upload queue |
| Font parser security | Pin versions, fuzz, enforce limits, own vulnerability updates, and avoid system-library ambiguity |
| Mobile/AOT interop failures | Prove packaging and native handles in Phase 0 before making the abstraction public |
| API overfit to one rasterizer | Keep layout, shaping, rasterization, and atlas responsibilities separate |
| API or behavior overfit to one XNA runtime | Dual-compile from Phase 0, use shared XNA surfaces, and require peer behavior and API parity tests |
| Content-pipeline coupling | Require raw byte/stream loading; keep MGCB/XNB support behind the compatibility adapter and optional tooling |
| Catalog becomes the only validation | Require unit, integration, backend, package, and device-reset tests independently of stories |

## Completion Definition

This plan is complete only when the entire `Forma` namespace uses the shared retained text contracts,
dynamic text is the default in MonoGame and FNA builds that include a validated dynamic companion,
dynamic fonts are the validated default in both catalog hosts, complex text uses a shared shaped
layout for rendering and interaction, DPI changes select appropriate raster resources without
changing logical geometry, atlas memory is bounded, all selected runtime/platform pairs package
their dependencies, public text APIs remain symmetric, and `SpriteFont` remains a tested,
native-free deployment option.
