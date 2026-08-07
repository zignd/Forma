---
title: Container controls
description: Choose Forma layout, content, scrolling, split, and view containers.
---

# Container controls

Use the smallest container that states the allocation rule. The [layout guide](../../layout-and-sizing.md)
explains constraints and size flags; the [ownership guide](../../controls-and-containers.md) explains
logical versus visual parentage.

| Role | Types |
| --- | --- |
| Base ownership/template | [Control](xref:Forma.Control), [Container](xref:Forma.Container), [TemplatedControl](xref:Forma.TemplatedControl), [Panel](xref:Forma.Panel), [PanelContainer](xref:Forma.PanelContainer) |
| Axis and flow | [BoxContainer](xref:Forma.BoxContainer), [HBoxContainer](xref:Forma.HBoxContainer), [VBoxContainer](xref:Forma.VBoxContainer), [FlowContainer](xref:Forma.FlowContainer), [HFlowContainer](xref:Forma.HFlowContainer), [VFlowContainer](xref:Forma.VFlowContainer), [StackPanel](xref:Forma.StackPanel), [WrapPanel](xref:Forma.WrapPanel), [FlexPanel](xref:Forma.FlexPanel) |
| Grid and placement | [GridContainer](xref:Forma.GridContainer), [GridPanel](xref:Forma.GridPanel), [CanvasPanel](xref:Forma.CanvasPanel), [OverlayPanel](xref:Forma.OverlayPanel), [CenterContainer](xref:Forma.CenterContainer) |
| Insets and transforms | [MarginContainer](xref:Forma.MarginContainer), [Border](xref:Forma.Border), [AspectRatioContainer](xref:Forma.AspectRatioContainer), [Viewbox](xref:Forma.Viewbox) |
| Content and projection | [ContentControl](xref:Forma.ContentControl), [ContentPresenter](xref:Forma.ContentPresenter) |
| Scrolling | [ScrollContainer](xref:Forma.ScrollContainer), [ScrollPresenter](xref:Forma.ScrollPresenter), [ScrollBar](xref:Forma.ScrollBar), [HScrollBar](xref:Forma.HScrollBar), [VScrollBar](xref:Forma.VScrollBar) |
| Splitting | [SplitContainer](xref:Forma.SplitContainer), [HSplitContainer](xref:Forma.HSplitContainer), [VSplitContainer](xref:Forma.VSplitContainer), [SplitContainerDragger](xref:Forma.SplitContainerDragger), [SplitContainerMultiDragger](xref:Forma.SplitContainerMultiDragger) |
| Disclosure/pages | [FoldableContainer](xref:Forma.FoldableContainer), [TabContainer](xref:Forma.TabContainer) |

`GridContainer.Columns` clamps to at least one. `BoxContainer` defaults to vertical orientation and
theme-derived separation. `ContentControl` accepts one content value. Presenter and dragger types are
primarily template parts, not first-choice application roots. Avoid nested same-axis scroll owners.

Containers expose their logical child peers. Scroll, tab, and foldable controls add scroll-view,
tab-panel, or group semantics. Base accessible naming falls back from `AccessibilityLabel` to
`Name`; assign a user-facing label when an internal name is not meaningful.

Catalog: [GridPanel](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/GridPanel.xaml),
[ScrollContainer](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ScrollContainer.xaml),
[SplitContainer](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/SplitContainer.xaml),
[WrapPanel](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/WrapPanel.xaml).
