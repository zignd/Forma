// SPDX-License-Identifier: MIT

using Forma.Xaml;
using Microsoft.Xna.Framework;

namespace Forma.QuickStart;

public sealed class DialogWorkflowView : BoxContainer
{
    public DialogWorkflowView() : base(Orientation.Vertical)
    {
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("DialogWorkflowView did not create a namescope.");
        var open = scope.Find<Button>("OpenDialogButton");
        var status = scope.Find<Label>("DialogStatus");
        var dialog = scope.Find<ConfirmationDialog>("DeleteDialog");

        open.Pressed += (_, _) => dialog.PopupAt(new Vector2(180, 120));
        dialog.Confirmed += (_, _) => status.Text = "The save slot was deleted.";
        dialog.Canceled += (_, _) => status.Text = "Deletion was canceled.";
    }
}