# Dynamic Text Rendering Implementation Plan

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

The implementation must remain natural to MonoGame: use `Texture2D`, existing graphics backends,
the render thread, .NET streams and spans, and an adapter over `SpriteFont` rather than replacing
the established MonoGame content pipeline.

## Decision Summary

- **Target default:** runtime TTF/OTF fonts for retained UI.
- **UI integration:** `Forma` uses the shared dynamic text layout and renderer
  by default across display, editing, selection, menu, tree, dialog, tooltip, and rich-text controls.
- **Compatibility path:** preserve `SpriteFont`; expose it through a UI font adapter.
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
- [ ] Phase 1: UI Font Abstraction and SpriteFont Adapter
- [ ] Phase 2: Runtime Font Loading and FreeType Rasterization
- [ ] Phase 3: Dynamic Glyph Atlas and Renderer
- [ ] Phase 4: Unicode Shaping, Fallback, and Text Layout
- [ ] Phase 5: Retained Control Integration
- [ ] Phase 6: Catalog Typography Stories and Diagnostics
- [ ] Phase 7: Performance, Platform, and Resilience Gates
- [ ] Phase 8: Default Rollout, Compatibility, and Documentation

Check a phase only after all implementation tasks and exit criteria in that phase are complete.

### Progress Tracking Workflow

Use `scripts/track-plan.sh` at the start and end of each implementation session:

```sh
bash scripts/track-plan.sh docs/dynamic-text-rendering-plan.md
```

Update task boxes only when the implementation and its focused validation are complete. Add newly
discovered required work to this document rather than tracking it only in issue comments or session
notes. A phase dashboard entry may be checked only when every task and exit criterion in that phase
is checked.

## Success Criteria

- [ ] A TTF or OTF can be loaded at runtime from a file, stream, or byte array without MGCB.
- [ ] The same font remains crisp when a window moves between 1x and Retina/high-DPI displays.
- [ ] Arbitrary logical font sizes do not require separate `.spritefont` or `.xnb` assets.
- [ ] Measurement and drawing consume the same shaped layout and cannot disagree about advances.
- [ ] Font fallback renders mixed-script text without replacing supported characters with `?`.
- [ ] Arabic joining, Indic shaping, combining marks, ligatures, emoji sequences, and bidirectional
  text have explicit automated or approved visual coverage.
- [ ] Caret movement, hit testing, wrapping, selection, ellipsis, and visible-character behavior use
  grapheme/glyph mappings rather than assuming one UTF-16 code unit equals one glyph.
- [ ] Glyph atlas memory is bounded, observable, and recoverable after graphics-device reset.
- [ ] Warm text rendering performs no font-file parsing, glyph rasterization, or atlas allocation per
  frame when the text, font, size, and display scale are unchanged.
- [ ] `SpriteFont` UI applications continue to compile and render through a documented adapter path.
- [ ] Every text-bearing control in `Forma` uses the shared text-layout service;
  no control retains a private raw-string measurement or drawing path.
- [ ] The catalog demonstrates dynamic sizing, DPI behavior, fallback, shaping, bidi, wrapping,
  atlas behavior, and `SpriteFont` compatibility.
- [ ] `Forma.Catalog` uses dynamic text throughout its application chrome and existing
  component stories, without catalog-specific font-resolution wiring.
- [ ] Direct3D, OpenGL, Vulkan, and Metal use the same public text contracts and layout results.
- [ ] Native dependency packaging works for every supported MonoGame runtime target selected in
  Phase 0, including trimming and AOT configurations where applicable.

## Non-Goals

- Remove `SpriteFont`, `.spritefont`, XNB font assets, or `SpriteBatch.DrawString`.
- Make the first milestone a complete clone of Godot's `TextServerAdvanced`.
- Depend on operating-system text APIs whose output or availability differs by backend.
- Perform graphics-resource creation or `Texture2D.SetData` from arbitrary worker threads.
- Normalize or alter application text silently.
- Guarantee that every Unicode character exists without an application-supplied fallback family.
- Ship MSDF, color emoji, SVG glyphs, vertical writing, and every OpenType feature in the first MVP.
- Change MonoGame's general-purpose `SpriteFont` public API as part of the UI migration.

