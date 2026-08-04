# Dynamic Text Desktop Spike

The Phase 0 spike is `tests/Forma.DynamicTextSpike`. It loads the repository Inter and Noto Sans
Arabic fixtures from bytes, shapes Latin and Arabic with HarfBuzz, rasterizes the returned glyph IDs
with FreeType, packs one 256x256 Alpha8 page, uploads it, and samples it through SpriteBatch into a
color render target.

Run packaged MonoGame and FNA parity with:

```sh
make text-spike
```

Include the local FNA fork with:

```sh
make text-spike-local
```

On macOS arm64, packaged MonoGame DesktopGL, packaged FNA Metal, and the local FNA Metal build
produced the same normalized shaping/raster/coverage SHA-256. The spike also exposed a graphics
contract difference: MonoGame DesktopGL samples Alpha8 coverage from red with opaque alpha, while
FNA Metal samples coverage from alpha with zero RGB. The dynamic renderer must normalize that
backend behavior through an internal shader/adapter; stock SpriteBatch tint semantics are not a
portable Alpha8 text-rendering contract.

This result validates the selected desktop bindings and graphics-resource path. It does not close
the Linux arm64 FreeType asset gap or prove untested platform/backend cells.