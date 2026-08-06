## Plan: Declarative Forma XAML

Add a Forma-native XAML dialect whose release path compiles to IL with XamlX, while Forma itself owns resources, typed bindings, selector styles, storyboards, validation rules, and lifecycle. Build-time output remains trim/NativeAOT compatible; opt-in debug hot reload uses XamlX SRE to compile a detached replacement subtree, then a registered host swaps it on the MonoGame/FNA update thread while retaining its DataContext and host layout slot. Migrate the catalog shell and authored stories, and add a small dual-backend sample game, to prove structure, styles, animation, and two-way binding on both MonoGame and FNA. A shared semantic service powers MSBuild, a CLI, and a complete v1 LSP.

**Implementation Checklist**

### Phase 1: Language contract and XamlX feasibility
- [x] Write `docs/xaml-language.md` before implementation, fixing the Forma namespace URI, `.xaml` item convention, `x:Class` populate semantics, `x:Name` namescopes, resources, converters, binding grammar, style selectors, trigger/storyboard syntax, diagnostics policy, and supported/unsupported XAML 2006 directives. Promise a Forma-native dialect, not WPF/Avalonia source compatibility. Ratify the intended-v1 examples in this plan as canonical syntax fixtures; if feasibility work changes the grammar, update the language document, examples, sample XAML, and parser/compiler golden tests in the same change.
- [x] Add a small compiler spike against pinned NuGet `XamlX`/`XamlX.IL.Cecil` 1.0.0. Prove `Control.AddChild` through XamlX's `IAddChild` mapping, populate of a constructor-initialized root, CLR/event resolution, custom markup extensions, custom AST diagnostics with line/column, Cecil emission with portable PDBs, SRE compilation, and that the build-time compiler can produce release output accepted by Forma's trimming/macOS arm64 NativeAOT gate without introducing a runtime XamlX/Cecil dependency. If 1.0.0 lacks a required public extension point, pin one audited upstream commit rather than following `master`. NativeAOT incompatibility is not by itself a no-go: when a bounded, maintainable patch can remove the incompatible compiler/runtime surface, create a Forma-maintained fork, preserve upstream licensing and attribution, pin an exact commit, and consume it through a Git submodule under `external/XamlX`; keep the fork delta covered by focused tests and documented for upstreaming/rebasing. Record the selected NuGet/upstream/fork path and go/no-go rationale before broader work.

### Phase 2: Forma runtime foundation
- [x] Add XAML runtime primitives to the existing `Forma` assembly so generated views have no XamlX/Cecil/runtime-compiler dependency: XAML attributes/XML namespace mappings, `IAddChild<Control>` integration, namescopes, inherited `DataContext`, control/UI resource dictionaries, class lists, value converters, and `FormaXamlLoader.Load/Populate`. Support named/hex `Color`, `Vector2`, `Thickness`, `TimeSpan`, enum/flag, nullable, and resource conversion. Add declarative access to existing `Theme`, `StyleBox`, and per-control theme overrides.
- [x] Add an internal XAML attachment scope tied to `Control.SetContext`, `AddChild`, `RemoveChild`, and `UIContext.Update`. It owns subscriptions, binding expressions, active styles, triggers, and animation clocks, and disposes them when a subtree leaves the UI. Add only the narrowly required state notifications for hover, focus, enabled, checked/pressed, class, DataContext, and tree changes; do not retrofit a general dependency-property system.
- [x] Introduce a coordinated value layer only for properties touched by XAML. Precedence is theme/default < selector style < inherited/data binding or local XAML value < active animation; completion restores the underlying value. Plain C# setters remain valid, but changes to a currently styled/animated property must go through the documented XAML value API if they need immediate precedence reconciliation.

