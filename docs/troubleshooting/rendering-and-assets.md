---
title: Troubleshoot rendering and assets
description: Resolve blank Forma output, missing assets, SVG, scaling, and device lifecycle issues.
---

# Troubleshoot rendering and assets

## Window opens but UI is blank

Confirm the context has a root, the root has a nonzero logical `Size`, and the host calls both update
and draw. `UIComponent` performs those integrations and updates `ViewportSize`. A custom host must do
the equivalent. Check `Visible`, opacity, clipping, and whether the root lies outside the viewport
before changing renderer code.

Run a bounded rendered fixture to distinguish host setup from application composition:

```sh
FORMA_RUNTIME=MonoGame bash scripts/check-quick-start.sh
FORMA_RUNTIME=FNA bash scripts/check-quick-start.sh
```

The command renders both C# and XAML roots, validates PNG output, and exits without interaction.

## Font or content is missing

Project-file fonts must be copied beside the executable at the path supplied to
`UIFontFace.FromProjectFile`. Content-pipeline `SpriteFont` values require their compiled XNB and
matching content root. Keep `UIFontFace` alive until all contexts using its dynamic fonts stop
drawing. See [Dynamic text](../dynamic-text.md) and [Resource lifetime](../resource-lifetime.md).

For text rendered as boxes or missing graphemes, inspect the configured family and fallback faces
before increasing atlas limits. Missing glyphs are data and should remain a bounded diagnostic.

## SVG is absent or reports an unhealthy backend

Install exactly one runtime-matched SVG backend before first SVG use. Source-project hosts call the
backend's `Install()`; `Verify()` adds a bounded raster probe. Do not mix Skia and ThorVG or expect
application SVG to cross-fallback between them. Inspect `SvgRuntime.Health` and
`UIContext.SvgRasterDiagnostics.LastFailure`.

```sh
make svg-selection
make svg-packages
```

The [runtime SVG guide](../runtime-svg.md) owns source limits, caching, security, and deployment.

## Size or input is wrong on a dense display

`DisplayScale` is physical pixels per logical UI coordinate and defaults to `1`. Supply a finite,
positive value; keep root sizes and control geometry in logical coordinates. Forma maps pointer
input back to logical coordinates and invalidates scale-sensitive caches. Do not multiply both the
host viewport and control dimensions by the scale. See [Layout and sizing](../layout-and-sizing.md).

## Output breaks after graphics-device reset

The renderer, dynamic glyph atlas, and SVG raster cache subscribe to device reset and recreate or
invalidate device objects. Application-created textures remain application-owned and must follow the
runtime peer's reset rules. Do not retain internal atlas textures; use immutable diagnostic snapshots.
If a reset-only issue reproduces in repository fixtures, run the relevant SVG/text smoke and report
the selected peer, backend, RID, and first diagnostic.

## Graphics device is unavailable in automation

Graphical smoke requires a supported desktop session and backend. Linux CI uses software graphics
under Xvfb. Unit/compiler/docs tests can run headlessly, but a rendered smoke cannot prove startup
without a device. Use the host matrix in [Runtime support](../runtime-support.md) rather than treating
a missing display as a control-tree failure.
