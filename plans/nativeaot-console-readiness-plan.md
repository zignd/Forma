# NativeAOT and Console Readiness Implementation Plan

## Objective

Make Forma safe to consume from trimmed and ahead-of-time compiled games without relying on runtime
code generation, accidental reflection preservation, dynamic assembly discovery, or desktop-only
native-library loading behavior. Prove that the MonoGame and FNA package variants preserve the same
public API and behavior when published with .NET NativeAOT on every public target that declares AOT
support.

Treat compiled Forma XAML as part of that production contract. XAML source, XamlX, Mono.Cecil,
MSBuild tasks, the Forma compiler, and Debug hot reload run on the build host before trimming and AOT;
the target game contains only Forma runtime code and injected IL. Console readiness therefore
requires proving both sides of the boundary: deterministic build-host compilation and a target
artifact with no compiler, source-XAML, dynamic-code, or reflection-binding dependency.

Use that work as a portability foundation for licensed console integrations. NativeAOT compatibility
is necessary engineering preparation, but it is not a claim that public .NET NativeAOT can target a
particular console. Console support additionally requires the platform holder's SDK, an authorized
MonoGame or FNA platform port, approved native binaries, packaging rules, and validation performed
inside the applicable NDA environment.

This plan owns trimming contracts, AOT analyzers, reflection removal, generated metadata, compiled
XAML target artifacts, native interop packaging, AOT consumer applications, restricted-platform
probes, and the public/private console integration boundary. The
[MonoGame and FNA compatibility plan](monogame-fna-compatibility-plan.md) continues to own peer
runtime packaging and API parity. The
[dynamic text rendering plan](dynamic-text-rendering-plan.md) owns font behavior and rendering, while
this plan owns proving that its FreeType and HarfBuzz path survives AOT and restricted deployment.

## Guiding Principles

- **AOT compatibility is a tested contract:** a successful compiler invocation is insufficient;
  the published executable must start and exercise the relevant behavior.
- **Warnings are defects:** supported assemblies must produce no Forma-owned `IL2xxx` trimming or
  `IL3xxx` AOT warnings. Blanket suppressions do not constitute support.
- **Generated metadata over discovery:** use source-generated JSON, explicit registries, generic
  enum APIs, and compile-time adapters instead of runtime scanning or activation.
- **Build-time compilers stay off the target:** XamlX, Mono.Cecil, MSBuild APIs, Forma compiler
  assemblies, source XAML, and file watchers may exist in private build assets, but never in a
  trimmed/AOT game artifact. Cecil injection must complete before the platform linker or AOT tool.
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
- Ship `Forma.Xaml.Build.<Runtime>` as a private build package. Pin and audit the maintained XamlX
  fork and Mono.Cecil version, compile `x:DataType` bindings to IL, and reject runtime or publish
  output containing source XAML, XamlX, Cecil, Forma compiler/build assemblies, or hot reload.
- Keep Debug hot reload explicitly outside trimmed, NativeAOT, and console target artifacts. It may
  use dynamic compilation on the development host, but production behavior cannot depend on it.
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

- [x] Phase 0: Contracts, Inventory, and Reproducible Baseline
- [x] Phase 1: Forma Core Trimming and AOT Cleanup
- [x] Phase 2: Compiled XAML and Build-Time Dependency Boundary
- [x] Phase 3: Optional Media and Platform Capability Boundaries
- [ ] Phase 4: Native Dependency and Static-Link Readiness
- [x] Phase 5: Peer Runtime NativeAOT Consumers and CI
- [ ] Phase 6: Restricted-Platform and Lifecycle Proxies
- [ ] Phase 7: Authorized Console Integration Contract
- [x] Phase 8: Support Matrix, Packaging, and Release Gates

Check a phase only after every task and exit criterion in that phase is complete.

### Progress Tracking Workflow

Run the shared tracker at the start and end of each implementation session:

```sh
bash scripts/track-plan.sh plans/nativeaot-console-readiness-plan.md
```

Update task boxes only after implementation and focused validation pass. Add discovered work here
rather than relying on private notes. Public tasks must remain NDA-neutral.

