# Runtime Support and Migration

This document is the support authority for Forma's MonoGame and FNA peer artifacts. A configured CI
cell is a gate, not a support claim: the cell must pass on the release commit before that artifact is
declared supported.

## Status Terms

- **Validated:** the repository check has passed on the named platform/runtime.
- **CI gate:** automated on every push and required to pass before release review.
- **Manual gate:** reproducible instructions exist because a hosted runner or licensed tool is not
  available.
- **Unsupported:** no compatibility claim is made.

## Graphics and Catalog Matrix

| Runtime / host | Windows x64 | Linux x64 | macOS arm64 | macOS x64 |
| --- | --- | --- | --- | --- |
| MonoGame DesktopGL | CI gate | Validated + CI gate | Validated + CI gate | Manual gate |
| MonoGame WindowsDX | Validated + CI gate | N/A | N/A | N/A |
| MonoGame Native Vulkan | N/A | Validated + CI gate | N/A | N/A |
| MonoGame Native Metal | N/A | N/A | Validated + CI gate | Manual gate |
| FNA selected backend | Validated D3D11 + CI gate | Validated OpenGL + CI gate | Validated Metal + CI gate | Manual gate |

Every automated cell builds the selected core, media, tests, render tests, and catalog host. It runs
the shared unit/catalog inventory suite and a bounded three-frame catalog smoke. Windows and Linux
execute graphics render tests. macOS compiles the same render tests but excludes fixture setup
because SDL graphics-device creation must occur on the process main thread.

Peer catalog comparison renders the same 1440x900 scene and requires identical dimensions/alpha
coverage plus non-background and RGB totals within 1%. Exact hashes remain diagnostics because the
two runtimes rasterize text differently.

For an Intel macOS manual gate, run on an x64 host:

```sh
bash scripts/check-runtime-parity.sh
bash scripts/check-catalog-smoke.sh
FormaRuntime=FNA bash scripts/check-catalog-smoke.sh
bash scripts/check-catalog-render-parity.sh
bash scripts/check-fna-video-smoke.sh
bash scripts/test-package-consumer.sh
```

## SVG Companion Matrix

The explicit SVG companions inherit the graphics matrix above. An SVG
companion is only useful with its matching core runtime; the platform validation scope for SVG is
therefore the same as for the core cell on each host.

| SVG companion | Package/native-size budget | Known validated host |
| --- | --- | --- |
| `Forma.Svg.Skia.*` | Svg.Skia/SkiaSharp dependencies and RID-selected Skia assets only | Existing Windows x64, Linux x64, and macOS arm64 reference matrix |
| `Forma.Svg.ThorVG.*` | Forma ABI 1 native asset only; no Skia dependency | Experimental macOS arm64 and Linux x64 |
| `Forma.Svg.*` | Compatibility dependency on explicit Skia with migration warning | Same as Skia during one migration window |

ThorVG Windows x64 and every console target are currently untested. Static desktop proof is called
console-ready architecture, not console-qualified support. Qualification requires authorized target
evidence as defined in [the migration/support guide](svg-backend-migration.md).

