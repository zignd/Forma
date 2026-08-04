# ADR 0002: Dynamic Text Dependencies and Packaging

- Status: Accepted for the desktop spike; Linux arm64 packaging remains a release blocker
- Date: 2026-08-01
- Owners: Forma maintainers
- Unicode baseline: 17.0.0

## Decision

Forma will use maintained managed bindings over pinned native libraries:

| Capability | Selection | Native/upstream version | License | Upstream |
| --- | --- | --- | --- | --- |
| Rasterization | `FreeTypeSharp` 3.1.0 | FreeType 2.13.2 | Binding: MIT; FreeType: FTL or GPL-2.0 | [FreeTypeSharp](https://github.com/ryancheung/FreeTypeSharp), [FreeType](https://freetype.org/) |
| Shaping | `HarfBuzzSharp` 14.2.1.1 | HarfBuzz 14.2.1 | MIT | [HarfBuzzSharp](https://github.com/mono/SkiaSharp/tree/main/binding/HarfBuzzSharp), [HarfBuzz](https://github.com/harfbuzz/harfbuzz) |
| Unicode algorithms | Forma managed code generated from Unicode 17.0.0 data | Unicode 17.0.0 | Unicode Data Files and Software License | [Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/) |

`FreeTypeSharp` is a thin, generated binding. Forma wraps it behind safe handles and internal
services rather than exposing its pointers or structs. `HarfBuzzSharp` supplies managed lifetime
wrappers and a byte-backed shaping API; Forma still owns the source bytes for each face lifetime.

Forma-owned interop is limited to lifetime, error translation, security limits, and any missing
native RID assets. Forma will not fork either complete managed binding unless maintenance or
security response becomes inadequate.

## Alternatives Evaluated

- Direct P/Invoke and generated bindings owned entirely by Forma provide maximum packaging control,
  but duplicate active binding work and create a large unsafe API maintenance surface.
- `FreeTypeSharp` alone covers the intended API with current FreeType and most desktop assets. Its
  missing Linux arm64 binary is a bounded packaging gap that Forma can fill from the same pinned
  FreeType source and build options.
- `HarfBuzzSharp` is actively maintained by Microsoft/Xamarin owners, has a reserved NuGet prefix,
  and supplies official native packages for all desktop architectures in this plan.
- `SharpFont`, `FriBidiSharp`, `Bidi`, `Unicode.Bidi`, and low-adoption UAX #14 packages were rejected
  because of stale native baselines, incomplete algorithm coverage, low maintenance signals, or
  nondeterministic platform dependencies.
- ICU was rejected for the initial implementation because it adds a second large native deployment,
  data-version coupling, and mobile static-link complexity. A future optional locale-tailoring layer
  may use ICU without changing Forma's layout contract.
- MonoGame Content Pipeline FreeType code is useful prior art, but its build-time assemblies and
  backend assumptions are not runtime dependencies and cannot be used by the FNA peer package.

## Unicode Strategy

Forma pins Unicode 17.0.0 and generates compact managed tables from the versioned UCD inputs. The
source URLs and SHA-256 values are committed in `assets/unicode/manifest.json`; release builds consume generated
source and never download data.

- UAX #9 full bidirectional resolution uses `DerivedBidiClass.txt`, `BidiBrackets.txt`,
  `BidiMirroring.txt`, `BidiTest.txt`, and `BidiCharacterTest.txt`.
- UAX #29 extended grapheme clusters use `GraphemeBreakProperty.txt`, emoji properties, Indic
  conjunct properties, and `GraphemeBreakTest.txt`.
- UAX #24 script runs use `Scripts.txt`, `ScriptExtensions.txt`, and property aliases. Common and
  Inherited values resolve at grapheme granularity before fallback and shaping.
- UAX #14 default line breaking uses `LineBreak.txt`, East Asian width, emoji properties, and
  `LineBreakTest.txt`. Complex-context `SA` runs initially use conservative emergency wrapping at
  grapheme boundaries; dictionary tailoring is a documented extension point and is not called UAX
  #14 default conformance.
- UTF-16 is decoded with `System.Text.Rune`; malformed sequences produce U+FFFD with stable source
  ranges. `StringInfo` is not the normative implementation because its Unicode data may vary with
  the runtime or operating system.

The Unicode algorithms and generated tables are shared managed code. MonoGame, FNA, operating
system globalization mode, and installed system fonts therefore cannot change layout decisions.

## Package Shape

`Forma.MonoGame` and `Forma.FNA` keep the shared `UIFont`, retained layout, controls, and
`SpriteFontAdapter` contracts without managed or native dynamic-text dependencies. Applications
opt into runtime font loading, shaping, and rasterization with the matching
`Forma.DynamicText.MonoGame` or `Forma.DynamicText.FNA` companion package.

Both companions depend on the same managed versions. `HarfBuzzSharp.NativeAssets.Linux` 14.2.1.1
is explicit because `HarfBuzzSharp` itself references macOS and Win32 assets but not Linux assets.
FreeType and HarfBuzz notices are distributed with both companions. During the package-boundary
migration, dynamic public types remain in the core assembly and require the companion at runtime;
they move to the companion assembly before this checklist item is considered complete.

Applications supply font bytes through Forma APIs or embedded/application content. Forma does not
load developer-machine or system fonts implicitly.

## Desktop RID Matrix

| RID | FreeType 2.13.2 | HarfBuzz 14.2.1 | MonoGame peer | FNA peer |
| --- | --- | --- | --- | --- |
| `win-x64` | `FreeTypeSharp` asset | Win32 native asset | CI gate | CI gate |
| `win-arm64` | `FreeTypeSharp` asset | Win32 native asset | Dependency-ready; runtime gate required | Unsupported by selected FNA.NET native package |
| `linux-x64` | `FreeTypeSharp` asset | Linux native asset | CI gate | CI gate |
| `linux-arm64` | **Forma-built asset required** | Linux native asset | Runtime gate required | FNA native-ready; Forma graphics gate required |
| `osx-x64` | universal `osx` asset | universal `osx` asset | Manual gate | Manual gate |
| `osx-arm64` | universal `osx` asset | universal `osx` asset | CI gate | CI gate |

The Linux arm64 FreeType asset must be built reproducibly from FreeType 2.13.2, retain the library
name expected by `FreeTypeSharp`, carry the FreeType license, and pass the same ABI smoke as bundled
assets before either peer claims that RID. No developer system library is an accepted fallback.

Android ABIs and iOS/tvOS architectures are deferred because Forma declares no mobile
runtime/platform compatibility pair. `FreeTypeSharp` and `HarfBuzzSharp` publish mobile assets, but
that is not a Forma support claim. Mobile adoption requires explicit MonoGame and FNA runtime
matrices, static-link review, store compliance, and device tests.

## Deployment Constraints

- Trimming and NativeAOT are executed for both peers on `osx-arm64`. Dedicated packed consumers
  preserve native entry points and pass multilingual layout, native-load, atlas-render,
  JIT/AOT-diagnostic-parity, and package-content tests. Other RIDs remain unsupported until the same
  evidence exists. The public API avoids reflection-based activation.
- Desktop uses dynamic native libraries. Linux/macOS library names and Windows DLL search behavior
  are validated from an empty NuGet cache; system installation is neither required nor searched by
  Forma.
- macOS application publishers own final bundle placement, hardened-runtime code signing,
  notarization, and signing of nested dylibs. The distributed universal dylibs contain x64 and
  arm64 slices.
- Sandboxed applications load only packaged native libraries and caller-provided font bytes. Forma
  requires no font-directory, network, temporary-file, or executable-memory entitlement.
- iOS/tvOS static linking and symbol preservation are not supported until mobile is declared. The
  desktop dynamic packages must not be reused as an implied mobile solution.

## Updates and Security Response

Forma maintainers own version pins, release-note and ABI review, license/notice updates, native
asset inspection, empty-cache restores, and the complete runtime matrix. Dependabot alerts, GitHub
security advisories, NuGet owner changes, and upstream FreeType/HarfBuzz advisories are reviewed as
release blockers.

A security update receives a patch release after both peer builds, native-load smoke, malformed-font
limits, Unicode conformance tests, package inspection, and catalog rendering pass. A native or
Unicode version never floats independently at restore or runtime.

## Consequences

The desktop spike can proceed on macOS with maintained packages and no system installation. Core
packages remain native-text-free; matching companions carry dynamic-text dependencies and notices.
Linux arm64 cannot be marked supported until the supplemental FreeType binary and CI cell exist,
and mobile remains explicitly outside the matrix.