### Phase 3: Binding, styles, and animation
- [x] Implement `{Binding}` with inherited DataContext, dotted/null-safe paths, `OneTime`, `OneWay`, and `TwoWay`, `FallbackValue`, `TargetNullValue`, `StringFormat`, converters, and `UpdateSourceTrigger`. Require `x:DataType` for release/AOT compiled DataContext bindings so the compiler emits typed accessors and validates every segment. Subscribe through `INotifyPropertyChanged`; do not poll. Register explicit two-way target adapters for `LineEdit/TextEdit.Text`, `Range.Value`, `BaseButton.ButtonPressed`/`CheckBox.Checked`, `OptionButton.Selected`, and other existing controls only where a matching change event is present. Reject unsupported two-way targets or read-only source paths at validation time.
- [x] Implement `ResourceDictionary`, merged dictionaries, `StaticResource`, and observable `DynamicResource`. Implement cascading `Style` resources with v1 selectors for type, `.class`, `Type.class`, `#name`, and pseudo states `:hover`, `:focus`, `:disabled`, `:pressed`, and `:checked`; exclude descendant/sibling combinators in v1. Define deterministic specificity, declaration order, resource lookup, local-value precedence, and restoration when a selector stops matching.
- [x] Implement `Storyboard`, typed timelines/keyframes for float, color, `Vector2`, and `Thickness`, easing, repeat/auto-reverse/fill behavior, and target lookup by `x:Name` plus property path. Support event triggers and property/pseudo-state triggers. Tick clocks from `UIContext.Update(GameTime)` for deterministic tests; animations overlay rather than write through a two-way binding source unless explicitly configured.

### Phase 4: Compiler and build integration
- [x] Create `src/Forma.Xaml.Compiler` as the shared parser/schema/semantic/compiler library. Configure XamlX type mappings, custom converters, AST transformers for `x:Class`, `x:DataType`, names/resources, bindings, selectors, triggers, and storyboards, plus Forma diagnostic codes. Keep a normalized semantic document independent of XamlX AST so CLI/LSP features do not depend on emitted IL.
- [x] Create `src/Forma.Xaml.Build` as the packable MSBuild task/buildTransitive package. A pre-compile XML pass discovers XAML and root classes; normal C# compilation produces the target assembly; a post-compile Cecil pass validates and injects hidden Build/Populate methods, namescope metadata, typed binding/property delegates, and a static loader registry, updating PDB sequence points. `FormaXamlLoader.Load(this)` remains a normal source-level call, so no generated fields are required; code-behind resolves names through the namescope. Reject multiple implicit XAML files for one root type.
- [x] Add `@(FormaXaml)` defaulting to project `.xaml` files, opt-out/metadata controls, incremental inputs over XAML/compiler/reference hashes, deterministic output, clean support, and MSBuild diagnostics with file/line/column. Expose `FormaXamlValidateOnly`, `FormaXamlHotReload`, and `FormaXamlRequireCompiledBindings` properties. When `FormaXamlHotReload=true` in a supported Debug build, copy XAML to a development output directory with stable project-relative paths and include only the development host/runtime-compiler package needed to watch it; Release defaults to compiled bindings and never copies source XAML, the watcher, XamlX SRE, or compiler binaries.

