# XAML Templates, Items, and Virtualization Baseline

This record freezes the pre-migration evidence for
`plans/xaml-templates-items-and-virtualization-plan.md`. Update an outcome only by rerunning its
command from the repository root. Generated logs and measurements belong under
`Artifacts/xaml-templates-baseline/`; committed references remain in their existing locations.

## Environment

| Field | Baseline |
| --- | --- |
| Commit | `cb9c8c4` |
| Date | 2026-08-04 |
| Host | macOS 26.5.2 (`25F84`) arm64 |
| .NET SDK | 10.0.103 |
| Runtime peers | MonoGame 3.8.5 DesktopGL and FNA.NET 2.2.11.2602 Metal |
| FNA native assets | 2.1.2.2602 |
| Configuration coverage | Debug and Release |

## Executable Baseline Matrix

| Evidence | MonoGame command | FNA command | Outcome |
| --- | --- | --- | --- |
| Unit and Catalog inventory | `make test-unit-monogame` | `make test-unit-fna` | Pass: 551/551 tests per peer |
| XAML runtime, compiler, tooling, and Signal Run behavior/hot reload | `make test-xaml-monogame` | `make test-xaml-fna` | Pass in Debug and Release: 49 XAML, 6 tooling, and 3 Signal Run tests per peer; Release skips the intentionally Debug-only reload case; build fixtures pass |
| Complete Debug application graph | `make build-monogame` | `make build-fna` | Pass: Catalog and Signal Run hosts build for both peers |
| Complete Release application graph | `CONFIGURATION=Release make build-monogame` | `CONFIGURATION=Release make build-fna` | Pass: Catalog and Signal Run hosts build for both peers without hot-reload source metadata |
| Catalog smoke and Signal Run continuity | `make smoke-monogame` | `make smoke-fna` | Pass: 92 stories, 3 frames, 2x density font, 720x450 logical viewport; 3/3 Signal Run tests per peer |
| Catalog render parity | `make render-parity` | `make render-parity` | Pass: exact 1440x900 hash `d99fb205704d47f0`, within 1% coverage/color tolerance |
| Packed consumers | `make packages` | `make packages` | Pass: eight peer packages, 11 native-free core publishes, nine DynamicText publishes, compiled XAML consumers, and mixed-variant rejection |
| Trim and NativeAOT consumers | `NATIVEAOT_RUNTIME=MonoGame make nativeaot` | `NATIVEAOT_RUNTIME=FNA make nativeaot` | Pass: core, media, SpriteFont, and DynamicText trimmed/AOT cells; only classified upstream `IL2104` summaries from MonoGame/FNA |

The XAML game tests are the authoritative Signal Run baseline. They must retain collect, pause,
low-time, result, restart, settings, typed binding, storyboard, invalid-XAML rollback, and
state-preserving Debug hot-reload behavior. Release builds must continue to exclude hot reload.

## Catalog Measurements

The canonical pre-migration smoke reference is
`samples/Forma.Catalog/catalog-metrics-baseline.json`. The canonical 1x and 2x screenshots and
startup/allocation measurements are `docs/baselines/dynamic-text-before-1x.png`,
`docs/baselines/dynamic-text-before-1x.json`, `docs/baselines/dynamic-text-before-2x.png`, and
`docs/baselines/dynamic-text-before-2x.json`. Capture fresh peer-specific measurements before the
first rendering change with:

```sh
bash scripts/capture-xaml-templates-baseline.sh
```

The capture must preserve the Catalog story inventory and report startup milliseconds,
steady-state allocated bytes, logical/physical viewport, display scale, runtime/backend identity,
render diagnostics, and screenshots for both peers. The current runtime has no item generator, so
the pre-migration realized-control baseline is **not applicable**; Phase 3 introduces the first
realization counter, and Phase 4 freezes its visible-plus-overscan bound before virtualization is
accepted.

The 2026-08-04 capture produced these 120-frame values at a 1440x900 physical viewport:

| Peer | Scale | Stories | Startup ms | Allocated bytes | Bytes/frame | Texture bytes | Pixel hash | Non-background pixels |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| MonoGame DesktopGL | 1x | 92 | 906.9932 | 176,548,352 | 1,483,599.60 | 414,720 | `54536ed5b1bdc81e` | 303,509 |
| MonoGame DesktopGL | 2x | 92 | 657.4752 | 173,396,472 | 1,457,113.21 | 1,238,016 | `8043477dbb6f8a55` | 258,327 |
| FNA Metal | 1x | 92 | 466.4611 | 180,283,216 | 1,514,985.01 | 414,720 | `125256962b9d4579` | 303,509 |
| FNA Metal | 2x | 92 | 379.6665 | 175,258,472 | 1,472,760.27 | 1,238,016 | `f51e4e06591af2ac` | 258,315 |

