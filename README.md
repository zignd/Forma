# Forma

Forma is an independent retained-mode UI toolkit for MonoGame, with a separately versioned core
library, optional media integration, test suite, and component catalog.

The current alpha candidate is validated across DesktopGL, WindowsDX, Native Vulkan, and Native
Metal. NuGet publication remains approval-gated; manual release workflow runs produce installable
package artifacts without publishing them.

Future runtime TTF/OTF work is scoped in the
[dynamic text rendering plan](docs/dynamic-text-rendering-plan.md).

## Build

Build against the supported public MonoGame package:

```sh
dotnet build Forma.slnx
```

DesktopGL is the default. Validate the library packages against every supported reference surface
with `bash scripts/check-backend-references.sh`, or select one directly with
`-p:MonoGamePlatform=DesktopGL`, `WindowsDX`, or `Native`. Forma packages do not impose a transitive
backend; applications must reference one matching `MonoGame.Framework.*` 3.8.5 package.

The catalog build restores the repository-local MGCB 3.8.5 tool and regenerates its Inter UI and
JetBrains Mono code font atlases from repository inputs.

For coordinated development against a local MonoGame checkout:

```sh
dotnet build Forma.slnx -p:MonoGameProjectPath=../MonoGame/MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj
```

Add `Forma.Media` alongside `Forma` when `VideoStreamPlayer` is required. It builds against stock
MonoGame; seeking is available when the runtime MonoGame fork exposes `VideoPlayer.SetPlayPosition`,
or through an injected `IVideoPlaybackBackend`.

## Catalog

Launch the component catalog against stock MonoGame DesktopGL:

```sh
dotnet run --project samples/Forma.Catalog/Forma.Catalog.csproj
```

Capture a bounded basic runtime report with `--metrics <path> --frames <count>`. Fork-specific shader
pipeline/cache metrics remain in the fork; the catalog preserves watched compiled-effect reload and
records UI scale, density-font, viewport, story-count, and reload status metrics.

## Tests

Run the stock-compatible unit and catalog inventory suite:

```sh
dotnet test tests/Forma.Tests/Forma.Tests.csproj
```

Compile and run the graphics fixture where supported:

```sh
dotnet test tests/Forma.RenderTests/Forma.RenderTests.csproj
```

On macOS, NUnit excludes the five graphics tests before fixture setup because SDL graphics-device
creation must run on the process main thread. The project and tests still compile, and Windows/Linux
hosts retain the executable graphics-device path.

Validate package contents, deterministic font artifacts, and an external package consumer with:

```sh
bash scripts/test-package-consumer.sh
```

## Release

Running the `Release` workflow manually builds, validates, and uploads the NuGet packages as a
workflow artifact without publishing them. A matching version tag, such as `v0.1.0-alpha.1`, also
enters the protected `nuget.org` environment and publishes through NuGet trusted publishing.

Before tagging, configure that GitHub environment for required reviewer approval, add the repository
variable `NUGET_USER`, and register the `release.yml` workflow and `nuget.org` environment as a
trusted publishing policy for both package IDs on nuget.org.

## Licensing

Forma-authored portions are available under the MIT License. Adapted and third-party portions keep
their original terms and attribution; see [NOTICE.md](NOTICE.md) and
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).