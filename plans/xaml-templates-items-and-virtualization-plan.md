## Plan: XAML Templates, Items, and Virtualization

Add a template-first visual architecture to Forma on top of a stable, non-templated foundation of visual primitives, layout panels, and presenters, then build typed `DataTemplate`, `ItemsControl`, virtualized panels, and `ListBox` on it. The goal is HTML/CSS-like composition freedom for game UI, not browser or CSS syntax compatibility. The implementation will be a deliberate breaking release: every semantic widget moves to a default `ControlTemplate`, foundational elements remain directly rendered and fully styleable, bindings gain compile-time `RelativeSource`, repeated rows are generated from explicit templates, and release/trimmed/NativeAOT builds retain the current no-reflection/no-runtime-XAML guarantees. The existing Forma Catalog and Signal Run XAML sample game remain supported first-party applications throughout the migration on both MonoGame and FNA; each breaking phase updates them in the same change and may not leave either application on a temporary legacy UI path.

## Proposed Capability Showcase

The syntax below is the proposed authoring contract, not pseudocode for capabilities that may be omitted. Phase 0 may refine individual type or property names, but each example must retain equivalent typed behavior. Copy every example into compiler golden tests, the Catalog, and the packed-consumer fixtures as the corresponding phase lands.

### Proposed Selector Language Contract

Selector syntax is not defined by XAML itself. Forma v1 currently supports only one compound selector containing an optional type, optional `#name`, `.class` terms, and built-in pseudo-states. This release deliberately replaces that parser with a Forma-owned, Avalonia-inspired selector language. Forma uses the compact `>>` combinator to cross one control-template boundary; the grammar, state names, scope rules, typed validation, specificity, and runtime invalidation below are Forma contracts and do not promise source compatibility with Avalonia, WPF, or CSS.

#### Grammar

The proposed grammar is:

```text
selector-list     = complex-selector (ws? "," ws? complex-selector)* ;
complex-selector  = compound-selector (combinator compound-selector)* ;
combinator        = descendant | template-child | child ;
descendant        = ws ;
template-child    = ws? ">>" ws? ;
child             = ws? ">" ws? ;
compound-selector = [type-selector | "*"] simple-selector* ;
simple-selector   = class-selector | name-selector | pseudo-state | negation ;
type-selector     = [xml-prefix ":"] identifier ;
xml-prefix        = identifier ;
class-selector    = "." identifier ;
name-selector     = "#" identifier ;
pseudo-state      = ":" identifier ;
negation          = ":not(" compound-selector ")" ;
ws                = one-or-more XML whitespace characters ;
```

An identifier uses the same case-sensitive identifier rules as Forma classes and names. An optional type prefix resolves through the `xmlns` declarations in scope, so `views:ProjectCard` can disambiguate referenced CLR types; unqualified built-in and imported type names remain valid only when resolution produces one type. A compound selector contains no combinator whitespace, may contain at most one type or `*`, and may contain at most one `#name`. `:not(...)` accepts one compound selector in this release; selector lists and combinators inside `:not` are diagnostics. Parentheses have no other selector meaning.

The supported terms are:

| Syntax | Meaning | Specificity contribution |
| --- | --- | --- |
| `Button` | Runtime type is `Button` or derives from it | `(0,0,1)` |
| `*` | Any visual `Control` | `(0,0,0)` |
| `.primary` | `Classes` contains case-sensitive `primary` | `(0,1,0)` |
| `#SaveButton` | `Name` equals case-sensitive `SaveButton`; this is matching, not namescope lookup | `(1,0,0)` |
| `:hover` | Pseudo-state provider reports `hover` | `(0,1,0)` |
| `:not(.danger)` | Its argument does not match the same candidate | Specificity of the argument |
| `A B` | `B` has an `A` visual ancestor within the same boundary | Combinators add none |
| `A > B` | `B` has `A` as its direct visual parent | Combinators add none |
| `A >> B` | `B` is in the current control-template visual subtree owned by `A` | Combinators add none |
| `A, B` | Independent selector arms sharing one setter list | Each arm is ranked independently |

No whitespace is allowed between a term marker and its identifier. Selector tokenization first recognizes `>>`, then `>`, and discards adjacent whitespace around either explicit combinator. Only whitespace that is not adjacent to an explicit combinator emits a descendant token, so `A > B` cannot be tokenized as `A` followed by both descendant and child combinators. Whitespace between the two characters of `>>` is invalid. Formatting whitespace is therefore meaningful only between complete compound selectors.

#### Matching Direction and Setter Target

Selectors match from right to left. The rightmost compound selector is the **subject** and receives the setters; compounds to its left constrain its visual ancestry.

```text
ListBoxItem:selected >> Border.selection
```

is evaluated as:

1. Consider a `Border.selection` as the setter target.
2. Walk outward through exactly one control-template ownership boundary.
3. Require the owner to be a `ListBoxItem` whose pseudo-state provider currently reports `selected`.
4. Apply the setters to the `Border`, not the `ListBoxItem`.

Conceptually:

```text
ListBoxItem :selected                 constraint
└── ControlTemplate boundary         crossed by >>
	└── Border .selection             subject and setter target
		└── ContentPresenter
```

Other examples:

```text
Button.primary:hover
```

Targets a primary `Button` while it is hovered.

```text
Dialog .command
```

Targets any `.command` visual descendant of a `Dialog`, at any depth, without crossing a template or data-template boundary.

```text
ToolBar > Button:not(.overflow)
```

Targets a `Button` whose direct visual parent is `ToolBar` and whose classes do not contain `overflow`.

```text
Button.primary, MenuButton.primary
```

Targets either type. Every setter must be valid for the statically inferred subject type of every arm; otherwise the compiler reports an incompatible selector-list target diagnostic.

```text
Button:hover >> Border.chrome > ContentPresenter
```

Targets a `ContentPresenter` directly parented by `Border.chrome` inside the hovered button's current control-template instance.

#### Visual Tree and Template Boundaries

Child and descendant combinators use `VisualParent`, never logical `Parent`. This lets selectors describe what is drawn while presenters preserve separate logical ownership.

Ordinary ` ` and `>` combinators may inspect visual ancestry only inside the current styling boundary. They do not enter or leave a `ControlTemplate`, `DataTemplate`, or separate compiled-view boundary. A style declared inside a template instance starts inside that boundary and may use ordinary combinators within it.

`>>` crosses exactly one `ControlTemplate` boundary from a matched `TemplatedControl` on the left into the visual subtree owned by its current `TemplateInstance` on the right. It does not cross a `DataTemplate`, does not search an arbitrary descendant widget's template, and does not continue through nested control templates. Crossing another nested control template requires another explicit `>>` segment.

```text
ListBox >> Border.frame
```

may target the `ListBox` template's frame.

```text
ListBox >> ListBoxItem >> Border.selection
```

may cross the `ListBox` template and then a nested `ListBoxItem` template, if that visual relationship actually exists. It still cannot enter the `DataTemplate` projected by the item presenter.

Using `>>` intentionally couples a style to template structure. It is appropriate for application-owned templates and packaged theme styles shipped with the corresponding template. Public controls must document stable part names or part classes used by external styles; undocumented primitive types, names, and classes inside a packaged default template remain implementation details and may change. Replacing a `ControlTemplate` does not require the replacement to retain undocumented selector hooks.

#### Style Scope

A selector never searches the entire application indiscriminately. Its candidate subjects are restricted to the attachment scope of the style resource:

- A style in application resources applies to all attached roots in that `UIContext`.
- A style in `Control.Resources` applies to that control's visual subtree, subject to template boundaries.
- A style created inside a `ControlTemplate` applies only to that template instance.
- A style created inside a `DataTemplate` applies only to that realized data-template instance.
- A selector may inspect visual ancestors outside its candidate subject scope when resolving its left-hand constraints, but it may never select a subject outside the scope.

Resource lookup scope and selector attachment scope are related but distinct: `{DynamicResource ...}` walks resource ancestry to find a value, while a selector starts from the visual candidates owned by the location where its style is attached.

#### Pseudo-States

Pseudo-states are boolean names supplied through an extensible typed provider on `Control`; the selector engine does not hard-code concrete control classes. This release standardizes:

| State | Meaning |
| --- | --- |
| `:hover` | Pointer is over the control according to hit testing and capture rules |
| `:focus` | Control itself owns keyboard focus |
| `:focus-within` | Control or one of its visual descendants owns focus, without crossing out of its styling boundary |
| `:disabled` | Effective inherited enabled state is false |
| `:pressed` | Press/activation interaction is currently visually active |
| `:checked` | Toggle/check state is true |
| `:selected` | Selector container's logical source slot is selected |
| `:current` | Selector container's logical source slot is the current navigation item |

Unknown pseudo-state names are compile-time diagnostics unless a referenced control assembly registers that state through assembly metadata readable without loading or executing that assembly. Metadata declares a stable state identifier, the applicable control type, inheritance behavior, and the runtime typed state-provider member. `Control` exposes one state-changed notification carrying that identifier; compiler and runtime tests reject duplicate registration, unavailable-on-type states, and metadata/provider disagreement. A pseudo-state belongs to the candidate matched by the compound containing it; state does not implicitly propagate into a template. The selected owner in `ListBoxItem:selected >> Border.selection` supplies `:selected`, while the border is merely the setter target.

#### Specificity, Precedence, and Lists

Specificity is the lexicographic tuple `(name count, class plus pseudo-state count, type count)`. `*` and combinators contribute zero. `:not(...)` contributes the specificity of its argument but not an extra pseudo-state point. For example:

```text
Button                              (0,0,1)
Button.primary                      (0,1,1)
Button.primary:hover                (0,2,1)
#SaveButton                         (1,0,0)
Button:hover >> Border              (0,1,2)
```

Each matching arm of a selector list has its own specificity; the most specific matching arm is used for that style application. When two matching styles have equal specificity, later declaration order wins. Specificity resolves competition only inside the style value layer; local XAML/binding values and active animations retain the higher precedence defined by the XAML value system.

#### Typed Validation and Runtime Invalidation

The compiler resolves qualified type selectors through in-scope XML namespaces and unqualified selectors against referenced CLR types, validates pseudo-state metadata, builds a typed selector AST, infers the rightmost subject type for each arm, and validates every setter against all possible subject types. A rightmost compound without a type has static subject type `Control` and may set only `Control` properties unless the style declares an explicit `TargetType`. Malformed combinators, empty list arms, unknown or ambiguous types, unsupported pseudo-states, structurally impossible template crossings, and incompatible setters produce stable `FXAML400x` diagnostics with exact token locations. A crossing is structurally impossible only when its left subject cannot be a `TemplatedControl`, it attempts to cross a data-template/view boundary, or it violates a declared template-part contract. A selector against replaceable template structure that simply has no part in the currently known fixture is valid and matches nothing; concrete structure is checked only for template-local styles or explicitly sealed template identities.

Runtime matching is event-driven and indexed by the rightmost subject terms. The engine re-evaluates only potentially affected candidates when a name, class, pseudo-state, effective enabled state, visual parent, style scope, or template instance changes. An ancestor change invalidates only descendants whose compiled selector dependencies mention that ancestor term. No frame may poll states or scan the full visual tree.

#### Deliberately Unsupported Selector Features

This release does not support adjacent/general sibling combinators (`+`, `~`), attribute/property selectors (`[Enabled=true]`), `:is`, `:where`, `:has`, structural position selectors such as `:nth-child`, arbitrary data predicates, CSS namespaces, cascade layers, or CSS text stylesheets. State belongs in typed pseudo-state providers; data belongs in typed bindings/triggers; layout position belongs in container metadata such as `AlternationIndex`. Unsupported syntax is a diagnostic rather than a selector that silently never matches.

### Foundational Visual Vocabulary

The foundation is intentionally closer to **HTML box/layout primitives plus an SVG-like retained vector scene graph** than to a widget toolkit. HTML and CSS contribute box composition, flow/flex/grid layout, typography, images, clipping, transforms, and compositing. SVG contributes paths, geometry, fills, strokes, and reusable vector drawings. Semantic controls such as `Button`, `ListBox`, `LineEdit`, dialogs, menus, and sliders are not foundational: they own behavior and are assembled from this vocabulary by `ControlTemplate`.

