# NativeAOT and Console Readiness Implementation Plan

## Objective

Make Forma safe to consume from trimmed and ahead-of-time compiled games without relying on runtime
code generation, accidental reflection preservation, dynamic assembly discovery, or desktop-only
native-library loading behavior. Prove that the MonoGame and FNA package variants preserve the same
public API and behavior when published with .NET NativeAOT on every public target that declares AOT
support.

Use that work as a portability foundation for licensed console integrations. NativeAOT compatibility
is necessary engineering preparation, but it is not a claim that public .NET NativeAOT can target a
particular console. Console support additionally requires the platform holder's SDK, an authorized
MonoGame or FNA platform port, approved native binaries, packaging rules, and validation performed
inside the applicable NDA environment.

This plan owns trimming contracts, AOT analyzers, reflection removal, generated metadata, native
interop packaging, AOT consumer applications, restricted-platform probes, and the public/private
console integration boundary. The [MonoGame and FNA compatibility plan](monogame-fna-compatibility-plan.md)
continues to own peer runtime packaging and API parity. The
[dynamic text rendering plan](dynamic-text-rendering-plan.md) owns font behavior and rendering, while
this plan owns proving that its FreeType and HarfBuzz path survives AOT and restricted deployment.

## Guiding Principles

- **AOT compatibility is a tested contract:** a successful compiler invocation is insufficient;
  the published executable must start and exercise the relevant behavior.
- **Warnings are defects:** supported assemblies must produce no Forma-owned `IL2xxx` trimming or
  `IL3xxx` AOT warnings. Blanket suppressions do not constitute support.
- **Generated metadata over discovery:** use source-generated JSON, explicit registries, generic
  enum APIs, and compile-time adapters instead of runtime scanning or activation.
- **No accidental rooting:** do not preserve an entire assembly merely to hide missing trimming
  annotations. Root only narrowly documented third-party surfaces when no upstream contract exists.
- **Peer runtime treatment:** MonoGame and FNA must pass equivalent core tests and package-consumer
  gates. A limitation in one variant is documented rather than silently assigned to the other.
- **Capabilities over reflection:** optional media, filesystem, clipboard, input, and platform
  behavior use explicit runtime adapters or injected contracts.
- **Static-friendly native interop:** native dependency names, callbacks, exports, and ownership are
  known ahead of time and can be adapted to dynamic or static linking per platform.
- **NDA hygiene:** the public repository contains no confidential SDK names, paths, headers,
  binaries, logs, or platform-holder requirements. Private validation reports only non-confidential
  pass/fail capability summaries back to public documentation.
- **No premature console claim:** Forma is console-ready only after an authorized target build,
  startup, rendering, input, suspend/resume, and resource-lifetime gate passes on hardware or an
  approved equivalent environment.

## Decision Summary

- Target warning-free trimming and NativeAOT for `Forma` first, then `Forma.Media` where the selected
  runtime exposes a statically callable media capability.
- Keep catalog reflection and diagnostics out of the production compatibility contract. The catalog
  may gain generated registries so it can serve as an AOT smoke host, but catalog discovery is not a
  required Forma package feature.
- Add `<IsAotCompatible>true</IsAotCompatible>` only after an assembly is analyzer-clean and its
  dedicated published consumer passes. Until then, enable analyzers explicitly in validation
  projects without advertising package compatibility.
- Treat `PublishTrimmed`, `PublishAot`, and restricted native linking as separate gates. Passing one
  does not imply the others.
- Use source-generated `System.Text.Json` metadata for embedded manifests and diagnostics that are
  part of supported AOT paths.
- Replace runtime-dependent optional API probing with runtime-specific adapters or compile-time
  feature symbols. Do not use reflection to discover methods on framework types.
- Keep FreeType and HarfBuzz behind internal ownership and loading contracts. Desktop packages may
  use RID native assets; restricted platforms may supply approved static or platform-packaged builds.