## Current State

### Existing MonoGame Capabilities

- `SpriteFont` stores a prebuilt bitmap atlas and metrics and is drawn by `SpriteBatch`.
- `.spritefont` XML selects a font, size, style, character ranges, and optional default character.
- MGCB uses FreeType in `MonoGame.Framework.Content.Pipeline` to rasterize selected glyphs offline
  and writes the result to XNB.
- `MonoGame.Library.FreeType` is currently a Content Pipeline dependency, not a guaranteed runtime
  dependency of all supported MonoGame backend packages.
- `SurfaceFormat.Alpha8` and partial `Texture2D` updates are available building blocks for a compact
  grayscale atlas.

### Existing UI Limitations

- UI controls expose `SpriteFont` directly in approximately twenty public font properties.
- Measurement is spread across controls through direct `SpriteFont.MeasureString` and
  `LineSpacing` calls.
- Wrapping, caret placement, hit testing, selection, and visible-character behavior frequently walk
  UTF-16 characters and remeasure substrings.
- `UIRenderContext.Text` centralizes drawing, but layout is not centralized.
- `DisplayFontResolver` can switch between a 1x and 2x bitmap atlas; it solves the immediate Retina
  blur but does not provide arbitrary sizes, shaping, or fallback.
- The catalog currently ships 14-point and 28-point IBM Plex Sans XNB atlases.

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

- Reuse lessons and generated bindings from the Content Pipeline FreeType integration, but do not
  reference the Content Pipeline assembly from the Forma runtime packages.
- [ ] Evaluate direct native bindings, maintained .NET bindings, and a small MonoGame-owned interop
  layer for FreeType and HarfBuzz.
- [ ] Select a Unicode bidi/segmentation/line-break strategy. HarfBuzz shapes runs but does not by
  itself implement the full bidi or line-breaking algorithms; do not mark shaping complete without
  this decision.
- [ ] Record exact versions, licenses, update ownership, vulnerability response, and upstream URLs.
- [ ] Define one RID/platform matrix for Windows x64/arm64, Linux x64/arm64, macOS x64/arm64,
  Android ABIs, and iOS architectures supported by MonoGame.
- [ ] Verify NativeAOT, trimming, sandbox, code-signing, and static-link requirements per target.
- [ ] Decide whether dynamic text is in every framework package or an optional companion package.
  The UI public contract must not change by backend even if implementation packaging is optional.
- [ ] Add native binaries and notices through the same reproducible build/release process as other
  MonoGame native dependencies; do not depend on a developer's system installation.
- [ ] Add CI verification that published packages contain the expected native assets for each RID.

## Delivery Phases

### Phase 0: Contracts, Dependency Spike, and Baselines

- [ ] Write an API decision record covering the provisional public concepts, ownership, disposal,
  source compatibility, binary compatibility, and error behavior.
- [ ] Inventory every UI `SpriteFont` property, `MeasureString`, `LineSpacing`, glyph-width loop, and
  `UIRenderContext.Text` call; classify it as measurement, layout, hit testing, or rendering.
- [ ] Decide the compatibility shape: parallel `UIFont` property, forwarding legacy property,
  implicit adapter, or a planned breaking change while the UI toolkit is experimental.
- [ ] Build a desktop FreeType/HarfBuzz spike that loads IBM Plex Sans from bytes, shapes one Latin
  and one Arabic string, rasterizes returned glyph IDs, and uploads one `Alpha8` atlas page.
- [ ] Run the packaging spike on Metal and at least one non-Metal backend before fixing the public
  API around assumptions from a single platform.
- [ ] Select and document bidi, grapheme, script, and line-break dependencies or data tables.
- [ ] Capture baseline catalog screenshots at 1x and 2x plus startup time, frame allocations, XNB
  font size, and steady-state texture memory.
- [ ] Add a representative multilingual corpus licensed for tests: Latin combining marks, Cyrillic,
  Greek, Arabic, Hebrew, Devanagari, Thai, CJK, emoji sequences, and malformed UTF-16.
- [ ] Establish security limits for font size, face count, table sizes, glyph bitmap dimensions,
  atlas pages, fallback depth, layout length, and shaping/rasterization timeouts.