### Local and CI Validation

Run the complete currently declared macOS arm64 matrix locally with:

```sh
make nativeaot
```

For faster iteration, select any combination of runtime, profile, and publish mode:

```sh
make nativeaot NATIVEAOT_RUNTIME=MonoGame NATIVEAOT_PROFILE=core NATIVEAOT_MODE=aot
make nativeaot NATIVEAOT_RUNTIME=FNA NATIVEAOT_PROFILE=dynamic NATIVEAOT_MODE=trimmed
make aot-analyzers
make native-font-failures
```

Valid runtimes are `MonoGame` and `FNA`; profiles are `core`, `media`, `spritefont`, and `dynamic`; modes are
`trimmed` and `aot`. Omit any selector to run all values for that dimension. `make check-all` includes
the full NativeAOT matrix along with the broader repository gates.

The `nativeaot` CI job runs `make nativeaot` on GitHub's macOS arm64 runner for every push and pull
request. It fails on publish or execution failure, new unclassified linker/AOT diagnostics,
unexpected dynamic-text dependencies in native-free profiles, native import regressions, or XAML
development artifacts in target output. Per-cell publish logs, binaries, native manifests, and
render diagnostics are uploaded even when the job fails.

## Success Criteria

- [x] `Forma.MonoGame` and `Forma.FNA` build with trimming and AOT analyzers enabled and produce no
  Forma-owned warnings.
- [x] Supported `Forma.Media` variants build with the same warning policy, or expose a documented
  compile-time unavailable capability without reflection.
- [x] Package metadata advertises AOT compatibility only for assemblies and runtime pairs that pass
  published-consumer execution gates.
- [x] No supported runtime path depends on `Assembly.GetTypes`, string-based type activation,
  runtime generic construction, expression compilation, or unbounded reflection discovery.
- [x] Embedded JSON manifests and supported serialization paths use source-generated metadata.
- [x] Packed MonoGame and FNA consumers compile and execute a Forma XAML view with namescope lookup
  and typed one-way/two-way bindings in both trim-only and NativeAOT modes.
- [x] Release and NativeAOT outputs contain no source XAML, XamlX, Mono.Cecil, `Forma.Xaml.Build`,
  `Forma.Xaml.Compiler`, or `Forma.Xaml.HotReload` artifacts.
- [x] The XamlX fork revision, local delta, license, and Mono.Cecil compatibility update are pinned,
  documented, and covered by focused compiler/build regression tests.
- [x] NativeAOT consumers exercise every production XAML feature required by a real game UI,
  including resources, styles, triggers, storyboards, events, and generated binding disposal.
- [x] Reflection retained for unavoidable compatibility has narrow annotations, focused tests, and a
  documented reason that is reviewed before every support claim.
- [x] MonoGame and FNA NativeAOT consumers load Forma from packed artifacts rather than source
  references or a pre-populated NuGet cache.
- [x] NativeAOT consumers exercise retained layout, input, theme icons, dynamic font loading,
  shaping, fallback, glyph rasterization, atlas upload, drawing, and disposal.
- [x] Dynamic text AOT tests use non-ASCII shaping and fallback, not only ASCII measurement.
- [x] FreeType and HarfBuzz native assets are reproducibly selected for every declared public RID.
- [x] Native callbacks and P/Invoke entry points used by supported paths are statically discoverable
  and survive trimming without whole-assembly roots.
- [x] A clean machine can publish and execute each declared AOT consumer using documented commands.
- [ ] Restricted-platform proxies validate no-dynamic-code, sandboxed filesystem, suspend/resume,
  graphics reset, and bounded-memory behavior before private console work begins.
- [x] The public support matrix distinguishes analyzer-clean, published, executed, and
  platform-validated states per runtime and target.
- [ ] An authorized console integration can provide platform services and native libraries without
  changing Forma's public namespace or duplicating shared controls.
- [x] Console readiness documentation contains no confidential implementation details or implied
  support for platforms that have not passed private gates.

## Non-Goals

