---
title: Forma documentation
description: Learn to build game user interfaces with Forma on MonoGame and FNA.
---

# Forma documentation

Forma is a retained-mode .NET user-interface toolkit for games built on MonoGame or FNA. The
project is currently preparing its first public preview; package commands will become the canonical
installation path after the approved packages are published to NuGet.org.

## Start here

- **Evaluate Forma:** run the [C# first UI](getting-started/csharp-first-ui.md), then browse the
  [runtime support matrix](runtime-support.md) and Catalog.
- **Build with XAML:** read the [Forma XAML language contract](xaml-language.md) and the hot-reload
  guidance in the Catalog and sample projects.
- **Add text or SVG:** choose [dynamic text](dynamic-text.md) or a
  [runtime SVG backend](runtime-svg.md) after the core UI is working.
- **Find an API:** use the <xref:Forma> reference for public types and members.
- **Contribute:** start with the
  [contribution guide](https://github.com/zigrok/Forma/blob/main/CONTRIBUTING.md), then use the
  [architecture map](contributor-architecture.md) to choose focused validation.
- **Prepare a release:** follow [release operations](release-operations.md) for tagged publication,
  correction, symbols, ownership, and credential recovery.

## Choose a runtime

Forma publishes binary-incompatible peer packages for MonoGame and FNA. Keep every Forma package in
one application on the same runtime family and version. The public API is intentionally equivalent;
use the runtime already selected by the host game.

| Host | Core package | Compiled XAML package |
| --- | --- | --- |
| MonoGame | `Forma.MonoGame` | `Forma.Xaml.Build.MonoGame` |
| FNA | `Forma.FNA` | `Forma.Xaml.Build.FNA` |

See [runtime support and migration](runtime-support.md) for the validated backend and platform matrix.

## Choose C# or XAML

| Route | Start here | Use it when |
| --- | --- | --- |
| C# | [Build your first UI in C#](getting-started/csharp-first-ui.md) | The host creates and wires controls directly |
| XAML | [Build your first UI in XAML](getting-started/xaml-first-ui.md) | The project wants compiled declarative views, typed bindings, or Debug hot reload |

Both routes use the same controls, layout engine, themes, input, and runtime peer selection. Start
with C# when evaluating the minimum host responsibilities; add XAML when its build-time model fits
the project.
