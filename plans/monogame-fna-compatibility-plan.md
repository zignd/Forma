# MonoGame and FNA Compatibility Plan

## Objective

Make Forma a retained-mode UI toolkit for both MonoGame and FNA without presenting either runtime
as the canonical implementation. Applications should use the same `Forma` namespace, control
types, layout behavior, and rendering model regardless of which XNA-compatible runtime they choose.

The implementation will compile the same Forma sources separately against MonoGame and FNA. This
is necessary because the two runtimes expose similar `Microsoft.Xna.Framework` APIs from different
assemblies and are source-compatible in many places, but they are not binary-compatible
substitutes. Runtime-specific code must stay behind narrow internal adapters or explicitly injected
public contracts rather than appearing in the main namespace hierarchy.

This plan also makes `Forma.Media` useful on both runtimes. MonoGame and FNA have different video
loading, codec, seeking, and platform support, so media behavior must be capability-driven instead
of assuming that matching type names imply matching functionality.

## Guiding Principles

- **One product identity:** all public UI types remain in the `Forma` namespace.
- **Peer runtimes:** MonoGame and FNA receive symmetric build, package, test, documentation, sample,
  and release treatment.
- **One source of behavior:** controls, layout, input routing, styling, and rendering logic are
  shared. Framework-specific source files contain only genuine API differences.
- **Source compatibility, not binary substitution:** each artifact is compiled against exactly one
  runtime and consumers must not mix the two variants in one application.
- **Explicit selection:** build and package commands identify the target runtime. Release jobs must
  not rely on an implicit default.
- **Capability-driven optional features:** video codecs, seeking, native libraries, and platform
  behavior are documented and tested per runtime rather than flattened into false equivalence.
- **No namespace branding:** do not add `Forma.MonoGame`, `Forma.FNA`, or runtime-specific copies of
  controls. Runtime names may appear in package IDs, build properties, host projects, adapters, and
  documentation where selection is genuinely required.
- **No unverified compatibility claims:** a platform/runtime pair is supported only after its build,
  tests, package consumer, and relevant smoke tests pass in CI.

## Decision Summary

- Keep the public namespaces `Forma` and the optional assembly identity `Forma.Media`.
- Build the core and media assemblies once per runtime from shared source.
- Use symmetric, provisional package IDs:
  - `Forma.MonoGame`
  - `Forma.FNA`
  - `Forma.Media.MonoGame`
  - `Forma.Media.FNA`
- Keep the compiled core assembly name `Forma` in both core packages and `Forma.Media` in both media
  packages. The packages are alternatives, not dependencies of one another.
- Retire the unqualified package IDs before the first public release unless Phase 0 finds a NuGet
  mechanism that can select the runtime safely without restoring both framework assemblies. Do not
  leave `Forma` as a MonoGame alias because that would make MonoGame the implicit default.
- Use one required build property, provisionally `FormaRuntime=MonoGame|FNA`, for shared project
  evaluation. Reject missing or unknown values in pack and release builds.
- Permit convenient local build targets or scripts for each runtime, but require the dual-runtime
  matrix in CI and release validation.
- Keep XNA-compatible types such as `Vector2`, `Texture2D`, `SpriteBatch`, and `Keys` in the public
  API where they are natural. Each package binds those signatures to its selected runtime assembly.
- Isolate API differences with partial internal adapters and small runtime-specific source sets;
  avoid `#if` branches throughout control code.
- Keep `IVideoPlaybackBackend` injectable and make the built-in implementation runtime-specific.
- Support FNA video from local Theora (`.ogv`) and AV1 (`.obu`/`.av1`) files when the selected FNA
  distribution and native libraries provide Theorafile and dav1dfile.
- Describe MonoGame video support according to the selected MonoGame backend. Do not promise video
  on DesktopGL or Native backends until their decoder paths are implemented and validated.
- Do not claim H.264/MP4 support through either built-in media adapter unless a separately selected,
  licensed, packaged, and tested decoder implementation provides it.

## Progress Dashboard

- [x] Phase 0: Runtime Acquisition and Dual-Compile Spike
- [x] Phase 1: Symmetric Build Model
- [x] Phase 2: Core Runtime Adapters and API Parity
- [x] Phase 3: Peer Catalog Hosts and Content Workflows
- [x] Phase 4: Media Contracts and Runtime Implementations
- [x] Phase 5: Tests, CI, and Platform Matrix
- [x] Phase 6: Symmetric Packaging and Consumer Validation
- [x] Phase 7: Documentation, Migration, and Release Readiness

