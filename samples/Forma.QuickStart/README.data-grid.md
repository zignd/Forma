# Observable DataGrid example

This focused example binds an `ObservableCollection` of typed rows to a `DataGrid`. Selection is
two-way, collection additions appear immediately, and row property notifications refresh progress.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --data-grid
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --data-grid
```

Select a quest and choose **Advance selected** to update its progress, or choose **Add quest** to
append and select a new observable row.

See [Data binding](../../docs/data-binding.md) and the
[data-display reference](../../docs/reference/controls/data-display.md).
