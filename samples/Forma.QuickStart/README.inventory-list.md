# Scrollable inventory example

This focused example binds 24 typed inventory records to a `ListBox`, renders each record through a
compiled `DataTemplate`, scrolls within a bounded viewport, and projects selection into a status
label.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --inventory-list
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --inventory-list
```

Scroll through the rows with the wheel or keyboard, then select a row. The summary below the list
updates from the selected typed model.

See [Controls and containers](../../docs/controls-and-containers.md) and the
[collection-control reference](../../docs/reference/controls/collections.md).
