# Runtime-SVG example

This focused example validates an immutable in-memory `SvgImageSource`, renders it through an
`Image`, and reports the explicitly installed Skia reference backend and bounded profile.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --runtime-svg
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --runtime-svg
```

The result is a gradient signal badge rendered from copied SVG bytes. This source-project host calls
`SvgSkiaBackendDefaults.Verify()` only for this selector; no browser or network fallback exists.

See [Runtime SVG](../../docs/runtime-svg.md), the
[supported profile](../../docs/runtime-svg-profile-v1.md), and
[Resource lifetime](../../docs/resource-lifetime.md).
