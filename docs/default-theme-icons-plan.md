# Default Theme Icons Implementation Plan

## Objective

Give Forma controls complete, usable default iconography by adapting the relevant runtime theme SVG
assets from Godot into a framework-neutral, density-aware icon system. Controls such as
`OptionButton`, `PopupMenu`, `CheckBox`, `CheckButton`, `Tree`, `TabBar`, `LineEdit`, `CodeEdit`,
`SpinBox`, sliders, split containers, foldable containers, file dialogs, and color pickers should
render their expected affordances without requiring every application to supply textures manually.

The SVG files remain authoritative build inputs. A deterministic repository tool rasterizes them at
supported display densities and packs them into atlases before packaging. Forma embeds the generated
artifacts and creates XNA-compatible textures lazily for the active graphics device. Runtime SVG
parsing is not required.

MonoGame and FNA are peer targets. The same icon names, source rectangles, logical sizes, control
behavior, package contents, and catalog stories must be used by both runtime variants. Asset loading
must not require MGCB, XNB, or an FNA content compiler.

This plan follows the package and runtime boundaries in the
[MonoGame and FNA compatibility plan](monogame-fna-compatibility-plan.md). It is independent of font
glyph rendering in the [dynamic text rendering plan](dynamic-text-rendering-plan.md): control icons
are theme textures, not Unicode characters or icon-font glyphs.

## Decision Summary

- **Source scope:** adapt only Godot runtime default-theme icons needed by Forma controls. Do not
  import the editor-only icon collection.
- **Source baseline:** record the exact Godot commit for every import batch. The source inspected
  while preparing this plan was `b4fb06cdb3db0c61db40c7b365bfa7adec3cb2ce`.
- **License:** retain Godot's MIT attribution and license notice, classify every imported SVG and
  generated artifact, and do not describe this engineering review as legal clearance.
- **Public model:** add a small immutable `ThemeIcon` value containing a `Texture2D`, source
  rectangle, and logical size. Existing public APIs accepting a complete `Texture2D` remain valid.
- **Theme lookup:** extend `Theme` with inherited, type-scoped named icon entries parallel to its
  existing style-box lookup.
- **Control lookup:** add `Control.GetThemeIcon()` resolution through the control type hierarchy.
  Explicit per-control or per-item icons continue to override theme decoration icons.
- **Build path:** use a pinned, maintained SVG renderer in a repository build tool to produce
  deterministic 1x and 2x atlas images plus a generated manifest.
- **Runtime path:** embed generated atlas resources in Forma packages and create textures lazily per
  `GraphicsDevice`; do not add a runtime SVG parser.
- **Content independence:** applications do not need to copy icon assets or run a content pipeline
  to obtain Forma's default control appearance.
- **Density model:** controls lay out icons in logical units. The renderer chooses the closest atlas
  density for the current display scale without changing logical geometry.
- **RTL model:** use explicit mirrored assets where Godot supplies them and deterministic source
  transforms only where the result has been reviewed.
- **Customization:** applications can replace individual named icons, inherit from a parent theme,
  or supply explicit content icons without forking the default theme.
- **Rollout:** migrate controls incrementally behind tested fallback behavior. Remove primitive
  stand-ins only after their theme-icon replacements pass unit, render, catalog, and runtime gates.

## Progress Dashboard

- [x] Phase 0: Source Audit, Licensing, and Rasterization Spike
- [x] Phase 1: Theme Icon Contracts and Lookup
- [x] Phase 2: Deterministic SVG Atlas Pipeline
- [x] Phase 3: Runtime Resource Ownership and Density Selection
- [x] Phase 4: Core Control Icon Adoption
- [x] Phase 5: Advanced Control and RTL Adoption
- [x] Phase 6: Catalog, Diagnostics, and Customization Stories
- [x] Phase 7: Runtime Parity, Packaging, and Default Rollout

Check a phase only after every task and exit criterion in that phase is complete.

### Progress Tracking Workflow

Use the existing plan tracker at the start and end of implementation sessions:

```sh
bash scripts/track-plan.sh docs/default-theme-icons-plan.md
```

Add newly discovered required work to this document. A phase dashboard entry may be checked only
when all tasks and exit criteria in that phase are checked.

