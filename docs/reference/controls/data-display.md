---
title: Data display controls
description: Choose Forma text, progress, shape, separator, and diagnostic controls.
---

# Data display controls

| Role | Types |
| --- | --- |
| Plain text | [Label](xref:Forma.Label), [TextBlock](xref:Forma.TextBlock) |
| Rich text | [RichTextDocument](xref:Forma.RichTextDocument), [RichTextLabel](xref:Forma.RichTextLabel) |
| Progress | [ProgressBar](xref:Forma.ProgressBar), [TextureProgressBar](xref:Forma.TextureProgressBar) |
| Shapes/drawing | [Shape](xref:Forma.Shape), [RectangleShape](xref:Forma.RectangleShape), [EllipseShape](xref:Forma.EllipseShape), [LineShape](xref:Forma.LineShape), [PathShape](xref:Forma.PathShape), [PolygonShape](xref:Forma.PolygonShape), [PolylineShape](xref:Forma.PolylineShape), [DrawingElement](xref:Forma.DrawingElement), [ColorRect](xref:Forma.ColorRect) |
| Separators | [Separator](xref:Forma.Separator), [HSeparator](xref:Forma.HSeparator), [VSeparator](xref:Forma.VSeparator) |
| Diagnostics/layout | [DynamicGlyphAtlasView](xref:Forma.DynamicGlyphAtlasView), [ReferenceRect](xref:Forma.ReferenceRect) |

`Label` defaults to no wrapping, a visible ratio of `1`, and padding `3`. `ProgressBar` uses a `0.01`
step and removes mutating range actions because it presents status rather than accepting input.
`TextureProgressBar` is pointer-pass-through by default.

Progress controls expose the progress-bar role and value. Rich text exposes document semantics.
Purely visual shapes and separators retain generic accessibility behavior; mark decoration out of
the semantic flow in the host bridge, or provide `AccessibilityLabel` when the visual conveys data.

Catalog: [Label](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/Label.xaml),
[RichTextLabel](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/RichTextLabel.xaml),
[ProgressBar](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ProgressBar.xaml),
[RectangleShape](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/RectangleShape.xaml).

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-rich-text-label`; the
Catalog's **Open reference** link returns to this page.