- Provide public console SDKs, runtime ports, native libraries, signing tools, or deployment keys.
- Claim that `dotnet publish -p:PublishAot=true` produces PlayStation, Nintendo, or Xbox artifacts.
- Infer console support from desktop NativeAOT, iOS AOT, simulator, or emulator success.
- Make MonoGame or FNA console ports public or bypass their platform-access processes.
- Guarantee that every optional media codec is available on every AOT or console target.
- Preserve arbitrary consumer reflection over Forma types without explicit consumer annotations.
- Root complete assemblies to silence the trimmer.
- Make XamlX, Mono.Cecil, MSBuild, source XAML, or the Forma compiler runtime dependencies of a game.
- Rewrite assemblies after the platform AOT compiler, linker, signer, or package finalization step.
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

That compile-only probe was superseded by `scripts/test-nativeaot-package-consumer.sh`. Its 16
packed-consumer cells now pass from empty package caches. The gate was extended on
2026-08-03 so every cell restores `Forma.Xaml.Build.<Runtime>` privately, compiles a package-owned
XAML view into the consumer assembly, executes namescope lookup and typed one-way/two-way binding,
and rejects all XAML development artifacts from publish output:

| Runtime | RID | Profiles | Compiled XAML | Trim/AOT analysis | Published | Executed graphics |
| --- | --- | --- | --- | --- | --- | --- |
| MonoGame 3.8.5 | `osx-arm64` | core, media, SpriteFont, dynamic text | View, namescope, typed one/two-way binding | No Forma-owned warnings | Both modes | OpenGL |
| FNA.NET 2.2.11.2602 | `osx-arm64` | core, media, SpriteFont, dynamic text | View, namescope, typed one/two-way binding | No Forma-owned warnings | Both modes | Metal |

The baseline used .NET SDK 10.0.103, ILCompiler/runtime pack 10.0.3, macOS 26.5.2 arm64,
FreeTypeSharp 3.1.0/FreeType 2.13.2, HarfBuzzSharp 14.2.1.1, and
FNA.NET.NativeAssets 2.1.2.2602. MonoGame graphical cells and FNA trim/AOT cells retain one
classified upstream `IL2104` assembly summary each. All executables launched; graphical cells loaded
packed XNB content, theme icons, and dynamic glyph atlases. Dynamic cells loaded the packaged
FreeType and HarfBuzz modules rather than global libraries. Trimmed JIT and AOT dynamic cells
produced matching multilingual layout, glyph, pixel, and atlas diagnostics for each peer. Media
cells exercised stable compile-time capabilities without requiring a codec. Outputs, logs, native
manifests, binaries, and render diagnostics are written under `Artifacts/nativeaot/`.

The build-time XAML path uses the maintained XamlX fork pinned at
`0337e9b2f6450ac90cb988a3fac61f36f58c4fcc`, XamlX IL generation, and Mono.Cecil 0.11.6. Those
assemblies are intentionally packaged under `tools/net10.0` in `Forma.Xaml.Build.<Runtime>` and are
not runtime package dependencies. Deterministic package checks, incremental/PDB build fixtures, a
compiler-free NativeAOT spike, and packed-consumer output inspection enforce this separation. Debug
hot reload remains development-only and is intentionally absent from Release and NativeAOT output.

The public executed matrix is intentionally limited to these two macOS arm64 runtime rows. Other
RIDs, media codec playback, restricted public platforms, and authorized consoles remain unvalidated.

Known owned reflection or metadata-sensitive surfaces include:

