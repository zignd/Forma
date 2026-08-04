// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;
using Microsoft.Xna.Framework;
using System.ComponentModel;

namespace Forma.Xaml.Build.Integration;

public sealed class HudView : Control
{
    public int AttachedHandlerCalls { get; private set; }

    public HudView()
    {
        FormaXamlLoader.Load(this);
    }

    private void OnStyleTargetAttached(object? sender, EventArgs args) => AttachedHandlerCalls++;
}

internal static class Program
{
    private static int Main()
    {
        var view = new HudView();
        var model = new HudViewModel { Message = "Ready" };
        view.DataContext = model;
        var scope = NameScope.GetNameScope(view);
        var label = scope?.Find<Label>("Child");
        var editor = scope?.Find<LineEdit>("Editor");
        if (view.Name != "HudRoot" || view.Position != new Vector2(3, 4) || view.Children.Count != 5 || label?.Text != "Ready" || editor?.Text != "Ready")
            throw new InvalidOperationException("Compiled Forma XAML did not populate the code-behind root.");
        if (!view.Resources.ContainsKey("LocalPalette") || !view.Resources.TryFind("MergedPalette", out _))
            throw new InvalidOperationException("Compiled Forma XAML did not populate local and merged resources.");
        var winner = (ResourceDictionary)view.Resources["Winner"];
        if (!winner.ContainsKey("LocalMarker") || winner.ContainsKey("MergedMarker"))
            throw new InvalidOperationException("Compiled Forma XAML did not preserve local-over-merged resource precedence.");
        if (view.Resources["TargetStyle"] is not Style)
            throw new InvalidOperationException("Compiled style was not registered as a keyed resource.");
        if (scope?.Find<ResourceTarget>("StaticTarget")?.Value.Name != "Static")
            throw new InvalidOperationException("Compiled static resource did not resolve its typed target value.");
        var dynamicTarget = scope?.Find<ResourceTarget>("DynamicTarget");
        if (dynamicTarget?.Value.Name != "Dynamic")
            throw new InvalidOperationException("Compiled dynamic resource did not resolve its initial value.");
        var styleTarget = scope?.Find<ResourceTarget>("StyleTarget");
        if (styleTarget?.TooltipText != "Styled" || styleTarget.Margins != new Thickness(1, 2, 3, 4) || styleTarget.Value.Name != "Static")
            throw new InvalidOperationException("Compiled selector style did not apply its typed setter.");
        styleTarget.Classes.Remove("styled");
        if (styleTarget.TooltipText != "Underlying" || styleTarget.Margins != new Thickness(0) || styleTarget.Value.Name != "Underlying")
            throw new InvalidOperationException("Compiled selector style did not restore the underlying value.");
        styleTarget.Classes.Add("styled");
        using var context = new UIContext();
        context.Add(view);
        if (view.AttachedHandlerCalls != 1)
            throw new InvalidOperationException("Compiled event hookup did not invoke the code-behind handler.");
        if (styleTarget.CustomMinimumSize != new Vector2(2, 3))
            throw new InvalidOperationException("Compiled event trigger did not start its storyboard.");
        context.Update(new GameTime(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)));
        if (styleTarget.CustomMinimumSize != new Vector2(8, 9))
            throw new InvalidOperationException("Compiled storyboard did not advance to its deterministic final value.");
        model.IsActive = true;
        if (dynamicTarget.CustomMinimumSize != new Vector2(1, 2))
            throw new InvalidOperationException("Compiled property trigger did not start its storyboard.");
        context.Update(new GameTime(TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(50)));
        if (dynamicTarget.CustomMinimumSize != new Vector2(1, 2))
            throw new InvalidOperationException("Compiled storyboard did not honor its repeat behavior.");
        dynamicTarget.RaiseStopRequested();
        if (dynamicTarget.CustomMinimumSize != Vector2.Zero)
            throw new InvalidOperationException("Compiled StopStoryboard did not restore the underlying value.");
        model.IsActive = false;
        if (dynamicTarget.CustomMinimumSize != Vector2.Zero)
            throw new InvalidOperationException("Compiled property trigger did not stop and restore its storyboard.");
        view.Resources["DynamicValue"] = new ResourceValue { Name = "Replaced" };
        if (dynamicTarget?.Value.Name != "Replaced")
            throw new InvalidOperationException("Compiled dynamic resource did not observe replacement values.");
        model.Message = "Updated";
        if (label.Text != "Updated") throw new InvalidOperationException("Compiled one-way binding did not observe its typed source property.");
        editor.Text = "Edited";
        if (model.Message != "Edited") throw new InvalidOperationException("Compiled two-way binding did not write through its typed source property.");
        context.Remove(view);
        if (styleTarget.CustomMinimumSize != Vector2.Zero)
            throw new InvalidOperationException("Compiled storyboard clock did not restore its underlying value on detach.");
        if (dynamicTarget.StopRequestedSubscriberCount != 0)
            throw new InvalidOperationException("Compiled event trigger remained subscribed after detach.");
        if (styleTarget.TooltipText != "Underlying" || styleTarget.Margins != new Thickness(0) || styleTarget.Value.Name != "Underlying")
            throw new InvalidOperationException("Compiled selector style did not dispose on detach.");
        if (dynamicTarget?.Value.Name != "Underlying")
            throw new InvalidOperationException("Compiled dynamic resource did not restore its underlying value on detach.");
        view.Resources["DynamicValue"] = new ResourceValue { Name = "AfterDetach" };
        if (dynamicTarget?.Value.Name != "Underlying")
            throw new InvalidOperationException("Compiled dynamic resource remained subscribed after detach.");
        var detachedLabelText = label.Text;
        model.Message = "AfterDetach";
        if (label.Text != detachedLabelText)
            throw new InvalidOperationException("Compiled one-way binding remained subscribed after detach.");
        editor.Text = "DetachedEdit";
        if (model.Message == "DetachedEdit")
            throw new InvalidOperationException("Compiled two-way binding remained subscribed after detach.");
        model.IsActive = true;
        if (dynamicTarget.CustomMinimumSize != Vector2.Zero)
            throw new InvalidOperationException("Compiled property trigger remained subscribed after detach.");
        context.Add(view);
        if (view.AttachedHandlerCalls != 1)
            throw new InvalidOperationException("Compiled event hookup remained subscribed after detach.");
        Console.WriteLine("Forma XAML build integration: PASS");
        return 0;
    }
}

public sealed class ResourceValue
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ResourceTarget : Control
{
    private EventHandler? _stopRequested;
    public int StopRequestedSubscriberCount { get; private set; }
    public event EventHandler? StopRequested
    {
        add { _stopRequested += value; StopRequestedSubscriberCount++; }
        remove { _stopRequested -= value; StopRequestedSubscriberCount--; }
    }
    public ResourceValue Value { get; set; } = new ResourceValue { Name = "Underlying" };
    public void RaiseStopRequested() => _stopRequested?.Invoke(this, EventArgs.Empty);
}

public sealed class HudViewModel : INotifyPropertyChanged
{
    private string _message = string.Empty;
    private bool _isActive;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            _message = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        }
    }
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
}