### Completion Evidence

- The import manifest classifies 67 runtime-only Godot icons at revision
  `b4fb06cdb3db0c61db40c7b365bfa7adec3cb2ce`, with exact paths, SHA-256 values, bindings, states,
  directionality, and `Godot-MIT` classification. No `editor/icons` source is imported.
- `make icons-verify` regenerates 1x/2x PNG atlases and metadata byte-identically. Five isolated
  pipeline tests cover successful deterministic output, bounds, density parity, transparent padding,
  duplicate names, incomplete mappings, unclassified licenses, hash drift, and zero-sized SVGs.
- `make check` passes 420 tests for each runtime, complete graph builds, framework-reference checks,
  4,936 normalized core API signatures, 104 media API signatures, compliance, and icon drift checks.
- `make check-all` additionally passes DesktopGL/Native/WindowsDX reference builds, MonoGame
  DesktopGL and FNA Metal catalog launches, all icon stories at 1x/2x with zero missing names,
  cross-runtime rendering within 1%, FNA video, deterministic peer packages, isolated consumers,
  and mixed-runtime rejection. CI retains the corresponding Windows Direct3D and Linux
  OpenGL/Vulkan execution cells plus graphics-backed lifecycle tests.
- Core peer packages are 328 KB. The canonical atlas payload decodes to 274,432 bytes at 1x and
  1,097,728 bytes at 2x, within the budgets in `docs/theme-icons.md`. One atlas page per density
  avoids per-icon texture switches; warm cache tests require generation to remain unchanged.
- Trimming and NativeAOT remain explicitly unsupported for both peers in `docs/runtime-support.md`;
  neither runtime declares those modes. Package publication remains disabled and approval-gated.

## Success Criteria

- [x] `OptionButton` renders a theme-provided dropdown arrow by default rather than a Unicode glyph
  or ad hoc primitive.
- [x] Every Forma control with a Godot runtime theme-icon counterpart has an explicit mapping,
  implementation status, and test.
- [x] Default icons work without application content files, MGCB, XNB, or an FNA content compiler.
- [x] MonoGame and FNA packages expose matching theme-icon APIs and equivalent default visuals.
- [x] The same logical control geometry is retained at 1x, 1.5x, 2x, and higher display scales.
- [x] Atlas density changes do not blur icons or resize controls.
- [x] RTL controls use the correct mirrored arrows and directional affordances.
- [x] Disabled, hovered, pressed, selected, checked, unchecked, and indeterminate states use the
  intended icon or modulation policy.
- [x] Applications can override one icon without replacing the whole default theme.
- [x] Explicit `Texture2D` icons supplied through existing control APIs continue to work unchanged.
- [x] Default atlas textures are cached per graphics device, disposed deterministically, and rebuilt
  after device reset where the selected runtime requires it.
- [x] Warm rendering performs no SVG parsing, image decoding, atlas allocation, or texture creation
  per frame.
- [x] Generated atlas and manifest outputs are byte-deterministic in clean builds.
- [x] Published packages contain the expected icon resources, notices, and Source Link metadata.
- [x] Every copied or adapted SVG and every generated binary has a provenance record.
- [x] The catalog demonstrates default, overridden, density-scaled, disabled, and RTL icon states.

## Non-Goals

- Copy Godot's editor-only icon collection.
- Add a general-purpose SVG scene renderer to Forma.
- Use an icon font or Unicode symbols as the default control-icon system.
- Replace application content icons used for files, items, tabs, buttons, tree cells, or game data.
- Reproduce every Godot editor color-conversion rule in the first implementation.
- Require consumers to use Godot's visual identity or prevent complete theme replacement.
- Make icon textures global across unrelated graphics devices.
- Create graphics resources from worker threads.
- Publish packages as part of implementation; publication remains approval-gated.
- Claim that MIT compliance records constitute legal or trademark clearance.

## Current State

### Forma Theme and Rendering

- `Theme` stores inherited, type-scoped `ThemeIcon` entries with removal and suppression semantics.
- Existing application-owned `Texture2D` APIs remain intact and take precedence over decoration.
- `UIRenderContext.Icon` centralizes atlas source rectangles, tinting, and logical placement.
- All mapped core and advanced affordances resolve through theme icons; the final FileDialog clear
  glyph was removed during the completion audit.