| Surface | Current behavior | Target behavior |
| --- | --- | --- |
| Default theme icon manifest | Source-generated `JsonSerializerContext` | Complete for core package consumers |
| `FileDialogCustomization` sizing | Compile-time final-enum bound | Reflection removed; dual-runtime dialog behavior retained |
| Named XNA colors | Explicit common MonoGame/FNA table | Reflection removed; test-side runtime inventory verifies parity |
| `XamlValueConverter` enum conversion | Consumer-supplied `Type` with `Enum.Parse` | Retained with `PublicFields` metadata annotation and analyzer enforcement |
| Theme inheritance | Walks instantiated runtime base types for names | Retained; analyzer-clean and executed from packed trim/AOT consumers |
| XAML type selectors | Walks instantiated runtime base types by name | Retained; analyzer-clean and executed from packed trim/AOT consumers |
| Compiled XAML views and typed bindings | Injected into the application assembly before trim/AOT | Simple packed consumer complete; expand to all production language features |
| XamlX and Mono.Cecil | Private build-package tools pinned to audited revisions | Keep off target; validate build-host/toolchain compatibility and license provenance |
| Debug XAML hot reload | Uses runtime compilation and file watching | Development-only; explicitly unavailable in trim/AOT and console artifacts |
| MonoGame video seeking | Compile-time runtime adapter reports the pinned package capability | No reflection; seeking remains unsupported until a package API is validated |
| Dynamic text native interop | Wrapper handles, callbacks, and one owned variation P/Invoke | Inventory and isolate behind a replaceable native-font binding contract |
| File dialog and project font files | Injected filesystem capability; desktop provider is optional | Complete; byte/stream font APIs remain portable |
| URI launching | Optional host callback and request event | Complete; unavailable hosts do not invoke desktop shell APIs |
| Catalog story discovery | Scans assemblies and activates controls | Generated story/control registry |
| Catalog JSON diagnostics | Reflection serialization | Source-generated DTO metadata |

The inventory found no assembly loading, runtime generic construction, expression compilation,
dynamic methods, function pointers, `NativeLibrary` resolution, or reflection-based activation in
the core runtime. SRE and expression compilation are confined to the build-host compiler and Debug
hot reload. Catalog discovery and diagnostics are outside the production package contract. Each
remaining runtime surface is owned by its corresponding phase below; tests and scripts may use
reflection or platform inspection to verify the target artifact without making those mechanisms
runtime dependencies.

## Target Architecture

```text
Build host
  XAML source
  |
  v
  Forma.Xaml.Build + Forma.Xaml.Compiler
  pinned XamlX fork + Mono.Cecil + MSBuild APIs
  |
  | inject IL before trim/AOT
  v
Application assembly (no source XAML or compiler dependencies)
  |
  v
Forma shared managed runtime
controls / compiled bindings / layout / Unicode / rendering / resources
  |
analyzer-clean static contracts / generated metadata / explicit registries
  |
runtime-specific internal adapters
  +---------------------------+
  v                           v
MonoGame package              FNA package
  |                           |
  +-------------+-------------+
        v
platform service boundary
graphics / input / filesystem / lifecycle / native fonts
  |                           |
  v                           v
public AOT consumers     authorized console host and toolchain
```

The platform service boundary must remain narrow. Shared controls and text layout cannot depend on
console SDK types. Authorized hosts may provide runtime-specific implementations and approved native
font libraries without changing public Forma APIs.

## Phase 0: Contracts, Inventory, and Reproducible Baseline

- [x] Define which target assemblies are intended to become trim-compatible and AOT-compatible:
  `Forma`, each peer package variant, optional `Forma.Media`, compiled consumer views, and bounded
  smoke hosts. Compiler/build/hot-reload assemblies are build-host tools, not target assemblies.
- [x] Define the initial public AOT RID matrix separately for MonoGame and FNA.
- [x] Record the .NET SDK, runtime package, architecture, native dependency, and linker versions used
  by every baseline.
- [x] Add reproducible trim-only and NativeAOT publish commands that write under `Artifacts/`.
- [x] Capture all `IL2xxx`, `IL3xxx`, and third-party assembly warnings in a classified baseline.
- [x] Inventory reflection, dynamic activation, runtime serialization, assembly scanning, native
  callbacks, P/Invoke, and native-library resolution in core, media, catalog, and tests.
- [x] Classify each finding as remove, generate, annotate, isolate behind a capability, upstream,
  or explicitly unsupported.
- [x] Define the warning ownership rule for Forma, MonoGame/FNA, and third-party native wrappers.
- [x] Define execution evidence required beyond publish success, including exit codes, diagnostics,
  rendered output, and native dependency provenance.
- [x] Document that console validation remains private and target-specific.

