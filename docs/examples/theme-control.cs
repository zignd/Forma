// SPDX-License-Identifier: MIT

using Forma.Xaml;

namespace Forma.QuickStart;

public sealed class ThemedActionButton : BaseButton
{
}

public sealed class ThemeControlView : BoxContainer
{
    public ThemeControlView() : base(Orientation.Vertical)
    {
        FormaXamlLoader.Load(this);
        var scope = NameScope.GetNameScope(this)
            ?? throw new InvalidOperationException("ThemeControlView did not create a namescope.");
        var status = scope.Find<Label>("ThemeStatus");
        scope.Find<ThemedActionButton>("ThemedButton").Pressed += (_, _) =>
            status.Text = "The custom templated control was activated.";
    }
}