The vocabulary is frozen only after a Phase 0 renderer-feasibility gate proves the representative pipeline on both runtime peers. That gate defines a backend-neutral `DrawingContext`, normalized path representation, deterministic fill/stroke tessellation, brush/material binding, clip/mask composition, offscreen-pass scheduling, render-target pooling, mesh/resource caches, and graphics-device loss/recreation. The spike must render and hit-test one transformed curved path with a gradient stroke, one arbitrary geometry clip, one opacity mask, and one bounded effect on MonoGame and FNA without reflection or runtime shader compilation. A capability that fails parity is narrowed behind an explicit documented limit before its public API is frozen; it is not left as an implementation detail for Phase 1.

A foundational **element** is an authorable visual-tree node that derives from `Control`, renders, lays out, or projects content directly, and never resolves a `ControlTemplate`. A foundational **value resource** such as a brush, geometry, transform, drawing, effect, or text inline is not a visual-tree node; elements consume these resources through typed properties. Both kinds are bindable, resource-aware, styleable where mutation is meaningful, compiler-known, and safe for trimming and NativeAOT.

The following catalog is the required first-release vocabulary, not an illustrative subset.

#### Box, Text, Image, and Vector Elements

| Element | Required responsibility | Closest web/SVG analogue |
| --- | --- | --- |
| `Border` | One visual child; background brush; per-side border brush/thickness; independent corner radii; padding; clipping to rounded bounds; outer and inset box-shadow collection | CSS box/background/border/border-radius/box-shadow |
| `TextBlock` | Read-only text and inline content; wrapping, trimming, maximum lines, selection-free hit geometry, font family/size/weight/style/stretch, line height, letter spacing, decoration, transform, alignment, locale, and bidi direction | HTML inline text and CSS typography |
| `Image` | Bitmap or compiled vector source; source rectangle; contain/cover/fill/none/scale-down stretch; alignment; repeat/tile modes; tint; opacity; nearest/linear sampling | HTML `img`, CSS `background-image`, SVG `image` |
| `NineSliceImage` | Bitmap source with four slice insets, independent edge tiling/stretching, center draw policy, tint, and sampling | Nine-slice game surface; replaces repeated nested CSS border-image cases |
| `ThemeIconView` | Displays the existing theme-keyed `ThemeIcon` bitmap/compiled-vector resource with density selection, tint, and intrinsic logical size; replaces `ThemeIconRect` | Icon font/SVG symbol use |
| `RectangleShape` | Rectangle geometry with independent corner radii | SVG `rect` |
| `EllipseShape` | Ellipse or circle geometry | SVG `ellipse`/`circle` |
| `LineShape` | One stroked line segment | SVG `line` |
| `PolylineShape` | Open point sequence | SVG `polyline` |
| `PolygonShape` | Closed point sequence with fill rule | SVG `polygon` |
| `PathShape` | Arbitrary `Geometry` with fill and stroke | SVG `path` |

All shape elements expose `Fill`, `Stroke`, `StrokeThickness`, `StrokeAlignment`, line cap/join/miter, dash array/offset, fill rule where applicable, stretch, and geometry-relative transform. Shapes participate in normal layout and may contain no children. Complex illustrations are ordinary trees of shape elements under `CanvasPanel` or `OverlayPanel`; grouping, opacity, clipping, and transforms come from `Control`, so a separate SVG document object model is unnecessary.

`TextBlock` supports a plain `Text` fast path and a typed inline collection containing `Run`, `Span`, `LineBreak`, and `InlineImage`. `Span` may carry inherited font, foreground, background, decoration, language, and direction overrides. Inlines are text-layout resources rather than controls: they do not receive focus, own templates, or become independent selector subjects. Interactive rich text remains a semantic control with a specialized presenter.

#### Layout and Projection Elements

| Element | Required responsibility | Closest web/CSS analogue |
| --- | --- | --- |
| `CanvasPanel` | Absolute positioning with `Left`, `Top`, `Right`, `Bottom`, anchor, and explicit z-order attached properties | Absolutely positioned containing block |
| `OverlayPanel` | All children share the arranged content box; per-child alignment and z-order layer them | Stacking context/overlaid grid area |
| `StackPanel` | Ordered horizontal or vertical flow with gap and cross-axis alignment | Simple flex column/row without flexing |
| `WrapPanel` | Ordered flow that wraps at available extent with line and item gaps | CSS flex-wrap for intrinsic items |
| `FlexPanel` | Row/column direction, reverse, wrapping, order, grow, shrink, basis, justify, align, align-content, row gap, and column gap | CSS flexbox |
| `GridPanel` | Explicit rows/columns; pixel, auto, percent, fractional/star, `MinMax`, and `FitContent` tracks; row/column/span placement; gaps; per-child alignment | CSS grid subset |
| `Viewbox` | One child scaled uniformly or non-uniformly into its box with contain/cover/fill modes and alignment | SVG `viewBox` plus CSS object-fit |
| `ContentPresenter` | Projects one logical control/value or one `DataTemplate` result into a visual slot without taking logical ownership | Component slot/single content projection |
| `ItemsPresenter` | Hosts the panel produced by an `ItemsPanelTemplate` and connects it to an owner-provided item generator | Repeated component slot/list projection |
| `ScrollPresenter` | Clips and offsets one visual child and reports viewport, extent, and offset; scrolling policy and input remain on the semantic owner | Overflow viewport, not the scrollbar widget |

Every layout element uses one shared typed length model. Element width/height constraints accept `Auto`, device-independent pixels, and percentages where a definite containing size exists. Grid tracks additionally accept fractional/star units, `MinMax`, and `FitContent`; flex basis accepts `Auto`, pixels, percentages, and content basis. Cyclic percentage dependencies resolve through a documented measure fallback and produce diagnostics in statically provable cases rather than iterating without a bound.

Presenters are foundational because templates need behavior-free projection points, but they are not general-purpose decoration. Specialized leaves such as `LineEditPresenter`, rich-text layout, graph canvas, tree cells, video/subviewport output, and color-field rendering are permitted only when the domain requires rendering or hit geometry that cannot be composed from the general vocabulary. Their semantic owner remains templated, and each specialized presenter exposes the narrowest possible typed rendering contract.

#### Foundational Value Resources

| Resource family | Required first-release types and behavior |
| --- | --- |
| Brushes | `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, `ConicGradientBrush`, and `ImageBrush`; gradient stops, relative/absolute coordinates, spread/repeat mode, interpolation color space, brush opacity, and brush transform |
| Geometry | `RectangleGeometry`, `EllipseGeometry`, `LineGeometry`, `PathGeometry`, `GeometryGroup`, and `CombinedGeometry`; union/intersect/exclude/xor; even-odd/nonzero fill rules; immutable/frozen sharing |
| Path data | Absolute and relative `M`, `L`, `H`, `V`, `C`, `S`, `Q`, `T`, `A`, and `Z` commands with culture-invariant numbers, matching the useful SVG path vocabulary |
| Transforms | `TranslateTransform`, `ScaleTransform`, `RotateTransform`, `SkewTransform`, `MatrixTransform`, and ordered `TransformGroup`; every `Control` also has transform origin |
| Drawings | `GeometryDrawing`, `ImageDrawing`, `TextDrawing`, and `DrawingGroup`, consumable through `DrawingImage`; groups carry transform, clip, opacity, and child drawings without creating live controls |
| Effects | Bounded `BoxShadow`, shape/text `DropShadowEffect`, `BlurEffect`, `ColorMatrixEffect`, and ordered `EffectGroup`; explicit expansion bounds, cache policy, device-loss behavior, and at most one bounded effect group per composited element |
| Clipping and masks | Rectangle or arbitrary `Geometry` clip plus brush-based `OpacityMask`; clip/mask coordinates may be relative to the element or absolute |
| Stroke and paint enums | Line cap/join, miter, dash pattern, stroke alignment, fill rule, gradient spread, interpolation space, image sampling, stretch, alignment, and tile mode |

`DrawingImage` is the reusable, non-interactive vector-asset equivalent of an SVG image. Tooling may compile an SVG subset into `DrawingImage` resources, but Forma does not ship a browser SVG DOM, runtime XML parser, CSS-inside-SVG engine, scripting model, or SVG filter implementation. Unsupported SVG features fail at asset compilation instead of falling back to runtime interpretation.

The first compositing contract is finite and deterministic. Phase 0 records concrete backend-tested constants for maximum effect-group length, shadow count, blur radius, expanded offscreen bounds, offscreen nesting depth, render-target dimension/area, and total device-scoped target/cache bytes. Exceeding a statically known limit is a compiler diagnostic; exceeding a data-bound runtime limit disables that effect for the element and reports one bounded diagnostic rather than allocating without limit. Composition order is outer shadow, element content and descendants, geometry/bounds clip, opacity mask, ordered effect group, element opacity, then parent composition; inset shadows participate in element content before clipping. One element owns at most one offscreen effect group, nested elements consume the same depth and byte budgets, and cache eviction is least-recently-used with frame-safe disposal. `EffectGroup` is the only supported filter chain; arbitrary shader/filter graphs remain deferred.

#### Universal Visual and Box Properties

Every concrete foundational element and semantic control receives the same compositional surface from `Control`:

- `Width`, `Height`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight`, `AspectRatio`, `Margin`, horizontal/vertical alignment, and pixel-snapping policy.
- `Opacity`, `Visibility`, `IsHitTestVisible`, `ZIndex`, inherited enabled state, flow direction, language, cursor, and tooltip attachment.
- `RenderTransform`, `TransformOrigin`, rectangular or geometry `Clip`, `ClipToBounds`, `OpacityMask`, and bounded `Effect`.
- `Classes`, `Name`, resources, inherited data context/design tokens, style value layers, transitions, and selector participation.
- Consistent world-transform, clip, opacity, and hit-test composition used by drawing, pointer picking, focus bounds, accessibility bounds, and bring-into-view.

Text remains package-independent. `TextBlock`, `Run`, `Span`, `TextDrawing`, and text-bearing presenters accept only the core `UIFont`/`UIFontFamily` abstractions and retained text-layout results. With core packages alone, explicit `SpriteFontAdapter` fonts provide deterministic static-font measurement and drawing; unsupported shaping or glyph coverage follows the documented core font fallback rather than loading an optional assembly. The DynamicText companion may install `DynamicUIFont` providers and packaged defaults through its existing compile-time initializer, but foundational types, templates, generated factories, and default themes never name or require those provider types.

Background, border, padding, and shadows deliberately belong to `Border` rather than every `Control`. Authors compose or template a `Border` when they need a CSS-like box. This keeps the universal base small and avoids every shape, panel, and presenter carrying duplicate surface rendering behavior. Multiple backgrounds or borders are represented by nested `Border` elements or layered children in `OverlayPanel`.

#### Completeness and Extension Boundary

“HTML/CSS-like freedom” means that ordinary 2D application UI and control chrome expressible with boxes, flex/grid/absolute layout, text, bitmap/vector images, SVG-like paths, gradients, shadows, clipping, masks, transforms, opacity, and bounded effects can be authored entirely in XAML without creating a semantic control or overriding rendering. It does not mean browser layout compatibility or support for every CSS/SVG filter, blend mode, foreign object, script, or media feature.

The foundation is complete for this release only when all of the following pass on MonoGame and FNA:

1. Every packaged default `ControlTemplate` is composed solely from the catalog above plus documented specialized presenters.
2. Catalog fixtures reproduce a responsive dashboard/card layout, navigation bar, dialog, data grid/list row, radically different button skins, a vector icon set, a multi-layer illustration, and text-heavy content without a custom `Draw` override.
3. A compiled vector fixture exercises every path command, fill rule, stroke mode, gradient kind, geometry combination, transform, clip, mask, shadow, and bounded effect with screenshot parity.
4. Layout fixtures exercise absolute, overlay, stack, wrapping, flex, grid, percentage/min-max/aspect constraints, RTL, high display scale, and constrained overflow without manual positioning code.
5. Foundation types have deterministic measure/arrange, rendering, hit-test, resource, selector, animation, device-loss, serialization, trimming, and NativeAOT tests.

