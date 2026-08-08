---
title: Media controls
description: Choose Forma image, texture, icon, sub-viewport, and video controls.
---

# Media controls

| Type | Use |
| --- | --- |
| [Image](xref:Forma.Image) | Bitmap, vector, or scalable image source |
| [TextureRect](xref:Forma.TextureRect), [NinePatchRect](xref:Forma.NinePatchRect), [NineSliceImage](xref:Forma.NineSliceImage) | Low-level texture layout and stretchable chrome |
| [ThemeIconRect](xref:Forma.ThemeIconRect), [ThemeIconView](xref:Forma.ThemeIconView) | Theme-owned icon lookup with stable logical sizing |
| [SubViewportContainer](xref:Forma.SubViewportContainer) | Embedded rendered control tree/view surface |
| [VideoStreamPlayer](xref:Forma.VideoStreamPlayer) | Backend-dependent video playback |

`TextureRect` defaults to scale/keep-size behavior and pointer pass-through. `NinePatchRect` is fully
click-through. `SubViewportContainer.StretchShrink` must be positive. Video availability, seeking,
looping, and loading depend on the selected runtime/backend; query
[VideoPlaybackCapabilities](xref:Forma.VideoPlaybackCapabilities) and unavailable state.

`SubViewportContainer` exposes a viewport role. Images, textures, icons, and video otherwise need an
explicit accessibility label when they convey meaning; decorative media should not introduce a
misleading interactive peer. Theme icons are non-owning values, and application textures remain
application-owned. See [Resource lifetime](../../resource-lifetime.md) and
[Runtime support](../../runtime-support.md).

Catalog: [Image](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/Image.xaml),
[TextureRect](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/TextureRect.xaml),
[SubViewportContainer](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/SubViewportContainer.xaml),
[VideoStreamPlayer](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/VideoStreamPlayer.xaml).

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-theme-icon-view`; the
Catalog's **Open reference** link returns to this page.