#### Phase 0 Exit Criteria

- [ ] Dependency choices work on the selected runtime/platform matrix and have an approved license
  and packaging path.
- [ ] The spike proves shaped glyph IDs can be rasterized and rendered through MonoGame graphics.
- [ ] The API decision record identifies a migration path for every current UI font call site.

### Phase 1: UI Font Abstraction and SpriteFont Adapter

- [ ] Add the logical UI font and immutable text-layout contracts without adding dynamic native
  dependencies yet.
- [ ] Implement `SpriteFontAdapter` with metrics, fallback behavior, layout output, and drawing that
  match current `SpriteFont` behavior.
- [ ] Centralize text measurement behind the layout service; eliminate new direct
  `SpriteFont.MeasureString` calls in UI code.
- [ ] Add a UI font collection/fallback type even though the initial adapter contains one font.
- [ ] Define value-based identities for font instances and layout options so caches do not rely on
  object reference equality.
- [ ] Define disposal semantics: controls do not own fonts; font services and application content
  lifetime own native faces and atlas resources.
- [ ] Preserve the current `DisplayFontResolver` behavior through the adapter until dynamic fonts
  supersede it.
- [ ] Add unit tests proving existing ASCII measurements, wrapping, alignment, clipping, and 1x/2x
  rendering remain unchanged through the adapter.

#### Phase 1 Exit Criteria

- [ ] The complete retained UI suite passes using `SpriteFontAdapter`.
- [ ] Existing catalog behavior is visually unchanged and no dynamic native library is required.

### Phase 2: Runtime Font Loading and FreeType Rasterization

- [ ] Implement safe runtime initialization and shutdown for the selected FreeType integration.
- [ ] Load font faces from `Stream`, `ReadOnlyMemory<byte>`, and a project-relative file API with
  deterministic ownership of copied or pinned bytes.
- [ ] Support collection face indices and expose family/style metadata.
- [ ] Read ascender, descender, line gap, underline, glyph advance, glyph bounds, variation axes, and
  supported-character coverage.
- [ ] Convert logical UI size and display scale into FreeType sizing without baking DPI into layout
  geometry.
- [ ] Rasterize grayscale glyphs with explicit hinting options and correct positive/negative pitch
  handling.
- [ ] Return `.notdef` consistently when a font maps a character but cannot produce a valid glyph.
- [ ] Add deterministic limits and errors for malformed, unsupported, or oversized font data.
- [ ] Add an optional Content Pipeline asset that packages original font bytes and metadata without
  pre-rasterizing them; direct runtime loading must remain available.
- [ ] Test multiple faces, combining marks, large and fractional logical sizes, variable-font axes,
  malformed files, and repeated create/dispose cycles.

#### Phase 2 Exit Criteria

- [ ] Runtime metrics and grayscale glyph bitmaps are stable on Windows, Linux, macOS, Android, and
  iOS targets selected in Phase 0.
- [ ] Forma does not reference `MonoGame.Framework.Content.Pipeline` at runtime.
- [ ] Native handles and font-byte ownership pass leak and repeated-disposal tests.

### Phase 3: Dynamic Glyph Atlas and Renderer

- [ ] Implement a deterministic rectangle allocator with padding and configurable page dimensions.
- [ ] Store grayscale glyphs in `SurfaceFormat.Alpha8` pages and verify sampling behavior on each
  graphics backend.
- [ ] Implement render-thread upload batching so multiple new glyphs do not cause one full texture
  upload each.
- [ ] Render shaped glyph placements as batched quads with tint, clipping, transforms, opacity, and
  layer depth compatible with existing UI drawing.
- [ ] Define bounded page budgets per graphics device and rendering mode.
- [ ] Implement page/glyph usage tracking and an eviction policy that cannot invalidate glyphs while
  an active frame references them.
- [ ] Recover from device loss/reset and disposal without retaining invalid `Texture2D` references.
- [ ] Expose read-only diagnostics: page count, capacity, used area, glyph count, misses, uploads,
  evictions, bytes, and pending work.
- [ ] Add debug atlas visualization without exposing mutable atlas internals publicly.
- [ ] Test atlas edge placement, padding, fragmentation, eviction, clipping, device reset, multiple
  graphics devices, and disposal order.