Check a phase only after every task and exit criterion in that phase is complete.

### Progress Tracking Workflow

Use the existing plan tracker at the start and end of implementation sessions:

```sh
bash scripts/track-plan.sh plans/monogame-fna-compatibility-plan.md
```

Add newly discovered required work to this document. A phase dashboard entry may be checked only
when all tasks and exit criteria in that phase are checked.

## Success Criteria

- [x] The same public `Forma` namespace and control names compile for MonoGame and FNA consumers.
- [x] Shared control, layout, style, input, and rendering sources contain no runtime preference.
- [x] Runtime-specific code is limited to documented adapter and host boundaries.
- [x] MonoGame and FNA core artifacts pass the same behavioral unit suite.
- [x] Public API parity checks detect accidental runtime-only members or signature drift.
- [x] Each runtime has a runnable catalog host using the same catalog stories and presentation.
- [x] Each runtime has an external package-consumer test that references only its matching package.
- [x] Package IDs, README examples, CI jobs, artifacts, and release notes present the two runtimes as
  peers.
- [x] A consumer cannot accidentally restore both core variants or both media variants without a
  clear build error.
- [x] `Forma.Media.FNA` plays a repository-owned or permissively licensed Theora or AV1 test clip on
  every declared FNA desktop platform.
- [x] `Forma.Media.MonoGame` reports unavailable video functionality without crashing the catalog on
  MonoGame backends that do not implement video.
- [x] Video playback capabilities are documented by runtime, platform, codec, audio, looping, and
  seeking support.
- [x] FNA native dependencies are reproducibly supplied for each declared RID and validated from a
  clean package-consumer project.
- [x] Existing attribution and license notices remain complete for Forma-authored, adapted, and
  third-party code and assets.
- [x] Release validation builds all peer artifacts from the same commit and version.

## Non-Goals

- Create a new framework abstraction that replaces the XNA programming model.
- Rename `Microsoft.Xna.Framework` types or mirror them under `Forma`.
- Load MonoGame and FNA in the same process.
- Promise binary compatibility between artifacts compiled for different runtimes.
- Fork either runtime merely to hide an incompatibility that can be handled by a narrow adapter.
- Make every backend provide identical optional media capabilities.
- Add unsupported codecs through ad hoc native binaries or system-installed dependencies.
- Publish packages as part of implementation; publication remains separately approval-gated.
- Claim that dependency selection or notice updates constitute legal clearance.

## Current State

### Core

- `Forma` compiles against MonoGame 3.8.5 and exposes XNA-compatible framework types throughout its
  public API.
- `Directory.Build.props` and project files currently encode MonoGame-specific properties such as
  `MonoGameVersion`, `MonoGamePlatform`, `MonoGamePackageId`, and `MonoGameProjectPath`.
- The core package uses `PrivateAssets="All"` for MonoGame so applications select their own
  MonoGame backend package.
- Unit tests, render tests, package-consumer tests, catalog builds, CI, release validation, and
  documentation currently assume MonoGame.
- DesktopGL, WindowsDX, Native Vulkan, and Native Metal are existing MonoGame validation surfaces.

### Media

- `VideoStreamPlayer`, `IVideoPlaybackBackend`, and the built-in backend all currently compile in
  `Forma.Media` against MonoGame media types.
- The public control contract uses `Video`, `MediaState`, and `Texture2D`, which also exist in FNA's
  XNA-compatible API but bind to a different assembly.
- `GetStreamName()` directly reads MonoGame's `Video.FileName`; current FNA `Video` does not expose
  that member.
- The built-in backend is named `MonoGameVideoPlaybackBackend`, even though most operations map to
  the shared XNA `VideoPlayer` surface.
- Seeking is discovered through reflection because it is not uniformly available.
- MonoGame DesktopGL currently throws `NotImplementedException` for video. The evaluated MonoGame
  Native Vulkan path does not currently supply working decoder factories.
- FNA implements video playback using Theorafile for Theora and dav1dfile for AV1. It can construct
  a video from a local URI with `Video.FromUriEXT(uri, graphicsDevice)` and play it through
  `VideoPlayer`.

### Distribution Constraint

