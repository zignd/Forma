# Provenance

This manifest records the source snapshot and current classification of material being extracted
into Forma. A pending classification blocks replacement of the source file's existing header and
blocks a public package release.

The machine-readable companion [`provenance-files.tsv`](provenance-files.tsv) records every
distributable repository file with its origin, author or holder, derivation, copyright, license, and
audit state. `scripts/check-compliance.sh` rejects tracked or unignored files missing from that list.

## Source Snapshot

| Material | Repository revision | Source commit | Author |
| --- | --- | --- | --- |
| UI source and tests | `zignd/MonoGame` at `49ea4f3d4a7e3638a9ed0875469dcd6f5af6000f` | `35921960e8d8210bcd01476a54e8cb5d03895e1d` | zignd `<hello@zignd.dev>` |
| Component catalog | `zignd/MonoGame` at `49ea4f3d4a7e3638a9ed0875469dcd6f5af6000f` | `500ca0bc6dd8d3008f3ad9ed6d2885c563e8f25b` | zignd `<hello@zignd.dev>` |
| Godot comparison source | `godotengine/godot` at `b4fb06cdb3db0c61db40c7b365bfa7adec3cb2ce` | N/A | Godot contributors |

The initial extraction uses the clean committed MonoGame snapshot, not the dirty ZonoBak working
tree. Files are imported as a curated replay so the new repository excludes unrelated MonoGame
history while this manifest preserves source revisions and authorship.

## Classification

| Material | Origin and derivation | License status |
| --- | --- | --- |
| `src/Forma/OkColor.cs` | Adapted from Bjorn Ottosson's `ok_color`, as bundled by Godot | Complete Bjorn Ottosson MIT notice retained in source; classified |
| 18 retained UI implementation files | Introduced by zignd in retained UI commit `35921960`; individually compared with the pinned Godot sources listed below | Forma MIT with applicable per-file Godot attribution; classified |
| Unit and render tests | Retained fixtures were introduced by zignd in UI commit `35921960`; extraction-only inventory test was authored for Forma; graphics-device helper is a reduced adaptation of MonoGame's test fixture | Forma-authored tests are MIT; graphics-device helper remains MS-PL; classified |
| Catalog source | `CatalogGame.cs`, `CatalogShell.cs`, `Program.cs`, and `StoryCatalog.cs` were introduced by zignd in catalog commit `500ca0bc`; `CatalogBackend.cs` was authored during extraction to replace internal MonoGame platform inspection | Forma MIT; classified |
| Catalog effect reload and metrics baseline | Stock-compatible polling reload service, expanded UI metrics, and deterministic three-frame 2x baseline authored during extraction | Forma MIT; classified |
| IBM Plex Sans inputs and XNB outputs | IBM Plex Sans font and generated MonoGame content | OFL-1.1 notice and reproducible generation record required |
| MonoGame APIs | Separate package or project dependency | Microsoft Public License; not relicensed by Forma |
| Video seeking integration | Consumes fork-only `VideoPlayer.SetPlayPosition` | Must remain outside the stock-compatible core package |

### Extracted File Audit Queue

