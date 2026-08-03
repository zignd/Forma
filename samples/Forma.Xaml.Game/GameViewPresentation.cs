// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;
using Microsoft.Xna.Framework;

namespace Forma.Xaml.Game;

public static class GameViewPresentation
{
    private static readonly XamlProperty<Thickness> MarginsProperty = new(
        nameof(Control.Margins), target => ((Control)target).Margins, (target, value) => ((Control)target).Margins = value);
    private static readonly XamlProperty<Vector2> MinimumSizeProperty = new(
        nameof(Control.CustomMinimumSize), target => ((Control)target).CustomMinimumSize, (target, value) => ((Control)target).CustomMinimumSize = value);
    private static readonly XamlProperty<Color?> FontColorProperty = new(
        nameof(Label.FontColor), target => ((Label)target).FontColor, (target, value) => ((Label)target).FontColor = value);

    public static void AttachHud(Control root, GameHudViewModel viewModel)
    {
        var scope = RequireScope(root);
        var palette = new ResourceDictionary { ["TimerColor"] = new Color(48, 185, 164) };
        root.Resources.MergedDictionaries.Add(palette);
        DynamicResource.Attach(root, scope.Find<Label>("RemainingText"), FontColorProperty, "TimerColor", value => (Color)value);
        AttachSharedStyles(root);

        var pulse = new Vector2Timeline("RemainingText", MinimumSizeProperty);
        pulse.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, new Vector2(96, 28)));
        pulse.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.FromMilliseconds(280), new Vector2(112, 34), Easing.CubicOut));
        var lowTime = new Storyboard { AutoReverse = true, RepeatBehavior = RepeatBehavior.ForeverValue };
        lowTime.Timelines.Add(pulse);
        var path = new CompiledBindingPath<GameHudViewModel, bool>(
            source => BindingValue<bool>.FromValue(source.IsLowTime),
            (source, update) => BindingSubscriptions.PropertyChanged(source, nameof(GameHudViewModel.IsLowTime), update));
        StoryboardTriggers.AttachProperty(root, path, viewModel, true, lowTime);

        var eventTimeline = new Vector2Timeline("StatusText", MinimumSizeProperty);
        eventTimeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, new Vector2(220, 26)));
        eventTimeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.FromMilliseconds(180), new Vector2(240, 30), Easing.CubicOut));
        var pauseFeedback = new Storyboard { AutoReverse = true, FillBehavior = FillBehavior.Stop };
        pauseFeedback.Timelines.Add(eventTimeline);
        var pause = scope.Find<Button>("PauseButton");
        StoryboardTriggers.AttachEvent<EventHandler>(root, handler => pause.Pressed += handler, handler => pause.Pressed -= handler, action => (_, _) => action(), pauseFeedback);
    }

    public static void AttachSettings(Control root)
    {
        root.Resources["AccentColor"] = new Color(48, 185, 164);
        var scope = RequireScope(root);
        DynamicResource.Attach(root, scope.Find<Label>("SettingsTitle"), FontColorProperty, "AccentColor", value => (Color)value);
        AttachSharedStyles(root);
    }

    public static void AttachResult(Control root)
    {
        AttachSharedStyles(root);
        var entrance = new Vector2Timeline("ResultRoot", MinimumSizeProperty);
        entrance.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, new Vector2(380, 180)));
        entrance.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.FromMilliseconds(260), new Vector2(420, 220), Easing.CubicOut));
        var storyboard = new Storyboard { FillBehavior = FillBehavior.HoldEnd };
        storyboard.Timelines.Add(entrance);
        StoryboardTriggers.AttachEvent<EventHandler>(root, handler => root.Attached += handler, handler => root.Attached -= handler, action => (_, _) => action(), storyboard);
    }

    public static void SetLowTimeResource(Control root, bool lowTime) =>
        root.Resources["TimerColor"] = lowTime ? new Color(246, 185, 73) : new Color(48, 185, 164);

    private static void AttachSharedStyles(Control root)
    {
        var normal = new Style(".action");
        normal.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(0)));
        var hover = new Style(".action:hover");
        hover.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(2)));
        var pressed = new Style(".action:pressed");
        pressed.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(4, 3, 0, 0)));
        var focused = new Style(".setting:focus");
        focused.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(2, 0, 2, 0)));
        var checkedStyle = new Style(".setting:checked");
        checkedStyle.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(4, 0, 4, 0)));
        var disabled = new Style(".setting:disabled");
        disabled.Setters.Add(new StyleSetter<Thickness>(MarginsProperty, new Thickness(6, 0, 6, 0)));
        StyleEngine.Attach(root, new[] { normal, hover, pressed, focused, checkedStyle, disabled });
    }

    private static NameScope RequireScope(Control root) =>
        NameScope.GetNameScope(root) ?? throw new InvalidOperationException("The game XAML view has no namescope.");
}