### Phase 5: Validator, CLI, hot reload, and LSP
- [x] Create `tools/Forma.Xaml.Tool` as a .NET tool with `validate`, `watch`, `schema`, and `lsp --stdio`. `validate` accepts a project or files plus references/configuration, emits human, JSON, or SARIF diagnostics, and uses stable exit codes. MSBuild and CLI must call the exact same semantic validator and diagnostic catalog.
- [x] Implement opt-in debug hot reload in a development-only assembly/package with the following explicit contract:
	- The game starts the hot-reload service with its `UIContext` and registers each replaceable XAML root by stable project-relative source path. Registration supplies access to the current root and a host replacement callback because only the game knows whether that root belongs to a panel, scene, modal, navigation stack, or custom slot. The public API should support the equivalent of `Register<T>(source, current, replace)` without requiring game code to reference XamlX or compiler types.
	- A file watcher debounces saves and compiles the changed document with XamlX SRE against the CLR assemblies already loaded by the game. The compiler produces a detached replacement tree and fully creates its namescope, resources, bindings, styles, triggers, and animations before any live-tree mutation.
	- The watcher/compiler may run off-thread, but it only queues a successful replacement. `UIContext.Update(GameTime)` drains that queue on the MonoGame/FNA game update thread at a defined frame boundary; neither the watcher nor a compiler callback may mutate the live UI tree.
	- At the frame boundary, Forma rechecks that the registration still points to the expected current root, captures its inherited/effective `DataContext`, and invokes the host replacement callback. The callback preserves the parent and child index or named host slot plus host-owned layout metadata; Forma attaches the captured `DataContext` to the replacement, activates its attachment scope, and disposes the old scope and all old binding subscriptions, event-trigger subscriptions, and animation clocks. If the root changed while compilation was in flight, discard the stale result and compile or apply the newest save instead.
	- Parse, validation, resolution, or SRE failures produce file/line/column diagnostics and leave the currently displayed tree untouched. Multiple rapid saves are latest-wins, and a failed save does not prevent a later valid save from applying.
	- Preserve application state only when it lives in the retained `DataContext` or is explicitly transferred by the host callback. Do not promise identity for controls in the replaced tree, `x:Name` references, focus, caret/selection, scroll offsets, open popup state, animation progress, or manually attached handlers; code-behind must not retain long-lived references to replaced named controls.
	- XAML-only edits to structure, values, resources, bindings, styles, triggers, and storyboards are reloadable. CLR shape changes such as adding/renaming control or view-model members, changing constructors, or loading new assemblies require a normal rebuild/restart because SRE resolves only against already loaded assemblies.
	- Disable startup with a clear diagnostic under trimming or NativeAOT, make registration/service disposal stop watchers and queued work, and keep the development assembly, source XAML, watcher, and SRE/compiler dependencies out of release publish output.

	A normal MonoGame/FNA development loop is therefore: set `<FormaXamlHotReload>true</FormaXamlHotReload>` for Debug, run the game once, register the screen/story roots during host setup, edit and save a copied XAML source, and see the valid replacement on a subsequent game update without restarting. Editing C# or changing the CLR schema still requires rebuilding the game.
- [x] Implement the LSP over the shared semantic workspace: project/reference discovery, debounced incremental documents, diagnostics, context-aware element/property/event/enum/resource/binding-path/style completion, CLR/XAML hover, definition for XAML symbols and source-backed CLR types, references/rename for `x:Name`, resource keys, and style classes, plus deterministic XML formatting. Use Roslyn/MSBuildWorkspace for project-source symbols and metadata/SourceLink locations when available; do not rename CLR members from the XAML server. Reload schema when project assets or output assemblies change, and expose stdio so a later VS Code extension only needs process management and client UI.

### Phase 6: Catalog migration
- [x] Split `CatalogShell` into `CatalogShell.xaml` plus focused code-behind/view-model classes. Put the shell tree, shared colors/StyleBoxes, type/class styles, state triggers, and at least one visible storyboard in XAML. Bind search text two-way, selected story/header/description/count one-way, and dynamic-text toggle two-way; keep story loading, reflection-based inspector generation, font/texture services, and game-host operations in C#.
- [x] Convert manually authored icon/typography diagnostic story layouts into separate XAML views with typed view models and bindings, while retaining reflection-generated one-control stories and `ComponentStory` discovery in C#. Adapt `StoryCatalog` factories to compiled XAML loaders and attachment view models. Preserve story names, categories, control names used by tests, backend behavior, and catalog metrics.
- [x] Enable debug hot reload in both catalog hosts behind the MSBuild property. Start the service from each game host, register the shell and active story by project-relative XAML path, and implement replacement callbacks that preserve parent/slot metadata and retain their existing view models as `DataContext`. Ensure host shutdown disposes the service, and keep Release catalog builds on generated factories only.

