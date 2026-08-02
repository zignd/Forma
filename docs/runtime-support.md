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
| MonoGame DesktopGL | CI gate | CI gate | CI gate | Manual gate |
| MonoGame WindowsDX | CI gate | N/A | N/A | N/A |
| MonoGame Native Vulkan | N/A | CI gate | N/A | N/A |
| MonoGame Native Metal | N/A | N/A | Validated + CI gate | Manual gate |
| FNA selected backend | CI gate | CI gate | Validated Metal + CI gate | Manual gate |

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
| Seeking | Reported only when `VideoPlayer.SetPlayPosition` exists | Reported only when `VideoPlayer.SetPlayPosition` exists |
| Track selection | Stored for API compatibility; no built-in track-switch claim | Stored for API compatibility; no built-in track-switch claim |

`VideoPlaybackCapabilities`, `IsPlaybackAvailable`, and `PlaybackUnavailableReason` let applications
present optional behavior without treating graceful unavailability as successful playback.

## Native Dependencies

`FNA.NET` 2.2.11.2602 selects `FNA.NET.NativeAssets` 2.1.2.2602. The package supplies SDL3, FNA3D,
FAudio, Theorafile, and dav1dfile for Windows x64, Linux x64/arm64, and macOS universal deployment.
Empty-cache FNA consumers restore this package explicitly. Catalog and video CI gates exercise
native loading rather than checking filenames alone.

MonoGame applications select their own framework/backend package. Forma packages intentionally do
not impose a transitive MonoGame backend.

## Trimming and AOT

Forma targets `net10.0`. Trim-only and NativeAOT packed consumers are validated on macOS arm64 for
`Forma.MonoGame`, `Forma.FNA`, and their matching `Forma.DynamicText` packages. The gate covers
native-free core, packed-XNB `SpriteFont`, and dynamic-text graphical profiles for both peers. It
publishes from empty package caches, executes every output, rejects Forma-owned `IL2xxx`/`IL3xxx`
warnings, verifies native-free imports, and proves packaged FreeType/HarfBuzz loading:

```sh
bash scripts/test-nativeaot-package-consumer.sh
```

MonoGame graphics profiles currently report one upstream `MonoGame.Framework` `IL2104` summary;
FNA NativeAOT and graphical trim-only profiles report one upstream `FNA.NET` `IL2104` summary. These
classified third-party warnings do not hide any Forma-owned diagnostic. FNA self-contained outputs
also require unversioned aliases for the versioned native libraries supplied by
`FNA.NET.NativeAssets`; the gate stages those aliases and preserves only the XNB reader metadata used
by its packed SpriteFont.

This validation does not cover `Forma.Media`, other RIDs, iOS, Android, or any console. Those modes
remain unsupported until their own packed consumers execute. NativeAOT compatibility is not console
support; authorized hardware and platform-holder validation remain separate. Further work is tracked
in the [NativeAOT and console readiness plan](nativeaot-console-readiness-plan.md).

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

## Release Gate

The manual Release workflow builds both runtimes, checks API/reference parity, validates all six
packages and isolated consumers, and uploads reviewable artifacts from one commit and version. It
does not publish. Adding any package push path requires separate explicit user approval.