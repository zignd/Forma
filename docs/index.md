---
title: Forma documentation
description: Learn to build game user interfaces with Forma on MonoGame and FNA.
---

# Forma documentation

Forma is a retained-mode .NET user-interface toolkit for games built on MonoGame or FNA. The
project is currently preparing its first public preview; package commands will become the canonical
installation path after the approved packages are published to NuGet.org.

## Start here

- **Evaluate Forma:** browse the [runtime support matrix](runtime-support.md), then run the Catalog
  from the repository README.
- **Build with XAML:** read the [Forma XAML language contract](xaml-language.md) and the hot-reload
  guidance in the Catalog and sample projects.
- **Add text or SVG:** choose [dynamic text](dynamic-text.md) or a
  [runtime SVG backend](runtime-svg.md) after the core UI is working.
- **Find an API:** use the <xref:Forma> reference for public types and members.
- **Contribute:** follow the repository build and validation commands until the dedicated contributor
  guide is available.

## Choose a runtime

Forma publishes binary-incompatible peer packages for MonoGame and FNA. Keep every Forma package in
one application on the same runtime family and version. The public API is intentionally equivalent;
use the runtime already selected by the host game.

| Host | Core package | Compiled XAML package |
| --- | --- | --- |
| MonoGame | `Forma.MonoGame` | `Forma.Xaml.Build.MonoGame` |
| FNA | `Forma.FNA` | `Forma.Xaml.Build.FNA` |

See [runtime support and migration](runtime-support.md) for the validated backend and platform matrix.
