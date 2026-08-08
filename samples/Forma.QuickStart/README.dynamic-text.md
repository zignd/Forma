# Dynamic-text example

This focused example reuses a host-owned `UIFontFace`, creates two `DynamicUIFont` identities, and
applies hinting and OpenType features without exposing native font handles.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --dynamic-text
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --dynamic-text
```

The result shows runtime-loaded Inter at two logical sizes. The game retains and disposes the face;
the controls and fonts only borrow it.

See [Dynamic text](../../docs/dynamic-text.md) and
[Resource lifetime](../../docs/resource-lifetime.md).