### Phase 7: Dual-backend XAML sample game
- [x] Create `samples/Forma.Xaml.Game` as a small shared game implementation plus thin `samples/Forma.Xaml.Game.MonoGame` and `samples/Forma.Xaml.Game.FNA` executable hosts, following the existing catalog project split. Share all gameplay, view models, XAML, content, and Forma integration; backend host projects may contain only runtime-specific startup/build references and adapters. Both executables must present the same game rules, UI hierarchy, assets, input, and rendering rather than separate approximations.
- [x] Make the sample a minimal playable collect-and-score scene: the player moves a marker to collect targets before a short timer expires, with restart and pause/settings flows. Keep gameplay simulation, input, collision, target spawning, and persistence in C#; use focused typed view models implementing `INotifyPropertyChanged` to expose score, remaining time, status, player name, difficulty, sound enabled, and volume. Author the HUD, pause/settings overlay, and result overlay as separate compiled XAML views that demonstrate:
	- UI structure and layout through nested panels/containers, reusable resources, `x:Name` namescopes, and code-behind only where host/game operations are required.
	- `ResourceDictionary`, `StaticResource`/`DynamicResource`, shared `StyleBox`/color resources, type/class/name selectors, and visible hover, pressed, disabled, and checked pseudo-state styles.
	- One-way typed bindings for score, timer, game status, and result text; two-way typed bindings for player name, difficulty, sound toggle, and volume; converter, fallback/null, and formatted-value examples where they are natural rather than contrived.
	- At least one state-triggered animation and one event-triggered storyboard, such as a score pulse, low-time warning, and pause/result panel entrance, driven deterministically by `UIContext.Update(GameTime)`.
	- A runtime resource or class change driven by game/view-model state so dynamic resources and selector restoration are visible during normal play.
- [x] Enable the documented Debug hot-reload flow in both sample hosts and register each active XAML view using the shared host integration. Demonstrate that editing HUD layout/style/animation while the game runs preserves its view model and current score/timer state, while invalid XAML leaves the old UI active. Add a concise sample `README.md` with MonoGame and FNA run commands, controls, project structure, binding/view-model walkthrough, XAML feature map, and a short hot-reload exercise; do not duplicate the full language reference.

### Phase 8: Tests, packaging, and documentation
- [x] Add `tests/Forma.Xaml.Tests` for parser/converters, namescopes/resources, typed one/two-way binding and disposal, style specificity/state restoration, deterministic storyboard clocks, diagnostics, and hot-reload replacement. Cover off-thread compile/on-update apply, parent/index/slot and DataContext retention, old-scope disposal, stale/latest-wins saves, invalid-edit rollback, registration races, and service shutdown. Add invalid golden XAML cases for unknown types/members, constructor/content errors, binding paths/modes, selectors, animation property/type mismatch, duplicate names/keys, and unsupported directives.
- [x] Add build fixtures for valid/invalid projects, incremental/no-op rebuilds, portable PDB diagnostics, CLI text/JSON/SARIF, and LSP protocol tests for every requested capability. Run unit/build tests with both `FormaRuntime=MonoGame` and `FNA` where runtime types are involved.
- [x] Update catalog inventory/render/smoke tests and baselines only for intentional visual changes. Add sample-game smoke tests for both hosts that advance deterministic game/UI updates and assert matching initial, playing, paused, low-time, and result states, including one-way updates, two-way settings changes, selector state, and storyboard progress. Extend package-consumer and macOS arm64 NativeAOT gates with a compiled XAML view and typed two-way binding; assert no XamlX, Cecil, source XAML, watcher, or SRE assembly appears in release/trimmed/AOT output. Add package determinism, dependency, size-budget, and opposite-runtime guards for new artifacts.
- [x] Update `Forma.slnx`, `Makefile`, CI/check scripts, `README.md`, `RELEASE_NOTES.md`, `NOTICE.md`, and `THIRD-PARTY-NOTICES.md`. Include both sample hosts in solution/build/smoke entry points and document setup, syntax, code-behind/namescope access, styles, animation, typed bindings, CLI/MSBuild/LSP use, hot-reload limits, AOT behavior, diagnostic codes, and a compatibility matrix against XAML 2006/WPF/Avalonia concepts.

**Intended v1 XAML examples**

These examples define the desired shape of the Forma-native dialect and should become compiling fixtures in Phase 1. They use the proposed Forma namespace URI `https://forma.dev/xaml`; Phase 1 must ratify that URI or replace it consistently everywhere. Type names and ordinary properties intentionally follow the existing Forma API. Forma-specific resources, classes, bindings, selectors, triggers, and timelines are new language/runtime types, not claims of WPF or Avalonia compatibility.

### UI composition