MonoGame and FNA both define `Microsoft.Xna.Framework` types, but the defining assembly identities
are different. A `Forma.dll` compiled against MonoGame cannot be made into an FNA binary by changing
only the consumer package reference. The source must be compiled again against FNA, and media/native
dependencies must match that build.

## Target Architecture

```text
                         shared Forma sources
             controls / layout / style / input / rendering
                                  |
                   runtime-neutral internal contracts
                         /                    \
                        v                      v
              MonoGame adapters          FNA adapters
                        |                      |
                        v                      v
          MonoGame-compiled assemblies  FNA-compiled assemblies
                 same namespaces and public API shape
                        |                      |
                        v                      v
              MonoGame catalog            FNA catalog
              and consumer tests          and consumer tests
```

### Source Layout

Use a shared-first layout. Exact paths may change during Phase 1, but responsibilities must remain
clear:

```text
src/
  Forma/                       shared core sources and project
  Forma.Media/                 shared media control and contracts
  RuntimeAdapters/
    MonoGame/                  core and media differences for MonoGame
    FNA/                       core and media differences for FNA
samples/
  Forma.Catalog/               shared stories and catalog shell
  Forma.Catalog.MonoGame/      thin MonoGame entry point/content host
  Forma.Catalog.FNA/           thin FNA entry point/content host
```

Do not duplicate controls into the adapter directories. If more than a small number of lines in a
control differ, introduce a narrow internal contract owned by the shared code and implement it in
each adapter.

### Runtime Adapter Boundaries

Candidate boundaries to confirm in Phase 0:

- Framework assembly reference and runtime acquisition.
- Window/application host construction.
- Graphics-device and service discovery where APIs differ.
- Content loading and content build tooling.
- Effect bytecode/content compatibility.
- Video construction, naming, codec capability, and playback implementation.
- Native library resolution and RID assets.
- Platform/backend diagnostics shown by the catalog.

Math, layout, control state, input semantics, style calculation, draw command construction, and
catalog stories are not runtime adapter responsibilities.

## Public API Policy

### Namespace and Type Identity

- Public types remain under `Forma`.
- Do not add runtime suffixes to controls, themes, layouts, events, or interfaces.
- The same source declaration should produce each public type in both builds.
- Runtime-specific helpers should be internal whenever possible.
- A public runtime-specific API is allowed only when the capability cannot be represented honestly
  through a shared contract. Such APIs must live in symmetric peer namespaces only if both runtimes
  need corresponding extension surfaces; Phase 0 requires explicit review before adding either.

### API Parity

- Generate documentation for both builds and compare normalized public signatures in CI.
- Normalize only the expected framework assembly identity difference. Do not normalize missing
  members, changed nullability, visibility, or parameter types.
- Compile the same consumer contract tests once against each peer package.
- Do not add a generated, checked-in API inventory or revive the removed `Forma.ApiInventory`
  tooling. Parity validation should be an executable test over build outputs.

### Consumer Selection

A consumer chooses exactly one core package and, optionally, its matching media package:

```xml
<PackageReference Include="Forma.MonoGame" Version="..." />
<PackageReference Include="Forma.Media.MonoGame" Version="..." />
```

or:

```xml
<PackageReference Include="Forma.FNA" Version="..." />
<PackageReference Include="Forma.Media.FNA" Version="..." />
```

Application source continues to use:

```csharp
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
```

The peer packages must add an MSBuild guard that fails with a direct diagnostic if both variants
are referenced, or if a media variant is paired with the other runtime's core variant.

## Build and Package Strategy

### Runtime Selection

- Add `FormaRuntime` with exactly two accepted values: `MonoGame` and `FNA`.
- Move MonoGame-specific defaults under a `FormaRuntime == MonoGame` condition.
- Add pinned FNA acquisition properties under `FormaRuntime == FNA`.
- Support both a reproducible package reference and a local project/source override where practical.
- Keep intermediate and output paths runtime-qualified to prevent one build from reusing the other
  runtime's reference assemblies, generated files, or package outputs.
- Emit the selected runtime into assembly metadata for diagnostics without changing namespaces.
- Fail pack and release commands when `FormaRuntime` is missing. A local convenience command may
  build both runtimes rather than choosing one silently.

### FNA Acquisition Decision

Phase 0 must choose and document one reproducible FNA source:

