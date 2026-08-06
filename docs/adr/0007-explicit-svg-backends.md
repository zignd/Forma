# ADR 0007: Explicit Process-Wide SVG Backends

- Status: Accepted
- Date: 2026-08-06

## Context

Forma's bounded SVG source, cache, XAML, control, and GPU upload contracts are backend-neutral, but
the original `Forma.Svg.*` package identity implicitly selected Svg.Skia. Skia is the established
desktop reference renderer, while its native distribution model is not a basis for restricted or
console support. ThorVG 1.1.0 provides a small CPU/SVG source build and can render copied in-memory
SVG into caller-owned memory.

Three choices were evaluated:

| Choice | Desktop quality | Size/source build | Static linking | Risk |
| --- | --- | --- | --- | --- |
| Keep Skia only | Proven reference output | Largest dependency graph | Not established for restricted hosts | No console-readiness path |
| Replace Skia with ThorVG | Smaller native engine | Reproducible source build | Direct | Immediate output and migration risk |
| Explicit backend packages | Keeps Skia reference and adds ThorVG | Consumers carry one engine | ThorVG can use dynamic or static ABI | Two conformance/build surfaces |

The measured macOS arm64 67-icon release run recorded Skia/ThorVG health initialization of
526.37/132.84 ms, parse time of 115.84/0.49 ms, raster time of 1.95/0.54 ms, and managed allocation
of 3,495,792/299,136 bytes. These are local cold baselines, not universal performance promises.
The stripped Forma ThorVG dynamic library is 326 KiB on macOS arm64 and 404 KiB on Linux x64; the
dead-stripped macOS static smoke executable is 302 KiB.

## Decision

Forma ships independently installable Skia and ThorVG backend projects. Exactly one backend is
installed before the first parse. Selection is explicit, process-wide, trim-safe, and immutable.
There is no reflection discovery, per-document choice, or automatic fallback between native engines.

The stable IDs are `skia` and `thorvg`; both implement Runtime SVG Profile v1. Backend health reports
the ID, display/version strings, profile, native source, link mode, bounded diagnostic, and tested
features. Tests compare implementations in separate processes.

ThorVG is consumed through ABI 1 in `native/Forma.ThorVG`. The Forma ABI is narrower than both the
ThorVG C++ and upstream C APIs so Forma owns error categories, exact buffers, RGBA normalization,
version checks, export visibility, and static-host integration. The native side receives bytes and
caller-owned output only and is built with file I/O, threads, unrelated loaders, tools, exceptions,
and RTTI disabled.

`Forma.Svg.Skia.*` and `Forma.Svg.ThorVG.*` are the explicit packages. `Forma.Svg.*` remains a
warning-producing compatibility package that depends on Skia for one migration window. Core owns
the authoritative 67 SVG theme sources but no renderer dependency.

## Support Scope

The initial ThorVG experimental matrix is macOS arm64 and Linux x64. Windows x64 is not in the
initial declared matrix because the intended MSVC/CRT build has not run. No console target is
qualified or claimed. The static reference host establishes console-ready architecture only;
a console becomes qualified only after authorized SDK, hardware, lifecycle, and release evidence.

## Consequences

Consumers can remove Skia completely when selecting ThorVG, and existing compatibility consumers do
not silently change output. Forma must maintain two isolated package/build/conformance lanes. Any
new profile feature must pass both lanes before first-party assets use it. Backend-specific
extensions are not part of Forma's public SVG contract.

## Verification

```sh
make thorvg-spike
make thorvg-linux
make svg-selection
make svg-benchmark
dotnet test tests/Forma.Tests/Forma.Tests.csproj -c Release -p:FormaRuntime=MonoGame --filter SvgBackendTest
dotnet test tests/Forma.ThorVG.Tests/Forma.ThorVG.Tests.csproj -c Release -p:FormaRuntime=MonoGame
```