`HudView.xaml` shows an `x:Class` root populated by `FormaXamlLoader.Load(this)`, a required release binding type through `x:DataType`, inherited `DataContext`, namescope entries, class lists, enum flags, and normal `IAddChild<Control>` content:

```xml
<PanelContainer
	xmlns="https://forma.dev/xaml"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
	xmlns:vm="clr-namespace:Forma.Xaml.Game.ViewModels"
	x:Class="Forma.Xaml.Game.Views.HudView"
	x:DataType="vm:GameHudViewModel"
	x:Name="HudRoot"
	Classes="hud"
	Padding="16">
	<VBoxContainer Separation="12">
		<HBoxContainer Separation="16">
			<Label
				x:Name="ScoreText"
				Classes="metric score"
				Text="{Binding Score, StringFormat='Score: {0}'}" />
			<Label
				x:Name="TimerText"
				Classes="metric"
				Text="{Binding RemainingTime, Converter={StaticResource ClockConverter}}" />
		</HBoxContainer>

		<ProgressBar
			x:Name="TimeBar"
			MinValue="0"
			MaxValue="30"
			Value="{Binding RemainingSeconds}" />

		<Label
			Text="{Binding StatusText, FallbackValue='Ready', TargetNullValue='Ready'}" />

		<Button
			x:Name="PauseButton"
			Classes="primary"
			HorizontalSizeFlags="Fill, Expand"
			Text="Pause"
			Pressed="OnPausePressed" />
	</VBoxContainer>
</PanelContainer>
```

Event attributes resolve CLR code-behind handlers at compile time. `x:Name` registers controls in the local namescope but does not generate fields; code-behind uses the namescope lookup API when it needs a short-lived reference.

### Resources and selector styles

Resources may be local to a view or merged from a separate dictionary. Styles cascade by selector specificity and declaration order; a local XAML value remains above a style value. The exact per-control theme override names must be documented alongside the existing `Theme` and `StyleBox` API.

```xml
<ResourceDictionary
	xmlns="https://forma.dev/xaml"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<ResourceDictionary.MergedDictionaries>
		<ResourceDictionary Source="Palette.xaml" />
	</ResourceDictionary.MergedDictionaries>

	<Theme
		x:Key="HudTheme"
		PanelColor="#E6202630"
		PanelBorderColor="#FF556070"
		TextColor="#FFF4F7FA"
		AccentColor="#FF56C596" />

	<Theme
		x:Key="PrimaryHoverTheme"
		AccentColor="#FF7BDCB2"
		TextColor="#FF101714" />

	<StyleBoxFlat
		x:Key="HudPanel"
		BackgroundColor="#E6202630"
		BorderColor="#FF556070"
		BorderWidth="1"
		CornerRadius="6"
		ContentMargin="16" />

	<Style Selector="PanelContainer.hud">
		<Setter Property="ThemeOverride" Value="{StaticResource HudTheme}" />
		<Setter Property="ThemeOverride.Panel" Value="{StaticResource HudPanel}" />
	</Style>

	<Style Selector="Label.metric">
		<Setter Property="ThemeOverride.FontSize" Value="20" />
	</Style>

	<Style Selector="Button.primary:hover">
		<Setter Property="ThemeOverride" Value="{DynamicResource PrimaryHoverTheme}" />
	</Style>

	<Style Selector="Button.primary:pressed">
		<Setter Property="Margins" Value="1,1,-1,-1" />
	</Style>

	<Style Selector="#PauseButton:disabled">
		<Setter Property="TooltipText" Value="Pause is unavailable after time expires." />
	</Style>
</ResourceDictionary>
```

The compiler validates selector types, names, classes, pseudo states, setter paths, and setter value conversion. `StaticResource` resolves once using lexical resource lookup; `DynamicResource` observes the winning resource and reapplies precedence when it changes.

### Typed data binding

`x:DataType` makes every binding path below the element statically resolvable for release and AOT. `OneWay` is the default for display targets; writable controls use an explicitly supported adapter for `TwoWay` and `UpdateSourceTrigger`.

