# Theme and custom-control example

This focused example combines a custom button type, a compiled `ControlTemplate`, a structural
selector style, and Forma's default `OptionButton` arrow icon.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --theme-control
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --theme-control
```

Activate the teal custom control to verify its normal button behavior remains intact. The adjacent
icon is resolved from the inherited default theme rather than embedded by the example.

See [Styling and themes](../../docs/styling-and-themes.md), [Theme icons](../../docs/theme-icons.md),
and the [button reference](../../docs/reference/controls/buttons.md).