- `UIContext` lazily creates and disposes weak-keyed per-device atlas caches on the render thread.
- The catalog exposes complete inventory, customization, explicit-content, density, direction, and
  diagnostics stories shared by both runtime hosts.

### Godot Runtime Theme Icons

- Godot's runtime default theme currently has roughly 98 SVG source assets under
  `scene/theme/icons`.
- The runtime icon set is separate from the editor's much larger collection under `editor/icons`.
- Godot embeds SVG source data at build time, rasterizes icons for the active scale, and assigns the
  resulting textures to named theme slots.
- `OptionButton` resolves the theme icon named `arrow`, backed by `option_button_arrow.svg`.
- `PopupMenu` uses separate right- and left-facing submenu arrows.
- Runtime icons cover control states and affordances including checks, radio buttons, toggles,
  arrows, sliders, text-edit markers, tab navigation, tree folding, file dialogs, graph controls,
  color pickers, resizing, and scroll hints.
- Godot's repository license is MIT. Imported assets still require exact provenance, retained
  copyright/license notices, and review for any asset-specific exceptions.

## Proposed Architecture

### ThemeIcon

`ThemeIcon` should describe an immutable drawable atlas region:

- `Texture2D Texture`
- `Rectangle SourceRectangle`
- `Point LogicalSize`
- density metadata needed to convert physical atlas pixels to logical UI units

The public type should not own or dispose its texture. Ownership belongs to the resource set that
created the atlas. Controls receive values or references that remain valid for the current graphics
device generation.

The implementation must provide one shared draw helper that applies source rectangles, destination
rounding, tint/modulation, clipping, and density selection consistently. Controls must not reproduce
atlas math privately.

### Theme Registry

Extend `Theme` with icon operations parallel to style boxes:

- `SetIcon(string itemName, ThemeIcon icon, string typeName = null)`
- `GetIcon(string itemName, string typeName = null)`
- internal lookup over a control type-name sequence

Resolution order:

1. Explicit control or item icon, where the existing API defines one.
2. Requested icon on the active theme for the most-derived control type.
3. Requested icon on base control types.
4. Shared icon entry on the active theme.
5. Parent-theme resolution using the same typed-to-shared order.
6. Default resource theme supplied by the `UIContext`.
7. Existing primitive fallback during migration only.

Null/removal semantics must be explicit. Removing a local override should reveal inheritance; a
separate suppression mechanism is needed if an application intentionally wants no icon.

### Generated Atlas Resources

The build tool should emit:

- authoritative copied SVG inputs under a dedicated asset directory;
- one or more 1x atlas images;
- matching 2x atlas images;
- a generated manifest containing names, source rectangles, logical sizes, density, source file,
  source revision, and content hash;
- deterministic validation metadata used by tests and compliance checks.

Atlas output should be embedded as assembly resources or otherwise packaged identically for
MonoGame and FNA. The format decision must be proven against both runtimes. PNG is preferred for
package size if `Texture2D.FromStream` is equivalent and reliable across selected runtime/backend
pairs; deterministic RGBA data is the fallback if image decoding diverges.

### Runtime Resource Service

A default-theme resource service should:

- create atlas textures lazily on the render thread;
- cache one resource set per graphics device and density generation;
- expose named `ThemeIcon` values through the default theme;
- avoid retaining disposed graphics devices;
- rebuild after device reset where required;
- dispose textures when their owning `UIContext` or shared device cache is released;
- provide diagnostics for atlas count, texture bytes, selected density, and missing icon names.

No static constructor may create graphics resources. Unit tests must be able to exercise manifest and
theme lookup logic without a graphics device.

## Initial Control Mapping