```xml
<PanelContainer
	xmlns="https://forma.dev/xaml"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
	xmlns:vm="clr-namespace:Forma.Xaml.Game.ViewModels"
	x:Class="Forma.Xaml.Game.Views.SettingsView"
	x:DataType="vm:GameSettingsViewModel">
	<VBoxContainer Separation="8">
		<LineEdit
			Text="{Binding PlayerName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, FallbackValue='Player 1'}" />

		<CheckBox
			Text="Sound enabled"
			Checked="{Binding SoundEnabled, Mode=TwoWay}" />

		<Slider
			MinValue="0"
			MaxValue="1"
			Step="0.05"
			Value="{Binding Volume, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />

		<Label Text="{Binding Volume, StringFormat='Volume: {0:P0}'}" />
		<Label Text="{Binding Player.Profile.DisplayName, TargetNullValue='Guest'}" />
	</VBoxContainer>
</PanelContainer>
```

Compiled accessors subscribe through `INotifyPropertyChanged`; they never poll. A missing member, read-only source used by `TwoWay`, unsupported target adapter, incompatible converter, or invalid `UpdateSourceTrigger` is a build diagnostic rather than a runtime fallback.

### Triggers and animation

Storyboards are resources. Timelines target controls in the local namescope and validated property paths. Property triggers compare a typed binding value; event triggers subscribe to an existing CLR event. Animation values overlay the underlying style/local/binding value and restore it when the clock stops.

```xml
<PanelContainer
	xmlns="https://forma.dev/xaml"
	xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
	xmlns:vm="clr-namespace:Forma.Xaml.Game.ViewModels"
	x:Class="Forma.Xaml.Game.Views.ResultView"
	x:DataType="vm:GameResultViewModel"
	x:Name="ResultRoot">
	<PanelContainer.Resources>
		<ResourceDictionary>
			<Storyboard x:Key="LowTimePulse" RepeatBehavior="Forever" AutoReverse="True">
				<ColorTimeline
					TargetName="TimerText"
					Property="ThemeOverride.TextColor"
					Duration="0:0:0.35">
					<KeyFrame Time="0:0:0" Value="#FFFFFFFF" />
					<KeyFrame Time="0:0:0.35" Value="#FFFF5252" Easing="CubicInOut" />
				</ColorTimeline>
			</Storyboard>

			<Storyboard x:Key="ResultEntrance" FillBehavior="HoldEnd">
				<Vector2Timeline
					TargetName="ResultPanel"
					Property="Position"
					Duration="0:0:0.25">
					<KeyFrame Time="0:0:0" Value="0,40" />
					<KeyFrame Time="0:0:0.25" Value="0,0" Easing="CubicOut" />
				</Vector2Timeline>
			</Storyboard>
		</ResourceDictionary>
	</PanelContainer.Resources>

	<PanelContainer x:Name="ResultPanel" Classes="result">
		<VBoxContainer>
			<Label x:Name="TimerText" Text="{Binding RemainingText}" />
			<Label Text="{Binding ResultText}" />
			<Button x:Name="RestartButton" Text="Play again" />
		</VBoxContainer>
	</PanelContainer>

	<PanelContainer.Triggers>
		<PropertyTrigger Binding="{Binding IsLowTime}" Value="True">
			<BeginStoryboard Storyboard="{StaticResource LowTimePulse}" />
		</PropertyTrigger>

		<EventTrigger SourceName="ResultRoot" Event="Attached">
			<BeginStoryboard Storyboard="{StaticResource ResultEntrance}" />
		</EventTrigger>

		<EventTrigger SourceName="RestartButton" Event="Pressed">
			<StopStoryboard Storyboard="{StaticResource LowTimePulse}" />
		</EventTrigger>
	</PanelContainer.Triggers>
</PanelContainer>
```

Phase 1 must settle whether lifecycle events such as `Attached` are public XAML events or represented by a dedicated trigger kind. Whichever form is chosen, the compiler must reject unknown events, names, property paths, timeline/property type mismatches, and invalid keyframe values with file/line/column diagnostics.

