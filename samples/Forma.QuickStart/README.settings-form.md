# Settings form example

This focused example demonstrates retained form layout, live validation, explicit keyboard focus
order, and two-way user input through `LineEdit.Text`.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --settings-form
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --settings-form
```

Enter fewer than three non-space characters to see validation keep the save action disabled. Enter a
longer name, use Tab and Shift+Tab to move between the input and button, then activate **Save
settings** to see the retained status update.

See [Controls and containers](../../docs/controls-and-containers.md), [Input and focus](../../docs/input-and-focus.md),
and the [text-input reference](../../docs/reference/controls/text-input.md).