| File | Initial classification | Audit state |
| --- | --- | --- |
| `AdvancedControls.cs` | C#/MonoGame advanced controls introduced by zignd in retained UI commit `35921960`; syntax highlighting, single/multiline editing, spin/option selection, tab-page layout, scrolling, and popup behavior adapt Godot `scene/resources/syntax_highlighter.cpp` plus `scene/gui/line_edit.cpp`, `text_edit.cpp`, `spin_box.cpp`, `option_button.cpp`, `tab_container.cpp`, `scroll_container.cpp`, and `popup.cpp` (and corresponding headers) at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `ColorControls.cs` | C#/MonoGame color controls introduced by zignd in retained UI commit `35921960`; picker modes/shapes, interactive commits, presets, HTML correction, old-color sampling, and button popup lifecycle adapt Godot `scene/gui/color_picker.cpp` and `color_picker.h` at the pinned revision. OKHSL math is consumed from separately classified `OkColor.cs` | Forma MIT with file-level Godot attribution; classified |
| `Containers.cs` | Simplified C#/MonoGame layout implementation introduced by zignd in retained UI commit `35921960`; child fitting, stretch starvation, centering, grid distribution, and margin layout adapt Godot `scene/gui/container.cpp`, `box_container.cpp`, `center_container.cpp`, `grid_container.cpp`, and `margin_container.cpp` at the pinned revision, without Godot's desired/max-size layer | Forma MIT with file-level Godot attribution; classified |
| `Control.cs` | C#/MonoGame retained-tree implementation introduced by zignd in retained UI commit `35921960`; enum/API concepts, anchors, event acceptance, minimum-size propagation, grow-direction clamping, RTL mirroring, and resize invalidation adapt Godot `scene/gui/control.cpp`, `control.h`, and `container.cpp` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `Controls.cs` | C#/MonoGame basic controls introduced by zignd in retained UI commit `35921960`; label layout/visibility, button input/group state, range snapping, slider interaction, progress display, and panel behavior adapt Godot `scene/gui/label.cpp`, `base_button.cpp`, `button.cpp`, `check_box.cpp`, `range.cpp`, `slider.cpp`, `progress_bar.cpp`, and `panel.cpp` (and corresponding headers) at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `FoldableControls.cs` | C#/MonoGame implementation introduced by zignd in retained UI commit `35921960`; group state, fold/expand transitions, and signal behavior adapt Godot `scene/gui/foldable_container.cpp` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `GraphAndCodeControls.cs` | C#/MonoGame graph and code-editing controls introduced by zignd in retained UI commit `35921960`; graph element/node/frame layout and interaction, graph connection/navigation/arrangement, and code editing behavior adapt Godot `scene/gui/graph_element.cpp`, `graph_node.cpp`, `graph_frame.cpp`, `graph_edit.cpp`, `graph_edit_arranger.cpp`, and `code_edit.cpp` (and corresponding headers) at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `GraphOverlays.cs` | C#/MonoGame overlays and geometry helpers introduced by zignd in retained UI commit `35921960`; `GraphEditMinimap` coordinate transforms, camera bounds, panning, and resize behavior adapt Godot `scene/gui/graph_edit.cpp` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `MenusAndDialogs.cs` | Simplified C#/MonoGame menu and dialog controls introduced by zignd in retained UI commit `35921960`; menu item mutation/search/submenus/shortcuts, menu-button/bar routing, confirmation ordering, and file-dialog filtering/history/mode policy adapt Godot `scene/gui/popup_menu.cpp`, `menu_button.cpp`, `menu_bar.cpp`, `dialogs.cpp`, and `file_dialog.cpp` (and corresponding headers) at the pinned revision; native-menu and platform-window layers are not ported | Forma MIT with file-level Godot attribution; classified |
| `OkColor.cs` | Adapted `ok_color` implementation | Complete Bjorn Ottosson MIT notice and Godot source revision recorded in file; classified |
| `Primitives.cs` | C#/MonoGame theme and value types introduced by zignd in retained UI commit `35921960`; UI enums map Godot `scene/gui/control.h`, `scene/gui/scroll_container.h`, `core/input/input_enums.h`, and `core/math/math_defs.h` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `SelectionControls.cs` | C#/MonoGame controls introduced by zignd in retained UI commit `35921960`; link presentation, texture-button geometry/hit masks, scrollbar interaction, texture-progress geometry, tab state/navigation, item-list selection/layout, and rich-text document/selection behavior adapt Godot `scene/gui/link_button.cpp`, `texture_button.cpp`, `scroll_bar.cpp`, `texture_progress_bar.cpp`, `tab_bar.cpp`, `item_list.cpp`, and `rich_text_label.cpp` (and corresponding headers) at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `SpecializedControls.cs` | Simplified C#/MonoGame implementations introduced by zignd in retained UI commit `35921960`; APIs and behavior adapt Godot `scene/gui/subviewport_container.cpp`, `virtual_joystick.cpp`, `rich_text_label.cpp`, and `rich_text_effect.cpp` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `src/Forma.Media/VideoStreamPlayer.cs` | Video control moved from retained UI commit `35921960`; API and behavior adapt Godot `scene/gui/video_stream_player.cpp`; extraction adds the stock/fork playback abstraction | Forma MIT with file-level Godot attribution; classified |
| `StyleBoxes.cs` | MonoGame-specific implementation introduced by zignd in retained UI commit `35921960`; API concepts and nine-patch behavior inspired by Godot `scene/resources/style_box.cpp`, `style_box_flat.cpp`, and `style_box_texture.cpp` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `Tree.cs` | Cohesive C#/MonoGame Tree and TreeItem implementation introduced by zignd in retained UI commit `35921960`; hierarchy/cell state, selection, editing, scrolling, drag/drop, hit testing, and rendering behavior adapt Godot `scene/gui/tree.cpp` and `tree.h` at the pinned revision | Forma MIT with file-level Godot attribution; classified |
| `UIContext.cs` | Independently authored MonoGame UI coordinator introduced by zignd in retained UI commit `35921960`; tooltip ancestor traversal adapts `scene/main/viewport.cpp::_gui_get_tooltip` from pinned Godot revision | Forma MIT with file-level Godot attribution; classified |
| `UIRenderContext.cs` | Independently authored MonoGame rendering adapter introduced by zignd in retained UI commit `35921960`; distinctive APIs have no match in pinned Godot source | Forma MIT; file header classified |
| `VisualControls.cs` | C#/MonoGame visual controls introduced by zignd in retained UI commit `35921960`; texture geometry, nine-patch behavior, aspect fitting, split dragging, flow layout, and panel layout adapt Godot `scene/gui/texture_rect.cpp`, `nine_patch_rect.cpp`, `aspect_ratio_container.cpp`, `split_container.cpp`, `flow_container.cpp`, and `panel_container.cpp` (and corresponding headers) at the pinned revision; simple color/reference/separator drawing is Forma-specific | Forma MIT with file-level Godot attribution; classified |
| `README.md` | Retained documentation, rewritten for Forma identity | Forma MIT |
| `tests/Forma.Tests/UITest.cs` | Independently authored behavioral fixture introduced by zignd in retained UI commit `35921960`; assertions encode documented Godot-compatible behavior but do not copy Godot test or implementation source; headless texture helper was adapted during extraction for stock MonoGame | Forma MIT; classified |
| `tests/Forma.Tests/CatalogInventoryTest.cs` | Extraction-only Forma test that reflects over public controls and verifies catalog story coverage | Forma MIT; classified |
| `tests/Forma.RenderTests/UIRenderTest.cs` | Independently authored rendering fixture introduced by zignd in retained UI commit `35921960`; adapted during extraction to use Forma's isolated graphics host and explicit macOS policy | Forma MIT; classified |
| `tests/Forma.RenderTests/GraphicsDeviceTestFixtureBase.cs` | Reduced adaptation of MonoGame `Tests/Framework/Graphics/GraphicsDeviceTestFixtureBase.cs`; retains its game/device/content setup shape and device-creation call while omitting MonoGame's capture infrastructure | MonoGame Foundation copyright retained; MS-PL; classified |
| `tests/Forma.PackageConsumer/Program.cs` | Extraction-only public API smoke consumer compiled against the packed Forma package | Forma MIT; classified |
| `samples/Forma.Catalog/CatalogGame.cs`, `CatalogShell.cs`, `Program.cs`, `StoryCatalog.cs` | Independently authored catalog host, shell, entry point, and complete story registry introduced by zignd in retained catalog commit `500ca0bc`; namespace, stock-package integration, and metrics output were adapted during extraction | Forma MIT; classified |
| `samples/Forma.Catalog/CatalogBackend.cs` | Extraction-only assembly-metadata reader replacing the original catalog's internal MonoGame `PlatformInfo` dependency | Forma MIT; classified |
| `tests/Assets/Fonts/Catalog.spritefont` | 14px IBM Plex Sans catalog font description from retained UI commit | Forma-authored description; font remains OFL-1.1 |
| `tests/Assets/Fonts/Catalog@2x.spritefont` | 28px IBM Plex Sans density font description from retained UI commit | Forma-authored description; font remains OFL-1.1 |
| `tests/Assets/Fonts/Catalog.xnb` | Canonical catalog font atlas regenerated during extraction | MGCB 3.8.5 output from OFL-1.1 font; SHA-256 `a39a25be1718ea6d9ba6f3cfcbad63ef69306227f17f3ed23043ddbd5d3294ab` |
| `tests/Assets/Fonts/Catalog@2x.xnb` | Canonical density font atlas regenerated during extraction | MGCB 3.8.5 output from OFL-1.1 font; SHA-256 `bfd33d56e0eb860c927565db057cbc3ad6fd9f0f80ec1ca31904f42613eeb352` |
| `tests/Assets/Fonts/IBMPlexSans-Regular.ttf` | IBM Plex Sans source font | OFL-1.1, reserved font name "Plex" |
| `tests/Assets/Fonts/IBMPlexSans-Regular-License-OFL.txt` | IBM Plex Sans license from retained UI commit | OFL-1.1 |
| Repository configuration, project files, scripts, workflows, and remaining documentation | Authored or rewritten by Igor Hipólito Vieira during extraction; exact per-file records are in `docs/provenance-files.tsv` | Forma MIT; classified |

