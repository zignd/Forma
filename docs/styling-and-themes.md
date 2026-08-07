---
title: Styling and themes
description: Apply Forma theme defaults, overrides, selectors, icons, and templates.
---

# Styling and themes

Themes provide inherited defaults; styles select controls declaratively; local control values make
the final targeted override. Keep global design tokens in a `Theme`, reusable rules in XAML
`Style` resources, and one-off state in the control that owns it.

## Theme defaults and inheritance

`Theme` defaults include `Separation=4`, `BorderWidth=1`, and default font hinting. `FontSize=0`
means no theme size has been selected. Assign `Theme.Parent` for fallback and `Control.ThemeOverride`
for an inherited subtree override. Cycles in the theme-parent chain are rejected.

Theme mutations increment `Version` and raise `Changed`; attached controls invalidate themselves.
Use `SetStyleBox`, `SetIcon`, and `SetControlTemplate` for named theme resources. `SuppressIcon`
intentionally blocks a parent/default icon, while a missing override continues fallback. The
[default theme icon guide](theme-icons.md) owns icon names, atlas behavior, and customization.

## Selector styles

This Catalog story is compiled as part of both peer hosts and staged unchanged for this page:

[!code-xml[](./_generated/examples/StylesStoryView.xaml)]

Selectors can target a type, `.class`, `#name`, and pseudo-state such as `:hover`. More specific
matching rules win; a local property value outranks selector contributions and reveals the prior
winner when cleared. Ordinary descendant/child selectors do not cross a control-template boundary.
Use the template-child combinator `>>` once for each boundary that must be crossed.

Selector attachments are lifecycle-managed: detaching a compiled XAML root removes subscriptions
and restores the underlying value. See the [XAML language contract](xaml-language.md)
for grammar, specificity, adaptive conditions, and transitions.

## Templates and per-control overrides

Use a `ControlTemplate` when structure changes, not merely color or spacing. A templated control
resolves its explicit template, then its theme, then packaged defaults. Keep required named parts
and states compatible with the control contract; the
[template guide](xaml-templates-migration.md) is canonical for ownership and recycling.

`StyleBoxFlat` starts with a white fill, transparent border, and zero border width/radius.
`StyleBoxTexture.Modulate` defaults to white. Set these values explicitly when a style must be
portable across theme changes.

## Common mistakes

- Do not duplicate a complete theme to change one subtree; use `Theme.Parent` and an override.
- Do not expect a normal descendant selector to style a generated template child; use `>>`.
- Do not fight a local value with selector specificity; clear or move the local value.
- Do not dispose the texture referenced by a shared `ThemeIcon` through the icon value itself.

Use the Catalog's
[selector styles](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/StylesStoryView.xaml)
and [icon customization](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/IconCustomizationStoryView.xaml)
stories to inspect these rules interactively.
