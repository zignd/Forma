# Forma XAML Language Contract

## Status and Scope

This document defines the Forma XAML v1 source language. It is the contract used by the runtime,
compiler, MSBuild integration, command-line validator, language server, tests, and samples.

Forma XAML is a Forma-native declarative language built on XML and selected XAML 2006 concepts. It
does not promise source compatibility with WPF, Avalonia, MAUI, UWP, WinUI, or any other XAML
framework. A construct is supported only when this document defines it and the Forma compiler
accepts it.

Release builds compile XAML and typed bindings to IL. Generated views and the Forma runtime do not
depend on XamlX, Mono.Cecil, a runtime XAML reader, reflection-based binding, or source XAML.
Development hot reload is an opt-in, non-trimmed, non-NativeAOT feature with separate compiler
dependencies.

## Namespaces and Project Items

The Forma namespace URI is fixed as:

```xml
xmlns="https://forma.dev/xaml"
```

The XAML language namespace is:

```xml
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
```

Application types use `clr-namespace:` declarations. An optional assembly component follows a
semicolon:

```xml
xmlns:views="clr-namespace:Game.Views"
xmlns:vm="clr-namespace:Game.ViewModels;assembly=Game.Core"
```

MSBuild treats project `.xaml` files as `@(FormaXaml)` by default, subject to the SDK's normal
default-item exclusions for output and intermediate directories. Projects may explicitly include
or remove items. Logical source identity is the normalized, project-relative path using `/`; this
identity is also used by diagnostics, incremental compilation, and hot-reload registration.

Each compiled view has one root element. Multiple implicit XAML files may not populate the same
root CLR type. Resource dictionaries without `x:Class` are allowed and compile as resources rather
than views.

## Project Setup and Build Properties

Use matching runtime and build peers. The build package is private because it contributes only
MSBuild targets and compiler tools:

```xml
<ItemGroup>
  <PackageReference Include="Forma.MonoGame" Version="0.1.0-alpha.1" />
  <PackageReference Include="Forma.Xaml.Build.MonoGame"
                    Version="0.1.0-alpha.1"
                    PrivateAssets="All" />
</ItemGroup>
```

Replace both `.MonoGame` suffixes with `.FNA` for FNA. Do not mix peers. The package imports the
compiler automatically and includes every project `.xaml` file as `@(FormaXaml)`.

MSBuild properties:

- `FormaXamlRequireCompiledBindings` requires inherited `x:DataType`; it defaults to `true` in
  Release and `false` in Debug.
- `FormaXamlValidateOnly=true` validates without injecting IL.
- `FormaXamlHotReload=true` copies Debug source XAML for the development host. It has no effect on
  Release output.
- `FormaXamlIntermediateDirectory` and `FormaXamlDevelopmentOutputDirectory` override generated
  intermediate and Debug source-copy locations.

Compilation is incremental over XAML, references, the target assembly, and compiler task. Release
outputs are deterministic and portable-PDB diagnostics retain source file, line, and column.

## Object Construction and Content

An element names a public CLR type in its XML namespace. Attribute syntax sets public properties
or subscribes public events. Property-element syntax uses `Owner.Property`. Child elements are
added through the configured `IAddChild<T>`/`IAddChild` content contract. Forma controls implement
that contract by forwarding to `Control.AddChild`.

The compiler validates constructors, members, event-handler signatures, content types, conversion,
and accessibility. It does not silently store unknown XML or use runtime reflection as a fallback.

Text content is supported only for types that explicitly define text content. Controls do not
implicitly map text to a `Text` property.

## `x:Class` and Populate Semantics

`x:Class` names the public or internal code-behind root type:

```xml
<PanelContainer
    xmlns="https://forma.dev/xaml"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="Game.Views.HudView">
</PanelContainer>
```

The code-behind type derives from, or is the same type as, the XAML root element and calls
`FormaXamlLoader.Load(this)` from its constructor. Build integration injects a hidden populate
method that applies the XAML to that existing constructor-created instance. Populate does not
replace the root, rerun its constructor, or discard fields initialized by the constructor.

