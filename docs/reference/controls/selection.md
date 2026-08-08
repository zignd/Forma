---
title: Selection controls
description: Choose Forma Boolean, option, range, tab, color, and joystick controls.
---

# Selection controls

| Type | Use |
| --- | --- |
| [CheckBox](xref:Forma.CheckBox), [CheckButton](xref:Forma.CheckButton) | Boolean state with check or switch presentation |
| [OptionButton](xref:Forma.OptionButton) | One value from a compact option set |
| [Range](xref:Forma.Range) | Abstract scalar-value behavior shared by range controls |
| [Slider](xref:Forma.Slider), [HSlider](xref:Forma.HSlider), [VSlider](xref:Forma.VSlider) | Continuous or stepped scalar selection |
| [TabBar](xref:Forma.TabBar) | Select a page header; pair with `TabContainer` for page ownership |
| [ColorPicker](xref:Forma.ColorPicker) | Detailed color selection |
| [VirtualJoystick](xref:Forma.VirtualJoystick) | Normalized two-axis pointer/touch input |

`Range` defaults to minimum `0`, maximum `100`, step `1`, and clamping; enable `AllowGreater` or
`AllowLesser` only when values outside the displayed interval are intentional. Sliders expose
increment, decrement, and set-value actions. Checks, options, tabs, colors, and joysticks specialize
their accessibility roles and state; do not replace them with unlabeled generic buttons.

Catalog: [OptionButton](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/OptionButton.xaml),
[TabBar](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/TabBar.xaml),
[ColorPicker](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ColorPicker.xaml),
[VirtualJoystick](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/VirtualJoystick.xaml).

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-option-button`; the
Catalog's **Open reference** link returns to this page.
