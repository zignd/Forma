---
title: Troubleshoot XAML build and hot reload
description: Resolve Forma XAML diagnostics, build integration, and replacement failures.
---

# Troubleshoot XAML build and hot reload

## Start with the first `FXAML` diagnostic

Forma reports stable `FXAML` codes with a project-relative path and one-based line and column.
Later messages can be consequences of the first invalid type, member, resource, binding, selector,
or directive. Run the same compiler outside the host to isolate authoring from graphics startup:

```sh
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- \
  validate --require-compiled-bindings --format human samples/Forma.Xaml.Game
```

Use `--format json` or `--format sarif` for automation. The
[XAML language contract](../xaml-language.md) owns diagnostic families and supported syntax.

## XAML file is not compiled

The build integration discovers XAML owned by the compiling project. A file linked from another
directory is not automatically part of that project's `**/*.xaml` discovery. Keep the view in the
owning project or explicitly include it using the build package's supported item contract. Ensure
the `.MonoGame` or `.FNA` XAML build package matches the core peer.

An `x:Class` root must resolve to a compatible, non-sealed code-behind type with one emitted root
class. Constructor-owned properties cannot be assigned from markup. Validate repository build,
incremental, deterministic, and portable-PDB fixtures with:

```sh
make xaml-build-fixtures
```

## Binding fails at compile time

Add `x:DataType` at the binding scope and correct the reported member path. `TwoWay` requires a
writable source member, reverse conversion, and a supported target change adapter. If updates should
wait until editing ends, use `UpdateSourceTrigger=LostFocus`; otherwise select `PropertyChanged`.
See [Data binding](../data-binding.md) for the task workflow.

## Hot reload reports an error or appears stale

Hot reload is Debug-only and must be registered by the host. An invalid edit leaves the currently
attached tree untouched; fix the latest diagnostic and save again. Replacement preserves the host
slot and data context, but not references to old named controls, focus/capture inside the subtree,
arbitrary code-behind state, or animation position.

Release, trimmed, and NativeAOT output must not contain source XAML, watchers, XamlX, Cecil, compiler,
or hot-reload assemblies. Run the peer fixture to check both Debug startup and Release isolation:

```sh
FORMA_RUNTIME=MonoGame bash scripts/check-quick-start.sh
FORMA_RUNTIME=FNA bash scripts/check-quick-start.sh
```