A root without `x:Class` compiles to a generated factory and may be loaded through
`FormaXamlLoader.Load<T>()`. Generated build and populate methods are implementation details and
are not a public API.

## Namescopes and `x:Name`

`x:Name` assigns `Control.Name` when applicable and registers the object in the nearest XAML
namescope. Names must be unique within that scope and use identifier syntax. A compiled view root
creates a namescope. V1 has no templates, so ordinary descendants remain in the view root's scope.

Names do not generate fields. Code-behind resolves short-lived references through the namescope
API. Storyboard targets and trigger source names resolve in the same local namescope. A name from
another compiled view is not visible unless an explicit host API passes that object.

Hot reload replaces a namescope with the detached replacement tree. Code must not retain
long-lived references to named controls across replacement. Code-behind uses namescope lookup
rather than generated fields:

```csharp
public sealed class HudView : Control
{
  public HudView() => FormaXamlLoader.Load(this);

  public Label ScoreText => NameScope.GetNameScope(this)!.Find<Label>("ScoreText");
}
```

## Data Context and Typed Binding

`DataContext` is inherited through the control tree. A local value replaces the inherited value
for that subtree. `x:DataType` declares the expected data-context CLR type and is inherited by
descendants unless overridden:

```xml
xmlns:vm="clr-namespace:Game.ViewModels"
x:DataType="vm:GameHudViewModel"
```

Release and NativeAOT builds require `x:DataType` for every data-context binding. The compiler
resolves every path segment and emits typed accessors; it does not use reflection or string-based
member lookup at runtime.

The v1 binding form is:

```xml
Text="{Binding Player.Profile.DisplayName,
               Mode=OneWay,
               FallbackValue='Player',
               TargetNullValue='Guest',
               StringFormat='Name: {0}',
               Converter={StaticResource NameConverter},
               ConverterParameter=Short,
               UpdateSourceTrigger=PropertyChanged}"
```

Supported binding behavior:

- The path is empty for the whole data context or is a dotted public property path.
- Null intermediate values short-circuit the path and use `TargetNullValue` or the target default.
- Modes are `OneTime`, `OneWay`, and `TwoWay`. `OneWay` is the default.
- `FallbackValue` applies when evaluation or conversion fails. `TargetNullValue` applies to a
  successfully evaluated null result.
- `StringFormat` uses invariant composite-format syntax unless a converter explicitly applies
  another culture.
- A converter implements Forma's `IValueConverter` contract and may be supplied from resources.
- Source notifications use `INotifyPropertyChanged`; bindings never poll.
- `TwoWay` requires a writable source property and a compiler-known target adapter with a matching
  change event. Unsupported target properties are diagnostics.
- `UpdateSourceTrigger` is `Default`, `PropertyChanged`, or `LostFocus`. The target adapter defines
  `Default`; an unsupported trigger is a diagnostic.

Explicit source, element-name binding, relative-source binding, multibinding, priority binding,
commands, indexers, methods, dynamic objects, and untyped reflection fallback are outside v1.

## Literals, Conversion, and Markup Extensions

The compiler converts attribute text to the target CLR type. V1 includes invariant conversion for
strings, booleans, numeric types, enums and flag enums, nullable values, named and hexadecimal
`Color`, `Vector2`, `Thickness`, and `TimeSpan`. Invalid or ambiguous values are diagnostics.

V1 markup extensions are:

- `{Binding ...}` for typed data binding.
- `{StaticResource Key}` for one-time lexical resource resolution.
- `{DynamicResource Key}` for observable resource resolution.

A leading `{}` escapes a literal value that otherwise begins with `{`. Markup extensions may be
nested only in arguments explicitly documented to accept them, such as a binding converter.

## Resources

`ResourceDictionary` stores values by unique `x:Key`. A key is required unless a resource type has
a documented implicit key. Duplicate keys in one dictionary are diagnostics.