For visuals outside that declared envelope, Forma provides a low-level `DrawingElement : Control` extension point with a protected typed `Render(DrawingContext)` method and explicit measure/hit-test hooks. It is non-templated and NativeAOT-safe, and custom effects may be registered through a backend-parity effect contract. This escape hatch is analogous to HTML canvas or a custom WebGL element; it is not required for ordinary control theming, and a custom semantic widget should still wrap specialized drawing in a narrow presenter behind a replaceable `ControlTemplate`.

### Primitive Composition, Brushes, Shapes, and Effects

Foundational elements render directly and can be combined without creating a custom control or overriding `Draw`:

```xml
<Border xmlns="https://forma.dev/xaml"
				Width="560"
				MaxWidth="720"
				Padding="24"
				CornerRadius="14"
				BorderBrush="#66FFFFFF"
				BorderThickness="1"
				ClipToBounds="True">
	<Border.Background>
		<LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
			<GradientStop Offset="0" Color="#FF17212B" />
			<GradientStop Offset="1" Color="#FF213B42" />
		</LinearGradientBrush>
	</Border.Background>
	<Border.Shadow>
		<BoxShadow Color="#66000000" Offset="0,8" BlurRadius="24" SpreadRadius="0" />
	</Border.Shadow>

	<GridPanel ColumnGap="16" RowGap="8">
		<GridPanel.ColumnDefinitions>
			<ColumnDefinition Width="Auto" />
			<ColumnDefinition Width="MinMax(180,1*)" />
			<ColumnDefinition Width="Auto" />
		</GridPanel.ColumnDefinitions>

		<EllipseShape Width="48" Height="48" Fill="#FF42C8B7" />
		<StackPanel GridPanel.Column="1" Gap="4">
			<TextBlock Text="Build completed" FontSize="18" FontWeight="SemiBold" />
			<TextBlock Text="NativeAOT package is ready" Foreground="#BFFFFFFF" />
		</StackPanel>
		<PathShape GridPanel.Column="2"
							 Width="20"
							 Height="20"
							 Data="M 2,10 L 8,16 L 18,4"
							 Stroke="#FF42C8B7"
							 StrokeThickness="2">
			<PathShape.RenderTransform>
				<RotateTransform Angle="-4" Center="10,10" />
			</PathShape.RenderTransform>
		</PathShape>
	</GridPanel>
</Border>
```

This fixture covers the box model, explicit grid tracks, attached placement, text inheritance, gradient brushes, geometry, opacity-ready compositing, clipping, transforms, and bounded shadows.

### Flex Layout, Adaptive Conditions, and Transitions

Responsive behavior remains typed XAML. Adaptive conditions participate in the same value precedence as ordinary styles:

```xml
<FlexPanel xmlns="https://forma.dev/xaml"
					 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
					 x:Name="CommandBar"
					 Classes="command-bar"
					 Direction="Row"
					 Wrap="NoWrap"
					 JustifyContent="SpaceBetween"
					 AlignItems="Center"
					 Gap="12">
	<FlexPanel.Resources>
		<ResourceDictionary>
			<Style x:Key="NarrowCommandBar" Selector="FlexPanel.command-bar">
				<Style.Condition>
					<AdaptiveCondition MaxViewportWidth="720" />
				</Style.Condition>
				<Setter Property="Direction" Value="Column" />
				<Setter Property="AlignItems" Value="Stretch" />
			</Style>

			<Style x:Key="TouchTargets" Selector="Button.command">
				<Style.Condition>
					<AdaptiveCondition InputModality="Touch" />
				</Style.Condition>
				<Setter Property="MinHeight" Value="44" />
			</Style>

			<Style x:Key="DimDisabledCommands" Selector="Button.command:disabled">
				<Setter Property="Opacity" Value="0.45" />
				<Style.Transitions>
					<FloatTransition Property="Opacity" Duration="0:0:0.12" Easing="CubicOut" />
				</Style.Transitions>
			</Style>
		</ResourceDictionary>
	</FlexPanel.Resources>

	<TextBlock Text="3 changes pending" FlexPanel.Grow="1" />
	<Button Classes="command" Content="Discard" />
	<Button Classes="command primary" Content="Publish" />
</FlexPanel>
```

Additional conditions use the same `AdaptiveCondition` shape with `MinViewportWidth`, `MinViewportHeight`, `MaxViewportHeight`, `DisplayScale`, and `ThemeVariant`. Multiple values on one condition are ANDed; separate styles compose through normal specificity and declaration order.

### A Fully Replaceable Button

The semantic `Button` owns focus, pointer capture, keyboard activation, enabled state, and `Pressed`. Its template owns all visuals:

```xml
<ResourceDictionary xmlns="https://forma.dev/xaml"
										xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<SolidColorBrush x:Key="CommandSurface" Color="#FF25323D" />
	<SolidColorBrush x:Key="CommandHover" Color="#FF315565" />

	<ControlTemplate x:Key="CommandButtonTemplate" TargetType="Button">
		<Border x:Name="Chrome"
						Classes="chrome"
						Padding="12,8"
						CornerRadius="6"
						Background="{DynamicResource CommandSurface}"
						BorderBrush="#55FFFFFF"
						BorderThickness="1">
			<ContentPresenter Content="{Binding Content,
																					RelativeSource={RelativeSource TemplatedParent}}"
												ContentTemplate="{Binding ContentTemplate,
																									RelativeSource={RelativeSource TemplatedParent}}"
												HorizontalContentAlignment="Center"
												VerticalContentAlignment="Center" />
		</Border>
	</ControlTemplate>

	<Style x:Key="CommandButton" Selector="Button.command">
		<Setter Property="Template" Value="{StaticResource CommandButtonTemplate}" />
	</Style>
	<Style x:Key="CommandButtonHover"
				 Selector="Button.command:hover >> Border.chrome">
		<Setter Property="Background" Value="{DynamicResource CommandHover}" />
		<Setter Property="RenderTransform">
			<Setter.Value>
				<ScaleTransform ScaleX="1.02" ScaleY="1.02" />
			</Setter.Value>
		</Setter>
	</Style>
	<Style x:Key="CommandButtonFocus"
				 Selector="Button.command:focus >> Border.chrome">
		<Setter Property="BorderBrush" Value="#FFFFFFFF" />
		<Setter Property="BorderThickness" Value="2" />
	</Style>
</ResourceDictionary>
```

Application content can be text or an arbitrary visual subtree without changing `Button` behavior:

```xml
<Button xmlns="https://forma.dev/xaml" Classes="command">
	<FlexPanel Direction="Row" AlignItems="Center" Gap="8">
		<ThemeIconView ThemeItemName="publish" ThemeTypeName="Button" />
		<StackPanel Gap="1">
			<TextBlock Text="Publish build" FontWeight="SemiBold" />
			<TextBlock Text="Release channel" FontSize="11" Opacity="0.7" />
		</StackPanel>
	</FlexPanel>
</Button>
```

An individual instance can replace the structure inline instead of using the keyed template:

```xml
<Button xmlns="https://forma.dev/xaml" Content="Minimal action">
	<Button.Template>
		<ControlTemplate TargetType="Button">
			<ContentPresenter Content="{Binding Content,
																					RelativeSource={RelativeSource TemplatedParent}}" />
		</ControlTemplate>
	</Button.Template>
</Button>
```

### All Relative Source Forms

Relative sources remain compiled and typed; they never perform runtime property discovery:

```xml
<ControlTemplate xmlns="https://forma.dev/xaml" TargetType="ListBoxItem">
	<GridPanel>
		<!-- Self: Width and Height are validated against RectangleShape. -->
		<RectangleShape Height="24"
										Width="{Binding Height, RelativeSource={RelativeSource Self}}"
										Fill="#FF42C8B7" />

		<!-- TemplatedParent: validated against ControlTemplate.TargetType. -->
		<ContentPresenter Content="{Binding Content,
																				RelativeSource={RelativeSource TemplatedParent}}" />

		<!-- FindAncestor: explicit type and one-based level determine the source type. -->
		<TextBlock Enabled="{Binding Enabled,
																 RelativeSource={RelativeSource FindAncestor,
																																AncestorType=ListBox,
																																AncestorLevel=1}}" />
	</GridPanel>
</ControlTemplate>
```

`TemplatedParent` is valid only inside a control template. `FindAncestor` walks visual parents, rebinds after visual reparenting, and cannot cross a detached template instance.

### Keyed Data Templates, Items Panels, and Virtualization

Templates are explicit. No item-type lookup or reflection selects a row implicitly:

```xml
<ResourceDictionary xmlns="https://forma.dev/xaml"
										xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
										xmlns:vm="clr-namespace:Game.ViewModels">
	<DataTemplate x:Key="ProjectCard" x:DataType="vm:ProjectRowViewModel">
		<Border Classes="project-card" Padding="12" CornerRadius="6">
			<GridPanel ColumnGap="10">
				<GridPanel.ColumnDefinitions>
					<ColumnDefinition Width="1*" />
					<ColumnDefinition Width="Auto" />
				</GridPanel.ColumnDefinitions>
				<StackPanel Gap="2">
					<TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
					<TextBlock Text="{Binding Status}" Opacity="0.7" />
				</StackPanel>
				<TextBlock GridPanel.Column="1"
									 Text="{Binding Duration, StringFormat='{0:mm\\:ss}'}" />
			</GridPanel>
		</Border>
	</DataTemplate>

	<ItemsPanelTemplate x:Key="ProjectStack">
		<VirtualizingStackPanel Orientation="Vertical"
														OverscanBefore="2"
														OverscanAfter="3" />
	</ItemsPanelTemplate>

	<ItemsPanelTemplate x:Key="ProjectGrid">
		<VirtualizingGridPanel CellWidth="260"
													 EstimatedCellHeight="84"
													 ColumnGap="12"
													 RowGap="12"
													 OverscanRows="1" />
	</ItemsPanelTemplate>

	<ItemsPanelTemplate x:Key="ProjectStrip">
		<VirtualizingStackPanel Orientation="Horizontal"
														EstimatedItemExtent="280"
														OverscanBefore="1"
														OverscanAfter="2" />
	</ItemsPanelTemplate>

	<Style x:Key="ProjectContainerStyle" Selector="ListBoxItem">
		<Setter Property="Margins" Value="0,0,0,6" />
	</Style>
</ResourceDictionary>
```

The same item template can feed a non-selecting list, a virtualized grid, or a selector:

```xml
<StackPanel xmlns="https://forma.dev/xaml" Gap="16">
	<ItemsControl ItemsSource="{Binding RecentProjects}"
								ItemTemplate="{StaticResource ProjectCard}"
								ItemsPanel="{StaticResource ProjectStack}" />

	<ItemsControl ItemsSource="{Binding AllProjects}"
								ItemTemplate="{StaticResource ProjectCard}"
								ItemsPanel="{StaticResource ProjectGrid}" />

	<ListBox ItemsSource="{Binding SearchResults}"
					 ItemTemplate="{StaticResource ProjectCard}"
					 ItemsPanel="{StaticResource ProjectStack}"
					 ItemContainerStyle="{StaticResource ProjectContainerStyle}"
					 AlternationCount="2"
					 SelectionMode="Single"
					 SelectedItem="{Binding SelectedProject, Mode=TwoWay}" />
</StackPanel>
```

Inline templates use the same compiler and lifetime path when reuse is unnecessary:

```xml
<ItemsControl xmlns="https://forma.dev/xaml"
			  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
							xmlns:vm="clr-namespace:Game.ViewModels"
							ItemsSource="{Binding RecentProjects}">
	<ItemsControl.ItemTemplate>
		<DataTemplate x:DataType="vm:ProjectRowViewModel">
			<TextBlock Text="{Binding Name}" />
		</DataTemplate>
	</ItemsControl.ItemTemplate>
	<ItemsControl.ItemsPanel>
		<ItemsPanelTemplate>
			<StackPanel Gap="4" />
		</ItemsPanelTemplate>
	</ItemsControl.ItemsPanel>
</ItemsControl>
```