- Do not expose native pointers, handles, loaders, or platform-specific font types in the public API.
- Establish four support levels:
  1. **Analyzer-clean:** no owned trim/AOT diagnostics.
  2. **Published:** the target produces a self-contained artifact.
  3. **Executed:** the artifact passes bounded startup and feature smoke tests.
  4. **Platform-validated:** lifecycle and rendering gates pass on the declared target.

## Progress Dashboard

- [ ] Phase 0: Contracts, Inventory, and Reproducible Baseline
- [ ] Phase 1: Forma Core Trimming and AOT Cleanup
- [ ] Phase 2: Optional Media and Platform Capability Boundaries
- [ ] Phase 3: Native Dependency and Static-Link Readiness
- [ ] Phase 4: Peer Runtime NativeAOT Consumers and CI
- [ ] Phase 5: Restricted-Platform and Lifecycle Proxies
- [ ] Phase 6: Authorized Console Integration Contract
- [ ] Phase 7: Support Matrix, Packaging, and Release Gates

Check a phase only after every task and exit criterion in that phase is complete.

### Progress Tracking Workflow

Run the shared tracker at the start and end of each implementation session:

```sh
bash scripts/track-plan.sh docs/nativeaot-console-readiness-plan.md
```

Update task boxes only after implementation and focused validation pass. Add discovered work here
rather than relying on private notes. Public tasks must remain NDA-neutral.

## Success Criteria

- [x] `Forma.MonoGame` and `Forma.FNA` build with trimming and AOT analyzers enabled and produce no
  Forma-owned warnings.
- [ ] Supported `Forma.Media` variants build with the same warning policy, or expose a documented
  compile-time unavailable capability without reflection.
- [ ] Package metadata advertises AOT compatibility only for assemblies and runtime pairs that pass
  published-consumer execution gates.
- [ ] No supported runtime path depends on `Assembly.GetTypes`, string-based type activation,
  runtime generic construction, expression compilation, or unbounded reflection discovery.
- [x] Embedded JSON manifests and supported serialization paths use source-generated metadata.
- [ ] Reflection retained for unavoidable compatibility has narrow annotations, focused tests, and a
  documented reason that is reviewed before every support claim.
- [x] MonoGame and FNA NativeAOT consumers load Forma from packed artifacts rather than source
  references or a pre-populated NuGet cache.
- [ ] NativeAOT consumers exercise retained layout, input, theme icons, dynamic font loading,
  shaping, fallback, glyph rasterization, atlas upload, drawing, and disposal.
- [ ] Dynamic text AOT tests use non-ASCII shaping and fallback, not only ASCII measurement.
- [ ] FreeType and HarfBuzz native assets are reproducibly selected for every declared public RID.
- [ ] Native callbacks and P/Invoke entry points used by supported paths are statically discoverable
  and survive trimming without whole-assembly roots.
- [x] A clean machine can publish and execute each declared AOT consumer using documented commands.
- [ ] Restricted-platform proxies validate no-dynamic-code, sandboxed filesystem, suspend/resume,
  graphics reset, and bounded-memory behavior before private console work begins.
- [x] The public support matrix distinguishes analyzer-clean, published, executed, and
  platform-validated states per runtime and target.
- [ ] An authorized console integration can provide platform services and native libraries without
  changing Forma's public namespace or duplicating shared controls.
- [ ] Console readiness documentation contains no confidential implementation details or implied
  support for platforms that have not passed private gates.

## Non-Goals

- Provide public console SDKs, runtime ports, native libraries, signing tools, or deployment keys.
- Claim that `dotnet publish -p:PublishAot=true` produces PlayStation, Nintendo, or Xbox artifacts.
- Infer console support from desktop NativeAOT, iOS AOT, simulator, or emulator success.
- Make MonoGame or FNA console ports public or bypass their platform-access processes.
- Guarantee that every optional media codec is available on every AOT or console target.
- Preserve arbitrary consumer reflection over Forma types without explicit consumer annotations.
- Root complete assemblies to silence the trimmer.
- Add platform-holder names or confidential target identifiers to public build matrices unless their
  use is publicly documented and authorized.
- Treat AOT as a performance project. Correctness, metadata preservation, native loading, and
  lifecycle behavior are the gates owned here.

