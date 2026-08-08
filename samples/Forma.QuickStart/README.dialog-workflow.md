# Dialog workflow example

This focused example owns a modal `ConfirmationDialog` in the same retained tree as its launch
button and handles confirmation and cancellation as explicit results.

```bash
dotnet run --project samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj -- --dialog-workflow
dotnet run --project samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj -p:FormaRuntime=FNA -- --dialog-workflow
```

Open the dialog, then choose **Delete** or **Keep**. Modal input stays within the dialog and focus
returns to the prior control when it closes; the status label records the chosen result.

See [Controls and containers](../../docs/controls-and-containers.md) and the
[dialog reference](../../docs/reference/controls/dialogs.md).