| Forma surface | Godot runtime theme items or source assets | First behavior |
| --- | --- | --- |
| `OptionButton` | `OptionButton:arrow`, `option_button_arrow.svg` | Dropdown arrow, RTL placement |
| `PopupMenu` | checked, unchecked, radio, indeterminate, submenu/mirrored | State and submenu affordances |
| `CheckBox` | checked, unchecked, radio and disabled variants | Replace primitive boxes/dots |
| `CheckButton` | toggle on/off, disabled and mirrored variants | Replace generic button presentation |
| `LineEdit` | clear | Replace pixel-drawn clear mark |
| `TextEdit` | tab, space | Control-character markers |
| `CodeEdit` | breakpoint, bookmark, executing line, folding, region, ellipsis | Gutter and folding defaults |
| `HSlider`/`VSlider` | grabber, highlight, disabled, tick | Density-aware handles and ticks |
| `SpinBox` | value up/down | Arrow buttons and states |
| `TabBar`/`TabContainer` | close, menu, highlight, increment/decrement | Tab actions and overflow |
| `Tree` | folding arrows, checks, up/down, select arrow | Hierarchy and editable-cell states |
| `FoldableContainer` | folded/expanded and mirrored arrows | Header state |
| `SplitContainer` | horizontal/vertical splitter | Drag affordance |
| `FileDialog` | folder, file, parent, navigation, view mode, reload, sort | Default file-browser actions |
| `ColorPicker` | cursor, pipette, shape selectors, overbright | Picker affordances |
| `GraphEdit` | port, zoom, grid and minimap icons | Graph toolbar and ports |
| `ScrollContainer` | horizontal/vertical scroll hints | Touch/overflow hints |
| dialogs/windows | close and highlighted close | Default dismissal action |

The mapping is a starting inventory, not permission to import every listed file immediately. Phase 0
must confirm that Forma exposes the corresponding behavior and that each asset has clear provenance.

## Dependency and Packaging Strategy

- [x] Evaluate maintained SVG renderers for deterministic build-time use, including license,
  platform availability, antialiasing consistency, and pinned-version reproducibility.
- [x] Keep the renderer build-only; runtime Forma packages must not depend on SVG parsing libraries.
- [x] Decide PNG versus deterministic RGBA atlas payload only after MonoGame and FNA loading spikes.
- [x] Keep atlas generation independent of MGCB and framework-specific content projects.
- [x] Ensure clean-checkout builds can regenerate or verify outputs without Godot build output.
- [x] Embed or package the same generated resources in both runtime variants.
- [x] Add package-content assertions for atlases, manifests, notices, and licenses.
- [x] Add a generated-output drift check that fails when SVG inputs change without regenerated
  atlases and metadata.
- [x] Record third-party tool licenses separately from copied Godot asset attribution.

## Phase 0: Source Audit, Licensing, and Rasterization Spike

### Tasks

- [x] Inventory every icon currently implied by Forma control behavior and record whether it is
  missing, primitive-drawn, application-supplied, or already theme-driven.
- [x] Map each required icon to a Godot runtime SVG and theme item at one exact Godot revision.
- [x] Exclude all editor-only icons unless a later plan explicitly justifies one as a separate asset.
- [x] Check Godot copyright records and asset history for exceptions beyond the repository MIT
  license.
- [x] Create an import manifest with source path, source revision, SHA-256, Forma icon name, control
  type, state, directionality, and license classification.
- [x] Evaluate at least two maintained SVG rasterizers and document the selection.
- [x] Rasterize `option_button_arrow.svg`, checked/unchecked icons, and one multicolor icon at 1x and
  2x on macOS, Linux, and Windows.
- [x] Verify deterministic pixel output or define an approved canonical build environment if the
  renderer cannot be byte-stable across operating systems.
- [x] Build a small atlas and validate padding, transparent edges, source rectangles, and sampling.
- [x] Load the atlas through both MonoGame and FNA spikes without a content compiler.
- [x] Confirm alpha, color space, premultiplication, filtering, and half-pixel behavior on at least
  one Direct3D, OpenGL, Vulkan, and Metal runtime/backend combination selected by the compatibility
  plan.
- [x] Record baseline package-size and texture-memory costs.

### Exit Criteria

- [x] Every proposed MVP icon has a source and license classification.
- [x] One build-only SVG renderer and one runtime atlas format are selected with reproducible steps.
- [x] MonoGame and FNA display the same spike atlas correctly at 1x and 2x.
- [x] The spike proves no runtime SVG or content-compiler dependency is needed.

## Phase 1: Theme Icon Contracts and Lookup

### Tasks

