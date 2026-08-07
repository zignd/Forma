---
title: Data binding
description: Bind compiled Forma XAML views to observable application state.
---

# Data binding

Forma bindings are compiled with the XAML view. Use `x:DataType` so member paths, conversion, and
writability errors are reported at build time, then choose the least powerful mode that expresses
the data flow.

| Mode | Use it for | Updates |
| --- | --- | --- |
| `OneTime` | Immutable setup values | Once when attached; later source/context changes are ignored |
| `OneWay` | Status and presentation | Source to target; this is the default |
| `TwoWay` | Editable form state | Source to target and target to writable source |

`UpdateSourceTrigger.Default` selects the target adapter's normal behavior. Use `PropertyChanged`
for immediate updates or `LostFocus` to defer a write until editing ends.

## Bind an editable view

The XAML QuickStart is compiled and executed against both runtime peers:

[!code-xml[](./_generated/examples/FirstView.xaml)]

Its typed model raises `PropertyChanged` for `Name` and `Greeting`:

[!code-csharp[](examples/xaml-first-ui.cs)]

The `LineEdit.Text` adapter supports two-way updates. Forma also supplies adapters for range values,
button/check state, option selection, and list selection. For a custom target, provide the target
change contract required by `TwoWay` rather than polling.

## DataContext and source selection

`DataContext` inherits through the logical/inheritance parent, including controls projected into a
template. Assigning `null` still creates a local value and stops fallback; call `ClearDataContext()`
to resume inheritance. Use explicit `Self`, `TemplatedParent`, or `FindAncestor` sources when the
value belongs to control structure rather than application data.

One-way and two-way expressions subscribe to context changes and source notifications while their
compiled XAML attachment is active. Detaching an ordinary compiled root disposes those subscriptions
and restores the target's underlying value.

## Common mistakes

- `TwoWay` requires a writable source member, reverse conversion, and a target change adapter.
- `OneTime` does not refresh after replacing `DataContext`; choose `OneWay` for live state.
- Setting `DataContext=null` is not the same as clearing the local value.
- Do not rely on detached compiled roots to reactivate bindings when re-added; construct a new
  ordinary view or use the explicit template recycling lifecycle.

The [XAML language contract](xaml-language.md) remains canonical for syntax,
fallback/null options, converters, relative sources, and diagnostics. The Catalog's
[compiled binding story](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/DataBindingStoryView.xaml)
shows more modes and source forms.