**Relevant files**
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Control.cs` — child/content mapping, DataContext/resources/classes, lifecycle and pseudo-state notifications.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/UIContext.cs` — XAML attachment lifecycle and deterministic animation update.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Primitives.cs` and `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/StyleBoxes.cs` — resources, conversion, Theme/StyleBox declarative support.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Controls.cs` and `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/AdvancedControls.cs` — target notification adapters for two-way binding.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/` — new loader, namescope, resources, binding, style, trigger, animation, and runtime attachment implementation.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/` — new shared semantic validator and XamlX/Cecil compiler.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Build/` — new MSBuild tasks, targets, and pack assets.
- `/Users/zignd/hack/games/engine-workshop/Forma/tools/Forma.Xaml.Tool/` — new CLI and stdio LSP host.
- `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Catalog/CatalogShell.cs`, `CatalogShell.xaml`, and `StoryCatalog.cs` — shell/view-model split and authored-story migration.
- `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Xaml.Game/`, `Forma.Xaml.Game.MonoGame/`, and `Forma.Xaml.Game.FNA/` — shared sample gameplay/XAML plus thin backend hosts.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Tests/CatalogInventoryTest.cs`, `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.Tests/`, and package/smoke scripts — regression, protocol, packaging, and AOT coverage.

**Verification**
- [x] Run focused Forma XAML unit and compiler/build fixture tests, then `make test-unit-monogame` and `make test-unit-fna`.
- [x] Run CLI golden validation in text/JSON/SARIF and an LSP protocol suite covering diagnostics, completion, hover, definitions, references, rename, and formatting.
- [x] Build valid catalog XAML and prove an invalid copy fails MSBuild at the expected file/line/column; rebuild unchanged inputs and verify the compiler task is skipped and outputs are byte-deterministic.
- [x] Run both catalog hosts, exercise search/toggle/selection and two-way story controls, edit shell/story XAML under hot reload, and verify replacement occurs only during `UIContext.Update`, preserves the parent/index or host slot and DataContext, and stops old subscriptions/clocks. Save invalid XAML and verify the live tree remains intact with diagnostics, then save a valid correction and verify it applies; burst-save multiple valid edits and verify only the newest result becomes visible.
- [x] Build and run `Forma.Xaml.Game.MonoGame` and `Forma.Xaml.Game.FNA`; play through start, collect/score, pause/settings, low-time, result, and restart. Verify identical behavior and visuals, one/two-way bindings, selector changes, and deterministic animations, then edit each XAML view under hot reload and confirm current game/view-model state survives replacement in both hosts.
- [x] Run `make smoke`, `make render-parity`, `make packages`, and `make nativeaot`; inspect publish output for forbidden compiler/debug dependencies.
- [x] Run `make check-all` after intentional catalog/sample baselines and package budgets are reviewed.

**Decisions**
- Forma-native XAML; build-time compiled release plus opt-in debug hot reload.
- XamlX is an implementation dependency of compiler/tooling only, pinned to 1.0.0 unless the feasibility gate justifies an exact audited upstream revision. If Forma must modify XamlX or another dependency for NativeAOT compatibility, use a Forma-maintained fork pinned as a Git submodule, preserve licensing/attribution, document the fork delta and update procedure, and prevent the dependency from entering generated-view or Forma runtime publish output.
- LSP v1 includes diagnostics, completion, hover, definition, references/rename, and formatting; a VS Code client extension is deliberately deferred.
- Styles use resources plus type/class/name selectors and pseudo states; v1 excludes selector combinators and templates.
- Animation v1 uses storyboards/keyframes with event and state triggers.
- Catalog scope is shell plus authored stories; reflection-generated default stories remain C#.
- The sample is one shared, minimal playable game with thin MonoGame and FNA hosts; its XAML owns UI composition and presentation while C# owns gameplay and persistent state.
- Hot reload compiles off-thread but mutates the UI only from `UIContext.Update`; a host registration owns each root replacement and preserves its parent/index or named slot, host layout metadata, and DataContext. Invalid or stale compilations never replace the live tree.
- Hot reload preserves model state, not control state: controls in the replaced subtree, namescope references, focus, selection, scroll, popup state, and animation progress may all change. CLR schema edits require rebuild/restart.
- Generated `x:Name` fields, WPF compatibility, control templates, collection virtualization/data templates, and an in-place diff hot-reload engine are outside v1.