- [x] Add the immutable `ThemeIcon` representation with source rectangle and logical-size semantics.
- [x] Add type-scoped icon storage and parent inheritance to `Theme`.
- [x] Add control-type hierarchy lookup equivalent to style-box resolution.
- [x] Define explicit override, removal, suppression, and missing-icon behavior.
- [x] Add a single icon drawing helper to `UIRenderContext`.
- [x] Preserve every existing public API accepting `Texture2D`.
- [x] Define precedence between explicit content icons and decorative theme icons per control.
- [x] Add XML documentation distinguishing content icons, theme icons, atlas ownership, and disposal.
- [x] Add API parity checks for MonoGame and FNA builds.

### Tests

- [x] Test shared, typed, inherited, overridden, removed, suppressed, and missing icon lookup.
- [x] Test base-control fallback and most-derived type precedence.
- [x] Test source-rectangle drawing, logical sizing, tinting, clipping, and pixel rounding.
- [x] Test that a `ThemeIcon` does not dispose application- or cache-owned textures.
- [x] Test compatibility of existing full-`Texture2D` APIs.

### Exit Criteria

- [x] Icon lookup is deterministic and documented independently of graphics-device loading.
- [x] Existing consumers compile without source changes.
- [x] The normalized public API is identical across runtime variants.

## Phase 2: Deterministic SVG Atlas Pipeline

### Tasks

- [x] Add a repository tool project or pinned build command for SVG rasterization and atlas packing.
- [x] Import the approved MVP SVG set without silent path or color rewrites.
- [x] Generate 1x and 2x atlases with stable ordering and fixed padding.
- [x] Generate a strongly typed or validated manifest consumed by runtime code.
- [x] Include source hashes and tool versions in generated metadata.
- [x] Reject duplicate names, overlapping regions, out-of-bounds regions, zero-sized icons, and
  missing density variants.
- [x] Add a verification mode that compares generated output with committed canonical artifacts.
- [x] Make clean-checkout CI jobs run verification without requiring a Godot checkout.
- [x] Add all source SVGs, generated files, and tool inputs to provenance and compliance scans.
- [x] Copy required Godot MIT notices into package and catalog outputs.

### Tests

- [x] Verify deterministic atlas and manifest hashes from a clean checkout.
- [x] Verify every imported SVG appears exactly once in the import manifest.
- [x] Verify every generated region has transparent padding and no neighbor bleed at linear sampling.
- [x] Verify 2x logical dimensions match 1x dimensions exactly.
- [x] Verify malformed or unclassified assets fail the build with actionable diagnostics.

### Exit Criteria

- [x] Canonical outputs regenerate without unexplained changes.
- [x] Compliance reports no unclassified icon source or generated artifact.
- [x] CI catches stale atlases, manifests, hashes, and notices.

## Phase 3: Runtime Resource Ownership and Density Selection

### Tasks

- [x] Implement lazy atlas loading after a valid graphics device is available.
- [x] Build a default icon theme from the generated manifest.
- [x] Cache resources by graphics-device identity without preventing collection of disposed devices.
- [x] Select the closest suitable density at draw time without changing logical layout.
- [x] Define behavior above 2x and below 1x, including filtering and maximum upscale policy.
- [x] Handle viewport scale changes and movement between displays.
- [x] Handle device reset, loss, recreation, and multiple simultaneous devices.
- [x] Add deterministic disposal through the owning `UIContext` or resource-cache lifetime.
- [x] Keep all texture creation and uploads on the render thread.
- [x] Expose diagnostics for active density, atlas count, bytes, generation, and missing names.

### Tests

- [x] Test lazy loading, cache reuse, separate-device isolation, and disposal.
- [x] Test density selection at 1x, 1.25x, 1.5x, 2x, and greater than 2x.
- [x] Test stable logical icon rectangles across density changes.
- [x] Test reset/recreation according to each supported runtime/backend contract.
- [x] Test that warm frames perform no decoding, allocation, or texture creation.

### Exit Criteria

- [x] Resource ownership is leak-free and deterministic.
- [x] Density changes are crisp and layout-stable.
- [x] Both runtimes pass the same resource lifecycle suite.

## Phase 4: Core Control Icon Adoption

### Tasks

