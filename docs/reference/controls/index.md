---
title: Control families
description: Choose a Forma control family and continue to generated API signatures.
---

# Control families

These pages organize every public type rooted at [Control](xref:Forma.Control) by application role.
They add selection guidance, defaults, limitations, accessibility expectations, and Catalog routes
around the generated API reference. The generated API remains canonical for current signatures.

| Family | Start here when you need |
| --- | --- |
| [Text input](text-input.md) | Single-line, multiline, or numeric editing |
| [Buttons](buttons.md) | Commands, links, menus, textures, or color actions |
| [Selection](selection.md) | Boolean, option, scalar, tab, color, or joystick state |
| [Containers](containers.md) | Layout, projection, scrolling, splitting, or view transforms |
| [Collections](collections.md) | Lists, trees, grids, item generation, or virtualization |
| [Dialogs and menus](dialogs.md) | Modal/transient surfaces, commands, files, or color workflows |
| [Data display](data-display.md) | Text, progress, shapes, separators, or diagnostics |
| [Graph and code](graph-code.md) | Source editing or node-graph composition |
| [Media](media.md) | Images, textures, icons, sub-viewports, or video |

The machine-readable [family manifest](../control-families.json) is validated against Docfx
inheritance metadata on every documentation build. A new public control must be assigned to exactly
one family, and every family page must build.

For retained ownership and composition first, read [Controls and containers](../../controls-and-containers.md).