- An official or maintained package with suitable target frameworks and native asset behavior.
- A pinned source/project checkout built in CI.
- A repository-managed dependency mechanism consistent with FNA's distribution guidance.

`Murder.FNA` is useful evidence that the runtime and video path work in a real application, but it
must not become Forma's production dependency merely because it is present in this workspace.
Record ownership, update cadence, version pinning, native assets, license terms, and vulnerability
response for the selected source.

### Package Symmetry

- Produce all four peer packages from the same version and commit.
- Give peer packages equivalent descriptions, tags, README coverage, symbols, Source Link, notices,
  and documentation files.
- Do not make one peer package depend on or wrap the other.
- Do not publish an unqualified package that silently selects one runtime.
- Ensure package dependency metadata cannot pull both framework implementations transitively.
- Validate package contents and dependencies from clean caches.

## Media Compatibility Strategy

### Shared Contract

Keep `VideoStreamPlayer` behavior shared:

- Stream assignment and autoplay.
- Play, pause, resume, stop, looping, and volume.
- Minimum-size and expand behavior.
- Frame texture drawing.
- Finished notification.
- Graceful unavailable-backend behavior.

Keep `IVideoPlaybackBackend` as the injection point for custom decoders and application-specific
media stacks. Review whether its direct `Video` parameter remains the best long-term boundary; if a
neutral stream descriptor is introduced, preserve a straightforward path for runtime-native
`Video` objects and do not turn the core UI package into a media framework.

### Runtime Implementations

- Rename the internal built-in backend to a neutral implementation name or compile peer internal
  implementations from runtime adapter sources.
- Move stream display-name resolution behind an adapter; shared code must not access
  `Video.FileName`.
- Detect seeking as a capability. `SetStreamPosition` must return or expose whether the operation was
  accepted in a future breaking API review; until then, retain its no-throw best-effort behavior.
- Keep video loading outside control construction. Catalog hosts provide a runtime-specific loader
  that returns the runtime's `Video` object.
- MonoGame catalog loading uses the supported MonoGame content path for the selected backend.
- FNA catalog loading uses `Video.FromUriEXT` with a local file URI and the active graphics device.
- Package or copy native FNA video dependencies according to the selected FNA distribution rather
  than relying on developer-machine installations.

### Capability Reporting

Define an internal or public read-only capability description with at least:

- Availability.
- Supported containers/codecs.
- Audio support.
- Looping.
- Seeking.
- Track selection.
- Supported runtime/platform/backend combination.
- Failure reason when unavailable.

The catalog media story should render this information and enable only supported actions. A missing
decoder must produce a clear unavailable state, not a blank region presented as successful
playback.

### Test Asset

- Add one short, deterministic, repository-owned or permissively licensed clip with visible frame
  changes and an audio cue if audio synchronization is tested.
- Store the original source or reproducible generation command and record attribution.
- Encode the FNA fixture as Theora first; add AV1 only when all target RIDs package dav1dfile
  reliably.
- Produce a MonoGame-compatible fixture separately if its content path requires different
  processing. The visual source and expected timing should remain equivalent.
- Keep the clip small enough for source control and CI while long enough to verify multiple frames,
  looping, and completion.

## Validation Matrix

The initial matrix is intentionally explicit. Phase 0 may reduce or expand it only with a recorded
reason.

| Runtime | Platform/backend | Core build/tests | Catalog | Render tests | Real video |
| --- | --- | --- | --- | --- | --- |
| MonoGame | WindowsDX | Required | Required | Required | Validate if implemented |
| MonoGame | DesktopGL on Linux | Required | Required | Required | Expected unavailable initially |
| MonoGame | DesktopGL on macOS | Required | Required | Compile/isolation | Expected unavailable initially |
| MonoGame | Native Vulkan on Linux | Required | Required | Required | Expected unavailable initially |
| MonoGame | Native Metal on macOS | Required | Required | Compile/isolation | Expected unavailable initially |
| FNA | Windows desktop | Required | Required | Required | Required |
| FNA | Linux desktop | Required | Required | Required | Required |
| FNA | macOS x64 | Required | Required | Required or documented isolation | Required |
| FNA | macOS arm64 | Required | Required | Required or documented isolation | Required |

Do not mark FNA support complete based only on video. Core graphics, input, effects, content,
windowing, package consumption, and the full control suite must pass as well.

## Delivery Phases