### Phase 0 Exit Criteria

- [x] A clean checkout reproduces the warning baseline for every selected public runtime/RID pair.
- [x] Every owned reflection and native interop surface has a disposition and an owner.
- [x] Public documentation distinguishes NativeAOT compatibility from console support.

## Phase 1: Forma Core Trimming and AOT Cleanup

- [x] Replace default theme icon JSON reflection with source-generated metadata or generated C# data.
- [x] Replace non-generic enum reflection with generic or compile-time alternatives.
- [x] Replace named-color reflection with a generated/static table and verify parity with the selected
  XNA runtime variants.
- [x] Evaluate runtime type-name walking used by theme inheritance under trimming; preserve it with a
  focused contract or replace it with generated metadata.
- [x] Search public APIs for types that require `DynamicallyAccessedMembers` contracts and add the
  narrowest correct annotations where reflection is intentionally consumer-driven.
- [x] Remove unsupported runtime activation and dynamic-code paths from core.
- [x] Enable trim and AOT analyzers for core in normal CI without yet advertising support.
- [x] Add tests that compare trimmed and untrimmed theme, control, layout, and resource behavior.
- [x] Set `<IsAotCompatible>true</IsAotCompatible>` for core only after all core gates pass.

### Phase 1 Exit Criteria

- [x] Both runtime variants of `Forma` produce zero owned trim/AOT warnings.
- [x] Core unit and package-consumer behavior is unchanged after reflection removal.
- [x] Core package metadata does not overstate runtime/platform execution coverage.

## Phase 2: Compiled XAML and Build-Time Dependency Boundary

- [x] Package `Forma.Xaml.Build.<Runtime>` as private `buildTransitive` tooling with no `lib`/`ref`
  runtime assets and no transitive runtime dependency from `Forma.<Runtime>`.
- [x] Pin the maintained XamlX fork and Mono.Cecil version; record the fork delta, upstream base,
  license, update procedure, and focused regression coverage.
- [x] Compile Release XAML and `x:DataType` bindings to application IL before trimming/AOT, with no
  runtime reader, reflection binding fallback, or source-XAML requirement.
- [x] Prove a compiler-free NativeAOT output through both the XAML spike and empty-cache packed
  MonoGame/FNA consumers.
- [x] Reject source XAML, XamlX, Mono.Cecil, Forma compiler/build assemblies, and hot reload from
  Release, trimmed, and NativeAOT output.
- [x] Verify deterministic XAML build packages and preserve portable PDB/diagnostic behavior through
  assembly injection.
- [x] Implement production Cecil emission for local resources, static/dynamic resource references,
  resource lookup precedence, and merged dictionaries; parser acceptance or runtime types alone do
  not count as compiler support.
- [x] Implement production Cecil emission for styles, setters, selectors, and style/resource
  application without runtime reflection discovery or source-XAML lookup.
- [x] Implement production Cecil emission for event hookup, property/event triggers, storyboards,
  timelines, and deterministic detach/stop behavior.
- [x] Generate ownership and disposal IL for bindings, event subscriptions, trigger listeners,
  storyboard clocks, and other compiler-created runtime objects.
- [x] Add focused compiler tests that inspect emitted IL and execute each advanced construct before
  introducing linker/AOT variables.
- [x] Expand packed fixtures to combine resources, merged dictionaries, styles, events, triggers,
  storyboards, typed bindings, and generated disposal in a representative production view.
- [x] Execute that fixture under trim-only and NativeAOT for MonoGame and FNA, verify observable
  behavior and cleanup, and reject reflection fallback, whole-assembly roots, source XAML, and
  compiler/build artifacts from every output.
- [x] Test XAML build tooling from every supported build-host OS and SDK used to produce declared AOT
  targets; record any platform toolchain restrictions on pre-link assembly rewriting.
- [x] Add a gate that fails if XAML injection runs after trimming, AOT compilation, signing, or final
  platform packaging.
- [x] Define dependency update and security-response policy for the maintained XamlX fork,
  Mono.Cecil, and their packaged build-time closure.

### Phase 2 Exit Criteria

