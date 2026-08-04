# Dynamic Text Call-Site Inventory

This inventory describes the current `src/Forma` source tree before dynamic text migration. Line
anchors identify the audited snapshot; the owning symbol is authoritative if later edits move a
line. Generated code, `bin`, and `obj` are excluded.

## Summary

| File | Font properties | `MeasureString` | `LineSpacing` | `context.Text` |
| --- | ---: | ---: | ---: | ---: |
| `AdvancedControls.cs` | 3 | 15 | 23 | 6 |
| `Controls.cs` | 3 | 8 | 3 | 5 |
| `GraphAndCodeControls.cs` | 1 | 14 | 15 | 6 |
| `MenusAndDialogs.cs` | 2 | 3 | 7 | 8 |
| `SelectionControls.cs` | 2 | 10 | 8 | 4 |
| `Tree.cs` | 1 | 2 | 5 | 2 |
| `UIContext.cs` | 1 | 1 | 1 | 1 |
| `UIRenderContext.cs` | 0 | 0 | 3 | 0 |
| **Total** | **13** | **53** | **65** | **32** |

Every occurrence migrates to one of these retained-layout behaviors:

- **Contract**: compatibility property or adapter boundary.
- **Measurement**: desired/minimum size from `TextLayout.Bounds`.
- **Layout**: wrapping, trimming, line metrics, or alignment from retained lines.
- **Interaction**: hit testing, carets, selection, or metadata regions from cluster maps.
- **Rendering**: drawing and decoration from the same retained layout.
- **Density**: legacy XNB font switching, replaced by density-aware raster selection.

## Contract and Ownership