#### Phase 3 Exit Criteria

- [ ] A shaped Latin layout renders through dynamic atlas pages on Direct3D, OpenGL, Vulkan, and
  Metal with approved pixel output.
- [ ] Atlas memory never exceeds its configured budget under a randomized glyph stress test.
- [ ] Warm repeated drawing performs no rasterization or texture upload.

### Phase 4: Unicode Shaping, Fallback, and Text Layout

- [ ] Segment text into grapheme clusters, scripts, bidi runs, and font-fallback runs while
  preserving mappings to the original UTF-16 input.
- [ ] Shape runs with locale, direction, script, language, variation coordinates, and configurable
  OpenType features.
- [ ] Resolve fallback per cluster so combining sequences and emoji ZWJ sequences are not split
  across fonts incorrectly.
- [ ] Implement the Unicode Bidirectional Algorithm and visual run ordering through the selected
  Phase 0 dependency/strategy.
- [ ] Implement Unicode line-break opportunities, mandatory breaks, tabs, whitespace preservation,
  and application-provided paragraph separators.
- [ ] Produce immutable line/run/glyph data containing advances, offsets, baselines, extents,
  clusters, and logical-to-visual mappings.
- [ ] Implement hit testing, caret positions, selection rectangles, word boundaries, grapheme
  movement, range bounds, trimming, ellipsis, and visible-glyph ranges on `TextLayout`.
- [ ] Define malformed UTF-16 behavior without throwing or reading outside input bounds.
- [ ] Add fallback-cycle detection and a deterministic final missing-glyph policy.
- [ ] Implement a bounded layout cache and invalidation when text, font settings, locale, direction,
  width, or relevant theme values change.
- [ ] Validate shaping against known HarfBuzz outputs and approved reference images, not only text
  width assertions.

#### Phase 4 Exit Criteria

- [ ] Arabic, Hebrew/Latin bidi, Devanagari, Thai, CJK wrapping, combining marks, standard
  ligatures, emoji sequences, and missing-glyph fallback pass focused tests.
- [ ] Measurement, hit testing, selection, and drawing all consume the same `TextLayout`.
- [ ] No UI API equates UTF-16 indices, Unicode scalar values, graphemes, and glyph indices.

### Phase 5: Retained Control Integration

- [ ] Route the complete `Forma` namespace through the shared `UIFont`,
  `TextLayout`, layout engine, and renderer contracts; the dynamic implementation becomes the
  normal path and `SpriteFontAdapter` remains the compatibility path.
- [ ] Migrate `Label` first, including wrapping, justification, ellipsis, visible characters,
  paragraph spacing, language, direction, and range queries.
- [ ] Migrate `LineEdit`, including horizontal scrolling, grapheme-safe caret movement, selection,
  secret text, IME composition ranges, clipboard text, and fallback rendering.
- [ ] Migrate `TextEdit` and `CodeEdit`, including multiline layout, line caches, gutters, tabs,
  syntax runs, code completion, and incremental invalidation for edited lines.
- [ ] Migrate buttons, menus, tabs, item lists, trees, graph controls, dialogs, tooltips, and every
  remaining direct `SpriteFont` measurement identified in Phase 0.
- [ ] Replace substring-measurement loops with layout hit testing and cluster maps.
- [ ] Ensure theme inheritance can supply a font family, size, features, and rendering policy.
- [ ] Invalidate control minimum size and text layout when font, locale, direction, width, scale, or
  theme values change.
- [ ] Keep logical layout stable when moving between displays; update only density-dependent glyph
  resources unless font hinting policy explicitly requires relayout.
- [ ] Preserve `SpriteFont` behavior through the adapter for every migrated control.
- [ ] Update `GODOT_COMPATIBILITY.md` with the behaviors now backed by real shaping and the remaining
  differences from Godot.

#### Phase 5 Exit Criteria

- [ ] Every text-bearing `Forma` control measures, lays out, renders, and maps
  interaction through the shared text system, with no control-specific bypass.
- [ ] No retained UI control directly calls `SpriteFont.MeasureString`, indexes a glyph atlas, or
  derives caret positions by measuring every UTF-16 substring.
