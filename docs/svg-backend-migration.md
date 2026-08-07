# SVG Backend Selection and Migration

## Explicit Packages

Choose exactly one runtime-matched backend:

```xml
<PackageReference Include="Forma.Svg.Skia.MonoGame" Version="0.1.0-alpha.1" />
```

or:

```xml
<PackageReference Include="Forma.Svg.ThorVG.MonoGame" Version="0.1.0-alpha.1" />
```

Use `.FNA` peers with `Forma.FNA`. Package build targets install the selected backend without
reflection. Source-project hosts call `SvgSkiaBackendDefaults.Install()` or
`SvgThorvgBackendDefaults.Install()` before first SVG use; `Verify()` additionally runs a bounded
2x2 raster check.

Forma's Catalog and SVG smoke hosts default to ThorVG on validated macOS arm64 and Linux x64
systems. Pass `-p:SvgBackend=Skia` for explicit rollback. Windows remains on explicit Skia until
ThorVG Windows native assets are qualified and distributed.

The unused `Forma.Svg.MonoGame` and `Forma.Svg.FNA` compatibility identities have no prior public
release to support and are excluded from the initial package manifest. Choose an explicit backend
package directly. Build guards reject mixed runtime peers and mixed SVG backends.

`SvgRuntime.Health` exposes `BackendId`, `Version`, `ProfileVersion`, `NativeAvailability`,
`LinkMode`, tested features, and a bounded diagnostic. Missing native assets, ABI mismatch, conflict,
and late selection fail explicitly. Forma never falls back from ThorVG to Skia. Default theme icons
can still use `ThemeIconRenderingPolicy.BitmapAtlas` as the renderer-independent rollback.

## Deployment Status

| Backend | macOS arm64 | Linux x64 | Windows x64 | Restricted/console |
| --- | --- | --- | --- | --- |
| Skia 5.2.0 | Supported reference | Supported reference | Supported reference | Untested |
| ThorVG 1.1.0 dynamic | Experimental, validated | Experimental, validated | Untested | Not applicable |
| ThorVG ABI 1 static | Reference host validated | Reference host validated | Untested | Console-ready architecture only |

"Console-ready" means source/static integration is available. "Console-qualified" requires current
authorized evidence for the exact SDK, compiler, linker, hardware, source commit, ABI/profile, and
Forma release. No console is currently qualified.

Rollback consists of removing the ThorVG package and selecting explicit Skia, or selecting bitmap
atlas policy when runtime SVG is optional. Application SVG failures remain explicit; only default
theme icons have their documented PNG fallback.
