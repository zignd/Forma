# Focused QuickStart examples

The shared QuickStart core contains the C#, XAML, settings, HUD, inventory, dialog, DataGrid,
theme/control, dynamic-text, and runtime-SVG examples. Each focused example has a sibling
`README.<name>.md` with its purpose, runtime commands, expected result, and related guides.

## Validation and screenshots

CI runs `scripts/check-quick-start.sh` for MonoGame and FNA from an empty NuGet cache on Linux,
macOS, and Windows. The check builds once, runs every selector for a bounded frame count, and rejects
missing, invalid, or unexpectedly small PNG output.

The project maintainer owns screenshot refresh and visual review. Capture review evidence on a
graphical host with:

```sh
FORMA_RUNTIME=MonoGame \
FORMA_SCREENSHOT_OUTPUT=Artifacts/quick-start-examples/monogame \
bash scripts/check-quick-start.sh

FORMA_RUNTIME=FNA \
FORMA_SCREENSHOT_OUTPUT=Artifacts/quick-start-examples/fna \
bash scripts/check-quick-start.sh
```

Generated captures stay under `Artifacts/` and are not committed by default. A change to visible
example output must include fresh captures in the pull-request or release-review evidence, with the
maintainer checking text fit, clipping, focus/selection state, default icons, and nonblank SVG
output at the affected display scale.