Controls expose a `Resources` property. Lookup starts at the requesting control's dictionary, then
walks parent controls, then uses `UIContext` application resources. Local entries override merged
dictionaries. Merged dictionaries are searched in reverse declaration order so the last merge has
the highest priority. Cycles and unresolved sources are diagnostics.

`StaticResource` resolves once when the tree is built. `DynamicResource` observes the winning
resource entry and reapplies the XAML value layer when that entry or lookup winner changes. Missing
static resources are compile errors; missing dynamic resources produce a diagnostic and use the
target's underlying value until a matching resource appears.

Resources can contain Forma controls only where a consuming property explicitly accepts them.
Shared mutable UI controls are otherwise rejected.

## Classes and Selector Styles

`Classes` is a whitespace-separated set of case-sensitive class names. Duplicate names collapse to
one entry. Runtime class changes notify the style engine.

V1 selectors contain one optional type, one optional `#name`, zero or more `.class` terms, and zero
or more pseudo states. Supported examples are:

```text
Button
.primary
Button.primary
#PauseButton
Button.primary:hover
#PauseButton:disabled
```

Supported pseudo states are `:hover`, `:focus`, `:disabled`, `:pressed`, and `:checked`. Descendant,
child, sibling, universal, attribute, negation, and selector-list syntax are outside v1.

Specificity is compared lexicographically as name count, class plus pseudo-state count, then type
count. Declaration order breaks equal specificity, with the later style winning. A setter property
path and converted value are validated against the selected control type. When a selector stops
matching, the previous winning style or underlying value is restored.

## XAML Value Precedence

Only properties touched by XAML participate in the coordinated value layer. Precedence from low to
high is:

1. Theme or control default.
2. Winning selector style.
3. Inherited value, binding value, or local XAML value.
4. Active animation value.

Later values do not destroy lower layers. Removing a class, ending a trigger, stopping an
animation, or detaching a binding reveals the next applicable value. A plain C# setter remains
valid, but code that needs immediate reconciliation while a property is styled or animated uses
the documented XAML value API.

## Events, Triggers, and Storyboards

An event attribute names a compatible method on the `x:Class` root. The compiler validates the
event and handler signature. Forma v1 adds public `Control.Attached` and `Control.Detached` events;
they fire when a control enters or leaves a `UIContext` and may be used like other CLR events.

`EventTrigger` resolves `SourceName` in the local namescope and validates `Event` on the source
type. `PropertyTrigger` uses a typed `Binding` and converts `Value` to the binding result type.
Trigger actions are `BeginStoryboard` and `StopStoryboard` in v1.

Storyboards are resources. V1 timelines target a local `x:Name` and a validated property path.
Timeline types are `FloatTimeline`, `ColorTimeline`, `Vector2Timeline`, and `ThicknessTimeline`.
Their keyframe values must match the target property type. Durations and keyframe times use
`TimeSpan`; easing names are validated against Forma's easing catalog.

Supported clock options are finite repeat counts or `Forever`, `AutoReverse`, and `FillBehavior`
values `Stop` and `HoldEnd`. `UIContext.Update(GameTime)` advances clocks deterministically.
Animation values overlay but do not write through a two-way binding source. Stopping a clock or a
`Stop` fill restores the underlying value.

## Supported XAML Directives

Forma v1 supports these directives in the XAML language namespace:

- `x:Class` on a compiled view root.
- `x:Name` on objects participating in a namescope.
- `x:Key` on resource dictionary entries.
- `x:DataType` on a binding scope.

The following are not supported in v1: `x:Arguments`, `x:FactoryMethod`, `x:TypeArguments`,
`x:Shared`, `x:Uid`, `x:Reference`, `x:Null`, `x:Type`, `x:Static`, `x:Code`, `x:Subclass`,
`x:FieldModifier`, `x:ClassModifier`, `x:Members`, and `x:Property`. Unknown directives and use of a
supported directive in an invalid location are diagnostics.

## Diagnostics

Every parser, schema, semantic, binding, style, trigger, and emission diagnostic has a stable
`FXAML` code, severity, project-relative file path, one-based line and column, and concise message.
Where practical it also includes an end position and related location. MSBuild, CLI text, JSON,
SARIF, and LSP output use the same diagnostic catalog.