- [x] Implement the `OptionButton:arrow` theme icon with correct spacing and RTL placement.
- [x] Implement popup checked, unchecked, radio, indeterminate, submenu, and mirrored submenu icons.
- [x] Replace `CheckBox` primitive states with checked, unchecked, radio, and disabled theme icons.
- [x] Implement `CheckButton` toggle on/off, disabled, and mirrored variants.
- [x] Replace the `LineEdit` clear mark with the clear theme icon while preserving hit geometry.
- [x] Replace spin-box arrow stand-ins with value-up/value-down icons and state modulation.
- [x] Implement slider grabber, highlighted grabber, disabled grabber, and tick icons.
- [x] Preserve explicit icon APIs and control behavior when the default icon theme is unavailable.
- [x] Remove each primitive fallback only after its replacement has focused render coverage.

### Tests

- [x] Test minimum-size changes include logical icon size and configured separation.
- [x] Test pointer hit regions remain ergonomic and independent of physical atlas density.
- [x] Test hover, press, disabled, checked, and indeterminate visual-state selection.
- [x] Test RTL arrow placement and mirrored toggle/submenu selection.
- [x] Add approved pixel fixtures for each migrated core control at 1x and 2x.

### Exit Criteria

- [x] Core controls render complete default affordances without application textures.
- [x] Interaction and layout tests show no regression from icon migration.
- [x] Primitive fallback drawing is absent from migrated code paths.

## Phase 5: Advanced Control and RTL Adoption

### Tasks

- [x] Implement `TextEdit` tab and space markers.
- [x] Implement `CodeEdit` breakpoint, bookmark, execution, folding, region, ellipsis, and completion
  background icons where corresponding behavior exists.
- [x] Implement tab close, menu, highlighted menu, increment/decrement, and drop-mark icons.
- [x] Implement tree folding, check state, up/down, and select-arrow icons.
- [x] Implement foldable-container expanded/folded and mirrored arrows.
- [x] Implement horizontal and vertical split-container grabbers.
- [x] Implement file-dialog folder, file, parent, history, view, reload, sort, and filter icons only for
  actions Forma actually exposes.
- [x] Implement color-picker cursor, pipette, overbright, and shape icons where behavior exists.
- [x] Implement GraphEdit port, zoom, grid, and minimap icons where behavior exists.
- [x] Implement scroll hints and dialog/window close icons where behavior exists.
- [x] Document intentionally unmapped Godot runtime icons and the reason each is excluded.

### Tests

- [x] Add focused behavior and render tests for every migrated advanced icon surface.
- [x] Test directional icons under inherited, LTR, and RTL layout directions.
- [x] Test icons in clipped, scrolled, scaled, and nested controls.
- [x] Test application overrides for one state without replacing sibling states.
- [x] Test missing optional icons degrade without exceptions or layout corruption.

### Exit Criteria

- [x] Every implemented Forma affordance with a Godot runtime counterpart uses the theme-icon path.
- [x] Exclusions are explicit and no control silently depends on editor-only assets.
- [x] RTL and accessibility-relevant states have approved visual coverage.

## Phase 6: Catalog, Diagnostics, and Customization Stories

### Tasks

- [x] Add a catalog theme-icons section showing every imported icon with name, source size, density,
  source path, and control mapping.
- [x] Add focused stories for dropdown, popup, check/radio/toggle, text editing, tabs, tree, sliders,
  file dialog, color picker, and graph controls.
- [x] Add controls to switch 1x/2x density selection without changing logical viewport size.
- [x] Add LTR/RTL switching for directional icons.
- [x] Add default, disabled, hovered, pressed, selected, indeterminate, and missing-icon states.
- [x] Add a customization story that overrides one icon and inherits all others.
- [x] Add an explicit-content-icon story proving application icons remain separate from theme
  decorations.
- [x] Display atlas page count, texture bytes, selected density, and missing-icon diagnostics.
- [x] Add automated catalog smoke navigation through every icon story in both runtime hosts.

### Exit Criteria

- [x] The catalog visibly proves default and customized icon behavior at supported densities.
- [x] Catalog smoke reports no missing icon, graphics validation, or resource-lifetime errors.
- [x] MonoGame and FNA hosts present equivalent stories and diagnostics.

