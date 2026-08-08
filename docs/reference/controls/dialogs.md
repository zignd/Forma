---
title: Dialog and menu controls
description: Choose Forma popups, menus, confirmation, color, and file dialogs.
---

# Dialog and menu controls

| Type | Use |
| --- | --- |
| [Popup](xref:Forma.Popup), [PopupPanel](xref:Forma.PopupPanel) | Base transient/modal surfaces |
| [PopupMenu](xref:Forma.PopupMenu), [PopupMenuItems](xref:Forma.PopupMenuItems) | Command menu and owner-created item surface |
| [MenuBar](xref:Forma.MenuBar) | Top-level menu navigation |
| [AcceptDialog](xref:Forma.AcceptDialog), [ConfirmationDialog](xref:Forma.ConfirmationDialog) | Acknowledgement or confirm/cancel workflows |
| [ColorPickerDialog](xref:Forma.ColorPickerDialog), [ColorPickerPopupPanel](xref:Forma.ColorPickerPopupPanel) | Color workflows |
| [FileDialog](xref:Forma.FileDialog) | Host/filesystem-backed file selection |

`Popup` defaults to modal, focusable, and dismissible by an outside click, and restores prior focus
when hidden. `AcceptDialog` defaults its confirmation text to `OK`, hides after confirmation, and
closes on Escape. `PopupMenuItems` is owner-created interaction infrastructure, not the menu's data
collection.

Popups report window/dialog/menu roles and modal state. Keep focus contained while modal, expose a
clear accessible title, and ensure every menu command has a name and disabled state. File access is
a host capability; unavailable hosts should report that state rather than fabricate paths.

Catalog: [AcceptDialog](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/AcceptDialog.xaml),
[FileDialog](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/FileDialog.xaml),
[PopupMenu](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/PopupMenu.xaml),
[ColorPickerDialog](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ColorPickerDialog.xaml).

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-confirmation-dialog`; the
Catalog's **Open reference** link returns to this page.