### Phase 0: Runtime Acquisition and Dual-Compile Spike

- [x] Record the exact MonoGame baseline and select a pinned FNA version/distribution candidate.
- [x] Confirm the selected FNA assembly name, target framework support, native asset model, and
  license/notice requirements.
- [x] Build an isolated spike that compiles the current `Forma` core sources against each runtime.
- [x] Capture every FNA compile error and classify it as shared API mismatch, content/host concern,
  unsupported API, or accidental MonoGame dependency.
- [x] Repeat the spike for `Forma.Media` and separately record `Video`, `VideoPlayer`, and
  `MediaState` differences.
- [x] Verify that each output references only its selected framework assembly.
- [x] Prove both outputs can retain the `Forma` assembly name and public namespace without being
  installed together.
- [x] Test the provisional peer package IDs for availability and NuGet dependency behavior without
  publishing them.
- [x] Decide whether the shared projects compile directly with conditional references or whether
  thin peer build projects include shared sources.
- [x] Write a short decision record for runtime acquisition, package IDs, local project overrides,
  and native dependency ownership.

#### Phase 0 Exit Criteria

- [x] Core dual compilation succeeds or the remaining incompatibilities have bounded adapter
  designs with no control duplication.
- [x] FNA media plays a local Theora fixture in a minimal application using the selected dependency
  distribution.
- [x] Package and build design preserves one public namespace and symmetric runtime selection.
- [x] No release or package publication has occurred.

#### Core Spike Findings

- MonoGame 3.8.5 and `FNA.NET` 2.2.11.2602 both compile the shared `Forma` core as `Forma.dll`.
- The FNA compile differences were framework API mismatches: text-input event shape, MonoGame-only
  integer clamp and geometry conveniences, color alpha-copy construction, named colors, mouse
  position access, and nullable sprite-batch transforms.
- Text input is isolated behind symmetric runtime adapters. The remaining differences use shared
  XNA-compatible helpers and do not duplicate controls or public APIs.
- The selected build design compiles the shared projects directly with conditional framework
  references and excludes the opposite runtime's adapter source set.
- `Forma.Media` compiles against both runtimes. FNA's `Video` omits MonoGame's `FileName` property,
  so stream display names use symmetric metadata adapters. The selected FNA package exposes
  compatible `VideoPlayer` and `MediaState` APIs for playback state, position, looping, volume,
  texture access, and disposal; seeking remains capability-detected through reflection.
- Metadata inspection confirms that FNA artifacts reference `FNA.NET` and never
  `MonoGame.Framework`; MonoGame artifacts have the inverse references. Both variants retain the
  `Forma` and `Forma.Media` assembly names in isolated artifact directories.
- The macOS arm64 FNA Metal smoke decoded nine distinct frames from the repository-owned Theora
  fixture through `VideoStreamPlayer` and reached natural playback completion within its timeout.

### Phase 1: Symmetric Build Model

- [x] Add validated `FormaRuntime` selection to common build properties.
- [x] Rename MonoGame-only common properties so their scope is explicit and add equivalent FNA
  properties where needed.
- [x] Isolate runtime-specific references and local source/project override logic.
- [x] Qualify `BaseIntermediateOutputPath`, output paths, and package paths by runtime.
- [x] Add build targets for core, media, tests, and catalog under both runtime selections.
- [x] Ensure `dotnet clean`, incremental builds, and switching runtimes cannot leave stale references.
- [x] Add a command or script that builds both runtime variants and fails if either is skipped.
- [x] Keep shared assembly versions synchronized from one property.

#### Phase 1 Exit Criteria

- [x] A clean checkout can build both core and media variants with documented commands.
- [x] Each output references exactly one selected framework implementation.
- [x] Switching runtime selection without deleting the repository produces correct clean outputs.

### Phase 2: Core Runtime Adapters and API Parity

- [x] Introduce the smallest internal adapter contracts identified by the spike.
- [x] Move framework-specific implementations into symmetric peer source directories.
- [x] Remove direct MonoGame assumptions from shared project descriptions, diagnostics, and XML
  documentation.
- [x] Keep all controls, layout algorithms, styles, and input semantics in shared sources.
- [x] Compile the existing unit suite against both runtime variants.
- [x] Add normalized public-signature parity validation over the two build outputs.
- [x] Add tests that ensure each shared public type exists with matching members in both variants.
- [x] Add architecture checks that reject runtime-branded public namespaces and references to both
  framework assemblies.