| Owner | Current `SpriteFont` surface | Classification and migration |
| --- | --- | --- |
| `LineEdit`, `SpinBox`, `TabContainer` | [Font properties](../src/Forma/AdvancedControls.cs#L410), [SpinBox backing field](../src/Forma/AdvancedControls.cs#L2230), [SpinBox property](../src/Forma/AdvancedControls.cs#L2262), [TabContainer property](../src/Forma/AdvancedControls.cs#L2713) | Contract; add parallel `UIFont`, adapt legacy assignments. |
| `Label`, `BaseButton`, `ProgressBar` | [Label.Font](../src/Forma/Controls.cs#L65), [BaseButton.Font](../src/Forma/Controls.cs#L611), [ProgressBar.Font](../src/Forma/Controls.cs#L1223) | Contract; add parallel `UIFont`, adapt legacy assignments. |
| `GraphNode` and `GraphFrame` | [GraphNode.Font](../src/Forma/GraphAndCodeControls.cs#L216) | Contract; `GraphFrame` inherits the adapter path. |
| `PopupMenu`, `AcceptDialog`, derived dialogs | [PopupMenu.Font](../src/Forma/MenusAndDialogs.cs#L155), [AcceptDialog.Font](../src/Forma/MenusAndDialogs.cs#L1215) | Contract; derived `ConfirmationDialog` and `FileDialog` inherit it. |
| `TabBar`, `ItemList`, inherited controls | [TabBar.Font](../src/Forma/SelectionControls.cs#L1105), [ItemList.Font](../src/Forma/SelectionControls.cs#L1656) | Contract; `LinkButton` and `RichTextLabel` inherit existing font properties. |
| `Tree` and `TreeItem` cells | [cell storage](../src/Forma/Tree.cs#L63), [cell setter/getter](../src/Forma/Tree.cs#L300), [Tree.Font](../src/Forma/Tree.cs#L704), [scaling helper](../src/Forma/Tree.cs#L1982) | Contract and density; add cell-level `UIFont` equivalents. |
| Tooltip service | [DisplayFontResolver](../src/Forma/UIContext.cs#L67), [TooltipFont](../src/Forma/UIContext.cs#L89) | Contract and density; retain resolver through adapter migration. |
| Render service | [internal resolver](../src/Forma/UIRenderContext.cs#L35), [Text overloads](../src/Forma/UIRenderContext.cs#L128), [scaled overload](../src/Forma/UIRenderContext.cs#L133), [DrawText helper](../src/Forma/UIRenderContext.cs#L137) | Contract and rendering; compatibility overloads construct/use retained layouts. |

## Measurement Calls

All 53 `MeasureString` calls are classified below. A line with two calls is noted explicitly.

- `AdvancedControls.cs`: interaction at [715](../src/Forma/AdvancedControls.cs#L715), [841](../src/Forma/AdvancedControls.cs#L841), [846-847](../src/Forma/AdvancedControls.cs#L846), [1533](../src/Forma/AdvancedControls.cs#L1533), [1596](../src/Forma/AdvancedControls.cs#L1596), [1602](../src/Forma/AdvancedControls.cs#L1602), and [1734](../src/Forma/AdvancedControls.cs#L1734); measurement at [1373-1376](../src/Forma/AdvancedControls.cs#L1373) and [2604](../src/Forma/AdvancedControls.cs#L2604); layout at [2018](../src/Forma/AdvancedControls.cs#L2018); rendering at [1619-1620](../src/Forma/AdvancedControls.cs#L1619) and [1718](../src/Forma/AdvancedControls.cs#L1718).
- `Controls.cs`: measurement at [419](../src/Forma/Controls.cs#L419), [437](../src/Forma/Controls.cs#L437), and [688](../src/Forma/Controls.cs#L688); layout at [447](../src/Forma/Controls.cs#L447), [892](../src/Forma/Controls.cs#L892), and [1299](../src/Forma/Controls.cs#L1299); segmented rendering at [517](../src/Forma/Controls.cs#L517) and [529](../src/Forma/Controls.cs#L529).
- `GraphAndCodeControls.cs`: measurement at [495](../src/Forma/GraphAndCodeControls.cs#L495), [613](../src/Forma/GraphAndCodeControls.cs#L613), and [672](../src/Forma/GraphAndCodeControls.cs#L672); layout at [1855](../src/Forma/GraphAndCodeControls.cs#L1855), [1875](../src/Forma/GraphAndCodeControls.cs#L1875), [2493](../src/Forma/GraphAndCodeControls.cs#L2493), and [2650](../src/Forma/GraphAndCodeControls.cs#L2650); interaction at [1857](../src/Forma/GraphAndCodeControls.cs#L1857), [1888](../src/Forma/GraphAndCodeControls.cs#L1888), and twice at [2651](../src/Forma/GraphAndCodeControls.cs#L2651); rendering at [561](../src/Forma/GraphAndCodeControls.cs#L561), [2396](../src/Forma/GraphAndCodeControls.cs#L2396), and [2422](../src/Forma/GraphAndCodeControls.cs#L2422).
- `MenusAndDialogs.cs`: interaction at [1009](../src/Forma/MenusAndDialogs.cs#L1009), rendering at [1065](../src/Forma/MenusAndDialogs.cs#L1065), and layout at [1341](../src/Forma/MenusAndDialogs.cs#L1341).
- `SelectionControls.cs`: rendering at [35](../src/Forma/SelectionControls.cs#L35) and [2528](../src/Forma/SelectionControls.cs#L2528); layout at [1541](../src/Forma/SelectionControls.cs#L1541), [2797](../src/Forma/SelectionControls.cs#L2797), [2909](../src/Forma/SelectionControls.cs#L2909), and [2960](../src/Forma/SelectionControls.cs#L2960); measurement at [2374-2378](../src/Forma/SelectionControls.cs#L2374); interaction at [2843](../src/Forma/SelectionControls.cs#L2843) and [3214](../src/Forma/SelectionControls.cs#L3214).
- `Tree.cs`: rendering alignment at [1608](../src/Forma/Tree.cs#L1608) and [1713](../src/Forma/Tree.cs#L1713).
- `UIContext.cs`: tooltip layout at [478](../src/Forma/UIContext.cs#L478).

## Line Metrics

All 65 `LineSpacing` reads are classified below. A line with two reads is noted explicitly.

- `AdvancedControls.cs`: layout at [583](../src/Forma/AdvancedControls.cs#L583), [1484](../src/Forma/AdvancedControls.cs#L1484), [1574](../src/Forma/AdvancedControls.cs#L1574), [1582](../src/Forma/AdvancedControls.cs#L1582), [1590](../src/Forma/AdvancedControls.cs#L1590), [1661](../src/Forma/AdvancedControls.cs#L1661), [1671](../src/Forma/AdvancedControls.cs#L1671), and [1678](../src/Forma/AdvancedControls.cs#L1678); interaction at [842](../src/Forma/AdvancedControls.cs#L842), [848](../src/Forma/AdvancedControls.cs#L848), [1104](../src/Forma/AdvancedControls.cs#L1104), [1525](../src/Forma/AdvancedControls.cs#L1525), [1529](../src/Forma/AdvancedControls.cs#L1529), [1597-1598](../src/Forma/AdvancedControls.cs#L1597), [1603-1604](../src/Forma/AdvancedControls.cs#L1603), and [1731](../src/Forma/AdvancedControls.cs#L1731); rendering at [1584](../src/Forma/AdvancedControls.cs#L1584), [1621](../src/Forma/AdvancedControls.cs#L1621), twice at [1673](../src/Forma/AdvancedControls.cs#L1673), and [2879](../src/Forma/AdvancedControls.cs#L2879).
- `Controls.cs`: layout at [148](../src/Forma/Controls.cs#L148), [191](../src/Forma/Controls.cs#L191), and [381](../src/Forma/Controls.cs#L381).
- `GraphAndCodeControls.cs`: layout at [1854](../src/Forma/GraphAndCodeControls.cs#L1854), [2389](../src/Forma/GraphAndCodeControls.cs#L2389), [2426](../src/Forma/GraphAndCodeControls.cs#L2426), and [2648](../src/Forma/GraphAndCodeControls.cs#L2648); interaction at [1886](../src/Forma/GraphAndCodeControls.cs#L1886), [2303](../src/Forma/GraphAndCodeControls.cs#L2303), and [2313](../src/Forma/GraphAndCodeControls.cs#L2313); rendering at [438](../src/Forma/GraphAndCodeControls.cs#L438), [562](../src/Forma/GraphAndCodeControls.cs#L562), [648](../src/Forma/GraphAndCodeControls.cs#L648), [2403-2404](../src/Forma/GraphAndCodeControls.cs#L2403), [2413](../src/Forma/GraphAndCodeControls.cs#L2413), [2423](../src/Forma/GraphAndCodeControls.cs#L2423), and [2680](../src/Forma/GraphAndCodeControls.cs#L2680).
- `MenusAndDialogs.cs`: rendering at [1004](../src/Forma/MenusAndDialogs.cs#L1004), [1061](../src/Forma/MenusAndDialogs.cs#L1061), [1066](../src/Forma/MenusAndDialogs.cs#L1066), [1329](../src/Forma/MenusAndDialogs.cs#L1329), [1347](../src/Forma/MenusAndDialogs.cs#L1347), [1687](../src/Forma/MenusAndDialogs.cs#L1687), and [1708](../src/Forma/MenusAndDialogs.cs#L1708).
- `SelectionControls.cs`: rendering at [1428](../src/Forma/SelectionControls.cs#L1428) and [2025](../src/Forma/SelectionControls.cs#L2025); layout at [2501](../src/Forma/SelectionControls.cs#L2501), [2768](../src/Forma/SelectionControls.cs#L2768), [2874](../src/Forma/SelectionControls.cs#L2874), and [2920](../src/Forma/SelectionControls.cs#L2920); interaction at [2812](../src/Forma/SelectionControls.cs#L2812) and [3192](../src/Forma/SelectionControls.cs#L3192).
- `Tree.cs`: rendering at [1611](../src/Forma/Tree.cs#L1611); layout/density compatibility at [1712](../src/Forma/Tree.cs#L1712), [1978](../src/Forma/Tree.cs#L1978), and twice at [1985](../src/Forma/Tree.cs#L1985).
- `UIContext.cs`: tooltip layout at [480](../src/Forma/UIContext.cs#L480).
- `UIRenderContext.cs`: density normalization at [141](../src/Forma/UIRenderContext.cs#L141) and twice at [143](../src/Forma/UIRenderContext.cs#L143).

## Rendering Calls

All 32 built-in `UIRenderContext.Text` calls are rendering paths:

- `AdvancedControls.cs`: [837](../src/Forma/AdvancedControls.cs#L837), [849](../src/Forma/AdvancedControls.cs#L849), [1587](../src/Forma/AdvancedControls.cs#L1587), [1674](../src/Forma/AdvancedControls.cs#L1674), [1719](../src/Forma/AdvancedControls.cs#L1719), [2879](../src/Forma/AdvancedControls.cs#L2879).
- `Controls.cs`: [503](../src/Forma/Controls.cs#L503), [516](../src/Forma/Controls.cs#L516), [535](../src/Forma/Controls.cs#L535), [894](../src/Forma/Controls.cs#L894), [1300](../src/Forma/Controls.cs#L1300).
- `GraphAndCodeControls.cs`: [438](../src/Forma/GraphAndCodeControls.cs#L438), [562](../src/Forma/GraphAndCodeControls.cs#L562), [648](../src/Forma/GraphAndCodeControls.cs#L648), [2397](../src/Forma/GraphAndCodeControls.cs#L2397), [2660](../src/Forma/GraphAndCodeControls.cs#L2660), [2680](../src/Forma/GraphAndCodeControls.cs#L2680).
- `MenusAndDialogs.cs`: [1005](../src/Forma/MenusAndDialogs.cs#L1005), [1061](../src/Forma/MenusAndDialogs.cs#L1061), [1066](../src/Forma/MenusAndDialogs.cs#L1066), [1329-1330](../src/Forma/MenusAndDialogs.cs#L1329), [1347](../src/Forma/MenusAndDialogs.cs#L1347), [1687](../src/Forma/MenusAndDialogs.cs#L1687), [1708](../src/Forma/MenusAndDialogs.cs#L1708).
- `SelectionControls.cs`: [1428](../src/Forma/SelectionControls.cs#L1428), [2025](../src/Forma/SelectionControls.cs#L2025), [2532](../src/Forma/SelectionControls.cs#L2532), [2535](../src/Forma/SelectionControls.cs#L2535).
- `Tree.cs`: [1611](../src/Forma/Tree.cs#L1611), [1717](../src/Forma/Tree.cs#L1717).
- `UIContext.cs`: [489](../src/Forma/UIContext.cs#L489).

The two compatibility overloads forward to `DrawText`, whose only direct `SpriteBatch.DrawString`
branches are [unscaled and scaled rendering](../src/Forma/UIRenderContext.cs#L146).
`DisplayFontResolver` is [copied into the renderer](../src/Forma/UIContext.cs#L243) and
[invoked before drawing](../src/Forma/UIRenderContext.cs#L140). It currently changes drawing while
measurement continues to use the logical 1x font.

## Manual Geometry Paths

These 19 paths compute text geometry outside a shared layout and therefore need explicit migration:

| Owner | Current behavior | Classification |
| --- | --- | --- |
| `Label` | [arbitrary wrapping](../src/Forma/Controls.cs#L335), [word wrapping](../src/Forma/Controls.cs#L345), [fallback/run measurement](../src/Forma/Controls.cs#L393), [font-backed run measurement](../src/Forma/Controls.cs#L414), [segmented rendering](../src/Forma/Controls.cs#L508), [iterative trimming](../src/Forma/Controls.cs#L578) | Layout and rendering. |
| `LineEdit`/`TextEdit` | [prefix hit test](../src/Forma/AdvancedControls.cs#L714), [second prefix hit test](../src/Forma/AdvancedControls.cs#L1533), [wrapped selection](../src/Forma/AdvancedControls.cs#L1105), [control-character prefix](../src/Forma/AdvancedControls.cs#L1613), [syntax prefix](../src/Forma/AdvancedControls.cs#L1713), [caret selection](../src/Forma/AdvancedControls.cs#L1725) | Interaction and rendering. |
| `CodeEdit` | [symbol lookup prefix hit test](../src/Forma/GraphAndCodeControls.cs#L1888) | Interaction. |
| `RichTextLabel` | [per-character render/wrap/select](../src/Forma/SelectionControls.cs#L2522), [line offsets](../src/Forma/SelectionControls.cs#L2790), [point hit test](../src/Forma/SelectionControls.cs#L2833), [visual lines](../src/Forma/SelectionControls.cs#L2903), [document metrics](../src/Forma/SelectionControls.cs#L2954), [metadata regions](../src/Forma/SelectionControls.cs#L3209) | Layout, interaction, and rendering. |

## Ambiguities and Escape Hatches

- `TextEdit` estimates wrapping columns from the width of `"0"`, then scans whitespace rather than
  measuring candidate runs ([current path](../src/Forma/AdvancedControls.cs#L2018)).
- `RichTextLabel` bulk-measures minimum size after replacing tabs, while drawing and hit testing use
  per-character widths and `TableCellWidth` ([minimum-size path](../src/Forma/SelectionControls.cs#L2368)).
- `Label` supports only `CharactersBeforeShaping`; other visible-character modes retain full text
  because glyph-level hiding is unavailable ([compatibility branch](../src/Forma/Controls.cs#L542)).
- Secret `LineEdit` fields hit-test original text but render and position carets with repeated secret
  characters ([hit-test path](../src/Forma/AdvancedControls.cs#L714), [render path](../src/Forma/AdvancedControls.cs#L836)).
- Direction and language metadata exists on several controls but is not consumed by rendering.
- Public [UIRenderContext.SpriteBatch](../src/Forma/UIRenderContext.cs#L36), [tree custom draw](../src/Forma/Tree.cs#L24), and [text-gutter custom draw](../src/Forma/AdvancedControls.cs#L310) allow application code to bypass `UIRenderContext.Text`. No built-in source path currently does so; these remain documented compatibility escape hatches.

## Audit Command

Use this source-only scan when migration changes the counts:

```sh
rg -n --glob '*.cs' --glob '!**/bin/**' --glob '!**/obj/**' \
  'SpriteFont|MeasureString|LineSpacing|\.Text\(' src/Forma
```