Startup and allocation values are host observations, not deterministic CI thresholds. Story count,
viewport, texture accounting, nonempty screenshots, and render-report structure are enforced by the
capture script. Cross-peer deterministic parity remains owned by `make render-parity`.

## First-Party Continuity Matrix

Every phase has one Catalog row and one Signal Run row. A row is complete only when all affected
call sites use that phase's final contract and the listed focused evidence passes on MonoGame and
FNA in Debug and Release.

| Phase | Application | Required continuity | Focused evidence | Status |
| --- | --- | --- | --- | --- |
| 0 | Catalog | Preserve inventory, search, property editors, DynamicText/media opt-ins, metrics, screenshots, and reload workflows while baselines and the migration manifest are frozen. | Unit inventory, smoke, render parity, Debug/Release builds | Complete |
| 0 | Signal Run | Preserve all game rules, HUD/settings/result behavior, and state-preserving Debug XAML reload; remain core-only in Release. | XAML game tests and Debug/Release builds | Complete |
| 1 | Catalog | Migrate affected stories to foundational visuals, panels, presenters, tree ownership, and template lifetime without losing authoring or diagnostics. | Focused unit/render tests, smoke, render parity, builds | Complete |
| 1 | Signal Run | Migrate affected views to foundational visuals and presenters while preserving gameplay and reload state. | XAML game tests, smoke, builds | Complete |
| 2 | Catalog | Compile all affected views through the shared typed IR, scoped templates, relative sources, selectors, and adaptive conditions. | Compiler/tool tests, smoke, render parity, builds | Complete |
| 2 | Signal Run | Compile existing views and row handlers through the shared typed IR with unchanged runtime behavior and Debug reload. | XAML and XAML game tests, builds | Complete |
| 3 | Catalog | Migrate repeated inventory/property-editor stories to explicit item templates and incremental collection updates. | Items tests, inventory tests, smoke, builds | Complete |
| 3 | Signal Run | Migrate repeated content only where present; otherwise prove the existing views remain unaffected and core-only. | XAML game tests and builds | Complete |
| 4 | Catalog | Add bounded virtualized list/grid stories and preserve scroll, focus, and metrics workflows. | Virtualization tests, smoke, render parity, builds | Complete |
| 4 | Signal Run | Preserve scroll/focus behavior in affected views without adding optional dependencies. | XAML game tests and builds | Complete |
| 5 | Catalog | Add selection stories covering single, multi, toggle, duplicates, keyboard, pointer, and recycled state. | Selection tests, smoke, render parity, builds | Complete |
| 5 | Signal Run | Preserve keyboard/pointer navigation and game input while selection infrastructure changes. | XAML game tests and builds | Complete |
| 6 | Catalog | Move every affected semantic widget story to the final packaged/default template contract with no legacy chrome path. | Per-manifest parity, smoke, render parity, builds | Complete |
| 6 | Signal Run | Move HUD/settings/result widgets to final templates without changing game rules, settings, focus, or accessibility behavior. | XAML game tests, smoke, builds | Complete |
| 7 | Catalog | Preserve XAML/effect reload, diagnostics, schema, completion, navigation, and rename across template artifacts. | Hot-reload, CLI/LSP, smoke, builds | Complete |
| 7 | Signal Run | Preserve valid-edit replacement, invalid-edit rollback, and active view-model/score/timer/settings state. | XAML game hot-reload tests and builds | Complete |
| 8 | Catalog | Ship the complete capability showcase through the existing Catalog shell on both peers. | Full gate matrix, package consumers, NativeAOT profiles | Complete |
| 8 | Signal Run | Ship the real game on final controls as the core-only Release sample on both peers. | Full gate matrix, package inspection, NativeAOT profiles | Complete |

## Acceptance

The baseline/tracking task is complete when every executable matrix outcome is recorded, fresh
Catalog captures exist for both peers, and all 18 application continuity rows identify their owner
and focused evidence. The Phase 0 dashboard was held open until the renderer gate, migration
manifest, compatibility contract, and both Phase 0 application continuity rows were complete. Later
phases update only their own continuity rows and retain the earlier evidence.