`AlternationCount="2"` exposes a typed `AlternationIndex` of `0` or `1` on each generated container and preserves it correctly through moves and recycling. `SelectionMode` may instead be `Multi` or `Toggle`. In those modes selection is tracked by source occurrence, so duplicate object references remain independently selectable; `SelectedItems` and `SelectedIndices` are read-only projections and ambiguous two-way collection binding is rejected.

### ListBox Chrome and Selection State

`ItemsPresenter` and `ScrollPresenter` let a template rearrange scrolling chrome without taking item ownership away from `ListBox`:

```xml
<ControlTemplate xmlns="https://forma.dev/xaml"
								 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
								 x:Key="FramedListBox"
								 TargetType="ListBox">
	<Border Classes="list-frame" Padding="6" CornerRadius="8">
		<ScrollPresenter x:Name="PART_ScrollPresenter">
			<ItemsPresenter x:Name="PART_ItemsPresenter" />
		</ScrollPresenter>
	</Border>
</ControlTemplate>
```

Container state can style a part inside each realized item template while preserving template encapsulation:

```xml
<ResourceDictionary xmlns="https://forma.dev/xaml"
										xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<ControlTemplate x:Key="SelectableRow" TargetType="ListBoxItem">
		<Border Classes="selection" Padding="4" CornerRadius="4">
			<ContentPresenter Content="{Binding Content,
																					RelativeSource={RelativeSource TemplatedParent}}"
												ContentTemplate="{Binding ContentTemplate,
																									RelativeSource={RelativeSource TemplatedParent}}" />
		</Border>
	</ControlTemplate>
	<Style x:Key="ListBoxItemTemplate" Selector="ListBoxItem">
		<Setter Property="Template" Value="{StaticResource SelectableRow}" />
	</Style>
	<Style x:Key="SelectedRow"
				 Selector="ListBoxItem:selected >> Border.selection">
		<Setter Property="Background" Value="#FF245B68" />
	</Style>
	<Style x:Key="CurrentRow"
				 Selector="ListBoxItem:current >> Border.selection">
		<Setter Property="BorderBrush" Value="#FFFFFFFF" />
	</Style>
</ResourceDictionary>
```

Scrolling 100,000 rows realizes only the visible range plus overscan. Recycling calls `Deactivate`, `Rebind`, and `Activate` on template instances; selection and current-item identity remain attached to logical source slots rather than recycled controls.

### Flat and Hierarchical Data Grids

Forma calls the combined flat-table and tree-table control `DataGrid`. The name follows the existing semantic-control vocabulary (`ListBox`, `Tree`, and `GridPanel`) without importing Avalonia's `TreeDataGrid` product name or adding a `View` suffix. `DataGrid : ListBox` reuses source-occurrence identity, selection, scrolling, container recycling, and viewport anchoring while adding shared columns and hierarchical row projection. `DataGridRow : ListBoxItem` is the generated row container, and `DataGridCell : ContentControl` is the generated cell container. The existing retained `Tree` remains supported and is migrated behind a specialized presenter in the semantic-widget phase; `DataGrid` is the data-bound, template-first control for new tabular views.

Columns are explicit typed XAML resources. `DataGridTextColumn`, `DataGridCheckBoxColumn`, and `DataGridTemplateColumn` derive from `DataGridColumn`; `DataGridExpanderColumn` wraps one inner column and supplies compiled `Children`, optional `HasChildren`, and optional two-way `IsExpanded` bindings. There is no reflected property-path discovery or automatic column generation in this release. Column headers, cells, expanders, rows, and the outer grid have replaceable `ControlTemplate` surfaces and documented pseudo-states for sort direction, selection, current cell, expansion, and hierarchy depth.

Flat data uses ordinary `ItemsSource`:

```xml
<DataGrid xmlns="https://forma.dev/xaml"
					xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
					xmlns:vm="clr-namespace:Game.ViewModels"
					x:DataType="vm:ProjectsViewModel"
					ItemsSource="{Binding Projects}"
					SelectionUnit="Row"
					SelectionMode="Single"
					CanUserSortColumns="True"
					CanUserResizeColumns="True">
	<DataGrid.Columns>
		<DataGridTextColumn Header="Name"
								Binding="{Binding Name}"
								SortBinding="{Binding Name}"
								Width="2*" />
		<DataGridTextColumn Header="Status"
								Binding="{Binding Status}"
								SortBinding="{Binding Status}"
								Width="*" />
		<DataGridCheckBoxColumn Header="Pinned"
									Binding="{Binding IsPinned, Mode=TwoWay}"
									SortBinding="{Binding IsPinned}"
									Width="96" />
	</DataGrid.Columns>
</DataGrid>
```

Hierarchical data uses the same `ItemsSource` for roots and one expander column for child discovery. Row addresses are immutable `IndexPath` values, where each component identifies a source occurrence at the corresponding depth; duplicate and null model values therefore remain distinct. `CellIndex` combines a column index with an `IndexPath`.

```xml
<DataGrid xmlns="https://forma.dev/xaml"
					xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
					xmlns:vm="clr-namespace:Game.ViewModels"
					x:DataType="vm:SceneTreeViewModel"
					ItemsSource="{Binding Roots}"
					Mode="Hierarchical"
					SelectionUnit="Cell"
					SelectionMode="Multi">
	<DataGrid.Columns>
		<DataGridExpanderColumn Header="Node"
									Children="{Binding Children}"
									HasChildren="{Binding HasChildren}"
									IsExpanded="{Binding IsExpanded, Mode=TwoWay}"
									Width="2*">
			<DataGridTextColumn Binding="{Binding Name}"
									SortBinding="{Binding Name}" />
		</DataGridExpanderColumn>
		<DataGridTemplateColumn Header="State" Width="*">
			<DataGridTemplateColumn.CellTemplate>
				<DataTemplate x:DataType="vm:SceneNodeViewModel">
					<StackPanel Orientation="Horizontal" Gap="6">
						<EllipseShape Width="8" Height="8" Fill="{Binding StateBrush}" />
						<TextBlock Text="{Binding State}" />
					</StackPanel>
				</DataTemplate>
			</DataGridTemplateColumn.CellTemplate>
		</DataGridTemplateColumn>
	</DataGrid.Columns>
</DataGrid>
```

Header activation applies one ascending or descending `DataGridSortDescription`; the public collection also supports stable multi-column programmatic sorting. Every sortable column requires a compiled `SortBinding` or typed comparer. Filtering is supplied by a typed predicate on `DataGridSource<T>` or an application-owned filtered `ItemsSource`; replacing the predicate and explicit `RefreshFilter` are cold operations allowed to visit the source, never warm-frame work. In hierarchical mode the default predicate evaluates each row independently; an opt-in `IncludeAncestorsOfMatches` policy retains ancestor paths, with bounded cycle detection and documented full-tree cost. Sorting and filtering preserve logical slot tokens for surviving occurrences, current/selection identity, expansion state, and viewport anchors.

`SelectionUnit` is `Row` or `Cell`; both support `Single` and `Multi`. Row selection uses the inherited slot model and exposes selected `IndexPath` values; cell selection exposes read-only `CellIndex` projections and rectangular range operations. Expansion has `Expand`, `Collapse`, `Toggle`, `ExpandAll`, `CollapseAll`, and cancellable expanding/collapsing plus completed events. `HasChildren` permits an expander without eagerly enumerating children, and expanding may populate an observable child collection before realization.

Only visible flattened rows plus overscan, pinned interactions, and a fixed transition reserve are realized. Collapsed descendants have no row or cell containers. The flattened hierarchy uses a dynamic visible-subtree index so expand/collapse and localized child deltas update affected ranges without rebuilding unrelated branches. Rows and their cells recycle together; columns are not virtualized in this release, so every visible row realizes one cell per visible column and the documented column-count limit remains part of the deterministic allocation gate.

### Eventful Rows Own Their Handlers

An event attribute is not legal directly inside a `DataTemplate`. The template instantiates an ordinary compiled `x:Class` row instead:

```xml
<ResourceDictionary xmlns="https://forma.dev/xaml"
										xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
										xmlns:vm="clr-namespace:Game.ViewModels"
										xmlns:views="clr-namespace:Game.Views">
	<DataTemplate x:Key="EventfulProjectRow" x:DataType="vm:ProjectRowViewModel">
		<views:ProjectRowView />
	</DataTemplate>
</ResourceDictionary>
```

`ProjectRowView.xaml`:

```xml
<GridPanel xmlns="https://forma.dev/xaml"
					 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
					 xmlns:vm="clr-namespace:Game.ViewModels"
					 x:Class="Game.Views.ProjectRowView"
					 x:DataType="vm:ProjectRowViewModel"
					 ColumnGap="12">
	<GridPanel.ColumnDefinitions>
		<ColumnDefinition Width="1*" />
		<ColumnDefinition Width="Auto" />
	</GridPanel.ColumnDefinitions>
	<TextBlock Text="{Binding Name}" />
	<Button GridPanel.Column="1" Content="Open" Pressed="OnOpenPressed" />
</GridPanel>
```

`ProjectRowView.cs`:

```csharp
using System;
using Forma;
using Forma.Xaml;
using Game.ViewModels;

namespace Game.Views;

public sealed class ProjectRowView : GridPanel
{
		public ProjectRowView() => FormaXamlLoader.Load(this);

		private void OnOpenPressed(object? sender, EventArgs args)
		{
				if (DataContext is ProjectRowViewModel project)
						project.Open();
		}
}
```

The handler is resolved only against `ProjectRowView`; it is never routed to the screen containing the `ItemsControl`.

### Observable Collection Deltas and Selection Binding

Ordinary .NET collections and notifications drive the item view without per-frame enumeration:

```csharp
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;

namespace Game.ViewModels;

public sealed class ProjectsViewModel : INotifyPropertyChanged
{
		private ProjectRowViewModel? _selectedProject;

		public ObservableCollection<ProjectRowViewModel> Projects { get; } = new();

		public ProjectRowViewModel? SelectedProject
		{
				get => _selectedProject;
				set
				{
						if (ReferenceEquals(_selectedProject, value)) return;
						_selectedProject = value;
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedProject)));
				}
		}

		public void Add(ProjectRowViewModel project) => Projects.Add(project);
		public void Remove(ProjectRowViewModel project) => Projects.Remove(project);
		public void Move(int oldIndex, int newIndex) => Projects.Move(oldIndex, newIndex);
		public void Replace(int index, ProjectRowViewModel project) => Projects[index] = project;
		public void Reset(IEnumerable<ProjectRowViewModel> projects)
		{
				Projects.Clear();
				foreach (var project in projects) Projects.Add(project);
		}

		public event PropertyChangedEventHandler? PropertyChanged;
}
```

Add/remove preserve unaffected logical slots, move preserves slot/selection identity and preserves a compatible container only while that slot remains realized, replace creates a new slot, and reset rebuilds. Once attached, notifications from a non-UI thread fail deterministically instead of being silently dispatched.

### Complex Controls Keep Replaceable Chrome

Specialized text, graph, tree, media, or viewport parts remain narrow direct-rendering leaves. The semantic owner retains behavior while the surrounding structure stays replaceable:

```xml
<ResourceDictionary xmlns="https://forma.dev/xaml"
										xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
	<ControlTemplate x:Key="CompactLineEdit" TargetType="LineEdit">
		<Border Classes="editor-chrome"
						Padding="8,6"
						CornerRadius="4"
						Background="#FF11181F">
			<LineEditPresenter x:Name="PART_Editor"
												 Text="{Binding Text,
																				RelativeSource={RelativeSource TemplatedParent},
																				Mode=OneWay}" />
		</Border>
	</ControlTemplate>
</ResourceDictionary>
```

