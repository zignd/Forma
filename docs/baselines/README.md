# Dynamic Text Baselines

The `dynamic-text-before-*` files are the canonical SpriteFont catalog baseline captured before the
dynamic-text migration. They record forced 1x and 2x screenshots, first-frame startup time,
post-first-frame managed allocations, the four font XNB sizes, and catalog-owned steady texture
memory.

The committed capture was produced on macOS arm64 with an Apple M4 Max through MonoGame DesktopGL.
Timing and allocations are comparison data, not cross-machine pass/fail thresholds. Regenerate all
four artifacts on a graphical desktop host with:

```sh
make text-baseline
```

The `dynamic-text-after/` matrix captures desktop 1x, Retina 2x, narrow wrapping/selection, and RTL
states for both runtime peers. Regenerate it with `scripts/capture-dynamic-text-states.sh`; inspect
the PNGs before treating them as approved visual evidence.

Template, items, selector, `DataGrid`, virtualization, and compositor invariants are documented
separately in [xaml-template-performance.md](xaml-template-performance.md). That record distinguishes
deterministic `make performance`/`make performance-graphics` bounds from host-specific benchmark
observations.