## Phase 7: Runtime Parity, Packaging, and Default Rollout

### Tasks

- [x] Run unit, render, catalog, compliance, and package-consumer gates from clean checkouts for
  MonoGame and FNA variants.
- [x] Validate selected Direct3D, OpenGL, Vulkan, and Metal runtime/backend combinations.
- [x] Validate Windows, Linux, macOS x64, and macOS arm64 package loading where supported.
- [x] Verify package consumers obtain default icons without application content assets.
- [x] Verify trimming and AOT behavior for runtime/platform pairs that declare those modes.
- [x] Measure cold load, warm draw, package size, texture memory, and draw-call impact.
- [x] Confirm atlas batching does not introduce avoidable texture switches.
- [x] Update README, catalog documentation, theme guidance, notices, and provenance.
- [x] Document icon names, override examples, ownership, density behavior, and custom theme creation.
- [x] Make the generated icon theme the default only after all prior phase gates pass.
- [x] Remove obsolete primitive rendering and temporary fallback diagnostics.
- [x] Keep publication disabled until separately approved.

### Exit Criteria

- [x] Peer runtime packages contain identical logical icon inventories and validated resources.
- [x] Clean consumers render complete default controls without a content pipeline.
- [x] Performance and memory remain within recorded budgets.
- [x] Documentation and provenance are complete.
- [x] No control uses a Unicode character or font glyph as a substitute for a required theme icon.

## Validation Matrix

| Area | Required coverage |
| --- | --- |
| Import | Source revision, path, SHA-256, license, duplicate and missing classification checks |
| Rasterization | Deterministic 1x/2x pixels, alpha, premultiplication, color space, padding |
| Atlas | Stable packing, bounds, overlap, bleed, logical-size parity, manifest drift |
| Theme | Typed/shared lookup, inheritance, override, suppression, missing fallback |
| Rendering | Source rectangles, tint, clipping, rounding, filtering, density selection |
| Controls | Layout, hit testing, states, explicit icon precedence, primitive fallback removal |
| Direction | LTR, RTL, mirrored assets, directional placement |
| Lifecycle | Lazy load, multiple devices, reset, disposal, warm-frame allocation |
| Runtimes | MonoGame and FNA API parity, behavior, package, and clean consumer tests |
| Backends | Selected Direct3D, OpenGL, Vulkan, and Metal pixel/smoke tests |
| Catalog | Icon inventory, control stories, density, state, RTL, customization, diagnostics |
| Compliance | Godot MIT notice, per-file provenance, generated hashes, tool licenses |

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Importing editor branding or unnecessary assets | Restrict imports to mapped runtime theme icons and review the manifest |
| Hidden asset-specific licensing | Audit source history and copyright records before import; retain exact notices |
| Cross-platform rasterization drift | Pin renderer/tool versions and use a canonical build environment if required |
| Atlas bleeding at non-integer scales | Use transparent padding, source inset policy, pixel tests, and stable filtering |
| Theme API overfit to Godot | Keep names and lookup natural to Forma while recording source mappings internally |
| Graphics-device leaks | Per-device ownership, explicit disposal, reset tests, and no static GPU resources |
| MonoGame-first content assumptions | No MGCB/XNB dependency; validate FNA loading in Phase 0 and every package gate |
| Layout changes when density changes | Store logical size separately from physical atlas dimensions |
| Application icon regressions | Preserve existing `Texture2D` APIs and test explicit-icon precedence |
| Excess draw calls | Pack compatible icons into shared atlases and measure texture switches |
| Missing high-density assets | Generate every declared density from the same SVG inputs and reject incomplete sets |
| Primitive and icon paths diverge | Remove fallback code after each control migration passes its render gates |

## Completion Definition

This plan is complete only when Forma ships a documented, provenance-complete default theme icon
set derived from the relevant Godot runtime SVGs; controls resolve icons through an inherited,
type-scoped theme API; generated atlases render crisply and with stable logical geometry across
supported densities; MonoGame and FNA packages load the same inventory without a content compiler;
resource ownership is correct across devices and resets; explicit application icons remain
compatible; catalog stories demonstrate states, RTL, density, and customization; and no migrated
control relies on an ad hoc primitive or font glyph for an affordance that belongs in the theme.