`LineEdit` still owns editing, selection, IME, clipboard, keyboard, focus, and validation semantics. `LineEditPresenter` owns only glyph, caret, selection, and composition rendering/hit geometry. Replacing the border or layout cannot remove editing behavior.

### First-Party Application Continuity

The existing applications under `samples/Forma.Catalog*` and `samples/Forma.Xaml.Game*` are compatibility fixtures, not disposable demonstrations to be rewritten after the architecture is complete. A phase that changes a public control, visual tree, XAML construct, compiler artifact, theme resource, or attachment lifecycle must migrate every affected Catalog story and Signal Run view in the same change. Phase exit requires their shared projects and thin MonoGame/FNA hosts to build in Debug and Release; no phase may rely on a long-lived compatibility shim, duplicate legacy renderer, or sample-only fork of the runtime contract.

The Catalog must preserve its Storybook-style inventory, search, selected-story surface, writable-property editors, typography and DynamicText stories/toggle, high-display-scale behavior, metrics/screenshot output, render-parity workflow, and Debug XAML/effect hot reload. It remains the opt-in full-feature application that intentionally exercises the matching DynamicText and Media companions; that does not weaken the core-only package boundary.

Signal Run must remain one shared collect-and-score game with thin MonoGame and FNA hosts. Its deterministic collect, pause, low-time, result, and restart flow; keyboard controls; HUD/settings/result XAML views; typed bindings; resources; selector styles; state/event storyboards; player-name, difficulty, sound, and volume editing; and Debug XAML hot reload must continue to work. Reloading a view must retain the active view model, score, timer, and settings, invalid XAML must leave the current view intact, and Release builds must exclude hot reload. Signal Run remains the core-only sample and may not acquire a DynamicText, Media, compiler-runtime, or hot-reload dependency in Release.

Before Phase 1, capture the current Catalog metrics/screenshots and Signal Run behavior/hot-reload test results for both runtime peers. After each phase, run the affected focused tests plus `make build`, `CONFIGURATION=Release make build`, `make test-xaml`, and `CONFIGURATION=Release make test-xaml`; phases that touch rendering, themes, input, focus, layout, text, or templates also require `make smoke` and Catalog render parity on a graphical validation host. Baselines may change only when the plan intentionally changes documented appearance or behavior, and each approved change records the reason rather than silently replacing the expected output.

### Runtime, Theme, Hot Reload, and Packaging Guarantees

The examples above carry these non-visual guarantees:

- Replacing a theme template reapplies affected semantic widgets at a frame boundary while preserving owner state. Dynamic brush/resource changes update the winning value without reconstructing unrelated trees.
- A valid hot-reload edit to a primitive, style, template, or eventful row replaces only the affected live instances. Invalid edits report diagnostics and leave the current visual tree intact.
- Template instances own their namescope, bindings, styles, resource subscriptions, triggers, transitions, and clocks. Pooling deactivates those scopes; rebinding changes the item context; eviction disposes them exactly once.
- Release compilation emits closed typed factories and typed binding/selector accessors. Packed, trimmed, and NativeAOT consumers contain no source XAML, runtime reader, reflection fallback, compiler, Cecil, or dynamic-code dependency.
- `Forma.MonoGame` and `Forma.FNA` remain complete native-free runtime profiles for this plan. Foundational visuals, layout, templates, selectors, items, selection, virtualization, and compiled XAML may not reference `Forma.DynamicText`, FreeTypeSharp, HarfBuzzSharp, or their native assets.
- `TextBlock` and text-bearing presenters consume only the core `UIFont`, `UIFontFamily`, and retained text-layout contracts. Core-only applications continue to use explicit fonts such as `SpriteFontAdapter`; installing `Forma.DynamicText.MonoGame` or `Forma.DynamicText.FNA` adds runtime-loaded fonts, shaping, rasterization, and its optional default-font initializer without changing XAML factory signatures or template behavior.
- Dynamic-text provider discovery remains compile-time/package-initializer based. Core runtime code and generated XAML perform no assembly scanning, reflection activation, dynamic generic construction, or fallback package probing. Missing optional services produce documented font-resolution behavior, never a template/compiler failure.
- Development XAML hot reload remains an optional non-trimmed host feature. Release applications, including applications that omit the hot-reload and DynamicText packages, execute the same precompiled template/selector IR and require neither SRE nor dynamic-code support.
- Warm frames do not compile templates, scan the full visual tree for selectors, enumerate the full item source, or allocate new realized controls during steady-state recycled scrolling.

Package gates cover two independent NativeAOT profiles for each runtime peer: a core-only consumer with `SpriteFontAdapter` and no DynamicText managed/native assets, and an opt-in DynamicText consumer with its declared packaged native libraries. The core-only profile is the portability baseline; no feature in this plan may make the companion package transitively required. Both profiles inspect generated assemblies for forbidden reflection/dynamic-code call sites and embedded source XAML, then publish and execute representative template, text, selector, items, and virtualization fixtures.

## Progress Dashboard

- [x] Phase 0: Contract, Baselines, and Tracking
- [x] Phase 1: Visual Foundation, Tree, and Template Lifetime
- [x] Phase 2: Compiler and Language Semantics
- [x] Phase 3: Items and Incremental Collection Updates
- [x] Phase 4: Viewport and Virtualizing Panels
- [x] Phase 5: ListBox and Selection
- [x] Phase 6: DataGrid and Hierarchical Tables
- [x] Phase 7: ControlTemplate, Theme Lookup, and Semantic Widget Migration
- [x] Phase 8: Hot Reload, Tooling, and Diagnostics
- [x] Phase 9: Catalog, Documentation, Packaging, and Rollout

Check a phase only after every task and exit condition in that phase is complete. Run the shared tracker at the start and end of each implementation session:

```sh
bash scripts/track-plan.sh plans/xaml-templates-items-and-virtualization-plan.md
```

Use `--remaining` to list open work with line references and `--fail-if-incomplete` for the final completion gate. Update a task box only after its implementation and focused validation pass; record newly discovered required work in the owning phase instead of tracking it outside this plan.

**Implementation Checklist**

### Phase 0: Contract, Baselines, and Tracking
- [x] 1. Track this file with a progress dashboard, checklist items for every phase below, explicit exit criteria, and the established `bash scripts/track-plan.sh plans/xaml-templates-items-and-virtualization-plan.md` workflow. Record the current unit, XAML, package, NativeAOT, Catalog render/metrics/story inventory, Signal Run behavior/hot-reload, startup, allocation, and realized-control baselines for MonoGame and FNA before changing runtime behavior. Add a continuity row for each first-party application to every phase; a phase is incomplete while an affected Catalog story or Signal Run view still uses the superseded contract or either runtime host fails its build/test/smoke gate.
- [x] 2. Complete the renderer-feasibility gate defined under **Foundational Visual Vocabulary** on MonoGame and FNA, record backend-neutral architecture and finite compositing/tessellation/cache limits, then freeze the public language/API contract in `/Users/zignd/hack/games/engine-workshop/Forma/docs/xaml-language.md`: the foundational-element versus templated-widget boundary; primitive, panel, presenter, and specialized-part responsibilities; core-only versus optional DynamicText behavior; `DataTemplate`; `ControlTemplate`; `ItemsPanelTemplate`; `ItemsControl`; `ListBox`; explicit `ItemTemplate`; keyed template resources; template-local namescopes; visual-tree style selectors and template-boundary traversal; adaptive style conditions; row-owned event code-behind; `RelativeSource`; selection semantics; virtualization guarantees; and unsupported/deferred behaviors. Treat every snippet in **Proposed Capability Showcase** as a required canonical fixture; syntax may change only when the fixture and this plan change together and equivalent typed behavior remains. The contract cannot freeze a renderer feature that has not passed the spike or acquired an explicit bounded fallback.
- [x] 3. Create a complete migration manifest covering every concrete public `Control` type in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma`. Classify each as a drawing primitive, layout panel, presenter, semantic templated widget, or internal specialized rendering/input part. For foundational types, record direct-render/layout responsibility and styleable properties. For semantic widgets, record the default template, required named parts, projected logical content, states/pseudo-states, specialized part, existing API behavior to preserve, and unit/render/Catalog parity gate. For every row, inventory affected Catalog stories and Signal Run XAML/C# call sites and name their migration and behavior/render checks. A type may not be both a foundational primitive and a templated widget. This phase blocks the template-first switch.
- [x] 4. Define deterministic compatibility rules: `Control` remains the universal styleable visual/layout node and may render directly; new `TemplatedControl : Control` owns `Template` and application lifecycle; primitives, panels, presenters, and internal parts never resolve a default `ControlTemplate`; semantic widgets do not draw unconditional chrome; templates are selected explicitly for items; no implicit item-type matching or runtime reflection; event attributes are forbidden directly inside `DataTemplate`; eventful rows are separate `x:Class` controls referenced by the template; all collection notifications occur on the attached UI thread; and ordinary non-template XAML attachment scopes retain detach-means-dispose behavior.

### Phase 1: Visual Foundation, Tree, and Template Lifetime
- [x] 5. Add `TemplatedControl : Control` in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Control.cs` and make the architectural boundary enforceable in APIs and tests. `Control` supplies geometry, layout participation, input, resources, classes, inherited values, and direct visual composition. Only `TemplatedControl` exposes `Template`, `ApplyTemplate`, `TemplateRoot`, `GetTemplateChild`, invalidation, and templated-parent state. Template lookup must stop at this boundary, so a default template can never recursively request a template for its primitive root.
- [x] 6. Implement the exact **Foundational Visual Vocabulary** above in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/VisualPrimitives.cs` and supporting resource files on the Phase 0 `DrawingContext`/tessellation/compositor architecture: `Border`; package-independent `TextBlock` and typed inlines; bitmap/vector `Image`, `NineSliceImage`, and `ThemeIconView`; the six shape elements; brushes; geometries and SVG-like path data; drawings/`DrawingImage`; transforms; clips, masks, shadows, and bounded effects. Reuse or migrate `ColorRect`, `TextureRect`, `NinePatchRect`, `ThemeIconRect`, `StyleBox`, and text-layout code where behavior already exists; keep the public `ThemeIcon` value type and migrate the element name to `ThemeIconView`. Every element is bindable, resource-aware, animatable where its value type permits, independently styleable, and has deterministic minimum-size, clip, hit-test, device-loss, MonoGame/FNA, trimming, and NativeAOT semantics without owning a `ControlTemplate` or requiring DynamicText.
- [x] 7. Add `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/LayoutPanels.cs` with the exact foundational layout catalog: `CanvasPanel`, `OverlayPanel`, `StackPanel`, `WrapPanel`, `FlexPanel`, `GridPanel`, and `Viewbox`. Implement the shared typed length/constraint model, absolute anchors, layering, intrinsic flow, wrapping, complete declared flex behavior, explicit grid tracks and placement, gaps, alignment, percentage-cycle fallback, overflow/clip, RTL, and high-scale rounding.
- [x] 8. wted `DrawingElement` extension point. Define inherited text/font, foreground, enabled, direction, language, and design-token/resource values across logical projection and template visuals. Rendering, clipping, masking, effects, opacity, transforms, hit testing, focus/accessibility bounds, and bring-into-view must consume the same composed state; render-target/effect caches and shadow expansion must be bounded and device-loss safe.
- [x] 9. Refactor `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Control.cs` to distinguish logical ownership, visual composition, and value inheritance. Keep `Children`/`Parent` as the logical tree used by application content and lifetime ownership; add an internal visual-child/`VisualParent` traversal used by layout, drawing, hit testing, focus, style matching, and context attachment; and add one explicit `InheritanceParent` used by data context, resources, theme/design tokens, effective enabled state, language, flow direction, and inherited text values. An ordinary child inherits from its logical parent; a template root and its primitive descendants inherit through the templated owner unless an intervening visual establishes a local value; projected content retains its logical owner as inheritance parent even while hosted elsewhere; generated containers inherit from the items owner and their data-template roots inherit from the container; detached or pooled instances have no active inheritance parent until reactivated. Define local-value and nearer-ancestor precedence, cycle rejection, and one typed parent/inheritance invalidation notification so bindings, resource lookup, selectors, and effective-value caches update together. A presenter may project a logical child into a template visual slot without changing its logical owner.
- [x] 10. Add template runtime contracts in a new `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/Templates.cs`: `FrameworkTemplate`, `DataTemplate`, `ControlTemplate`, `ItemsPanelTemplate`, `TemplateBuildContext`, `TemplateInstance`, template-part metadata, typed compiled-factory delegates, and a core-only `DefaultControlTemplateRegistry`. Establish type-hierarchical lookup and packaged typed-factory registration here so later phases can add runnable default templates as each semantic control lands; no registry path may scan assemblies or reference compiler/DynamicText types. `ItemsPanelTemplate` creates a fresh compatible panel per items presenter and may not share a mutable panel instance. `TemplateInstance` owns exactly one root, one local namescope, its binding/style/resource/trigger/transition/clock scope, templated parent or item context, and explicit `Activate`, `Deactivate`, `Rebind`, and `Dispose` transitions.
- [x] 11. Add the exact foundational presenter catalog in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Presenters.cs`: `ContentPresenter` for one logical value/control and optional `DataTemplate`, `ItemsPresenter` for the generated `ItemsPanelTemplate` panel, and `ScrollPresenter` for viewport/offset/clip projection. Define content/content-template precedence, fallback text conversion, alignment, inherited context/resources, one-visual-host enforcement, attach/detach ordering, and replacement/disposal. Presenters never become logical owners of projected controls and never have templates themselves. Add narrowly scoped text/media/graph/tree presenters only through the specialized-leaf exception defined by the foundational contract and record each exception in the Phase 0 manifest.
- [x] 12. Extend `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/XamlAttachment.cs` so template-owned attachment scopes can deactivate and reactivate without disposal while pooled, then dispose deterministically on source removal, template replacement, pool eviction, owner destruction, or context disposal. Keep existing view-root detach disposal semantics and add leak/double-dispose/rebind tests.
- [x] 13. Implement `TemplatedControl` template application. Applying a template creates a local namescope; reapplying disposes the old instance; logical `DataContext`, resources, theme, enabled state, layout direction, focus state, and inherited styling flow into the visual tree through defined inheritance rather than copied values. Validate that the root is foundational or a deliberate nested semantic widget and reject self-recursive template graphs with a diagnostic.
- [x] 14. Make template- and style-facing properties observable without reflection. Implement `INotifyPropertyChanged` (or an equivalent typed shared notification channel) on `Control`, update mutable properties consumed by templates and visual selectors to emit only on real changes, and teach binding subscriptions to re-resolve ancestor sources when the visual parent changes. Do not introduce a general dependency-property system unless the focused notification spike proves the shared channel cannot satisfy two-way binding, inheritance, style invalidation, and value-layer precedence.