- [ ] Existing retained UI tests pass through the compatibility adapter.
- [ ] New multilingual editing, selection, wrapping, and display-scale tests pass through dynamic
  fonts.

### Phase 6: Catalog Typography Stories and Diagnostics

Add explicit `Typography` stories in addition to reflected component stories. Give custom stories
access to the font service, display-scale controls, and diagnostics rather than hiding these in
catalog-global state.

- [ ] Migrate all `Forma.Catalog` application chrome, reflected component stories, and custom
  stories to the same dynamic text services used by `Forma`.
- [ ] **Dynamic Sizes:** live text, font-family picker, and a size slider covering small UI text
  through large headings without prebuilt assets.
- [ ] **Display Density:** side-by-side logical-size samples plus a 1x/1.5x/2x density simulator;
  show physical raster size and prove logical bounds remain stable.
- [ ] **Fallback Chain:** mixed Latin, Greek, Cyrillic, Arabic, Devanagari, CJK, symbols, and emoji;
  identify the selected face per shaped run.
- [ ] **Shaping and Features:** combining marks, `fi`/`ffi` ligatures, kerning pairs, optional
  OpenType feature toggles, and variable-font axes when the selected font supports them.
- [ ] **Bidirectional Text:** editable Arabic/Hebrew and Latin content with automatic, LTR, and RTL
  direction controls; visualize logical and visual run order in diagnostics.
- [ ] **Wrapping and Selection:** multilingual paragraphs with adjustable width, wrapping, ellipsis,
  mouse selection, caret movement, and range-bound overlays.
- [ ] **Atlas Inspector:** page thumbnails, occupancy, cache hits/misses, uploads, evictions, memory
  budget, clear-cache command, and a glyph stress input.
- [ ] **SpriteFont Compatibility:** render the same controls with dynamic font and
  `SpriteFontAdapter`; label differences that are intentional rather than implying pixel identity.
- [ ] **Failure States:** missing face, malformed asset, exhausted atlas budget, unsupported glyph,
  and fallback exhaustion must produce visible diagnostics without terminating the catalog.
- [ ] Add a catalog option to switch between dynamic and compatibility fonts at runtime.
- [ ] Add screenshot states for desktop 1x, Retina 2x, narrow viewport, and RTL examples.
- [ ] Package test fonts and licenses explicitly; use a broad fallback font only if its redistribution
  terms and package size are acceptable.

#### Phase 6 Exit Criteria

- [ ] Every catalog screen and story renders text through the dynamic path by default; the only
  `SpriteFont` rendering is inside explicit compatibility examples or runtime compatibility mode.
- [ ] Every typography story is interactive, searchable, keyboard accessible, and stable under
  window resize and display-scale changes.
- [ ] Automated catalog smoke visits every typography story and records no exception, missing native
  dependency, unbounded atlas growth, or graphics validation error.
- [ ] Approved screenshots demonstrate crisp 1x/2x output and correct complex-script shaping.

### Phase 7: Performance, Platform, and Resilience Gates

- [ ] Benchmark cold face load, first shape, first raster/upload, warm layout lookup, warm draw,
  fallback-heavy text, and atlas eviction.
- [ ] Record allocations and ensure unchanged warm UI text creates no managed allocation per frame.
- [ ] Add counters for shape time, raster time, queued glyphs, upload bytes, layout-cache hit rate,
  and atlas-cache hit rate.
- [ ] Stress rapid text editing, locale changes, display moves, font-size animation, fallback churn,
  device reset, and repeated game creation/disposal.
- [ ] Fuzz font loading and text shaping with bounded resources and malformed input.
- [ ] Test fonts with very large metrics, zero contours, negative bearings, color tables, collections,
  variable axes, and unsupported table combinations.
- [ ] Verify clipping and atlas sampling at fractional positions and non-integer display scales.
- [ ] Verify published packages in clean environments without system FreeType/HarfBuzz installs.
- [ ] Run trimming and AOT smoke tests for supported mobile/native targets.
- [ ] Define and meet release budgets for binary size, font load time, first-use latency, warm frame
  cost, layout-cache memory, and atlas memory; record numbers in this document before rollout.