Errors prevent emission or replacement. Warnings do not change semantics and may be promoted to
errors by project policy. The compiler does not downgrade unknown types, members, events,
resources, names, binding paths, selectors, directives, or incompatible values to runtime errors.

During hot reload, diagnostics leave the currently attached tree untouched. A later valid edit is
compiled independently and may replace it.

Diagnostic families:

| Code | Category |
| --- | --- |
| `FXAML1001`-`FXAML1004` | XML, root namespace, and directive errors |
| `FXAML2001`-`FXAML2002` | Duplicate or invalid names |
| `FXAML3001`-`FXAML3002` | Binding syntax and compiled-binding errors |
| `FXAML4001` | Selector errors |
| `FXAML5001` | Trigger errors |
| `FXAML6001` | Storyboard/timeline errors |
| `FXAML7001`-`FXAML7002` | IL emission and duplicate root-class errors |

## CLI and Language Server

The repository tool accepts files, directories, or projects and emits the same diagnostics used by
MSBuild:

```sh
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- \
  validate --require-compiled-bindings --format human samples/Forma.Xaml.Game
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- \
  validate --format json MyView.xaml
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- \
  validate --format sarif MyProject.csproj
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- watch MyProject.csproj
dotnet run --project tools/Forma.Xaml.Tool/Forma.Xaml.Tool.csproj -- schema --json
```

Start the language server with `forma-xaml lsp --stdio` (or the equivalent `dotnet run` command).
It discovers project references through Roslyn and supports diagnostics, completion, hover,
definition, references, rename, and formatting. V1 supplies the server protocol, not a bundled
editor extension.

## Hot Reload and AOT

Debug hot reload is opt-in and watches source files through `Forma.Xaml.HotReload`. Compilation
runs off-thread; a valid latest result is applied only during `UIContext.Update`. Replacement
retains the host slot and `DataContext`, then disposes old bindings, resource subscriptions,
styles, triggers, and clocks. Invalid edits report diagnostics without changing the live tree.
Burst saves are latest-wins.

Hot reload does not preserve references to old named controls, arbitrary code-behind control
state, focus/capture within the replaced subtree, or animation clock position. It is not supported
in trimmed or NativeAOT builds. Release, trimmed, and NativeAOT builds use only injected IL and may
not contain source XAML, watchers, SRE, XamlX, Cecil, or Forma compiler/hot-reload assemblies.

## Compatibility Matrix

| Concept | Forma XAML v1 | XAML 2006 / WPF / Avalonia comparison |
| --- | --- | --- |
| XML namespaces and `clr-namespace:` | Supported | Familiar syntax; Forma types and URI are distinct |
| `x:Class`, `x:Name`, `x:Key`, `x:DataType` | Supported | `x:Name` uses namescope lookup, not generated fields |
| Properties, property elements, content, events | Supported | Only public CLR members and Forma content contracts |
| Resources and merged dictionaries | Supported | Forma lookup and precedence rules apply |
| Type/class/name/pseudo selectors | Supported | Avalonia-like subset; no combinators or selector lists |
| Typed `OneTime`/`OneWay`/`TwoWay` binding | Supported | Requires `x:DataType` in Release; no reflection fallback |
| Static/dynamic resources | Supported | Forma value layers restore underlying values |
| Property/event triggers and storyboards | Supported | Deterministic `UIContext.Update` clocks |
| Templates, control themes, data templates | Not in v1 | Author reusable controls or C# factories |
| Commands, relative/element sources, multibinding | Not in v1 | Use typed view models and code-behind events |
| WPF/Avalonia namespace/source compatibility | Not promised | Forma XAML is its own dialect |

## Canonical Syntax Fixtures

The UI composition, resources and selector styles, typed data binding, and trigger/storyboard
examples in the declarative Forma XAML implementation plan are canonical v1 fixtures. They must be
copied into compiler golden tests and kept compiling. The `Attached` event shown there is the
public `Control.Attached` event defined by this contract.