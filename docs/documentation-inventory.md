# Documentation Inventory

Inventory date: 2026-08-07.

This maintainer audit assigns ownership to every source-facing README and Markdown page under
`docs/`. Generated files under `Artifacts/` and `docs/api/` are excluded. Upstream submodule
READMEs are listed but remain externally owned.

Audience abbreviations: evaluator (E), game developer (G), XAML user (X), control author (A),
runtime/backend integrator (R), and contributor (C).

## Page Ownership

| Page | Audience | Canonical topic | Status and destination |
| --- | --- | --- | --- |
| `README.md` | E, G, X | Product overview, runtime choice, validation entry points | Current repository front door |
| `plans/README.md` | C | Plan ownership and lifecycle | Current internal planning index; excluded from the site |
| `samples/Forma.Catalog/README.md` | E, G, A, C | Running and operating Catalog hosts | Current example guide |
| `samples/Forma.Xaml.Game/README.md` | G, X | Signal Run sample, XAML ownership, hot reload | Current end-to-end example guide |
| `src/Forma/README.md` | G | Minimal retained UI example | Stale MonoGame-only onboarding; merge into a future first-UI guide |
| `tests/Assets/Text/README.md` | R, C | Multilingual fixture provenance and regeneration | Current contributor reference |
| `tests/Assets/Video/README.md` | R, C | Video fixture provenance and deterministic generation | Current contributor reference |
| `external/ThorVG/README.md` | R, C | Upstream ThorVG documentation | Externally owned; link only from provenance documentation |
| `external/XamlX/README.md` | X, C | Upstream XamlX architecture | Externally owned; link only from compiler architecture documentation |
| `docs/index.md` | E, G, X, A, R, C | Documentation routes by task | Current site front door |
| `docs/layout-and-sizing.md` | G, X, A | Layout constraints, allocation, spacing, viewport scaling | Current conceptual guide |
| `docs/controls-and-containers.md` | G, X, A | Retained ownership, composition, projection, container choice | Current conceptual guide |
| `docs/input-and-focus.md` | G, A, R | Pointer, focus, keyboard, text, clipboard, host adapters | Current conceptual guide |
| `docs/styling-and-themes.md` | G, X, A | Theme inheritance, selectors, icons, templates | Current conceptual guide |
| `docs/data-binding.md` | G, X, A | Task-focused compiled binding workflow | Current conceptual guide; language contract owns syntax |
| `docs/resource-lifetime.md` | G, X, A, R | Context, font, SVG, device, and attachment ownership | Current conceptual guide; specialist contracts own details |
| `docs/authorized-host-checklist.md` | R, C | Approved runtime-host requirements | Current specialist host-integration checklist |
| `docs/dynamic-text.md` | G, A, R | Dynamic-font setup, shaping, caching, deployment | Current text and fonts guide |
| `docs/runtime-acquisition.md` | R, C | Runtime dependency selection and pins | Current architecture decision; package manifest is machine-owned |
| `docs/runtime-support.md` | E, G, R, C | Support terminology, runtime/platform/backend matrix, AOT | Canonical current support authority |
| `docs/runtime-svg.md` | G, A, R | Runtime SVG setup, security, caching, deployment | Current SVG guide; measured tables are dated evidence |
| `docs/runtime-svg-profile-v1.md` | A, R, C | Normative SVG feature and comparison profile | Current compatibility reference |
| `docs/svg-backend-migration.md` | G, R | Selecting explicit Skia or ThorVG packages | Current backend selection guide |
| `docs/svg-backend-rollout.md` | R, C | ThorVG rollout decision and qualification evidence | Dated release evidence |
| `docs/theme-icons.md` | G, A | Default icon policy, customization, regeneration | Current styling guide; manifest owns the icon count |
| `docs/thorvg-build-and-provenance.md` | R, C | ThorVG pin, provenance, ABI, build procedure | Current supply-chain reference |
| `docs/xaml-language.md` | X, A, C | Forma XAML language and tooling contract | Canonical current XAML reference |
| `docs/xaml-templates-migration.md` | X, A, C | Templates, items, and visual-tree migration | Current migration guide |
| `docs/control-template-migration-manifest.md` | A, C | Historical type-by-type template migration | Historical evidence; not the current API inventory |
| `docs/baselines/README.md` | C | Dynamic-text baseline regeneration | Current contributor validation guide |
| `docs/baselines/xaml-template-performance.md` | A, C | Deterministic performance gates and observations | Dated performance evidence |
| `docs/baselines/xaml-templates-items-and-virtualization.md` | A, C | Frozen pre-migration matrix | Historical baseline at its recorded commit |
| `docs/adr/0001-dynamic-text-api.md` | A, R, C | Dynamic-text API and ownership | Accepted architecture decision |
| `docs/adr/0002-dynamic-text-dependencies.md` | R, C | Native dependencies, Unicode baseline, RID matrix | Accepted desktop-spike decision; revalidate before expansion |
| `docs/adr/0003-dynamic-text-security-limits.md` | A, R, C | Dynamic-text work limits | Accepted normative decision |
| `docs/adr/0004-backend-neutral-drawing-and-compositing.md` | A, R, C | Drawing/compositing architecture | Accepted architecture decision |
| `docs/adr/0005-template-first-compatibility-and-lifetime.md` | A, C | Template ownership, compatibility, lifetime | Accepted architecture decision |
| `docs/adr/0006-runtime-svg-architecture.md` | A, R, C | Bounded SVG source/cache/upload architecture | Accepted; package ownership is superseded by ADR 0007 |
| `docs/adr/0007-explicit-svg-backends.md` | A, R, C | Explicit process-wide SVG backend selection | Accepted; measurements remain dated evidence |
| `docs/documentation-inventory.md` | C | Documentation ownership and drift audit | Canonical maintainer inventory |