#### Phase 7 Exit Criteria

- [ ] Performance budgets are documented with measured evidence and no unbounded cache remains.
- [ ] The supported platform matrix passes package, startup, render, device-reset, and disposal tests.
- [ ] Fuzzing and malformed-font tests fail safely without process termination or uncontrolled memory
  growth.

### Phase 8: Default Rollout, Compatibility, and Documentation

- [ ] Make dynamic text rendering the default implementation for `Forma` on
  every supported backend after Phases 0-7 pass.
- [ ] Make dynamic fonts the default throughout `Forma.Catalog`, including application chrome,
  reflected component stories, and typography examples, only after Phases 0-7 pass.
- [ ] Keep a runtime catalog toggle and CI compatibility coverage for `SpriteFontAdapter`.
- [ ] Remove the catalog-specific `Catalog@2x` asset and `DisplayFontResolver` wiring only after
  dynamic DPI tests and screenshots are accepted; do not remove the general compatibility API
  solely because the catalog no longer needs it.
- [ ] Document runtime font loading, fallback families, logical sizing, DPI behavior, OpenType
  features, cache budgets, disposal, and deployment requirements.
- [ ] Document when `SpriteFont` remains preferable: fixed glyph sets, pixel art, deterministic
  offline atlases, minimal native dependencies, and legacy XNA-compatible projects.
- [ ] Add migration examples converting a control tree from `SpriteFont` to dynamic fonts without
  changing layout intent.
- [ ] Update package readmes, templates, API docs, third-party notices, and release notes.
- [ ] Define semantic-versioning treatment for any UI font property changes and retain obsolete
  forwarding APIs for the agreed compatibility window.
- [ ] Add a rollback switch that selects `SpriteFontAdapter` if a platform-specific dynamic text
  regression is found after release.

#### Phase 8 Exit Criteria

- [ ] New `Forma` control trees use dynamic text by default without catalog-
  specific setup or a `DisplayFontResolver`.
- [ ] New UI catalog runs use dynamic fonts by default on every supported backend.
- [ ] Existing `SpriteFont` UI samples and tests remain functional and documented.
- [ ] The dual-XNB Retina workaround is no longer required by the catalog.
- [ ] Release artifacts contain all required native dependencies, licenses, docs, and examples.

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
| Compatibility | `SpriteFontAdapter`, existing UI suite, pixel-font sampling |
| Backends | Direct3D, OpenGL, Vulkan, Metal pixel and smoke tests |
| Platforms | Windows, Linux, macOS, Android, iOS package/startup/render/AOT gates |
| Catalog | Every typography story at 1x, 1.5x, 2x, narrow, and RTL states |

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Native dependency/package growth | Measure by RID, trim unused features, keep SpriteFont-only deployment viable if packaging permits |
| Incorrect claim of Unicode support | Gate on shaping, bidi, grapheme, fallback, and interaction tests rather than glyph rendering alone |
| Layout changes after async rasterization | Derive layout from shaping metrics before raster work; raster completion only invalidates drawing |
| Atlas churn during animated sizes | Quantize physical sizes deliberately, cap pages, expose diagnostics, document animation tradeoffs |
| UI migration regressions | Land the SpriteFont adapter first, migrate one control family at a time, run focused and full suites |
| Worker-thread graphics access | Separate CPU glyph jobs from an explicit render-thread upload queue |
| Font parser security | Pin versions, fuzz, enforce limits, own vulnerability updates, and avoid system-library ambiguity |
| Mobile/AOT interop failures | Prove packaging and native handles in Phase 0 before making the abstraction public |
| API overfit to one rasterizer | Keep layout, shaping, rasterization, and atlas responsibilities separate |
| Catalog becomes the only validation | Require unit, integration, backend, package, and device-reset tests independently of stories |

## Completion Definition

This plan is complete only when the entire `Forma` namespace uses dynamic text
by default, dynamic fonts are the validated catalog default, complex text uses a shared shaped
layout for rendering and interaction, DPI changes select appropriate raster resources without
changing logical geometry, atlas memory is bounded, all selected platforms package their
dependencies, and `SpriteFont` remains a tested compatibility option.