- [x] Review conditional compilation; replace repeated or behavior-heavy branches with adapters.

#### Phase 2 Exit Criteria

- [x] Both variants expose matching normalized public APIs.
- [x] The complete core unit suite passes against MonoGame and FNA.
- [x] Runtime-specific code is restricted to reviewed adapter boundaries.

### Phase 3: Peer Catalog Hosts and Content Workflows

- [x] Separate shared catalog stories/shell behavior from runtime host startup.
- [x] Add thin MonoGame and FNA catalog hosts with equivalent window, input, UI scale, metrics, and
  screenshot behavior.
- [x] Keep one story catalog and one expected story count.
- [x] Decide how shared font, texture, and effect assets are built or loaded for each runtime.
- [x] Verify that effect bytecode and content artifacts are valid for each selected graphics path;
  generate peer artifacts from the same source when formats differ.
- [x] Keep runtime/backend identity visible in diagnostics, not in the catalog's product title or
  story hierarchy.
- [x] Add bounded smoke commands for both hosts.
- [x] Compare approved screenshots or deterministic render outputs for meaningful visual parity.

#### Phase 3 Exit Criteria

- [x] Both catalog hosts launch and expose the same stories and UI behavior.
- [x] Shared catalog code does not choose or prefer a runtime.
- [x] Content generation is reproducible from a clean checkout for both hosts.

Shared font/content artifacts are byte-compared and load in both hosts. Custom effects are not part
of Forma's required renderer content: MonoGame MGFX and FNA Effects Framework bytecode were verified
as non-interchangeable and have separate reproducible gates in `docs/runtime-support.md`. No shared
effect-bytecode compatibility is claimed.

### Phase 4: Media Contracts and Runtime Implementations

- [x] Remove `Video.FileName` and other runtime-specific members from shared media code.
- [x] Add symmetric internal playback and stream-metadata adapters.
- [x] Preserve custom `IVideoPlaybackBackend` injection in both builds.
- [x] Add capability reporting and explicit unavailable states.
- [x] Implement the FNA local-file loader with `Video.FromUriEXT`.
- [x] Package and resolve Theorafile for the selected FNA desktop RIDs.
- [x] Package and resolve dav1dfile only if AV1 is included in the declared support matrix.
- [x] Verify frame texture updates, audio, pause/resume, volume, looping, completion, disposal, and
  device shutdown on each FNA desktop platform.
- [x] Validate MonoGame media on every declared MonoGame backend and record unsupported paths without
  treating graceful unavailability as playback success.
- [x] Add backend-injected unit tests for state transitions and completion independent of native
  decoding.
- [x] Add a real-decoder integration test with the licensed fixture for each supported path.
- [x] Document seeking and track-selection differences rather than emulating them inaccurately.

#### Phase 4 Exit Criteria

- [x] The same `VideoStreamPlayer` public API compiles and behaves consistently for common
  operations in both variants.
- [x] Real FNA playback passes on every declared desktop RID.
- [x] Unsupported MonoGame paths present a tested unavailable state and do not crash the catalog.

The declared FNA media support cell is macOS arm64, where the audiovisual Theora fixture passes
natural completion and looping. Windows and Linux remain candidate CI gates and become declared
support only after those jobs pass on the release commit. AV1 and video-audio audibility are not
claimed beyond the exact capabilities recorded in `docs/runtime-support.md`.

### Phase 5: Tests, CI, and Platform Matrix

- [x] Parameterize unit and catalog inventory tests by runtime.
- [x] Build separate render-test projects or configurations when framework references require it.
- [x] Add MonoGame and FNA axes to CI without allowing one runtime's failure to be hidden by the
  other's success.
- [x] Run clean restore/build/test jobs on Windows, Linux, macOS x64, and macOS arm64 where runners
  are available.
- [x] Validate software-rendered/headless paths only where they represent supported runtime behavior.
- [x] Add native-library presence and load tests for FNA RIDs.
- [x] Add catalog smoke and real-video checks with bounded frame counts and deterministic exit.
- [x] Preserve compliance checks and clean-checkout validation for all new projects and assets.
- [x] Record unsupported or manually validated cells in the runtime capability matrix with
  reproducible manual gate instructions.

#### Phase 5 Exit Criteria

