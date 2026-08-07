# Forma Catalog

A Storybook-style application for exploring every constructible public Forma control. The left pane
searches the live component inventory, the center renders the selected component, and the right pane
creates property editors for safe writable values. MonoGame and FNA use the same stories and shell.

## Run

From the Forma repository root:

```sh
dotnet run --project samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj \
  -p:FormaRuntime=MonoGame

dotnet run --project samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj \
  -p:FormaRuntime=FNA
```

The hosts use the pinned public runtime packages by default. Set `MonoGameProjectPath` or
`FnaProjectPath` to an absolute project path to replace the selected package across the catalog
graph. For example, the Retina-enabled MonoGame fork can be used directly from a local clone:

```sh
git clone --recurse-submodules --branch develop https://github.com/zignd/MonoGame.git ../MonoGame
MONOGAME_PROJECT="$(pwd)/../MonoGame/MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj"
dotnet run --project samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj \
  -p:FormaRuntime=MonoGame -p:MonoGameProjectPath="$MONOGAME_PROJECT"
```

The catalog opts into `GraphicsDeviceManager.AllowHighDpi` when the selected runtime provides it.
Runtime/backend identity appears in the window title (`Forma Catalog [MonoGame]` or
`Forma Catalog [FNA]`) and metrics.

The hosts verify and activate the matching `Forma.Svg` companion during startup. Select the
`Runtime SVG` story to inspect compiled and file sources, exact-size raster diagnostics, SVG/PNG
default-theme policy, tint, RTL, and rejected external input. Bounded captures can force a policy
with `--theme-icon-policy RuntimeSvg`, `BitmapAtlas`, or `Auto`.

ThorVG is the default SVG backend on the validated macOS arm64 and Linux x64 hosts. Pass
`-p:SvgBackend=Skia` to select the reference renderer explicitly; Windows currently requires this
rollback because ThorVG Windows native assets are not yet qualified or distributed.

Search the catalog for the `Typography` stories to exercise dynamic sizes, display density,
fallback, OpenType features, bidi ordering, atlas diagnostics, failure recovery, and SpriteFont
compatibility. The header's `Dynamic text` toggle is the shared rollback switch for both hosts.

Use `make catalog-monogame-local` for the default sibling clone at `../MonoGame`, or
`make catalog-fna-local` for `../FNA/FNA.Core.csproj`. Override `MONOGAME_PROJECT` or
`FNA_PROJECT` when a fork lives elsewhere.

For a bounded basic runtime capture:

```sh
dotnet run --project samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj \
  -p:FormaRuntime=MonoGame -- \
  --metrics Artifacts/catalog.json --frames 120 --display-scale 2
```

Add `--screenshot <png>` for a final-frame PNG and `--render-output <json>` for deterministic
dimensions, exact pixel hash, non-background coverage, and RGBA totals. Approved peer captures are
stored at `docs/images/catalog-monogame.png` and `docs/images/catalog-fna.png`.

The report records the configured backend, rendered frame count, story count, logical viewport,
display scale, density-font selection, and watched-effect status. MonoGame accepts its matching
platform-specific MGFX artifact through `--watch-effect <mgfxo>`. FNA requires an FXC-generated
Direct3D Effects Framework `.fxb`; MGFX is intentionally rejected by FNA. Fork-specific shader
pipeline/cache diagnostics are not part of the stock-compatible catalog.

Run `bash scripts/check-catalog-smoke.sh` and `FormaRuntime=FNA bash
scripts/check-catalog-smoke.sh` on a graphical host to compare each three-frame forced-2x run with
`catalog-metrics-baseline.json`.

Run `bash scripts/check-catalog-render-parity.sh` to render both hosts at 1440x900 and enforce the
documented 1% aggregate image tolerance while retaining exact runtime hashes for diagnostics.

Run the published Native Vulkan backend and runtime packages with:

```sh
MonoGamePlatform=Native CatalogBackend=Vulkan bash scripts/check-catalog-smoke.sh
```

The currently unpublished macOS Metal runtime can be validated after building MonoGame's `Build
Native Metal` target by setting `NativeRuntimePath` to its `libmgruntime.dylib` output and
`CatalogBackend=Metal`. Because MGCB 3.8.5 does not define a `Native` content platform, Native
catalog builds compile their assets with the compatible `DesktopVK` content profile.

The build uses the repository-local MGCB 3.8.5 tool to generate 1x and 2x Inter UI atlases and
JetBrains Mono code atlases from the inputs under `tests/Assets/Fonts`. Canonical copies remain
there for render tests and are byte-compared with fresh Release outputs by
`scripts/test-package-consumer.sh`. Both OFL-1.1 licenses are copied beside the runtime assets.