## Current Evidence and Gaps

An initial 2026-08-02 `osx-arm64` NativeAOT publish of the source-linked MonoGame catalog completed:

```sh
dotnet publish samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj \
  -p:FormaRuntime=MonoGame \
  -p:PublishAot=true \
  -p:EnableTrimAnalyzer=true \
  -p:EnableAotAnalyzer=true \
  -r osx-arm64 \
  -c Release
```

The publish emitted 30 warnings. Forma core owned three: reflection-based JSON deserialization for
the default icon manifest and non-generic `Enum.GetValues(Type)`. The catalog owned reflection,
activation, and serialization warnings. MonoGame also reported trim warnings. A successful publish
with warnings is evidence that the architecture is viable, not an AOT support gate.

That compile-only probe was superseded by `scripts/test-nativeaot-package-consumer.sh`. On the same
date, its 12 packed-consumer cells passed from empty package caches:

| Runtime | RID | Profiles | Trim/AOT analysis | Published | Executed graphics |
| --- | --- | --- | --- | --- | --- |
| MonoGame 3.8.5 | `osx-arm64` | core, SpriteFont, dynamic text | No Forma-owned warnings | Both modes | OpenGL |
| FNA.NET 2.2.11.2602 | `osx-arm64` | core, SpriteFont, dynamic text | No Forma-owned warnings | Both modes | Metal |

The baseline used .NET SDK 10.0.103, ILCompiler/runtime pack 10.0.3, macOS 26.5.2 arm64,
FreeTypeSharp 3.1.0/FreeType 2.13.2, HarfBuzzSharp 14.2.1.1, and
FNA.NET.NativeAssets 2.1.2.2602. MonoGame graphical cells and FNA trim/AOT cells retain one
classified upstream `IL2104` assembly summary each. All executables launched; graphical cells loaded
packed XNB content, theme icons, and dynamic glyph atlases. Dynamic cells loaded the packaged
FreeType and HarfBuzz modules rather than global libraries. Outputs and logs are written under
`Artifacts/nativeaot/`.

The public executed matrix is intentionally limited to these two macOS arm64 runtime rows. Other
RIDs, `Forma.Media`, restricted public platforms, and authorized consoles remain unvalidated.

Known owned reflection or metadata-sensitive surfaces include:

| Surface | Current behavior | Target behavior |
| --- | --- | --- |
| Default theme icon manifest | Source-generated `JsonSerializerContext` | Complete for core package consumers |
| `FileDialogCustomization` sizing | `Enum.GetValues(Type)` | Generic enum API or compile-time constant |
| Named XNA colors | Reflects static `Color` properties | Generated/static color table with parity test |
| Theme inheritance | Walks runtime base types for names | Retain only if analyzer-clean and behavior-tested; otherwise generated type-name metadata |
| MonoGame video seeking | Reflects optional `VideoPlayer.SetPlayPosition` | Runtime adapter with compile-time capability |
| Catalog story discovery | Scans assemblies and activates controls | Generated story/control registry |
| Catalog JSON diagnostics | Reflection serialization | Source-generated DTO metadata |

## Target Architecture

```text
                 Forma shared managed implementation
       controls / layout / Unicode / rendering / resources
                              |
                 analyzer-clean static contracts
             generated metadata / explicit registries
                              |
              runtime-specific internal adapters
                   /                       \
                  v                         v
          MonoGame package              FNA package
                  |                         |
                  +-----------+-------------+
                              |
                  platform service boundary
       graphics / input / filesystem / lifecycle / native fonts
                     /                    \
                    v                      v
          public AOT consumers     authorized console host
```

The platform service boundary must remain narrow. Shared controls and text layout cannot depend on
console SDK types. Authorized hosts may provide runtime-specific implementations and approved native
font libraries without changing public Forma APIs.

## Phase 0: Contracts, Inventory, and Reproducible Baseline

- [ ] Define which assemblies are intended to become trim-compatible and AOT-compatible: `Forma`,
  each peer package variant, optional `Forma.Media`, and catalog smoke hosts.
