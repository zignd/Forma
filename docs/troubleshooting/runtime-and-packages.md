---
title: Troubleshoot runtime and packages
description: Resolve Forma peer selection, restore, native assets, and host support failures.
---

# Troubleshoot runtime and packages

## Mixed MonoGame and FNA assemblies

**Symptoms:** assembly load/type identity failures, build guards naming both peers, or a consumer
that compiles but fails when the framework initializes.

Choose one runtime for the complete process. Pair every `.MonoGame` Forma package with MonoGame, or
every `.FNA` package with FNA. Do not reference both core peers, both XAML build peers, or both hot
reload peers. Remove stale `bin` and `obj` directories and regenerate lock files after switching.

In a source checkout, make the selection explicit:

```sh
dotnet build Forma.slnx -p:FormaRuntime=MonoGame
dotnet build Forma.slnx -p:FormaRuntime=FNA
```

The [runtime acquisition decision](../runtime-acquisition.md) owns current package identities and
pins. The [runtime support matrix](../runtime-support.md) owns validated host combinations.

## Restore or package not found

The first public preview is not published yet. Repository QuickStarts intentionally use project
references; package commands become canonical only after NuGet indexing is verified. For a source
consumer, initialize submodules and restore the selected graph. For a package consumer after
publication, clear only the affected package/version from the cache and retry against NuGet.org.

Validate repository packaging and isolated consumers with:

```sh
make packages
```

Do not add `Forma.Svg.MonoGame` or `Forma.Svg.FNA` as compatibility guesses; those IDs are excluded.
Select an explicit Skia or ThorVG backend from the [SVG migration guide](../svg-backend-migration.md).

## Native library missing or rejected

FNA applications require the matching `FNA.NET.NativeAssets` distribution. Dynamic text additionally
requires the RID assets used by FreeType and HarfBuzz. SVG backends have their own native deployment
contracts. Preserve the first bounded `FontLoadException` or `SvgRuntime.Health.Diagnostic`, then
compare the deployed RID files with the selected package.

Repository maintainers can reproduce dynamic-font failure classes in fresh processes:

```sh
make native-font-failures
```

Do not copy arbitrary native binaries into output to silence loading errors; version, architecture,
ABI, and license provenance are part of the contract. See [Dynamic text](../dynamic-text.md) and
[SVG backend selection](../svg-backend-migration.md).

## Unsupported or unavailable capability

Video, dialogs, AOT, native SVG, and specialized hosts have explicit support boundaries. Query the
public capability/health state when available rather than treating an unavailable backend as an
empty result. Check [Runtime support](../runtime-support.md) before filing a portability defect.