### Phase 2: Compiler and Language Semantics
- [x] 15. Extend `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/SemanticModel.cs` and `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/FormaXamlParser.cs` with explicit template nodes and lexical scope boundaries. `DataTemplate` requires one control root and an `x:DataType` when it contains bindings; `ControlTemplate` requires one visual root and a `TemplatedControl` `TargetType`; each template creates a namescope; duplicate names are checked per scope; storyboard/trigger names cannot cross a template boundary. Validate primitive/panel/presenter content models, attached layout properties, brushes/geometries, and template eligibility statically.
- [x] 16. Replace the regex-based special-feature stripping in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/FormaXamlCompiler.cs` with one complete backend-neutral typed lowered IR for bindings, resources, styles, triggers, transitions, templates, items-panel templates, brushes, geometries, attached layout properties, adaptive conditions, source ranges, scope IDs, and resolved symbol IDs. Template content must not be instantiated while the owning view is built. `Forma.Xaml.Compiler` owns semantic analysis and lowering; it exposes SRE and Cecil emitters over the same immutable IR, and parity fixtures compare emitted behavior and diagnostics for every construct. SRE is used only by optional development tooling and is never referenced by core/release runtime artifacts.
- [x] 17. Make `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Build/FormaXamlBuildTasks.cs` an MSBuild/Cecil orchestration and emission adapter over that shared IR. Its Cecil emitter writes one closed, typed factory method per template; the SRE emitter supplies equivalent detached factories only to the optional hot-reload host. Generated code directly constructs controls, primitives, panels, presenters, brushes, and geometries; creates the local namescope; attaches typed bindings/resources/styles/events/triggers; and returns a `TemplateInstance`. Neither emitter may call reflection, a service locator for property access, a runtime XAML reader, DynamicText-specific APIs, assembly scanning, or unbounded dynamic-code paths; the Release emitter has no `System.Reflection.Emit` dependency.
- [x] 18. Add compile-time `RelativeSource` binding sources with the canonical forms `Self`, `TemplatedParent`, and `FindAncestor` with required `AncestorType` and optional one-based `AncestorLevel`. Default bindings still use `DataContext`. Source types are resolved statically from the target element, `ControlTemplate.TargetType`, or explicit ancestor type; OneTime/OneWay/TwoWay rules and conversion remain typed. Ancestor bindings rebind on visual reparenting. Add stable template/relative-source diagnostic codes and source locations.
- [x] 19. Support inline templates and reusable keyed templates in `ResourceDictionary`; `ItemTemplate`, `ItemsPanel`, and `Template` accept direct property-element values or `{StaticResource ...}` of their corresponding template type. Keep implicit `DataType` template matching out of this release. Reject shared mutable template/panel instances, multiple template roots, unresolved target/data types, templates targeting foundational elements, cross-scope names, illegal nested `x:Class`, and event attributes directly inside a data template with actionable diagnostics explaining the separate row-control pattern.
- [x] 20. Implement the exact **Proposed Selector Language Contract** above by replacing the flat selector parser with a typed selector AST and compiled matcher over the visual tree. Support selector lists, universal/type/name/class terms, descendant and direct-child combinators, `:not(...)`, extensible pseudo-states, and Forma's explicit `>>` control-template combinator. Ordinary descendant selectors do not cross template or data-template boundaries; `>>` is required to style named/classed visual parts from outside, preserving component encapsulation. Add typed adaptive style conditions for viewport width/height, display scale, theme variant, and input modality. Defer sibling combinators and arbitrary property-value attribute selectors. Re-evaluation must be event-driven and proportional to the affected visual subtree, never a full-tree per-frame scan.
- [x] 21. Compile eventful rows as ordinary separate `x:Class` views. A `DataTemplate` may instantiate such a row control; `ItemsControl` assigns the item as its inherited `DataContext`; handlers resolve only on that row’s code-behind. Do not route template events to the outer screen root.

### Phase 3: Items and Incremental Collection Updates
- [x] 22. Add `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/ItemsControls.cs` with semantic `ItemsControl : TemplatedControl`, item-container generation, `ItemsSource`, required explicit `ItemTemplate`, `ItemsPanelTemplate ItemsPanel`, `ItemContainerStyle`, `AlternationCount`, and realization diagnostics. Its packaged typed default template contains an `ItemsPresenter`, using the minimal default-template registry and lookup established in Phase 1; full widget migration remains Phase 7. `ItemsSource` accepts `IEnumerable`; indexed `IList` sources remain indexed; non-list enumerables snapshot on assignment/reset; `INotifyCollectionChanged` drives add/remove/replace/move/reset deltas.
- [x] 23. Introduce an internal collection-view adapter with one logical slot per source occurrence, including duplicates. Add/remove preserve unaffected slots; move preserves slot/selection identity and preserves container association only while the slot remains realized and compatible; replace creates a new slot; reset rebuilds. Validate notification indices and reject cross-thread notifications once attached. Never enumerate the full source per frame.
- [x] 24. Add semantic `ContentControl : TemplatedControl` with `Content`, `ContentTemplate`, horizontal/vertical content alignment, and fallback scalar-to-text conversion, plus item-container lifecycle hooks (`GetContainerForItem`, `PrepareContainerForItem`, `ClearContainerForItem`). Migrate `Button`, `ListBoxItem`, and other content-bearing widgets to inherit or adopt the same arbitrary-content contract rather than remaining text-only. Base `ItemsControl` projects generated containers through `ItemsPresenter`; each base item container uses `ContentPresenter`, while derived controls may supply specialized containers. Item bindings update when a recycled instance is rebound, and removed items dispose all subscriptions immediately. Projection must use the same one-logical-owner/one-visual-host rules as ordinary template content.
- [x] 25. Add runtime tests for arrays, lists, observable collections, duplicate object occurrences, null items, empty/reset sources, each collection action, mutation during selection, item property changes, source replacement, owner detach/reattach, resource/theme inheritance, namescope isolation, presenter projection, visual/logical ancestry, and deterministic disposal. Migrate affected Catalog inventory/property-editor stories and any Signal Run repeated-content views in the same phase; both runtime hosts must remain buildable even when one application does not yet consume the new items API.

### Phase 4: Viewport and Virtualizing Panels
- [x] 26. Extract the current scrolling policy/input/anchoring logic into a non-visual, typed `ScrollViewportController` used by semantic owners implementing `IScrollViewportOwner`. Evolve `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/AdvancedControls.cs` `ScrollContainer` into one templated owner, and make `ListBox` another owner rather than nesting a semantic `ScrollContainer`; each requires a `ScrollPresenter` part whose `TemplatedParent` supplies `IScrollViewportOwner`. Expose viewport extent, offset/extent changes, bring-index/control-into-view, and scroll anchoring while retaining wheel, touch, focus-follow, RTL, scrollbar, and nested-scroll behavior. Keep policy/input/state on the semantic owner/controller and geometry/compositing on the presenter. The canonical `ListBox` template above is therefore valid without a hidden behavior owner.
- [x] 27. Add `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/VirtualizingPanels.cs` with a `VirtualizingPanel`/item-generator contract, `VirtualizingStackPanel` supporting vertical and horizontal variable-sized items, and `VirtualizingGridPanel` supporting uniform/fixed-or-estimated cells and two-dimensional realization. Each variable-size panel requires a positive `EstimatedItemExtent` or uses a documented theme default; initialize unrealized slots with that estimate, update a rolling type/template-scoped estimate from measured realizations, and correct the prefix index while preserving the anchor token and intra-item offset. Use a dynamic chunked Fenwick/order-statistic sequence so prefix lookup, point correction, and localized insert/remove/move avoid shifting or rebuilding the full source; reset may rebuild in linear time off the warm-frame path. Specify scrollbar-error tolerance and monotonic finite extent behavior while estimates converge.
- [x] 28. Materialize only the visible range plus configurable overscan. Pool containers per `UIContext`/owner by versioned control-template factory identity, versioned data-template factory identity, container type, and relevant style generation; call template `Deactivate`/`Rebind`/`Activate` across reuse. Define a recycling reset contract for focus/capture, hover/pressed state, animations/clocks, validation, local values, bindings, resources, and row code-behind; eventful rows without an explicit recyclable-state contract are non-poolable. Bound each pool, atomically drain obsolete versions on theme/hot-reload replacement, dispose evictions, and expose realized/recycled/pinned counts for tests and diagnostics.
- [x] 29. Preserve the first visible slot token and intra-item pixel offset when incremental collection changes occur above the viewport. For reset/source replacement, preserve an optional application item key when configured, otherwise preserve raw scroll offset clamped to the new extent; do not imply that rebuilt slots retain identity. Pin pointer-captured, dragged, or actively edited containers until interaction ends. When a focused item scrolls out, record a bookmark containing slot token, template-local focus path, and realization generation only when unrealization caused focus to move to the owning selector; restore it only if focus is still on that proxy and all tokens still match. User focus movement, removal, disabled descendants, template replacement, and pool-version change cancel restoration.
- [x] 30. Add deterministic large-source tests: 100,000 items must realize no more than visible plus overscan plus explicitly pinned containers plus a fixed transition reserve; scrolling must recycle rather than monotonically allocate; vertical/horizontal variable extents and uniform grids must produce correct bounds; add/remove/move must preserve token anchors and reset must follow the configured key/raw-offset policy; RTL horizontal and grid navigation must remain correct; no layout pass may enumerate all items. Test multiple pins and release, visible-to-offscreen moves, stale pool-version eviction, and focus-restoration cancellation.

### Phase 5: ListBox and Selection
- [x] 31. Add `ListBox : ItemsControl` and `ListBoxItem` with `ItemList`-compatible `Single`, `Multi`, and `Toggle` pointer/keyboard interaction modes but new slot-preserving collection semantics. Expose canonical two-way `SelectedIndex`, lossy convenience `SelectedItem`, read-only selected indices/items, `HasSelection`, current item/index, selection changed/activated events, reselect policy, search, and bring-into-view behavior. Selection identity is the logical source slot, not object equality. Setting `SelectedItem` selects the current matching occurrence when one exists, otherwise the first matching occurrence; setting `null` clears selection, so selecting a null occurrence requires `SelectedIndex`. Outbound `SelectedItem == null` is disambiguated by `HasSelection`. Multi-selection collection projections remain read-only.
- [x] 32. Port and generalize the proven keyboard/pointer semantics from `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/SelectionControls.cs`: Ctrl/Command toggle, Shift range, arrows, Home/End, PageUp/PageDown, Space, activation, incremental search, wrap policy, disabled/unselectable containers, pointer capture, and current-versus-selected behavior. Share behavior helpers with `ItemList` where that reduces divergence, but explicitly do not copy its index-based mutation quirks: insert/remove-before-current, move-across-current, replacement, reset, and source swap follow the new slot contract and atomic old/new selection snapshots. Document these compatibility exceptions and exact event/binding update order.
- [x] 33. Add `:selected` and `:current` through the extensible pseudo-state provider in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/Styles.cs`; remove hard-coded control-type checks for all states as controls migrate. Ensure selectors such as `ListBoxItem:selected >> Border.selection` invalidate only the affected template subtree. Add typed transitions for animatable style-value changes using the existing clock/value-layer system. Add two-way binding adapters for supported single-selection properties and reject ambiguous two-way binding to multi-selection collections.
- [x] 34. Test selection under virtualization, recycling, duplicate items, moves/replacements/resets, source swaps, focused-item unrealization, row enablement changes, RTL navigation, visual-part state styling, transitions, and all ItemList-compatible edge cases.

