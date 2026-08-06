# ADR 0006: Bounded Runtime SVG Architecture

- Status: Accepted
- Date: 2026-08-06

## Context

Forma needs sharp application and default-theme vector images at fractional and Retina display
scales on MonoGame and FNA. Core-only applications must remain free of SVG managed/native
requirements, untrusted SVG must not perform external I/O, and warm frames must not call the backend.
The existing default-theme PNG atlases must remain a deterministic rollback path.

Svg.Skia 5.2.0 was compared with SVG.NET. Svg.Skia provides one maintained Skia raster path across
the declared desktop systems and does not rely on Windows-only `System.Drawing.Common`. The existing
icon pipeline already uses the same pinned renderer to produce canonical PNG atlases.

## Decision

Core defines immutable `ScalableImageSource` / `SvgImageSource`, bounded preflight validation, an
opaque internal backend contract, exact-size CPU/GPU cache contracts, and consumer APIs. It exposes
no Svg.Skia, SkiaSharp, or backend-native type. Runtime-matched `Forma.Svg.MonoGame` and
`Forma.Svg.FNA` companions own Svg.Skia, native assets, backend installation, and authoritative
default-theme SVG resources.

Validation runs before backend parsing. It rejects external URLs/files/stylesheets/documents,
embedded raster data, scripts, events, animation, foreign objects, text/fonts, filters, unsupported
compositing, duplicate IDs, and cyclic references. Source bytes, XML depth, elements, attributes,
text, local references, dimensions, and pixel area have finite configurable limits. Unsupported or
forbidden content fails with a source-specific `SvgLoadException`; no browser or network fallback is
allowed.

The renderer parses each content identity once and rasterizes to premultiplied RGBA8. Cache keys use
content identity, exact transformed physical width and height, and render options. Exact integer
pixels were selected over scale quantization because the selected 1x, 1.25x, 1.5x, 1.75x, 2x, and
2.5x fixtures remain pixel-stable across peers and cache cardinality is bounded by LRU budgets.
Transparent atlas padding prevents linear sampling bleed.

CPU documents and raster pages survive graphics reset. GPU atlases are shared by graphics device,
created/uploaded on the render thread, and disposed with the device. A bounded zero-owner cache may
remain reusable until device disposal. Immediate last-context disposal was rejected because FNA's
SDL_GPU Metal path applies samplers lazily and exposes no portable post-present fence; queued texture
destruction could otherwise invalidate sampler state.

`ThemeIconRenderingPolicy.BitmapAtlas` is the initial default. `RuntimeSvg` and `Auto` use companion
sources only when the provider and backend are healthy. Every runtime SVG default icon carries its
PNG atlas region as a per-icon fallback. Application SVGs without a backend report an actionable
setup error instead of silently changing content.

Compiled XAML treats relative SVG paths as statically validated assets. MSBuild assigns a logical
name from assembly name plus SHA-256 of the normalized project-relative path and embeds source bytes
without MGCB/XNB. SRE hot reload resolves against the development XAML file and creates a new content
identity when bytes change.

## Consequences

Core package size and native-free deployment remain stable. Companion applications accept the
managed/native size of Svg.Skia and SkiaSharp. Cold SVG work is visible and bounded; warm exact-size
lookups perform no parse, rasterization, upload, texture creation, or managed allocation. Default
icons can always be rolled back globally to PNG.

The supported contract is a tested SVG subset, not browser SVG. Platform support is limited to the
runtime/RID cells validated by package, trim, AOT, and render gates. New SVG features require
preflight policy, hostile-input coverage, peer pixel review, and updated documentation before they
become supported.

## Verification

Run from the repository root:

```sh
bash scripts/test-dynamic-render-smoke.sh
bash scripts/test-package-consumer.sh
bash scripts/check-catalog-render-parity.sh
dotnet test tests/Forma.Tests/Forma.Tests.csproj -c Release -p:FormaRuntime=MonoGame --filter Svg
dotnet test tests/Forma.Tests/Forma.Tests.csproj -c Release -p:FormaRuntime=FNA --filter Svg
```

Capture the Catalog baseline at a selected scale and policy:

```sh
dotnet run --project samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj -c Release -- \
  --story "Runtime SVG" --frames 120 --display-scale 1.25 \
  --theme-icon-policy RuntimeSvg --screenshot docs/images/runtime-svg-monogame.png \
  --render-output Artifacts/runtime-svg-monogame.json
```

Use the FNA host and output names for the peer capture. The approved macOS arm64 baseline permits the
Catalog's existing aggregate tolerance; focused SVG smoke pixels must hash identically between
MonoGame and FNA at every selected scale.