- [x] Define the initial public AOT RID matrix separately for MonoGame and FNA.
- [x] Record the .NET SDK, runtime package, architecture, native dependency, and linker versions used
  by every baseline.
- [x] Add reproducible trim-only and NativeAOT publish commands that write under `Artifacts/`.
- [x] Capture all `IL2xxx`, `IL3xxx`, and third-party assembly warnings in a classified baseline.
- [ ] Inventory reflection, dynamic activation, runtime serialization, assembly scanning, native
  callbacks, P/Invoke, and native-library resolution in core, media, catalog, and tests.
- [ ] Classify each finding as remove, generate, annotate, isolate behind a capability, upstream,
  or explicitly unsupported.
- [x] Define the warning ownership rule for Forma, MonoGame/FNA, and third-party native wrappers.
- [x] Define execution evidence required beyond publish success, including exit codes, diagnostics,
  rendered output, and native dependency provenance.
- [x] Document that console validation remains private and target-specific.

### Phase 0 Exit Criteria

- [x] A clean checkout reproduces the warning baseline for every selected public runtime/RID pair.
- [ ] Every owned reflection and native interop surface has a disposition and an owner.
- [x] Public documentation distinguishes NativeAOT compatibility from console support.

## Phase 1: Forma Core Trimming and AOT Cleanup

- [x] Replace default theme icon JSON reflection with source-generated metadata or generated C# data.
- [ ] Replace non-generic enum reflection with generic or compile-time alternatives.
- [ ] Replace named-color reflection with a generated/static table and verify parity with the selected
  XNA runtime variants.
- [ ] Evaluate runtime type-name walking used by theme inheritance under trimming; preserve it with a
  focused contract or replace it with generated metadata.
- [ ] Search public APIs for types that require `DynamicallyAccessedMembers` contracts and add the
  narrowest correct annotations where reflection is intentionally consumer-driven.
- [ ] Remove unsupported runtime activation and dynamic-code paths from core.
- [ ] Enable trim and AOT analyzers for core in normal CI without yet advertising support.
- [ ] Add tests that compare trimmed and untrimmed theme, control, layout, and resource behavior.
- [ ] Set `<IsAotCompatible>true</IsAotCompatible>` for core only after all core gates pass.

### Phase 1 Exit Criteria

- [x] Both runtime variants of `Forma` produce zero owned trim/AOT warnings.
- [x] Core unit and package-consumer behavior is unchanged after reflection removal.
- [x] Core package metadata does not overstate runtime/platform execution coverage.

## Phase 2: Optional Media and Platform Capability Boundaries

- [ ] Replace reflective video seeking discovery with compile-time runtime adapters or an injected
  seeking capability.
- [ ] Define trim/AOT behavior when a media backend or codec is unavailable.
- [ ] Ensure unavailable optional capabilities do not root backend assemblies or fail startup.
- [ ] Audit file dialogs, clipboard, URI launching, text input, and filesystem access for desktop
  assumptions and isolate them behind capability contracts where needed.
- [ ] Add analyzer-clean media consumers for each runtime/backend pair that declares support.
- [ ] Mark unsupported media/runtime pairs explicitly rather than suppressing diagnostics.
- [ ] Set AOT-compatible package metadata for `Forma.Media` variants independently from core.

### Phase 2 Exit Criteria

- [ ] Supported media variants are warning-free and execute their declared capability smoke tests.
- [ ] Unsupported optional features report stable unavailable state without reflection or crashes.
- [ ] Core AOT support does not depend on installing `Forma.Media`.

## Phase 3: Native Dependency and Static-Link Readiness

- [ ] Inventory every FreeType and HarfBuzz native entry point, callback, handle, and library name
  reached by dynamic text.
- [ ] Verify that managed wrappers do not require runtime-generated marshalling or unbounded callback
  registration on supported targets.
- [ ] Add bounded startup diagnostics identifying the selected native library and packaging source
  without exposing native implementation types publicly.
- [x] Test RID-native asset loading from clean packed consumers for every public AOT target.
- [ ] Define an internal native font binding contract that can accept platform-provided dynamic or
  static implementations.