### Phase 6: DataGrid and Hierarchical Tables
- [x] 35. Add `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/DataGrid.cs` with semantic `DataGrid : ListBox`, generated `DataGridRow : ListBoxItem` and `DataGridCell : ContentControl`, `DataGridMode` (`Flat`, `Hierarchical`), and explicit typed `DataGridColumn` resources. Implement `DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridTemplateColumn`, and `DataGridExpanderColumn` with header/cell templates, pixel/auto/star width, min/max width, visibility, alignment, user resizing, stable display order, and compiled cell bindings. Do not auto-generate columns or discover model properties through reflection. Its default template owns header, scroll, and items presenters; rows and cells are independently templated and expose documented state providers.
- [x] 36. Add a typed `DataGridSource<T>` projection over the Phase 3 slot adapter. Flat mode maps root source occurrences directly. Hierarchical mode uses the expander column's compiled `Children`, optional `HasChildren`, and optional two-way `IsExpanded` accessors, assigns immutable occurrence-based `IndexPath` identities, rejects cycles deterministically, and observes `INotifyCollectionChanged` independently at every expanded level. Add `Expand`, `Collapse`, `Toggle`, `ExpandAll`, `CollapseAll`, and conditional recursive operations plus cancellable expanding/collapsing and completed events. Expansion may populate children before realization; collapsing removes descendant realizations without destroying their logical source identity or unrelated branch state.
- [x] 37. Implement typed sorting and filtering. Header interaction cycles none/ascending/descending according to column policy; each sortable column requires a compiled `SortBinding` or typed comparer, and programmatic stable multi-column `DataGridSortDescription` ordering is supported. Filtering accepts a typed predicate through `DataGridSource<T>` or an application-owned filtered `ItemsSource`; `RefreshFilter` is explicit and may enumerate on the UI thread. Hierarchical filtering defines independent-row and `IncludeAncestorsOfMatches` policies with documented full-tree cost. Sort/filter changes preserve surviving slot tokens, selection/current identity, expansion state, and scroll anchors; warm frames perform no comparisons, predicate scans, or source enumeration.
- [x] 38. Add row and cell selection using `DataGridSelectionUnit` (`Row`, `Cell`) with `Single` and `Multi` modes. Row selection reuses `ListBox` slot semantics and exposes selected `IndexPath` values; cell selection uses immutable `CellIndex` values, current cell, read-only selected-cell projections, rectangular range operations, and atomic old/new snapshots. Define keyboard/pointer behavior for arrows, Home/End, PageUp/PageDown, Shift ranges, Ctrl/Command toggles, row-header selection, RTL, disabled cells, sorting, filtering, collapse, source deltas, and focused-row unrealization. Add `:selected`, `:current`, `:expanded`, `:collapsed`, `:ascending`, and `:descending` state integration without hard-coded style-engine type checks.
- [x] 39. Virtualize the flattened visible hierarchy with the Phase 4 generator, viewport, anchor, and pool contracts. Use a dynamic visible-subtree order-statistic index so localized child deltas and expand/collapse update only affected ranges. Realize only visible rows plus overscan, pinned interactions, and a fixed transition reserve; recycle each row and its cells atomically. Columns are not virtualized in this release: every realized row creates one cell per visible column, so define and test a deterministic supported-column limit and allocation bound. Add 100,000-flat-row and deep/wide-tree tests for bounded realization, anchor preservation, sorting/filtering refresh, duplicate/null occurrences, expansion persistence, selection, recycling, accessibility peers, and no full-tree warm-frame traversal on MonoGame and FNA.
- [x] 40. Add compiler, XAML, Catalog, and package coverage for the two canonical `DataGrid` fixtures above. Validate typed column bindings against `x:DataType`, expander-only hierarchical bindings, template scope, sort/filter accessors, column collections, cell templates, selection properties, and stable diagnostics. Add Catalog stories for flat and hierarchical grids with sorting, filtering, row/cell selection, expansion, observable child deltas, and large-source virtualization. Keep Signal Run core-only and unchanged unless a real table workflow is introduced; both hosts must still pass Debug/Release build and smoke gates.

