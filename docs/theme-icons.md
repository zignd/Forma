# Default Theme Icons

Forma embeds the same 67 logical default icons in its MonoGame and FNA packages. Applications do
not need MGCB, XNB files, an SVG parser, or copied content assets. The PNG atlases remain the
native-free default and fallback. The matching optional `Forma.Svg` companion also packages the
authoritative SVG sources for exact-scale runtime rendering.

Set `UIContext.ThemeIconRenderingPolicy` to `BitmapAtlas`, `RuntimeSvg`, or `Auto`. Runtime SVG keeps
the selected PNG atlas available as a per-icon fallback, so a missing backend or unsupported source
never removes the current default icon. `ThemeIconDiagnostics` reports the selected density,
runtime SVG icon count, PNG fallback count, missing lookups, atlas memory, and cache generation.
See [runtime-svg.md](runtime-svg.md) for backend setup and security limits.

## Renderer Selection

The build spike compared Svg.Skia with SVG.NET. Svg.Skia was selected because it uses the same
cross-platform Skia raster path on macOS, Linux, and Windows, exposes explicit sRGB premultiplied
RGBA output, and does not rely on `System.Drawing.Common`. SVG.NET's normal bitmap path uses
System.Drawing, which is supported only on Windows in current .NET and therefore cannot provide the
same clean-checkout command on all CI operating systems. Svg.Skia 5.2.0 and SkiaSharp 4.148.0 are
pinned in central build properties; CI regenerates and byte-compares output on every supported host.

## Ownership and Density

`UIContext` lazily decodes the embedded PNG atlas on its render thread after receiving a valid
graphics device. Contexts sharing one device reuse a weak-keyed device cache; the last context
disposes the textures. A disposed atlas is recreated on the next draw. `ThemeIcon` values are
non-owning atlas views and must not dispose application or cache textures.

The 1x atlas is selected below a display scale of 1.5; the 2x atlas is selected at 1.5 and above.
Scales below 1x use the 1x atlas, and scales above 2x use the 2x atlas. Logical icon dimensions do
not change with density. Linear clamping and two logical pixels of transparent atlas padding avoid
neighbor bleed at fractional scales.

The current canonical payload is approximately 40 KB of PNG data. Decoded texture memory is about
274 KB for 1x and 1.05 MB for 2x. Atlases are loaded on demand, so a context normally retains only
the densities it has displayed.

## Rollout Budgets

The current peer packages are 328 KB each. Icon rollout budgets are: no more than 128 KB of
compressed icon/manifest payload, no more than 350 KB per core peer package, no more than 1.1 MB for
the active 2x texture, and no more than 1.4 MB if both densities have been visited on one device.
The canonical values are 274,432 decoded bytes at 1x and 1,097,728 bytes at 2x.

All default icons for one density occupy one atlas page. A control sequence using only default icons
therefore introduces at most one default-icon texture per density, without per-icon texture switches.
The lifecycle test requires cache generation to remain unchanged across repeated warm `Ensure`
calls, proving warm frames perform no image decode, texture creation, or atlas allocation. Cold load
is bounded by one embedded PNG decode and one upload for each density first displayed.

## Customization

Theme icons are type-scoped and inherited in the same derived-to-base order as style boxes:

```csharp
var customTheme = new Theme { Parent = context.Theme };
customTheme.SetIcon("arrow", customArrow, nameof(OptionButton));
option.ThemeOverride = customTheme;
```

`RemoveIcon` reveals inherited/default values. `SuppressIcon` intentionally hides an inherited
icon. Per-control `AddThemeIconOverride`, `RemoveThemeIconOverride`, and `SuppressThemeIcon` provide
the same behavior locally. Existing `Texture2D` properties on buttons, menu items, tabs, tree cells,
graph ports, and gutters remain application-owned content icons and take precedence where present.

## Regeneration

`make icons-verify` regenerates the canonical 1x/2x PNG atlases and JSON metadata and fails on byte
drift. `make icons` updates canonical outputs from the already imported SVG inputs. A reviewed
source update requires a Godot checkout at the revision recorded in `assets/theme-icons/imports.json`:

```sh
make icons-import GODOT_ROOT=../godot
make icons
make icons-verify
```

The pipeline rejects duplicate names or sources, editor-only paths, missing mappings, unclassified
licenses, zero-sized SVGs, source hash drift, and incomplete density output.

## Intentional Exclusions

Only runtime icons mapped to behavior Forma currently exposes are imported. Dialog/window close
icons, ColorPicker pipette/shape/overbright icons, tab drop marks,
and CodeEdit completion-color backgrounds remain excluded until corresponding interactive behavior
exists. Godot's editor icon collection is excluded entirely.