## Volatile Claims

Do not duplicate the following values as undated prose. Link to or derive from the canonical source.

| Claim | Canonical source | Documentation rule |
| --- | --- | --- |
| Forma and runtime versions | `Directory.Build.props` | Versioned snippets describe a release; general guides link to the property source |
| Public package IDs | `scripts/release-packages.json` | Package matrices may render the manifest but must not own a separate list |
| Theme icon inventory | `assets/theme-icons/imports.json` | Generate counts; retain older counts only in clearly dated evidence |
| Public controls and members | Docfx metadata from release assemblies | Do not maintain prose totals |
| Catalog stories | `StoryCatalog.Create` plus `CatalogInventoryTest` | Do not maintain prose totals |
| Test totals | Test discovery and CI results | Front-door docs describe suites without fixed counts |
| Current platform/backend support | `docs/runtime-support.md` | Other guides link to this matrix instead of restating support claims |
| Runtime dependency contents | `docs/runtime-acquisition.md` and build properties | Support docs state only Forma's validated subset |
| Package sizes and benchmark values | Release artifacts and benchmark artifacts | Keep only with date, commit, environment, and artifact identity |

Normative limits, ABI versions, profile tolerances, and Unicode baselines are compatibility
contracts rather than volatile measurements. Change them through their owning ADR or profile.

## Public Control Coverage

The 2026-08-07 source scan found 118 public classes rooted at `Control`: 43 have class-level XML
summaries, 75 do not, and 111 have direct reflected Catalog stories. Generated Docfx metadata is the
canonical current type inventory. No curated control-family reference exists yet.

| Coverage | Types |
| --- | --- |
| XML summary and direct story | `AcceptDialog`, `Border`, `CheckButton`, `CodeEdit`, `ColorPicker`, `ColorPickerButton`, `ColorPickerDialog`, `ColorPickerPopupPanel`, `ColorPresetButton`, `Control`, `DynamicGlyphAtlasView`, `FileDialog`, `FoldableContainer`, `GraphEdit`, `GraphEditFilter`, `GraphEditMinimap`, `GraphElement`, `GraphFrame`, `GraphNode`, `Image`, `ItemList`, `LineEdit`, `MenuBar`, `MenuButton`, `NineSliceImage`, `Popup`, `PopupMenu`, `RichTextDocument`, `RichTextLabel`, `SplitContainerDragger`, `SplitContainerMultiDragger`, `SubViewportContainer`, `TabBar`, `TemplatedControl`, `TextBlock`, `ThemeIconRect`, `ThemeIconView`, `Tree`, `VideoStreamPlayer`, `VirtualJoystick` |
| Missing XML summary; direct story | `AspectRatioContainer`, `BaseButton`, `BoxContainer`, `Button`, `CanvasPanel`, `CenterContainer`, `CheckBox`, `ColorRect`, `ConfirmationDialog`, `Container`, `ContentControl`, `ContentPresenter`, `DataGrid`, `DataGridCell`, `DataGridColumnHeader`, `DataGridRow`, `EllipseShape`, `FlexPanel`, `FlowContainer`, `GridContainer`, `GridPanel`, `HBoxContainer`, `HFlowContainer`, `HScrollBar`, `HSeparator`, `HSlider`, `HSplitContainer`, `ItemsControl`, `ItemsPresenter`, `Label`, `LineEditPresenter`, `LineShape`, `LinkButton`, `ListBox`, `ListBoxItem`, `MarginContainer`, `NinePatchRect`, `OptionButton`, `OverlayPanel`, `Panel`, `PanelContainer`, `PathShape`, `PolygonShape`, `PolylineShape`, `PopupPanel`, `ProgressBar`, `RectangleShape`, `ReferenceRect`, `ScrollBar`, `ScrollContainer`, `ScrollPresenter`, `Slider`, `SpinBox`, `SplitContainer`, `StackPanel`, `TabContainer`, `TextEdit`, `TextureButton`, `TextureProgressBar`, `TextureRect`, `TreePresenter`, `VBoxContainer`, `VFlowContainer`, `VScrollBar`, `VSeparator`, `VSlider`, `VSplitContainer`, `Viewbox`, `VirtualizingGridPanel`, `VirtualizingStackPanel`, `WrapPanel` |
| Summary; feature or owner story | `DrawingElement` through the Runtime SVG feature story; `PopupMenuItems` through `PopupMenu`; `SpinBoxLineEdit` through `SpinBox` |
| Missing summary; descendant stories only | Abstract `Range`, `Separator`, `Shape`, `VirtualizingPanel` |

The next reference pass should add useful summaries to the 75 gaps and create curated family pages
for text input, buttons, selection, containers, collections, dialogs, data display, graph/code
controls, and media. Catalog coverage remains enforced by `CatalogInventoryTest`; owner-only and
abstract controls do not require synthetic standalone stories.

## Audit Commands

```sh
# Source-facing Markdown pages
rg --files -g 'README.md' -g 'docs/**/*.md' -g '!Artifacts/**' -g '!docs/api/**'

# Drift-prone numeric and release claims
rg -n '\b[0-9]+\b.*\b(test|tests|icon|icons|package|packages|platform|RID|control|story)\b' \
  README.md RELEASE_NOTES.md docs

# Machine-owned icon inventory
jq '.Icons | length' assets/theme-icons/imports.json

# Version and runtime pin authority
rg -n 'FormaVersion|MonoGameVersion|FnaVersion|FnaNativeAssetsVersion' Directory.Build.props
```