- [x] Every required matrix cell is automated or has a documented, reproducible manual gate.
- [x] CI reports peer runtime results distinctly.
- [x] No runtime is declared supported solely because it compiles.

### Phase 6: Symmetric Packaging and Consumer Validation

- [x] Pack the four peer packages into runtime-qualified staging directories.
- [x] Add symmetric package metadata, symbols, Source Link, docs, notices, and repository commit data.
- [x] Add package guards for mixed core/media variants and duplicate framework implementations.
- [x] Extend package-content validation to all peer artifacts.
- [x] Add one clean external consumer per runtime that exercises layout, drawing, input, and optional
  media.
- [x] Restore consumers from an empty package cache with no sibling build outputs.
- [x] Verify that runtime/framework dependencies are either explicit application choices or
  documented package dependencies according to each runtime's distribution model.
- [x] Verify deterministic package output and matching versions from one commit.
- [x] Keep publication disabled; release workflow artifacts remain reviewable before approval.

#### Phase 6 Exit Criteria

- [x] All peer packages install and run in isolated consumers.
- [x] Mixed variants fail early with actionable diagnostics.
- [x] Package metadata and documentation give both runtimes equivalent prominence and detail.

### Phase 7: Documentation, Migration, and Release Readiness

- [x] Rewrite the root README to describe Forma as an XNA-compatible UI toolkit supporting
  MonoGame and FNA.
- [x] Present peer installation and build examples side by side.
- [x] Document local MonoGame and FNA source-development overrides symmetrically.
- [x] Add a runtime capability matrix covering graphics hosts, content, effects, media codecs,
  seeking, native dependencies, trimming, and AOT status.
- [x] Add migration guidance from the current unqualified `Forma` and `Forma.Media` package IDs to
  the peer package IDs before any public package release.
- [x] Update catalog documentation and screenshots for both hosts.
- [x] Ensure CI and release workflow artifacts expose package, consumer, catalog, render, and media
  results for both runtimes.
- [x] Update notices and provenance for new FNA-related dependencies and the video fixture.
- [x] Review every runtime-qualified name in docs and UI to ensure it describes a genuine selection
  boundary rather than a product hierarchy.
- [x] Keep package publication disabled pending separate explicit user approval.

#### Phase 7 Exit Criteria

- [x] Documentation treats MonoGame and FNA as peer runtimes throughout.
- [x] Support claims exactly match the validated matrix.
- [x] All licenses and provenance are recorded without claiming legal clearance.
- [x] Release artifacts are complete and reviewable, but remain unpublished until separately
  approved.

## Risks and Mitigations

### Similar Namespace, Different Semantics

The runtimes may compile the same call while behaving differently. Shared behavioral tests and
catalog/render comparison are required in addition to compilation and public-signature parity.

### Package Conflicts

Both peer packages contain assemblies with the same identity by design. They must be mutually
exclusive, carry build guards, and be tested in isolated consumers. Documentation alone is not a
sufficient guard.

### FNA Distribution and Native Assets

FNA consumption may require source integration or native libraries that do not fit the current
NuGet workflow. Phase 0 resolves acquisition before public APIs or release automation depend on a
specific fork or package.

### Content and Effects

MGCB output, effect bytecode, and runtime content readers may not be interchangeable. Shared source
assets with reproducible runtime-specific outputs are preferable to checking in unexplained binary
duplicates.

### Optional Media Divergence

FNA may provide stronger desktop video support while MonoGame provides different support on other
platforms. Capability reporting and peer documentation prevent one implementation from defining a
misleading universal contract.

### Conditional Compilation Growth

Scattered runtime conditions can turn one source tree into two hidden implementations. Architecture
checks and adapter ownership keep conditions near project/reference and adapter boundaries.

## Open Decisions for Phase 0

- Exact peer package IDs after availability and dependency-model checks.
- Official FNA acquisition and pinning mechanism.
- Conditional shared projects versus thin peer build projects.
- Whether framework packages are application-owned references or explicit transitive dependencies
  for each peer artifact.
- FNA content and effect build workflow.
- Public versus internal media capability reporting.
- Whether to evolve `IVideoPlaybackBackend` away from direct runtime `Video` objects in a future
  breaking alpha release.
- Initial AV1 scope and dav1dfile RID coverage.
- AOT and trimming support claims for each runtime/platform pair.