# Template, Items, and Visual-Tree Migration

This guide covers the breaking visual and collection changes in the template-first release. The
normative language rules remain in [xaml-language.md](xaml-language.md); the complete type-by-type
classification is in [control-template-migration-manifest.md](control-template-migration-manifest.md).

## Choose the Owning Type

`Control` remains the base styleable visual/layout node. Use a foundational element when the type's
job is to render, arrange, or project content directly:

- primitives: `Border`, text, image, shape, drawing, and effect elements;
- panels: `CanvasPanel`, `OverlayPanel`, `StackPanel`, `WrapPanel`, `FlexPanel`, `GridPanel`, and
  `Viewbox`;
- presenters: `ContentPresenter`, `ItemsPresenter`, and `ScrollPresenter`.

These types do not have templates. A semantic widget derives from `TemplatedControl`, retains its
state, input, focus, selection, and accessibility behavior, and gets all replaceable chrome from a
`ControlTemplate`. Do not move widget behavior into a `Border`, presenter, or template code-behind.

## Replace Custom-Drawn Chrome

Move outer chrome into a typed template and leave interaction on the semantic owner:

```xml
<ControlTemplate x:Key="ToolbarButtonTemplate" TargetType="Button">
  <Border Classes="chrome" Padding="10,6" CornerRadius="4">
    <ContentPresenter x:Name="PART_ContentPresenter"
                      Content="{Binding Content, RelativeSource=TemplatedParent}"
                      HorizontalContentAlignment="Center"
                      VerticalContentAlignment="Center" />
  </Border>
</ControlTemplate>

<Button Template="{StaticResource ToolbarButtonTemplate}" Content="Save" />
```

Use the documented named parts and minimum required part types. `GetTemplateChild` replaces casts or
child-index assumptions about a widget's former internal visual tree. Different templates may have
unrelated structure, so application code must not traverse from a semantic owner by fixed child
positions.

Selectors use the visual tree. Ordinary descendant and child combinators stop at a template or data
-template boundary. Cross exactly one control-template boundary with `>>`:

```xml
<Style Selector="Button.primary >> Border.chrome" />
<Style Selector="ListBoxItem:selected >> Border.selection" />
```

Logical content remains owned by its semantic control even while a presenter hosts it visually.
Use `Parent` for logical ownership, `VisualParent` for rendered ancestry, and a typed
`RelativeSource FindAncestor` binding when the relationship is visual.

## Replace Row Factories

A data item now requires an explicit `DataTemplate`; there is no implicit item-type lookup or
runtime `DataTemplateSelector`:

```xml
<DataTemplate x:Key="ServerRowTemplate" x:DataType="local:ServerRowModel">
  <local:ServerRow />
</DataTemplate>

<ItemsControl ItemsSource="{Binding Servers}"
              ItemTemplate="{StaticResource ServerRowTemplate}" />
```

Convert a C# row factory that only assembled visuals into a keyed or inline `DataTemplate`. Keep an
ordinary separate `x:Class` row only when row-local event handling or recyclable local state is
required. Event attributes are not legal directly inside `DataTemplate`; handlers on a referenced
row control resolve against that row's code-behind, not the outer view.

Observable sources use one identity slot per occurrence. Duplicate object references are distinct.
Add/remove/move notifications preserve unaffected slot identities, replacement creates a new slot,
and reset rebuilds. Notifications must arrive on the attached UI thread.

Rows are poolable when their template state can be deactivated, rebound, and activated. A custom
row with code-behind opts in through `IDataTemplateRecyclingState`; a custom item container uses
`IRecyclableItemContainer`. Otherwise it remains correct but is not pooled.

## Adopt Virtualization Deliberately

Select an explicit items panel:

- `VirtualizingStackPanel` supports vertical and horizontal variable item extents;
- `VirtualizingGridPanel` supports uniform fixed or estimated cells;
- non-virtualizing panels realize every source slot.

Virtualized realization is bounded by the visible range, overscan, and pinned focus/capture/edit
interactions. Set a positive estimated item extent when item sizes are not yet known. Preserve an
application identity across reset/source replacement with `ItemKeySelector`; otherwise Forma clamps
the prior raw offset.

`DataGrid` virtualizes rows and recycles all cells in a row together. Columns are explicit and are
not virtualized; at most `DataGrid.MaximumSupportedVisibleColumns` (256) may be visible. Keep wide,
unbounded spreadsheet scenarios outside this release's contract.

## Migrate Tables and Trees

Use `DataGridMode.Flat` for tables and `DataGridMode.Hierarchical` for expandable tree tables.
Declare `DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridTemplateColumn`, or
`DataGridExpanderColumn` explicitly with typed bindings. Forma does not reflect item properties or
auto-generate columns.

Sorting requires a typed `SortBinding` or comparer. Filtering belongs to `DataGridSource<T>` (or an
application-owned filtered source) and runs only when `RefreshFilter` is called. Hierarchical
sources declare typed children and optional has-children/expanded accessors; ancestor-preserving
filtering may inspect the full tree during refresh. Warm frames do not sort or filter.

Row selection preserves source-occurrence paths. Cell selection uses immutable `CellIndex` values.
Choose `DataGridSelectionUnit.Row` or `Cell` and the inherited single/multi selection mode
explicitly. Expansion, sorting, filtering, selection, and scroll anchors remain semantic owner
state when a control template changes.

The retained `Tree` remains available for compatibility. Prefer `DataGrid` for new typed,
data-bound, template-first flat or hierarchical tables.

## Package and Release Boundary

Reference exactly one runtime peer and its private build package. Add the matching
`Forma.Xaml.HotReload` package only to a Debug development host. Core templates, selectors, items,
selection, `DataGrid`, static-font text, and virtualization do not require DynamicText or hot
reload.

Release, trimmed, and NativeAOT output uses injected typed IL only. Do not ship source XAML, the
compiler, XamlX, Cecil, SRE, or the hot-reload package. Add `Forma.DynamicText` only when runtime font
loading, shaping, or rasterization is required; `SpriteFontAdapter` remains the core-only path.

## Verification Checklist

1. Replace fixed child traversal with namescope or named-part lookup.
2. Verify required parts after every explicit/theme template replacement.
3. Test pointer, keyboard, focus, selection, and accessibility behavior independently of template
   structure.
4. Test observable add/remove/move/replace/reset with duplicate occurrences.
5. Assert realized, recycled, and pinned counts under large-source scrolling.
6. Build Debug and Release for MonoGame and FNA; confirm Release excludes hot reload and source XAML.
7. Publish the matching core-only trim/NativeAOT consumer before adding optional companions.
