---
title: Troubleshooting
description: Diagnose Forma setup, XAML, rendering, and asset failures.
---

# Troubleshooting

Start with the surface that fails. Each page begins with bounded checks that preserve the original
diagnostic instead of masking it with fallback behavior.

| Symptom | Guide |
| --- | --- |
| Restore conflict, mixed peer assemblies, missing native library, or unsupported host | [Runtime and packages](runtime-and-packages.md) |
| `FXAML` diagnostic, build task failure, stale generated view, or hot-reload rejection | [XAML build and hot reload](xaml-build-and-hot-reload.md) |
| Blank UI, missing font/content/SVG, graphics-device startup, reset, or scaling issue | [Rendering and assets](rendering-and-assets.md) |

When reporting a problem, include the selected runtime peer, backend, configuration, target RID,
first complete error, and the smallest command that reproduces it. Follow the repository's
[support policy](https://github.com/zigrok/Forma/blob/main/SUPPORT.md) for questions, bugs, and
security reports.
