---
title: Collection controls
description: Choose Forma list, tree, grid, item-generation, and virtualization controls.
---

# Collection controls

| Role | Types |
| --- | --- |
| Direct entries | [ItemList](xref:Forma.ItemList) |
| Templated items and selection | [ItemsControl](xref:Forma.ItemsControl), [ListBox](xref:Forma.ListBox), [ListBoxItem](xref:Forma.ListBoxItem) |
| Hierarchy | [Tree](xref:Forma.Tree), [TreePresenter](xref:Forma.TreePresenter) |
| Tabular data | [DataGrid](xref:Forma.DataGrid), [DataGridRow](xref:Forma.DataGridRow), [DataGridCell](xref:Forma.DataGridCell), [DataGridColumnHeader](xref:Forma.DataGridColumnHeader) |
| Presentation/virtualization | [ItemsPresenter](xref:Forma.ItemsPresenter), [VirtualizingPanel](xref:Forma.VirtualizingPanel), [VirtualizingStackPanel](xref:Forma.VirtualizingStackPanel), [VirtualizingGridPanel](xref:Forma.VirtualizingGridPanel) |

Use `ItemList` for direct retained entries, `ListBox` for data-templated selectable items,
`DataGrid` for explicit columns/sorting/cell selection, and `Tree` for hierarchical columns.
Presenter, panel, row, and cell types normally belong to templates and realization infrastructure.

`ListBox` defaults to single selection, text search, and wrapped navigation. `ItemList` defaults to
search, wraparound, one column, and one text line. Virtualization bounds realized visuals, but the
accessibility tree retains peers for logical items and marks unrealized items offscreen.

Catalog: [ItemList](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ItemList.xaml),
[ListBox](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/ListBox.xaml),
[Tree](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/Tree.xaml),
[DataGrid](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/DataGrid.xaml).
The feature stories cover [flat and hierarchical grids](https://github.com/zigrok/Forma/tree/main/samples/Forma.Catalog).
Their exact Catalog names are **Collection Systems**, **Flat Data Grid**, and
**Hierarchical Data Grid**.

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-data-grid`; the Catalog's
**Open reference** link returns to this page.