The production boundary now includes generated view construction, namescope lookup, typed
one-way/two-way bindings, resources and merged dictionaries, styles, events, property/event
triggers, storyboards, timelines, and generated disposal. Focused Cecil inspection and packed
trim/AOT consumers execute the same advanced fixture for both runtime peers.

- [x] A packed consumer compiles and executes generated views and typed bindings under trim-only and
  NativeAOT for both peer runtimes without a compiler or source XAML in target output.
- [x] External compiler dependencies are pinned, licensed, reproducibly packaged, and isolated from
  target runtime dependencies.
- [x] Every supported production XAML construct has focused trim/AOT execution evidence, and every
  declared target has a supported build-host path that performs injection before platform linking.

## Phase 3: Optional Media and Platform Capability Boundaries

- [x] Replace reflective video seeking discovery with compile-time runtime adapters or an injected
  seeking capability.
- [x] Define trim/AOT behavior when a media backend or codec is unavailable.
- [x] Ensure unavailable optional capabilities do not root backend assemblies or fail startup.
- [x] Audit file dialogs, clipboard, URI launching, text input, and filesystem access for desktop
  assumptions and isolate them behind capability contracts where needed.
- [x] Add analyzer-clean media consumers for each runtime/backend pair that declares support.
- [x] Mark unsupported media/runtime pairs explicitly rather than suppressing diagnostics.
- [x] Set AOT-compatible package metadata for `Forma.Media` variants independently from core.

### Phase 3 Exit Criteria

- [x] Supported media variants are warning-free and execute their declared capability smoke tests.
- [x] Unsupported optional features report stable unavailable state without reflection or crashes.
- [x] Core AOT support does not depend on installing `Forma.Media`.

## Phase 4: Native Dependency and Static-Link Readiness

- [x] Inventory every FreeType and HarfBuzz native entry point, callback, handle, and library name
  reached by dynamic text.
- [x] Verify that managed wrappers do not require runtime-generated marshalling or unbounded callback
  registration on supported targets.
- [x] Add bounded startup diagnostics identifying the selected native library and packaging source
  without exposing native implementation types publicly.
- [x] Test RID-native asset loading from clean packed consumers for every public AOT target.
- [x] Define an internal native-font backend contract for bounded source loading, face creation,
  metadata, character/glyph lookup, metrics, variations, shaping, rasterization, diagnostics, and
  deterministic disposal; no native pointer, handle, loader, or platform type may enter public APIs.
- [x] Move the current FreeTypeSharp/HarfBuzzSharp implementation behind that contract while
  preserving `UIFontFace`, `DynamicUIFont`, `TextLayoutEngine`, control APIs, layout results, and
  existing desktop package behavior.
- [x] Define compile-time backend selection or authorized host injection without reflection,
  assembly scanning, runtime generic construction, or a mandatory dependency from core to dynamic
  text.
- [x] Add backend conformance tests for multilingual shaping, fallback, variations, glyph metrics,
  raster output, malformed input, bounded failures, repeated initialization, and idempotent
  disposal.
- [ ] Implement a static-link or platform-resolved spike in which approved FreeType/HarfBuzz symbols
  are supplied by the final executable, platform package, or compatible platform font adapter.
- [ ] Prove the spike publishes and executes without sidecar FreeType/HarfBuzz libraries, runtime
  library search, or changes to public font/layout namespaces and signatures.
- [ ] Validate backend lifecycle rules for worker-thread shaping, graphics-thread atlas upload,
  graphics reset, process shutdown, and native ownership on the selected restricted or authorized
  target.
- [x] Verify license notices and source-offer obligations for every redistributed native build.
- [x] Test missing, wrong-architecture, rejected, and duplicate native library failures.
- [x] Test native handle disposal, repeated initialization, graphics reset, and process shutdown.

### Phase 4 Exit Criteria

The validated desktop backend currently uses FreeTypeSharp P/Invokes and resolver-selected native
assets plus HarfBuzzSharp native assets. This is valid for the declared macOS arm64 AOT matrix, but
does not prove that a restricted target can dynamically load sidecar libraries or replace that
resolution path.

