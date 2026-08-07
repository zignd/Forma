# Runtime Acquisition Decision

## Status

Accepted for implementation. Revisit before the first stable release and whenever either pinned
runtime changes its package, license, native asset, or support policy.

## Selected Baselines

| Runtime | Package | Version | Framework assembly |
| --- | --- | --- | --- |
| MonoGame | `MonoGame.Framework.<backend>` | 3.8.5 | `MonoGame.Framework` |
| FNA | `FNA.NET` | 2.2.11.2602 | `FNA.NET` |
| FNA native assets | `FNA.NET.NativeAssets` | 2.1.2.2602 | Native libraries |

`FNA.NET` is selected as the reproducible FNA distribution candidate because it supports ordinary
NuGet restore, targets .NET 9 and .NET Standard 2.0, and supplies native assets for the declared
desktop matrix. It is an opinionated fork rather than an official FNA-XNA package. Forma therefore
keeps the framework reference replaceable through `FnaProjectPath` and does not rely on fork-only
extensions in shared code except at reviewed FNA adapter or host boundaries.

## Native Assets

`FNA.NET` 2.2.11.2602 depends on `FNA.NET.NativeAssets` 2.1.2.2602, which identifies its upstream
native baseline as FNA 26.02. The package contains native assets for Windows x64, Linux x64 and
arm64, and macOS. Each desktop set includes FAudio, FNA3D, SDL3, Theorafile, and dav1dfile, so the
selected distribution owns the default graphics, audio, windowing, Theora, and AV1 deployment
path. Forma does not copy those binaries into source or publish a second native bundle.

The Forma maintainers own version pin updates, restore and RID validation, release-note review,
license/notice review, and response to NuGet or GitHub security advisories. A runtime update must
pass both runtime builds, assembly-reference isolation, tests, catalog smoke checks, media fixture
checks, package inspection, and clean consumer restores before the pins change.

## Package IDs

The initial public release contains fourteen peer package IDs. The machine-readable authority is
[`scripts/release-packages.json`](https://github.com/zigrok/Forma/blob/main/scripts/release-packages.json),
which the release workflow uses for package construction, artifact validation, publication, and
post-publication restore checks. All manifest packages use the same Forma version and commit.

| Capability | MonoGame | FNA |
| --- | --- | --- |
| Core | `Forma.MonoGame` | `Forma.FNA` |
| Dynamic text | `Forma.DynamicText.MonoGame` | `Forma.DynamicText.FNA` |
| Media | `Forma.Media.MonoGame` | `Forma.Media.FNA` |
| SVG with Skia | `Forma.Svg.Skia.MonoGame` | `Forma.Svg.Skia.FNA` |
| SVG with ThorVG | `Forma.Svg.ThorVG.MonoGame` | `Forma.Svg.ThorVG.FNA` |
| Compiled XAML build | `Forma.Xaml.Build.MonoGame` | `Forma.Xaml.Build.FNA` |
| XAML hot reload | `Forma.Xaml.HotReload.MonoGame` | `Forma.Xaml.HotReload.FNA` |

The unused `Forma.Svg.MonoGame` and `Forma.Svg.FNA` compatibility identities are intentionally
excluded. Package availability is rechecked immediately before publication rather than asserted by
this decision record.

## Local Overrides

`MonoGameProjectPath` and `FnaProjectPath` replace the corresponding package reference with a local
project reference throughout library, catalog, and test graphs. Pass absolute paths because MSBuild
resolves relative `ProjectReference` values from each owning project directory. Package acquisition
remains the reproducible default used by CI and release validation. Local overrides are development
inputs and must not affect package dependency metadata.
