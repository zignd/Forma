# Forma Catalog

A Storybook-style MonoGame application for exploring every constructible public Forma control. The
left pane searches the live component inventory, the center renders the selected component, and the
right pane creates property editors for safe writable values.

## Run

From the Forma repository root:

```sh
dotnet run --project samples/Forma.Catalog/Forma.Catalog.csproj
```

The default build uses the public `MonoGame.Framework.DesktopGL` package. Coordinated development can
use the same `MonoGameProjectPath` override documented in the root README and set the displayed backend
label with `-p:CatalogBackend=<name>`.

For a bounded basic runtime capture:

```sh
dotnet run --project samples/Forma.Catalog/Forma.Catalog.csproj -- \
  --metrics Artifacts/catalog.json --frames 120 --display-scale 2
```

The report records the configured backend, rendered frame count, story count, logical viewport,
display scale, density-font selection, and watched-effect status. Pass `--watch-effect <mgfxo>` to
reload a compiled effect when its file changes. Fork-specific shader pipeline/cache diagnostics are
not part of the stock-compatible catalog.

Run `bash scripts/check-catalog-smoke.sh` on a graphical host to compare a three-frame forced-2x run
with `catalog-metrics-baseline.json`.

Run the published Native Vulkan backend and runtime packages with:

```sh
MonoGamePlatform=Native CatalogBackend=Vulkan bash scripts/check-catalog-smoke.sh
```

The currently unpublished macOS Metal runtime can be validated after building MonoGame's `Build
Native Metal` target by setting `NativeRuntimePath` to its `libmgruntime.dylib` output and
`CatalogBackend=Metal`. Native catalog builds copy the canonical XNBs because MGCB 3.8.5 does not
define a `Native` content platform.

The build uses the repository-local MGCB 3.8.5 tool to generate `Catalog.xnb` and `Catalog@2x.xnb`
from the IBM Plex Sans source and `.spritefont` descriptions under `tests/Assets/Fonts`. Canonical
copies remain there for render tests and are byte-compared with fresh Release outputs by
`scripts/test-package-consumer.sh`. The OFL-1.1 license is copied beside the runtime assets.