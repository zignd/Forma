# XAML Template Performance Gates and Observations

This record separates deterministic CI invariants from machine-dependent benchmark observations for
the template, items, `DataGrid`, selector, and compositing release.

## Deterministic Gates

Run the portable Release gate with:

```sh
make performance
```

It executes 56 focused tests for each of MonoGame and FNA. The tests require:

- 100,000-item indexed sources to realize only viewport, overscan, and pinned rows without source
  enumeration;
- scrolling and visible-to-offscreen moves to reuse compatible rows without monotonically growing
  factory calls;
- recycle pools to stay within `RecyclePoolCapacity` and drain obsolete template/theme versions;
- vertical/horizontal variable stacks and uniform grids to report finite extents and bounded ranges;
- `DataGrid` realized cells to equal realized rows multiplied by visible columns, with no more than
  256 visible columns;
- large expanded hierarchies and local deltas to preserve selection/anchors without revisiting
  unrelated subtrees;
- collection and inherited-value changes to notify only affected slots/subtrees;
- pseudo-state and selector invalidation to update only matching visual/template descendants; and
- warm hierarchy queries to perform no projection visits, sort, or filter pass.

The compiler/build/package gates separately inspect generated Release assemblies for reflection,
dynamic-code, runtime-reader, source-XAML, compiler, hot-reload, and optional-package leakage. Those
structural checks are deterministic and are not inferred from elapsed time.

Run bounded graphics/cache invariants on a supported graphical host with:

```sh
make performance-graphics
```

This requires finite render-target dimensions/area, a 64 MiB device cache ceiling, bounded offscreen
nesting, fallback rendering when limits are exceeded, one deduplicated diagnostic per repeated
failure, warm glyph/layout reuse, and device-reset-safe cache recreation on both runtime peers.

## Benchmark Observations

These values are diagnostics, not CI thresholds. They were captured on 2026-08-05 on macOS arm64,
Apple M4 Max, .NET SDK 10.0.103, Release, at a 1440x900 physical viewport. Catalog values use 120
frames and 131 stories from `bash scripts/capture-xaml-templates-baseline.sh`; generated evidence is
under `Artifacts/xaml-templates-baseline/`.

| Peer | Scale | Startup ms | Steady allocated bytes | Bytes/frame | Texture bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| MonoGame DesktopGL | 1x | 932.5509 | 143,402,512 | 1,205,063.13 | 414,720 |
| MonoGame DesktopGL | 2x | 760.3441 | 140,145,912 | 1,177,696.74 | 1,238,016 |
| FNA Metal | 1x | 521.5478 | 141,829,856 | 1,191,847.53 | 414,720 |
| FNA Metal | 2x | 473.2790 | 137,738,728 | 1,157,468.30 | 1,238,016 |

The focused deterministic test slice completed in 7.3 seconds for MonoGame and 6.7 seconds for FNA
on this host. The graphics smoke reported:

| Peer | Load ms | Shape ms | Raster/upload ms | Warm layout x1000 ms | Warm draw x100 ms | Fallback ms | Churn ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| MonoGame | 0.901 | 0.074 | 0.527 | 0.661 | 6.870 | 0.389 | 2.470 |
| FNA Metal | 0.583 | 0.072 | 0.273 | 2.212 | 2.626 | 5.996 | 8.428 |

Startup, elapsed time, and managed allocation totals vary with host, driver, JIT/AOT mode, story
inventory, and diagnostics. Regressions are investigated by comparing like-for-like captures; they
do not replace the structural bounds enforced by `make performance` and
`make performance-graphics`.