- [ ] Prove a static-link or platform-resolved FreeType/HarfBuzz spike without changing public font
  and layout APIs.
- [ ] Verify license notices and source-offer obligations for every redistributed native build.
- [ ] Test missing, wrong-architecture, rejected, and duplicate native library failures.
- [ ] Test native handle disposal, repeated initialization, graphics reset, and process shutdown.

### Phase 3 Exit Criteria

- [x] Dynamic fonts load, shape, rasterize, upload, draw, and dispose in a published AOT consumer.
- [ ] Native dependency failure is bounded and diagnostic on every declared public target.
- [ ] An authorized platform can replace native library resolution behind an internal adapter.

## Phase 4: Peer Runtime NativeAOT Consumers and CI

- [ ] Add minimal source consumers for fast local analyzer iteration.
- [x] Add isolated packed consumers for final package validation with empty NuGet caches.
- [x] Publish trim-only and NativeAOT consumers separately for MonoGame and FNA.
- [x] Exercise controls, layout, theme resources, input routing, and disposal in bounded headless or
  windowed smoke modes as supported by the target.
- [ ] Exercise Latin, Arabic, Indic, bidi, fallback, and missing-glyph dynamic text through the atlas.
- [ ] Compare retained layout results between JIT and AOT executions from identical font bytes.
- [ ] Compare approved render metrics or images between JIT and AOT without requiring byte-identical
  output across different graphics backends.
- [ ] Fail CI on new owned trim/AOT warnings.
- [ ] Store logs, binaries, native dependency manifests, and render diagnostics as review artifacts.
- [x] Ensure AOT validation does not rely on a developer machine's global native libraries.

### Phase 4 Exit Criteria

- [x] Every declared public runtime/RID pair publishes and executes from packed artifacts.
- [ ] JIT and AOT layout behavior matches for the shared multilingual corpus.
- [ ] CI detects removed metadata, missing native assets, startup failures, and warning regressions.

## Phase 5: Restricted-Platform and Lifecycle Proxies

- [ ] Select public targets that approximate console constraints without claiming equivalence, such
  as iOS device AOT or another sandboxed no-dynamic-code environment supported by the runtime.
- [ ] Validate startup without writable application directories or system font assumptions.
- [ ] Validate suspend, resume, backgrounding, graphics-device loss/reset, and atlas restoration.
- [ ] Validate controller-only navigation, safe-area layout, display-scale changes, and text input
  capability absence.
- [ ] Validate bounded font source, glyph atlas, fallback depth, and allocation limits under AOT.
- [ ] Validate worker-thread shaping and render-thread upload rules on the selected proxy targets.
- [ ] Document which proxy findings are transferable and which still require target hardware.

### Phase 5 Exit Criteria

- [ ] At least one public restricted-platform AOT target passes startup, rendering, input, lifecycle,
  memory, and dynamic-text gates.
- [ ] No support statement describes a proxy as console validation.
- [ ] Remaining private-platform risks have explicit tests defined before integration begins.

## Phase 6: Authorized Console Integration Contract

- [ ] Define an NDA-neutral host checklist covering runtime assembly, graphics, input, filesystem,
  lifecycle, native fonts, threading, diagnostics, packaging, and deployment.
- [ ] Keep platform SDK references and confidential adapters in authorized repositories.
- [ ] Verify the authorized MonoGame or FNA port's trimming/AOT preservation requirements.
- [ ] Supply approved FreeType/HarfBuzz builds or a compatible platform font adapter according to
  platform-holder policy.
- [ ] Validate startup, controller navigation, safe areas, suspend/resume, user switching, graphics
  reset, memory pressure, and clean shutdown.
- [ ] Run multilingual dynamic-text layout and rendering tests on hardware or an approved equivalent.
- [ ] Validate package size, native symbols, forbidden imports, signing, and submission diagnostics.
- [ ] Record only non-confidential capability results in the public support matrix.
- [ ] Require a separate approval for each platform/runtime/backend combination.

### Phase 6 Exit Criteria

