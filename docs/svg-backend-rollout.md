# SVG Backend Rollout and Qualification

## Default Decision

ThorVG is the default for Forma's Catalog and SVG validation hosts on validated macOS arm64 and
Linux x64 systems. Skia remains the compatibility backend and the explicit rollback renderer.
Windows x64 is untested for ThorVG and continues to select Skia explicitly. No console is qualified.
Existing consumers do not change silently because backend package selection remains explicit.

Legacy `Forma.Svg.MonoGame` and `Forma.Svg.FNA` package IDs remain available through the `0.x`
release line and emit build warnings. They are scheduled for removal in Forma `1.0.0`, after at
least one published warning-bearing migration release. The explicit `Forma.Svg.Skia.*` package IDs
are the long-term Skia surface.

## Measurement Snapshot

The following macOS arm64 development snapshot used .NET SDK 10.0.103, Svg.Skia 5.2.0,
SkiaSharp 4.148.0, ThorVG 1.1.0, Forma ABI 1, and the 67-icon corpus. Times are complete corpus
totals from `make svg-benchmark`, not portable performance guarantees.

| Metric | Skia | ThorVG |
| --- | ---: | ---: |
| Health verification | 17.950 ms | 119.014 ms |
| Parse, 67 icons | 115.610 ms | 0.633 ms |
| Raster, 67 icons | 5.070 ms | 0.680 ms |
| Managed allocations | 3,501,984 bytes | 299,136 bytes |
| Raster output | 274,432 bytes | 274,432 bytes |
| Root MonoGame package | 30,262 bytes | 368,447 bytes |
| Framework-dependent MonoGame publish | 588.5 MiB | 36.4 MiB |
| ThorVG self-contained single-file publish | N/A | 86.0 MiB |
| ThorVG dynamic/static native library | N/A | 334,152 / 467,656 bytes |

The Skia root package is a thin wrapper whose transitive native packages account for most published
size. Package and publish sizes therefore answer different questions. Loaded native code/data is
platform-loader dependent; the shipped ThorVG dynamic image size above is the reproducible public
proxy. The default accepts visual differences and slower cold health initialization in exchange for
the smaller dependency graph, faster corpus parsing/rasterization, and lower managed allocation.

## Public Release Gates

Run `make svg-selection svg-packages svg-compare thorvg-render thorvg-catalog
thorvg-nativeaot thorvg-static-host`. Run `make thorvg-linux` for the clean Linux x64 container
matrix. Native ASan/UBSan findings are fatal. macOS Leaks covers 1,000 document lifetimes. Contact
sheets and comparison JSON identify backend, version, profile, hashes, and out-of-tolerance pixels.

The release archives the ThorVG source commit, Forma ABI header, profile manifest, build inputs,
native symbols, notices, package artifacts, and these measurements. Rollback removes the ThorVG
package and installs the matching Skia package, or selects `BitmapAtlas` for default theme icons.
Arbitrary application SVG failures remain explicit and never trigger cross-backend fallback.

## Qualification Evidence

"Console-ready" means the public source and static desktop reference host pass. It never means
"console-qualified." Authorized qualification must record only policy-approved values:

- target identifier and approval date;
- Forma, ThorVG source, ABI, and profile versions;
- test-manifest hash and pass/fail counts;
- static link/startup, fractional raster, cache pressure, device reset, suspend/resume, repeated
  context creation, shutdown, memory, and frame-budget status.

SDK paths, headers, libraries, symbols, logs, and confidential platform details must not enter this
repository. A target without current evidence is explicitly untested. Private adapters bind the
same ABI and require no core Forma changes; platform owners and integrators own native compilation,
final linking, and qualification.
