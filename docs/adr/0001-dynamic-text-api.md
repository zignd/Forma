# ADR 0001: Dynamic Text API and Ownership

- Status: Accepted provisionally for implementation
- Date: 2026-08-01
- Owners: Forma maintainers
- Related plan: `plans/dynamic-text-rendering-plan.md`

## Context

Forma currently exposes `SpriteFont` directly from controls and performs measurement, wrapping,
hit testing, caret placement, and drawing through separate `SpriteFont` operations. This works for
precompiled bitmap fonts, but it cannot represent shaped runs, fallback, grapheme boundaries,
bidirectional ordering, arbitrary runtime sizes, or density-specific glyph caches.

MonoGame and FNA must expose the same Forma API. Native shaping and rasterization dependencies must
remain implementation details and must not introduce runtime-branded public types.

## Decision

### Public concepts

The dynamic text API will use these framework-neutral concepts in the `Forma` namespace:

- `UIFont`: immutable logical font selection. It identifies one face or a fallback family plus
  logical size, variation coordinates, OpenType features, hinting policy, and rendering mode.
- `UIFontFamily`: an ordered, immutable fallback collection. It owns references to font faces but
  does not own controls or graphics resources.
- `TextLayoutOptions`: an immutable value describing width, wrapping, alignment, locale, direction,
  line spacing, tab stops, trimming, and visible-range behavior.
- `TextLayout`: immutable lines, runs, glyph placements, logical bounds, baselines, UTF-16 ranges,
  grapheme/cluster maps, caret positions, and selection geometry.
- `TextLayoutEngine`: the service that segments, resolves fallback, shapes, orders, breaks, and
  caches layouts.
- `DynamicGlyphCache`: a graphics-device-scoped service that rasterizes glyphs, allocates bounded
  atlas pages, queues render-thread uploads, and exposes read-only diagnostics.
- `ITextRenderer`: an internal rendering service that consumes `TextLayout`; controls never draw
  dynamic text from raw strings.

The first implementation may keep constructors internal while the contracts stabilize. Public
members must remain identical in MonoGame and FNA package builds.

### Compatibility shape

Controls gain a parallel `UIFont` property while retaining their existing `SpriteFont Font`
property during migration.

- Assigning `Font` installs or updates a cached `SpriteFontAdapter` in the control's effective
  `UIFont` slot.
- Assigning `UIFont` selects the new layout path and does not mutate the legacy `Font` value.
- If both are assigned, the most recently assigned property is effective. This rule is observable
  and covered by tests.
- Theme inheritance resolves `UIFont` first, then adapts a legacy `SpriteFont`.
- Existing source continues to compile. Existing binaries are not promised compatibility while
  Forma remains `0.x`, but avoidable member removal is still prohibited.
- Phase 8 may obsolete, but will not silently reinterpret, legacy `Font` properties. Removal
  requires a separately documented major-version decision.

`SpriteFontAdapter` produces the same `TextLayout` shape as dynamic fonts. It may support only the
character map and metrics available from `SpriteFont`, but measurement, drawing, hit testing, and
selection still consume one retained layout.

### Ownership and disposal

- Controls never own or dispose fonts, layouts, font families, native faces, atlas pages, or
  textures.
- Applications or a `UIContext`-owned text service own `UIFont` and `UIFontFamily` lifetimes.
- A dynamic face owns an immutable copy or explicitly retained owner of its source bytes for as
  long as native FreeType/HarfBuzz objects may reference them.
- Native handles use idempotent safe-handle wrappers. Disposal rejects new work and waits for or
  cancels bounded pending CPU work before releasing handles.
- `TextLayout` is immutable managed data and is not disposable.
- `DynamicGlyphCache` is scoped to one `GraphicsDevice`, owns its textures, handles device reset,
  and is disposed by its owning text service.
- Cached layouts and glyph metadata may reference stable font identities, never disposed native
  pointers.

### Coordinates and cache identity

Layout uses logical UI units. Glyph raster size is derived from logical size, display scale, and
rendering policy. A display-scale change selects or creates raster entries without reshaping text
unless an option affecting logical layout also changed.

Font, layout-option, variation, feature, and raster keys use value equality. Cache identity must not
depend on object reference or native pointer values.

### Error behavior and limits

- Null required arguments throw `ArgumentNullException`.
- Invalid sizes, face indices, variation values, and limits throw `ArgumentOutOfRangeException`.
- Unsupported or malformed font data throws `FontLoadException` with a stable Forma error code and
  an inner native diagnostic when available.
- Missing glyphs are data, not exceptions. Fallback is attempted per grapheme cluster, then a
  deterministic final replacement glyph is emitted.
- Malformed UTF-16 is converted to replacement scalars without out-of-bounds reads or unbounded
  retry.
- Atlas exhaustion returns a bounded failure result and diagnostic; it does not allocate beyond the
  configured budget.
- Native library loading failure reports the library, runtime identifier, and expected packaging
  path without exposing implementation types in public signatures.

Security limits are explicit configuration with conservative defaults: source byte length, face
count, table count and size, glyph bitmap dimensions, atlas pages and bytes, fallback depth, input
length, lines, glyphs, and synchronous shaping/rasterization work.

## Migration map

| Current usage | Migration |
| --- | --- |
| `SpriteFont Font` control property | Retain; forward through cached `SpriteFontAdapter`; add parallel `UIFont` property. |
| `SpriteFont.MeasureString` | Replace with `TextLayoutEngine.Layout(...).Bounds`. |
| `SpriteFont.LineSpacing` | Replace with retained layout line metrics or `UIFont` logical metrics. |
| Prefix/sub-string width loops | Replace with `TextLayout.HitTest` and cluster/caret maps. |
| Manual wrapping and ellipsis | Replace with `TextLayoutOptions` and retained lines. |
| Selection rectangles and caret X | Replace with layout selection geometry and caret positions. |
| `UIRenderContext.Text` | Preserve compatibility overload; adapt and render a `TextLayout`. Add layout-rendering overload. |
| `DisplayFontResolver` | Preserve through `SpriteFontAdapter` until dynamic raster density is the default. |
| Per-cell `TreeItem` custom fonts | Add `UIFont` equivalents; preserve SpriteFont setters through adapters. |
| Catalog 1x/2x XNB pair | Keep until dynamic rendering passes Phase 8 gates, then remove. |

The detailed source inventory is maintained in `plans/dynamic-text-callsite-inventory.md`.

## Rejected alternatives

- Exposing only `MeasureString` and `DrawString`: rejected because complex-script layout, hit
  testing, selection, and rendering would still disagree.
- Replacing every `Font` property immediately: rejected because it causes unnecessary source churn
  before the adapter proves compatibility.
- Runtime-specific public APIs: rejected because MonoGame and FNA are peer package variants.
- Letting controls own fonts or atlases: rejected because shared resources and device reset require
  service-level lifetime management.
- Using display pixels as layout units: rejected because moving a window between densities must not
  change logical layout.

## Consequences

The migration temporarily carries parallel font properties and compatibility adapters. In return,
all text behavior converges on one immutable layout representation, native dependencies remain
replaceable, and both runtime variants can share the same controls, tests, and public contracts.