No images, shaders, native libraries, or other binary assets are present. The two XNB font atlases
and IBM Plex Sans TTF are the only repository binaries and are classified above.

## Baselines

- MonoGame reference surfaces: published `DesktopGL`, `WindowsDX`, and `Native` 3.8.5 packages;
  compile validation is automated by `scripts/check-backend-references.sh`. Package references are
  private so applications select exactly one runtime backend.
- Local development override: `MonoGame.Framework.DesktopGL.csproj` from the snapshot above.
- Public API baseline: 185 top-level types and 3,615 declaration lines from the clean MonoGame
  snapshot, normalized from `Microsoft.Xna.Framework.UI` to `Forma`. The approved stock-compatible
  core has 184 types and 3,567 lines; `VideoStreamPlayer` is approved separately in Forma.Media. See
  `docs/api-compatibility.md` and run `bash scripts/check-api-compatibility.sh`.
- Existing in-fork non-render UI fixture: 392 discovered passing tests.
- Forma stock-package unit and catalog inventory fixture: 396 passing tests, including the retained
  `VideoStreamPlayer` configuration test and an extraction-only media-backend boundary test.
- Forma render fixture: five tests discovered and compiled. NUnit excludes execution on macOS before
  fixture setup because SDL graphics-device creation requires the process main thread; Windows and
  Linux retain the executable self-contained fixture path.

## Catalog Font Generation

The authoritative inputs are `IBMPlexSans-Regular.ttf`, `Catalog.spritefont`, and
`Catalog@2x.spritefont` under `tests/Assets/Fonts`. The repository pins `dotnet-mgcb` and
`MonoGame.Content.Builder.Task` 3.8.5. `samples/Forma.Catalog/Content/Content.mgcb` builds both fonts
for `DesktopGL`, `Reach`, uncompressed XNB output, `Color` texture format, and premultiplied alpha.

Run `dotnet build samples/Forma.Catalog/Forma.Catalog.csproj --configuration Release` to regenerate
the runtime outputs. `bash scripts/test-package-consumer.sh` byte-compares those outputs with the
canonical XNB files and is executed by CI.