The complete hosted SVG matrix passed in
[CI run 31110056321](https://github.com/zigrok/Forma/actions/runs/31110056321) at exact implementation
snapshot `cd94582436e3ad8065262d8f5c9507ea03d98abe`. It executed the Windows Direct3D, Linux
OpenGL/Vulkan, and macOS OpenGL/Metal lifecycle gates for their selected peers. Package consumers in
the same run verified official NuGet provenance, RID-native selection, and the absence of Svg.Skia
from core-only graphs. See [docs/runtime-svg.md — Validation Gates](runtime-svg.md#validation-gates)
for the per-host commands.

## Content and Effects

Both catalog hosts build the same `.spritefont` sources through MGCB 3.8.5's DesktopGL-compatible
content format. The generated 1x/2x Inter and JetBrains Mono atlases load through both runtimes.

Effect bytecode is not interchangeable:

| Runtime | Format | Gate |
| --- | --- | --- |
| MonoGame | Platform-specific MGFX (`.mgfxo`) | Pass the artifact to catalog `--watch-effect` and require `hotReloadSucceeded: true` in bounded metrics. |
| FNA | Direct3D Effects Framework (`.fxb`) | Compile the same HLSL source with `fxc.exe /T fx_2_0 Input.fx /Fo Output.fxb`, then pass the artifact to `--watch-effect`. FXC may run under Wine. |

Forma does not bundle FXC or claim that MGFX works in FNA. No custom effect is required by the core
UI renderer; custom-effect support remains a runtime-specific application content decision.

## Media Matrix

| Capability | MonoGame | FNA |
| --- | --- | --- |
| Built-in backend | Backend/platform dependent; unavailable exceptions become explicit state | Available through `FNA.NET` on declared desktop RIDs |
| Local-file loading | Unsupported; applications use their selected content pipeline | `VideoStreamPlayer.LoadLocalFile` uses `Video.FromUriEXT` |
| Theora video | Unsupported until a selected backend decoder is validated | macOS arm64 validated; Windows/Linux are CI gates |
| AV1 video | Unsupported | Native dav1dfile is present, but AV1 is unsupported until a licensed fixture passes every desktop gate |
| Audio | Backend dependent | Native FAudio is supplied; video audio is not claimed because the fixture is silent |
| Pause/resume, volume, loop, completion, disposal | Shared/injected backend tests | Shared tests plus real Theora completion/disposal smoke |
| Seeking | Unsupported by the pinned 3.8.5 package API | Unsupported by the pinned 2.2.11.2602 package API |
| Track selection | Stored for API compatibility; no built-in track-switch claim | Stored for API compatibility; no built-in track-switch claim |

`VideoPlaybackCapabilities`, `IsPlaybackAvailable`, and `PlaybackUnavailableReason` let applications
present optional behavior without treating graceful unavailability as successful playback. Capability
selection is compile-time and does not use reflection. A missing backend or codec does not fail
startup; `Play` converts `NotImplementedException` and `PlatformNotSupportedException` into a stable
unavailable state and disposes the rejected backend.

## Platform Capabilities

Desktop services are explicit at the control boundary. `FileDialog.FileSystem` accepts an
`IFileDialogFileSystem`; its desktop default uses `System.IO`, while unavailable hosts can report
`IsAvailable = false` and receive a stable empty listing. `LinkButton.UriLauncher` is an optional
host callback and no longer starts a desktop process from core. Line and text edit clipboard reads
use host callbacks, clipboard writes use request events, and runtime text input is selected by the
MonoGame/FNA compile-time adapter. Dynamic fonts accept memory and streams; `FromProjectFile` is a
bounded desktop convenience API rather than a required font source.

## Native Dependencies

`FNA.NET` 2.2.11.2602 selects `FNA.NET.NativeAssets` 2.1.2.2602. The package supplies SDL3, FNA3D,
FAudio, Theorafile, and dav1dfile for Windows x64, Linux x64/arm64, and macOS universal deployment.
Empty-cache FNA consumers restore this package explicitly. Catalog and video CI gates exercise
native loading rather than checking filenames alone.

MonoGame applications select their own framework/backend package. Forma packages intentionally do
not impose a transitive MonoGame backend.

## Trimming and AOT

| Package/profile | RID | Analyzer-clean | Published | Executed | Platform-validated |
| --- | --- | --- | --- | --- | --- |
| `Forma.MonoGame` core | `osx-arm64` | Yes | Trim + AOT | Yes | macOS arm64 / OpenGL |
| `Forma.FNA` core | `osx-arm64` | Yes | Trim + AOT | Yes | macOS arm64 / Metal |
| `Forma.Media.MonoGame` capability smoke | `osx-arm64` | Yes | Trim + AOT | Yes | No codec claim |
| `Forma.Media.FNA` capability smoke | `osx-arm64` | Yes | Trim + AOT | Yes | No codec claim |
| `Forma.DynamicText.MonoGame` | `osx-arm64` | Yes | Trim + AOT | Multilingual atlas | macOS arm64 / OpenGL |
| `Forma.DynamicText.FNA` | `osx-arm64` | Yes | Trim + AOT | Multilingual atlas | macOS arm64 / Metal |
| `Forma.Svg.Skia.MonoGame` companion | `osx-arm64` | Yes | Trim + AOT | Svg.Skia verify + missing-native | macOS arm64 / OpenGL |
| `Forma.Svg.Skia.FNA` companion | `osx-arm64` | Yes | Trim + AOT | Svg.Skia verify + missing-native | macOS arm64 / Metal |
| `Forma.Svg.ThorVG.*` companion | `osx-arm64`, `linux-x64` | Yes | Dynamic source-generated interop + static `DirectPInvoke` reference host | Profile/67-icon suite, 492 comparisons, 1,000 lifetimes | Experimental |

ThorVG dynamic and static NativeAOT consumers execute for both runtime peers on macOS arm64 and
Linux x64. No other RID or platform has a public NativeAOT support claim. "Platform-validated" here means the
named public desktop host/backend only; it does not imply a restricted or console target.

Forma targets `net10.0`. Trim-only and NativeAOT packed consumers are validated on macOS arm64 for
`Forma.MonoGame`, `Forma.FNA`, and their matching `Forma.Media`, `Forma.DynamicText`, and
`Forma.Svg` packages. The gate covers native-free core, optional media, packed-XNB `SpriteFont`,
dynamic-text graphical, and bounded SVG companion profiles for both peers. The SVG profile publishes
with and without native Skia assets; the without-native probe requires the bounded failure diagnostic
(`SkiaSharp native initialization failed`). With native assets, `SvgBackendDefaults.Verify()`
completes a 2×2 rasterize sanity check. SVG NativeAOT and graphical trim-only profiles report one
upstream `IL2104` summary from the runtime peer (MonoGame.Framework or FNA.NET); this does not hide
any Forma-owned diagnostic.

Core consumers execute compiled primitives, brushes, attached layout,
selectors, adaptive conditions, keyed templates, relative sources, observable deltas,
virtualization, selection, flat/hierarchical data grids, accessibility peers, and control-template
application without DynamicText or dynamic code. It
publishes from empty package caches, executes every output, rejects Forma-owned `IL2xxx`/`IL3xxx`
warnings, verifies native-free imports, and proves packaged FreeType/HarfBuzz loading:

```sh
make nativeaot
make nativeaot NATIVEAOT_RUNTIME=MonoGame NATIVEAOT_PROFILE=media NATIVEAOT_MODE=aot
make nativeaot NATIVEAOT_RUNTIME=FNA NATIVEAOT_PROFILE=dynamic NATIVEAOT_MODE=trimmed
make nativeaot NATIVEAOT_RUNTIME=MonoGame NATIVEAOT_PROFILE=svg NATIVEAOT_MODE=aot
make nativeaot NATIVEAOT_RUNTIME=FNA NATIVEAOT_PROFILE=svg NATIVEAOT_MODE=trimmed
make aot-analyzers
make native-font-failures
```

Run on macOS arm64 with .NET SDK 10.0.x, Xcode command-line tools, recursive submodules, and network
access for the first restore. Valid profiles are `core`, `media`, `spritefont`, `dynamic`, and `svg`;
valid modes are `trimmed` and `aot`. The gate packs all selected Forma packages, restores each consumer
from an empty cache, publishes self-contained `osx-arm64` output, executes it, and records logs,
native manifests, binaries, and multilingual render/layout diagnostics under `Artifacts/nativeaot`.
The fast `aot-analyzers` target builds warning-as-error source-linked consumers for both peers.
`native-font-failures` runs fresh-process missing, incompatible, and rejected FreeType probes; the
full dynamic AOT cells additionally remove FreeType from copied native outputs and require the
bounded failure diagnostic.

MonoGame graphics profiles currently report one upstream `MonoGame.Framework` `IL2104` summary;
FNA NativeAOT and graphical trim-only profiles report one upstream `FNA.NET` `IL2104` summary. These
classified third-party warnings do not hide any Forma-owned diagnostic. FNA self-contained outputs
also require unversioned aliases for the versioned native libraries supplied by
`FNA.NET.NativeAssets`; the gate stages those aliases and preserves only the XNB reader metadata used
by its packed SpriteFont.

This validation does not cover media codec playback, other RIDs, iOS, Android, or any console. Those
modes remain unsupported until their own packed consumers execute. NativeAOT compatibility is not console
support; authorized hardware and platform-holder validation remain separate. Further work is tracked
in the [NativeAOT and console readiness plan](https://github.com/zigrok/Forma/blob/main/plans/nativeaot-console-readiness-plan.md).

Release XAML injection is built and stamp-verified with .NET 10 on current GitHub-hosted Ubuntu,
Windows, and macOS runners for both peers. The Cecil rewrite is build-host-neutral and must complete
before linker, NativeAOT, signing, output-copy, and publish targets. Compiler-signed target
assemblies are currently rejected because post-compile injection would invalidate the signature;
an authorized platform must provide an approved re-signing stage after injection. The public
`osx-arm64` NativeAOT execution gate itself runs on a macOS arm64 host; cross-OS AOT compilation is
not claimed.

### XAML Build Hosts

| Build host | SDK | Injection | Declared target restriction |
| --- | --- | --- | --- |
| Ubuntu latest | .NET 10.0.x | CI stamp-verified for both peers | Does not publish the declared macOS AOT target |
| Windows latest | .NET 10.0.x | CI stamp-verified for both peers | Does not publish the declared macOS AOT target |
| macOS latest | .NET 10.0.x | CI stamp-verified for both peers | macOS arm64 hosts execute the public AOT gate |

Releases pin XamlX fork commit `0337e9b2f6450ac90cb988a3fac61f36f58c4fcc` and Mono.Cecil 0.11.6.
Injection must precede platform linking, AOT, signing, and packaging. Signed intermediate assemblies
are rejected until an approved post-injection re-signing stage is supplied.

### Reflection Migration

Applications that discover Forma controls with reflection must own the required trim annotations or
replace discovery with an explicit registry. Prefer `typeof(MyControl)`, generic factories, and
generated registration over `Assembly.GetTypes`, string type names, or `Activator.CreateInstance`.
Do not root the complete Forma assembly. A linked application is expected to remove unused public
library APIs; compatibility means statically referenced and generated-XAML members survive and
execute, not that every unused package member remains in the application binary. Consumer-defined enum conversion through
`XamlValueConverter` preserves public fields only; broader consumer reflection remains the
application's trimming contract.

### AOT Diagnostics

NativeAOT outputs do not provide JIT compilation, runtime code generation, or the same managed stack
and dump experience as a normal framework-dependent build. Keep the publish `.pdb`/`.dSYM`, native
symbols, exact package lock/evidence artifact, RID, SDK version, and linker/AOT log for every release.
Crash addresses may require native symbolication and managed generic frames can be less descriptive.
Forma's bounded diagnostics expose capability state, native-font package provenance, and atlas
counters, but do not scan modules or reveal native handles. Reproduce failures with the matching
trim-only profile before comparing its deterministic diagnostics with AOT.

## Package Migration

The unqualified `Forma` and `Forma.Media` IDs are retired before public release. They are not aliases.

For MonoGame:

1. Replace `Forma` with `Forma.MonoGame`.
2. Replace optional `Forma.Media` with `Forma.Media.MonoGame`.
3. Keep exactly one explicit `MonoGame.Framework.<backend>` 3.8.5 application reference.

For FNA:

1. Replace `Forma` with `Forma.FNA`.
2. Replace optional `Forma.Media` with `Forma.Media.FNA`.
3. Add explicit `FNA.NET` 2.2.11.2602 and `FNA.NET.NativeAssets` 2.1.2.2602 references.

Namespaces and control names remain `Forma`; application source changes are not otherwise required.
Package build guards reject duplicate core variants, duplicate media variants, and mismatched
core/media pairs before reference resolution.

Applications adopting template-first controls must also account for changed visual ancestry and
replace custom-drawn widget chrome or C# row factories. See
[xaml-templates-migration.md](xaml-templates-migration.md). `Forma.Xaml.Build` is a private build
dependency. `Forma.Xaml.HotReload` is Debug-only and must be absent from Release, trim, and
NativeAOT outputs. `Forma.DynamicText` remains an independent opt-in companion; core template,
items, data-grid, and virtualization features do not require it.

## Release Gate

The manual Release workflow builds both runtimes, checks API/reference parity, validates all six
packages and isolated consumers, validates licenses and native redistribution notices, and uploads
reviewable artifacts from one commit and version. Its independent macOS arm64 job executes all 20
trim/AOT cells (adding four SVG companion cells) and retains their binaries, manifests, logs, and
diagnostics. It does not publish. Adding any package push path requires separate explicit user
approval.

Authorized targets use the [authorized host checklist](authorized-host-checklist.md). Completing it
requires private SDK, toolchain, deployment, and hardware evidence; this public repository records
only approved capability results.
