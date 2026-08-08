# Responsive HUD example

This focused example fills its root through anchors, groups related information in containers, and
pins four HUD clusters to canvas edges so they follow a resizable logical viewport.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --responsive-hud
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --responsive-hud --display-scale 2
```

Resize the window and confirm each cluster stays attached to its corner. The `--display-scale`
option sets physical pixels per logical UI coordinate; the second command renders a 2x density while
preserving the same logical layout rules.

See [Layout and sizing](../../docs/layout-and-sizing.md) and the
[container reference](../../docs/reference/controls/containers.md).