### Phase 7: ControlTemplate, Theme Lookup, and Semantic Widget Migration
- [x] 41. Complete the type-hierarchical control-template lookup established minimally in Phase 1 in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Primitives.cs` `Theme`: explicit `TemplatedControl.Template` wins, then the nearest theme template for the runtime type, then the packaged default. Theme changes invalidate/reapply templates at a frame-safe boundary and increment the version used by live instances and pools. Styles may set `Template` through the existing XAML value precedence model. Lookup on a foundational element is a compile-time or API error.
- [x] 42. Complete the packaged default-template set begun for Phase 3 integration using typed C# factories in `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/DefaultControlTemplates.cs`; this avoids a compiler/runtime package cycle while keeping application overrides fully XAML-authored. Default factories use the same `TemplateInstance`, parts, states, and lifetime APIs as compiled XAML templates and build visuals only from foundational primitives, layout panels, presenters, deliberate nested widgets, and documented specialized parts. They consume only core package APIs and explicit core theme fonts, never DynamicText types or package presence.
- [x] 43. Complete every Phase 0 manifest row. Foundational primitives, panels, and presenters retain their direct drawing/layout/projection paths and are proven template-free. Simple semantic widgets become state/property/interaction/accessibility owners whose visual tree is wholly supplied by the default template. Content widgets project logical content through presenters. Complex widgets (text editors, rich text, graph/tree/code surfaces, color picker, viewport, media/subviewport) keep narrowly scoped internal rendering/input parts behind replaceable public chrome templates. Accessibility roles, names, values, actions, selection/current state, and live events originate from the semantic owner/container; template parts contribute transformed bounds and optional labelled-by text only through typed part contracts. Virtualized collections expose logical item peers independently of realization and mark offscreen items without retaining visual containers. No semantic widget may retain an unconditional outer-chrome `Draw` path after migration, and changing its template must not remove its keyboard, focus, pointer, selection, or accessibility semantics.
- [x] 44. Define and validate named part contracts with metadata and runtime diagnostics. Missing optional parts degrade intentionally; missing required parts fail during `ApplyTemplate` with control/template/part names. Keep input semantics on the owning semantic widget unless a documented part contract delegates them. Part contracts name the minimum required interface/type rather than a concrete default visual wherever substitution is valid.
- [x] 45. Add per-foundation and per-widget API, behavior, minimum-size/layout, box-model, transform/clip/hit-test, input/focus, state, theme-resource, selector-boundary, accessibility-tree, screenshot, and MonoGame/FNA render-parity tests. Assert accessibility role/name/value/action/state stability before and after template replacement, and logical offscreen/realized item-peer consistency during virtualized scrolling. Add reference compositions proving that radically different button, list, data-grid, tree, editor chrome, dialog, and navigation designs require only XAML templates/styles/resources and no custom `Draw`. Migrate every affected Catalog story and all Signal Run HUD/settings/result views to the final template-first controls in this phase, preserving their recorded behavior and visual baselines or documenting intentional deltas. The template-first switch is blocked until every manifest and application-continuity row is complete, both applications pass on both runtime hosts, and the Catalog has no missing part/template diagnostics.

### Phase 8: Hot Reload, Tooling, and Diagnostics
- [x] 46. Extend `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.HotReload/FormaXamlHotReloadService.cs` around stable artifact IDs `(source, semantic node ID, kind, key)`, dependency edges, multicast live-instance registrations, and versioned template replacement. Prepare and validate a complete detached replacement before a frame-boundary commit; failed preparation leaves current artifacts and trees intact. A primitive/style/resource edit updates only affected visuals; a data-template edit re-realizes visible rows while preserving source slots, selection, and scroll anchor; a data-grid column/template edit rebuilds only affected headers and realized cells while preserving row paths, selection, expansion, sort/filter state, and anchors; a control-template edit reapplies affected controls while preserving owner state and draining obsolete pools; and a row `x:Class` XAML edit replaces all live row instances. The promise covers XAML edits against already-compiled code-behind; C# handler edits require separate .NET Hot Reload integration. Preserve the Catalog's current XAML/effect reload workflows and Signal Run's view replacement with active view-model, score, timer, and settings retention; extend `Forma.Xaml.Game.Tests` rather than replacing those behavioral checks. The hot-reload package and its SRE emitter are optional development dependencies and are absent from core-only, trimmed, and NativeAOT artifacts.
- [x] 47. Update `/Users/zignd/hack/games/engine-workshop/Forma/tools/Forma.Xaml.Tool/LanguageServer.cs` and CLI/schema output for foundational versus templated type classification, primitive content models, brushes, geometry, attached panel properties, adaptive conditions, template elements/properties, scoped `x:DataType`, `TargetType`, relative-source completion, visual combinators, template-boundary selectors, template-local names/references/rename, part names, pseudo-states, data-grid columns, expander/child bindings, sort/filter accessors, row/cell selection, and diagnostics. Replace regex-only symbol classification where template and visual scopes require semantic identity.
- [x] 48. Expand compiler, build-integration, runtime, hot-reload, CLI, LSP, invalid-golden, and package-consumer fixtures. Verify generated assemblies construct typed primitives/panels/presenters, data-grid columns, and cells and call typed template/binding/collection/style/sort/filter APIs; inspect assembly references, member references/call sites, generated factory bodies, manifest resources, and publish files to prove they contain no reflection fallback, dynamic-code dependency, source XAML, XamlX, Cecil, compiler, hot-reload, or DynamicText references in the core-only Release profile. Run equivalent behavior fixtures through SRE and Cecil emitters, then run separate core-only and opt-in DynamicText trim/NativeAOT consumers for MonoGame and FNA.

### Phase 9: Catalog, Documentation, Packaging, and Rollout
- [x] 49. Finish the incremental migration of the existing Catalog under `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Catalog` and Signal Run under `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Xaml.Game`; do not replace either with a new showcase shell. Preserve their continuity contract and add compiled Catalog stories for a primitive/layout/brush/effect showcase; the same semantic controls restyled into several structurally different visual systems without custom drawing; responsive flex/grid compositions; template-part selectors; keyed and inline data templates; an eventful `x:Class` row; live observable add/remove/move/replace/reset; single/multi/toggle selection; vertical/horizontal/grid virtualization over a large source; flat and hierarchical data grids with sorting, filtering, row/cell selection, expand/collapse, and virtualization; recycled-state correctness; control-template overrides; general relative-source bindings; theme template replacement; and hot reload diagnostics. Update Signal Run to demonstrate the final templates/presenters/selectors in its real HUD, settings, and result workflows without changing its game rules or requiring optional runtime packages.
- [x] 50. Update `/Users/zignd/hack/games/engine-workshop/Forma/docs/xaml-language.md`, README/release notes, package examples, migration guide, and runtime-support documentation. Replace the current “templates not in v1” language; document the foundational/templated boundary, primitive vocabulary, box/compositing model, presenter projection, visual selector and template-boundary rules, adaptive conditions, `DataGrid` flat/hierarchical modes, columns, sorting/filtering, row/cell selection, expansion, and virtualization, and breaking visual-tree/API changes; show how existing C# row factories and custom-drawn chrome migrate; define the no-implicit-template rule; and publish virtualization limits (variable stack extents; uniform grid cells; data-grid rows with non-virtualized bounded columns).
- [x] 51. Add deterministic performance/allocation gates: realized controls remain bounded by viewport/overscan/pinned interactions; data-grid rows and cells remain bounded by realized rows times the supported visible-column limit; pool and effect-cache sizes remain bounded; steady scrolling reuses instances; incremental collection, hierarchy, and pseudo-state changes touch only affected slots/visual subtrees plus visible layout metadata; warm frames perform no template compilation, reflection, selector-tree scan, sorting/filtering pass, or full-source/tree enumeration. Record benchmark numbers separately from deterministic CI invariants.
- [x] 52. Run and require `make build`, `CONFIGURATION=Release make build`, `make test-xaml`, `CONFIGURATION=Release make test-xaml`, `make smoke`, `make check`, `make packages`, `make nativeaot`, Catalog render parity, both runtime graphical tests, and all supported OS/backend CI cells. The build commands must compile the Catalog and Signal Run hosts for MonoGame and FNA in both configurations; the XAML/smoke commands must retain the Signal Run lifecycle, XAML presentation, and Debug hot-reload checks as well as the Catalog smoke gates, while Release verifies that hot reload is absent. For MonoGame and FNA, packed trimmed/NativeAOT consumers run once with only the core peer package and `SpriteFontAdapter`, and once with the matching optional DynamicText companion. Both exercise compiled primitives, brushes, text, attached layout properties, visual/template selectors, adaptive conditions, keyed templates, relative sources, observable collection deltas, virtualization, selection, flat/hierarchical data grids, sorting/filtering, expansion, accessibility peers, and control-template application with no dynamic-code requirement; the core-only publish must contain no DynamicText managed/native assets.
- [x] 53. Check the plan complete only when every foundation and semantic-widget manifest row, Catalog and Signal Run continuity row, focused test, package gate, backend/runtime matrix cell, documentation task, and phase exit criterion is checked; finish with `bash scripts/track-plan.sh --fail-if-incomplete plans/xaml-templates-items-and-virtualization-plan.md`.

**Relevant files**
- `/Users/zignd/hack/games/engine-workshop/Forma/plans/xaml-templates-items-and-virtualization-plan.md` — authoritative tracked implementation plan and migration dashboard.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Control.cs` — foundational `Control`, semantic `TemplatedControl`, logical/visual tree split, inherited/compositing properties, property notification, and focus/context traversal.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/VisualPrimitives.cs` — direct-rendered surfaces, text, image/icon, shape, brush, geometry, clip, and effect contracts.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/LayoutPanels.cs` — canvas, overlay, stack, wrap/flow, flex, and explicit-track grid composition.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Presenters.cs` — content, items, scrolling, and specialized projection elements.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/Templates.cs` — template contracts, namescope, factory, and instance lifetime.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/Binding.cs` — typed relative-source resolution and subscriptions.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/XamlAttachment.cs` — recyclable template activation/deactivation/disposal.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/XamlRuntime.cs` — namescope/resource/content contracts and loader integration.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/ItemsControls.cs` — new items, container generation, collection view, list selection.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/VirtualizingPanels.cs` — new stack/grid realization and extent indexing.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/DataGrid.cs` — new flat/hierarchical data grid, typed columns, row/cell selection, sorting/filtering, expansion, and row virtualization.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/AdvancedControls.cs` — viewport/scroll integration.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Primitives.cs` — theme template registry.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/DefaultControlTemplates.cs` — new packaged typed defaults.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma/Xaml/Styles.cs` — visual-tree selector AST/matcher, template-boundary combinator, adaptive conditions, transitions, template setters, and extensible pseudo-states.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/SemanticModel.cs` — scoped template semantic model.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/FormaXamlParser.cs` — template/relative-source validation and diagnostics.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Compiler/FormaXamlCompiler.cs` — structured lowering and SRE template factories.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.Build/FormaXamlBuildTasks.cs` — typed Cecil emission and registration.
- `/Users/zignd/hack/games/engine-workshop/Forma/src/Forma.Xaml.HotReload/FormaXamlHotReloadService.cs` — multicast row/template hot reload and state preservation.
- `/Users/zignd/hack/games/engine-workshop/Forma/tools/Forma.Xaml.Tool/LanguageServer.cs` — template-aware schema and language features.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Tests` — runtime, control migration, selection, virtualization, layout, disposal, rendering.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.Compiler.Tests` — parser/lowering/diagnostic/compiler tests.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.Build.Integration` — compiled typed-IL fixture.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.HotReload.Tests` — template and repeated-row replacement.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.Tool.Tests` — LSP/CLI schema, scope, completion, references, rename.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.Xaml.Game.Tests` — Signal Run gameplay, XAML presentation, binding/storyboard, and state-preserving hot-reload continuity.
- `/Users/zignd/hack/games/engine-workshop/Forma/tests/Forma.PackageConsumer` — package, trim, NativeAOT template consumer.
- `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Catalog` — interactive template/items/selection/virtualization stories.
- `/Users/zignd/hack/games/engine-workshop/Forma/samples/Forma.Xaml.Game` — shared Signal Run game, core-only compiled-XAML workflow, and sample migration fixture.
- `/Users/zignd/hack/games/engine-workshop/Forma/docs/xaml-language.md` — normative syntax and behavior contract.

**Verification**
1. Focused unit tests after each phase: primitive rendering/layout/compositing tests, presenter projection and logical/visual tree tests, runtime template lifetime tests, visual-selector invalidation tests, compiler semantic/emission tests, collection delta tests, virtual panel tests, selection tests, flat/hierarchical data-grid tests, per-manifest-row parity tests, and affected Catalog/Signal Run continuity tests for both `FormaRuntime=MonoGame` and `FormaRuntime=FNA`.
2. `make test-xaml` after every compiler/runtime/tooling phase; build-integration IL inspection must find typed primitive/panel/presenter construction, template factory, visual-selector, adaptive-condition, and relative-source calls and no `System.Reflection` calls.
3. Large-source deterministic tests assert bounded realization/pools and no whole-source per-frame enumeration for vertical, horizontal, grid, and flat/hierarchical data-grid layouts; sort/filter refresh is explicit cold work and no collapsed branch is realized.
4. Catalog smoke and screenshot/render-parity tests validate primitive-only compositions, structurally different templates, responsive flex/grid layouts, flat and hierarchical data grids, visual selectors, template boundaries, transforms/clips/effects, states, focus, row/cell selection, expansion, scrolling, RTL, hot reload, and all default widget visuals on MonoGame and FNA. Signal Run tests validate its complete game-state lifecycle, HUD/settings/result presentation, editing, resources, selectors, storyboards, and state-preserving XAML hot reload on both peers; Debug and Release builds verify the intended optional hot-reload boundary.
5. `make check`, `make packages`, and `make nativeaot` validate portable CI, isolated core-only and opt-in DynamicText packed consumers, trimming, and NativeAOT with compiled primitives/styles/templates and no compiler/runtime-reader artifacts; the core-only consumers additionally reject DynamicText references and native assets.
6. GitHub Actions must pass all six runtime/OS matrix jobs plus parity, package-consumers, and NativeAOT before rollout.

**Decisions**
- Include `ItemsControl`, keyed/inline explicit `DataTemplate`, `ListBox`, `DataGrid`, full `ControlTemplate`/theme support, and virtualization in the first release.
- Keep `Control` as the non-templated, directly rendered/styleable foundation and add `TemplatedControl` for semantic widgets. Primitive, layout, presenter, and specialized-part manifest rows are explicitly exempt from template lookup.
- Retrofit every semantic widget in a template-first breaking release; do not retain native outer-chrome fallback paths.
- Treat component template boundaries like scoped web components: ordinary visual selectors stop at the boundary, while the explicit `>>` combinator may target intentional part classes/names across exactly one control-template boundary.
- Provide HTML/CSS-like composition capabilities through typed XAML primitives, flex/grid panels, brushes/effects, visual selectors, adaptive conditions, resources, and transitions; do not implement a browser DOM, CSS parser, or web layout compatibility layer.
- Support virtualized vertical and horizontal variable-size stacks plus a uniform fixed/estimated-cell grid.
- Match existing `ItemList` Single/Multi/Toggle pointer and keyboard interaction behavior, but use the new slot-preserving mutation, event, and binding contracts rather than legacy index quirks.
- Name the combined flat-table and hierarchical tree-table control `DataGrid : ListBox`. Keep the existing retained `Tree` for compatibility while `DataGrid` becomes the template-first, data-bound path with explicit typed columns, row/cell selection, sorting/filtering, expansion, and row virtualization.
- Require explicit data-grid columns and compiled bindings/comparers. Do not auto-generate columns or reflect model properties. Virtualize rows and recycle their cells together; defer column virtualization and enforce a deterministic visible-column limit.
- Support general typed `RelativeSource` (`Self`, `TemplatedParent`, explicit typed ancestor), not only `TemplateBinding`.
- Require explicit `ItemTemplate`; defer implicit type-based template matching and template selectors.
- Event handlers belong to a separate row `x:Class`; do not route event attributes from a data template to the outer screen.
- Preserve Release/trim/NativeAOT guarantees: direct generated IL, no runtime XAML reader, no reflection fallback, and no dynamic-code dependency.
- Preserve the core-only package profile: templates, selectors, visual foundations, static-font text, items, selection, and virtualization require only `Forma.MonoGame` or `Forma.FNA`; DynamicText remains an optional AOT-compatible companion and development XAML hot reload remains optional and non-AOT.
- Default control templates are typed runtime factories to avoid a Forma/compiler package cycle; application overrides remain XAML-authored.
- Keep the existing Catalog and Signal Run applications operational throughout the migration. The Catalog remains the full optional-package showcase; Signal Run remains the core-only compiled-XAML game. Migrate affected application code in the same change as each breaking phase rather than restoring sample compatibility only at rollout.

**Deferred Scope**
- Implicit closest-type `DataTemplate` lookup and `DataTemplateSelector`.
- Heterogeneous/masonry virtualized grids and variable-size two-dimensional cells.
- Automatic data-grid column generation, reflected property paths, and data-grid column virtualization.
- Spreadsheet formulas, arbitrary merged cells, pivot/grouping panels, and unbounded multi-range cell selection.
- Cross-thread collection dispatch; callers must marshal notifications to the UI thread.
- General commands, multibinding, priority binding, and element-name binding remain separate language features.
- CSS text syntax, browser DOM APIs, cascade layers, sibling combinators, arbitrary property-value attribute selectors, floats, tables, and browser-specific layout compatibility.
- General shader graphs, backdrop filters, unbounded blur, and filter chains; the first compositing contract is intentionally bounded and cacheable.