An NDA-neutral external-backend spike now compiles a platform adapter into `Forma.DynamicText` for
both runtime peers, publishes it with NativeAOT, executes the unchanged `UIFontFace` API, and rejects
FreeType/HarfBuzz managed dependencies and sidecars from output. This proves compile-time replacement
and packaging isolation. The static/platform implementation tasks remain open until an actual
restricted or authorized font backend supplies production shaping, rasterization, and lifecycle
behavior rather than the deterministic conformance adapter.

- [x] Dynamic fonts load, shape, rasterize, upload, draw, and dispose in a published AOT consumer.
- [x] Native dependency failure is bounded and diagnostic on every declared public target.
- [ ] An authorized platform can replace native library resolution behind an internal adapter.

## Phase 5: Peer Runtime NativeAOT Consumers and CI

- [x] Add minimal source consumers for fast local analyzer iteration.
- [x] Add isolated packed consumers for final package validation with empty NuGet caches.
- [x] Publish trim-only and NativeAOT consumers separately for MonoGame and FNA.
- [x] Compile and execute a packed Forma XAML view with generated namescope and typed one-way/two-way
  bindings in every current trim-only and NativeAOT consumer profile.
- [x] Exercise controls, layout, theme resources, input routing, and disposal in bounded headless or
  windowed smoke modes as supported by the target.
- [x] Exercise Latin, Arabic, Indic, bidi, fallback, and missing-glyph dynamic text through the atlas.
- [x] Compare retained layout results between JIT and AOT executions from identical font bytes.
- [x] Compare approved render metrics or images between JIT and AOT without requiring byte-identical
  output across different graphics backends.
- [x] Fail CI on new owned or unclassified trim/AOT warnings through a dedicated macOS arm64 job that
  executes the complete packed-consumer matrix on every push and pull request.
- [x] Expose the same full matrix as `make nativeaot`, with optional runtime/profile/mode selectors
  for focused local iteration.
- [x] Upload per-cell trim/AOT publish logs from CI even when validation fails.
- [x] Store binaries, native dependency manifests, and render diagnostics as review artifacts.
- [x] Ensure AOT validation does not rely on a developer machine's global native libraries.

### Phase 5 Exit Criteria

- [x] Every declared public runtime/RID pair publishes and executes from packed artifacts.
- [x] JIT and AOT layout behavior matches for the shared multilingual corpus.
- [x] CI detects removed metadata, missing native assets, startup failures, and warning regressions.

## Phase 6: Restricted-Platform and Lifecycle Proxies

- [ ] Select public targets that approximate console constraints without claiming equivalence, such
  as iOS device AOT or another sandboxed no-dynamic-code environment supported by the runtime.
- [ ] Validate startup without writable application directories or system font assumptions.
- [ ] Validate suspend, resume, backgrounding, graphics-device loss/reset, and atlas restoration.
- [ ] Validate controller-only navigation, safe-area layout, display-scale changes, and text input
  capability absence.
- [ ] Validate bounded font source, glyph atlas, fallback depth, and allocation limits under AOT.
- [ ] Validate worker-thread shaping and render-thread upload rules on the selected proxy targets.
- [ ] Document which proxy findings are transferable and which still require target hardware.

### Phase 6 Exit Criteria

- [ ] At least one public restricted-platform AOT target passes startup, rendering, input, lifecycle,
  memory, and dynamic-text gates.
- [x] No support statement describes a proxy as console validation.
- [x] Remaining private-platform risks have explicit tests defined before integration begins.

## Phase 7: Authorized Console Integration Contract

- [x] Define an NDA-neutral host checklist covering runtime assembly, graphics, input, filesystem,
  lifecycle, native fonts, threading, diagnostics, packaging, and deployment.
- [ ] Keep platform SDK references and confidential adapters in authorized repositories.
- [ ] Verify the authorized MonoGame or FNA port's trimming/AOT preservation requirements.
- [ ] Verify the authorized toolchain can consume application assemblies after Forma XAML injection,
  or supply an approved build-host stage before the platform linker, AOT compiler, signer, and packager.
