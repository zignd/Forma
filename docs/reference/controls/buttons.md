---
title: Button controls
description: Choose Forma command, link, menu, texture, and color buttons.
---

# Button controls

Start with [Button](xref:Forma.Button) for ordinary commands. Use a specialized control only when
its semantics or rendering match the interaction.

| Type | Use |
| --- | --- |
| [BaseButton](xref:Forma.BaseButton) | Base behavior for a custom button control |
| [Button](xref:Forma.Button) | Text/icon command |
| [LinkButton](xref:Forma.LinkButton) | Link-like action with link accessibility semantics |
| [MenuButton](xref:Forma.MenuButton) | Command that owns a popup menu |
| [TextureButton](xref:Forma.TextureButton) | Texture-state command and optional click mask |
| [ColorPickerButton](xref:Forma.ColorPickerButton) | Opens color selection for a current color |
| [ColorPresetButton](xref:Forma.ColorPresetButton) | Selects one preset color |

Button behavior defaults to keyboard focus, release activation, left-pointer activation, and
non-toggle mode. Text is centered by default. Enter and Space activate a focused button. Choose
toggle behavior only for persistent state; for standard Boolean settings prefer
[CheckBox](xref:Forma.CheckBox) or [CheckButton](xref:Forma.CheckButton).

Buttons expose press actions, button/link roles, optional toggle actions, and checked state. Preserve
visible focus and a meaningful accessibility name when replacing the default template.

Catalog: [Button](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/Button.xaml),
[LinkButton](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/LinkButton.xaml),
[TextureButton](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/TextureButton.xaml),
[MenuButton](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/MenuButton.xaml).
