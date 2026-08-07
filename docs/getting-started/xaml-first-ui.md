---
title: Build your first UI in XAML
description: Compile a typed Forma XAML view and run it on MonoGame or FNA.
---

# Build your first UI in XAML

This route renders the same first interface through Forma's build-time XAML compiler. It uses an
`x:Class` root, typed one-way and two-way bindings, namescope lookup, and a button event. Release
builds contain injected view construction and binding code, not source XAML or a runtime reader.

## Run the compiled view

```sh
# MonoGame
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj \
  --configuration Release -p:FormaRuntime=MonoGame -- --xaml

# FNA
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj \
  --configuration Release -p:FormaRuntime=FNA -- --xaml
```

The source checkout references `Forma.Xaml.Build` as a private build project. After the first NuGet
preview is indexed, package consumers will pair `Forma.Xaml.Build.MonoGame` or
`Forma.Xaml.Build.FNA` with the matching core package and version.

## Define the view

Docfx stages this block from `samples/Forma.QuickStart/FirstView.xaml`, the file compiled by both
runtime hosts:

[!code-xml[](../_generated/examples/FirstView.xaml)]

`x:DataType` makes every binding compile against `FirstViewModel`. The `Name` field updates on each
text change through a two-way binding. `Greeting` is one-way and refreshes when the view model raises
`PropertyChanged`.

## Load and wire the class

The code-behind and view model are also compiled directly by both hosts:

[!code-csharp[](../examples/xaml-first-ui.cs)]

`FirstView` derives from non-sealed `BoxContainer`; its constructor chooses vertical orientation
before `FormaXamlLoader.Load(this)` populates it. XAML cannot set `Orientation` because that property
is constructor-owned. The generated namescope resolves `GreetButton`, and the event calls the typed
view model.

## Debug hot reload

Debug builds reference `Forma.Xaml.HotReload` and copy development XAML beside the executable. Run:

```sh
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj \
  --configuration Debug -p:FormaRuntime=MonoGame -- --xaml
```

Use the FNA host and `FormaRuntime=FNA` for its peer. While the process runs, edit
`samples/Forma.QuickStart/FirstView.xaml`; the registered service recompiles and replaces the root at
a frame boundary. Diagnostics are written by the service/compiler instead of being deferred to
Release.

Hot reload is a development aid, not the production loading model. The clean-cache gate separately
builds and starts Debug, then verifies Release output excludes `Forma.Xaml.HotReload.dll`,
`Forma.Xaml.Compiler.dll`, `XamlX.dll`, and `XamlX.IL.Cecil.dll`:

```sh
FORMA_RUNTIME=MonoGame bash scripts/check-quick-start.sh
FORMA_RUNTIME=FNA bash scripts/check-quick-start.sh
```

For namespaces, resources, templates, selectors, binding modes, diagnostics, command-line tooling,
and compatibility rules, continue with the [Forma XAML language contract](../xaml-language.md).