- [ ] Verify deployed binaries and platform packages contain no source XAML, XamlX, Mono.Cecil,
  MSBuild task, Forma compiler/build assembly, hot reload, file watcher, or dynamic-code path.
- [ ] Supply approved FreeType/HarfBuzz builds or a compatible platform font adapter according to
  platform-holder policy.
- [ ] Validate startup, controller navigation, safe areas, suspend/resume, user switching, graphics
  reset, memory pressure, and clean shutdown.
- [ ] Exercise representative compiled-XAML menus, HUDs, settings, localization, controller focus,
  and lifecycle restoration on hardware without reflection binding fallback.
- [ ] Run multilingual dynamic-text layout and rendering tests on hardware or an approved equivalent.
- [ ] Validate package size, native symbols, forbidden imports, signing, and submission diagnostics.
- [ ] Record only non-confidential capability results in the public support matrix.
- [ ] Require a separate approval for each platform/runtime/backend combination.

### Phase 7 Exit Criteria

- [ ] At least one authorized platform integration passes its private release checklist.
- [ ] No confidential material is present in public source, CI, issues, logs, or artifacts.
- [ ] Public documentation names support only where disclosure and validation are both authorized.

## Phase 8: Support Matrix, Packaging, and Release Gates

- [x] Add analyzer-clean, published, executed, and platform-validated columns to runtime support docs.
- [x] Document exact public publish commands, prerequisites, native asset behavior, and limitations.
- [x] Add package metadata and README claims only for validated runtime/RID pairs.
- [x] Add release gates that consume packed artifacts from an empty cache and execute AOT smokes.
- [x] Publish the XAML build-host/toolchain support matrix separately from the target runtime/RID
  matrix, including the pinned compiler dependency revisions used by each release.
- [x] Verify API/reference parity between JIT and AOT package variants; do not create separate public
  AOT namespaces or APIs.
- [x] Verify trimming does not remove public constructors, embedded resources, themes, controls, or
  runtime adapters required by documented usage, or members directly referenced by generated XAML IL.
- [x] Add migration guidance for consumers that reflect over Forma types.
- [x] Document crash-dump, symbol, and diagnostic limitations for AOT deployments.
- [x] Review third-party notices and native redistribution terms before each release.
- [x] Keep unvalidated and NDA-restricted targets clearly marked as unsupported or privately gated.

### Phase 8 Exit Criteria

- [x] Release validation fails on warning, publish, startup, native loading, behavior, or documentation
  regressions for every declared AOT target.
- [x] Package and support claims match the evidence level recorded in the matrix.
- [x] Console readiness and actual authorized console support remain distinct, reviewable claims.

## Validation Matrix

| Layer | Required evidence |
| --- | --- |
| XAML build host | Pinned/reproducible XamlX and Cecil tools; deterministic injection before trim/AOT; portable diagnostics |
| Compiled XAML target | Generated views/bindings execute; source/compiler/hot-reload artifacts and reflection fallback absent |
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
| Build-time XAML dependencies are mistaken for target dependencies | Keep them in private build assets and inspect every Release/AOT output for source and tooling artifacts. |
| A platform toolchain cannot accept post-compile Cecil injection | Run injection before the platform linker/AOT/signing stages and validate that build-host path before claiming the target. |
| Generated XAML IL keeps simple views but loses advanced features under trimming | Execute every supported XAML feature from packed trim/AOT consumers rather than relying on compiler-only tests. |
| The maintained XamlX fork or Cecil becomes stale or vulnerable | Pin exact revisions, document the delta and licenses, and require focused regression/security review for updates. |
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
their advertised trimming/AOT contracts; compiled XAML covers the production language without
shipping source, compiler, reflection-binding, or hot-reload dependencies; packed consumers publish
and execute across the public support matrix; dynamic text and native dependencies pass
restricted-platform gates; and at least one authorized console integration has passed its private
checklist. That integration must compile XAML on an approved build host before platform linking/AOT
and verify clean target artifacts on hardware. Public documentation must continue to distinguish
general AOT compatibility, console readiness, and validated support for a specific authorized
platform.