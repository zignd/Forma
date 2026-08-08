---
title: Graph and code controls
description: Use Forma source-editing and node-graph controls.
---

# Graph and code controls

Use [CodeEdit](xref:Forma.CodeEdit) for source editing, completion, and highlighting.
[GraphEdit](xref:Forma.GraphEdit) owns the graph canvas; [GraphNode](xref:Forma.GraphNode) supplies
ports/content, and [GraphFrame](xref:Forma.GraphFrame) groups elements.
[GraphEditFilter](xref:Forma.GraphEditFilter) and [GraphEditMinimap](xref:Forma.GraphEditMinimap) are
graph-owned overlays. [GraphElement](xref:Forma.GraphElement) is the shared selectable/draggable base.

Graph elements default to focusable, selectable, and draggable. Nodes start at `140x80`.
`GraphEdit` starts at zoom `1` with snapping and grid enabled and a `240x160` minimum. These controls
are interaction-dense; test keyboard, focus, zoom, pan, and selection together rather than treating
the canvas as a static visual.

`CodeEdit` inherits text-box and read-only accessibility behavior. Graph elements expose selectable
group semantics, while `GraphEdit` exposes a canvas. Supply useful names for nodes and ports; visual
coordinates alone are not a usable accessible identity.

Catalog: [CodeEdit](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/CodeEdit.xaml),
[GraphEdit](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/GraphEdit.xaml),
[GraphNode](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/GraphNode.xaml),
[GraphFrame](https://github.com/zigrok/Forma/blob/main/samples/Forma.Catalog/Stories/Controls/GraphFrame.xaml).

Every public type in the table has a Catalog story with the exact unqualified type name. Its stable
identifier is `catalog-` plus the kebab-case type name, such as `catalog-graph-edit`; the Catalog's
**Open reference** link returns to this page.