- [ ] At least one authorized platform integration passes its private release checklist.
- [ ] No confidential material is present in public source, CI, issues, logs, or artifacts.
- [ ] Public documentation names support only where disclosure and validation are both authorized.

## Phase 7: Support Matrix, Packaging, and Release Gates

- [ ] Add analyzer-clean, published, executed, and platform-validated columns to runtime support docs.
- [ ] Document exact public publish commands, prerequisites, native asset behavior, and limitations.
- [ ] Add package metadata and README claims only for validated runtime/RID pairs.
- [ ] Add release gates that consume packed artifacts from an empty cache and execute AOT smokes.
- [ ] Verify API/reference parity between JIT and AOT package variants; do not create separate public
  AOT namespaces or APIs.
- [ ] Verify trimming does not remove public constructors, embedded resources, themes, controls, or
  runtime adapters required by documented usage.
- [ ] Add migration guidance for consumers that reflect over Forma types.
- [ ] Document crash-dump, symbol, and diagnostic limitations for AOT deployments.
- [ ] Review third-party notices and native redistribution terms before each release.
- [ ] Keep unvalidated and NDA-restricted targets clearly marked as unsupported or privately gated.

### Phase 7 Exit Criteria

- [ ] Release validation fails on warning, publish, startup, native loading, behavior, or documentation
  regressions for every declared AOT target.
- [ ] Package and support claims match the evidence level recorded in the matrix.
- [ ] Console readiness and actual authorized console support remain distinct, reviewable claims.

## Validation Matrix

| Layer | Required evidence |
| --- | --- |
| Managed core | Zero owned trim/AOT warnings; unit parity; public API preserved |
| Resources | Embedded manifests and textures load without reflection loss |
| Dynamic text | Font bytes load; multilingual shaping/fallback; rasterization; atlas upload/draw |
| Native interop | Correct architecture; known imports/callbacks; bounded load failure; disposal |
| Runtime peers | MonoGame and FNA packed consumers publish and execute independently |
| Graphics | Startup, render, reset, and shutdown on each declared backend/target |
| Platform services | Input, filesystem, clipboard/text input capability, lifecycle, safe areas |
| Distribution | Empty-cache restore; self-contained artifact; notices; deterministic manifest |
| Authorized console | Private SDK build, deployment, hardware smoke, policy and submission checks |

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| A successful publish is mistaken for runtime support | Require executed feature smokes and record evidence levels separately. |
| Linker warnings are hidden by broad roots or suppressions | Fail owned warnings and review every descriptor or suppression narrowly. |
| Reflection removal changes catalog or theme behavior | Generate registries/tables and compare against untrimmed inventory tests. |
| Native wrappers work on desktop but fail under static linking | Keep an internal binding boundary and prove a static/platform resolver spike early. |
| Dynamic text silently falls back after native load failure | Require non-ASCII shaping, glyph IDs, atlas coverage, and explicit diagnostics. |
| Optional media blocks core AOT adoption | Keep media independently packaged and capability-driven. |
| Third-party runtime warnings remain outside Forma's control | Track upstream versions and isolate accepted warnings from zero-warning owned code. |
| Console SDK assumptions leak into shared APIs | Keep platform services internal/injected and review public API parity. |
| Public proxy testing is presented as console validation | Use explicit evidence levels and require authorized hardware gates. |
| Confidential data leaks through CI or logs | Keep private adapters and artifacts in authorized infrastructure; publish only approved summaries. |
| Native dependency redistribution violates platform policy | Review licenses and platform-holder requirements before packaging each target. |
| AOT increases iteration time substantially | Separate fast analyzer/trim jobs from scheduled or release NativeAOT execution matrices. |

## Definition of Done

This plan is complete when Forma's declared core and optional media packages are warning-free under
their advertised trimming/AOT contracts, packed consumers publish and execute across the public
support matrix, dynamic text and native dependencies pass restricted-platform gates, and at least one
authorized console integration has passed its private checklist. Public documentation must continue
to distinguish general AOT compatibility, console readiness, and validated support for a specific
authorized platform.