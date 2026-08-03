// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;

namespace Forma.Xaml.Game;

public sealed class GameScreen : Control, IDisposable
{
    private readonly List<IDisposable> _hotReloadRegistrations = new();
    private Control _hudView;
    private Control _settingsView;
    private Control _resultView;

    public GameScreen()
    {
        Presenter = new GamePresenter(new GameSession(), new GameHudViewModel(), new GameSettingsViewModel(), new GameResultViewModel());
        _hudView = new GameHudView(Presenter.Hud);
        _settingsView = new GameSettingsView(Presenter.Settings);
        _resultView = new GameResultView(Presenter.Result);
        AddChild(_hudView);
        AddChild(_settingsView);
        AddChild(_resultView);
        WireHud(_hudView);
        WireSettings(_settingsView);
        WireResult(_resultView);
        Present();
    }

    public GamePresenter Presenter { get; }
    public Control HudView => _hudView;
    public Control SettingsView => _settingsView;
    public Control ResultView => _resultView;

    public void Update(TimeSpan elapsed, GameInput input)
    {
        Presenter.Update(elapsed, input);
        Present();
    }

    public void Arrange(Microsoft.Xna.Framework.Vector2 viewport)
    {
        Size = viewport;
        _hudView.Position = new Microsoft.Xna.Framework.Vector2(20);
        _hudView.Size = new Microsoft.Xna.Framework.Vector2(300, 190);
        ArrangeOverlay(_settingsView, viewport);
        ArrangeOverlay(_resultView, viewport);
    }

#if FORMA_XAML_HOT_RELOAD
    public void EnableHotReload(Forma.Xaml.HotReload.FormaXamlHotReloadService service)
    {
        _hotReloadRegistrations.Add(service.Register<Control>("GameHudView.xaml", () => _hudView, (oldView, replacement) => ReplaceView(ref _hudView, oldView, replacement, Presenter.Hud, WireHotReloadedHud)));
        _hotReloadRegistrations.Add(service.Register<Control>("GameSettingsView.xaml", () => _settingsView, (oldView, replacement) => ReplaceView(ref _settingsView, oldView, replacement, Presenter.Settings, WireHotReloadedSettings)));
        _hotReloadRegistrations.Add(service.Register<Control>("GameResultView.xaml", () => _resultView, (oldView, replacement) => ReplaceView(ref _resultView, oldView, replacement, Presenter.Result, WireHotReloadedResult)));
    }
#endif

    private void Present()
    {
        _settingsView.Visible = Presenter.Session.Phase == GamePhase.Paused;
        _resultView.Visible = Presenter.Session.Phase == GamePhase.Result;
        GameViewPresentation.SetLowTimeResource(_hudView, Presenter.Session.IsLowTime);
        if (Presenter.Session.IsLowTime) _hudView.Classes.Add("low-time");
        else _hudView.Classes.Remove("low-time");
        var volume = NameScope.GetNameScope(_settingsView)?.Find<HSlider>("Volume");
        if (volume != null) volume.Enabled = Presenter.Settings.SoundEnabled;
    }

    private void WireHud(Control root)
    {
        var pause = RequireScope(root).Find<Button>("PauseButton");
        pause.Pressed += (_, _) => TogglePause();
    }

    private void WireSettings(Control root)
    {
        var scope = RequireScope(root);
        scope.Find<Button>("ResumeButton").Pressed += (_, _) => TogglePause();
        scope.Find<Button>("RestartButton").Pressed += (_, _) => Restart();
        scope.Find<CheckBox>("SoundEnabled").Toggled += (_, _) => Present();
    }

    private void WireResult(Control root) => RequireScope(root).Find<Button>("PlayAgainButton").Pressed += (_, _) => Restart();

    private void WireHotReloadedHud(Control root)
    {
        GameViewPresentation.AttachHud(root, Presenter.Hud);
        var scope = RequireScope(root);
        AttachText(root, scope.Find<Label>("ScoreText"), Presenter.Hud, nameof(GameHudViewModel.ScoreText), source => source.ScoreText);
        AttachText(root, scope.Find<Label>("RemainingText"), Presenter.Hud, nameof(GameHudViewModel.RemainingText), source => source.RemainingText);
        AttachText(root, scope.Find<Label>("StatusText"), Presenter.Hud, nameof(GameHudViewModel.StatusText), source => source.StatusText);
        WireHud(root);
    }

    private void WireHotReloadedSettings(Control root)
    {
        GameViewPresentation.AttachSettings(root);
        var scope = RequireScope(root);
        CompiledBinding.AttachTwoWay(root, scope.Find<LineEdit>("PlayerName"), (GameSettingsViewModel source) => source.PlayerName, (source, value) => source.PlayerName = value, nameof(GameSettingsViewModel.PlayerName), BindingTargetAdapters.LineEditText);
        CompiledBinding.AttachTwoWay(root, scope.Find<LineEdit>("Difficulty"), (GameSettingsViewModel source) => source.Difficulty, (source, value) => source.Difficulty = value, nameof(GameSettingsViewModel.Difficulty), BindingTargetAdapters.LineEditText);
        CompiledBinding.AttachTwoWay(root, scope.Find<CheckBox>("SoundEnabled"), (GameSettingsViewModel source) => source.SoundEnabled, (source, value) => source.SoundEnabled = value, nameof(GameSettingsViewModel.SoundEnabled), BindingTargetAdapters.ButtonPressed);
        CompiledBinding.AttachTwoWay(root, scope.Find<HSlider>("Volume"), (GameSettingsViewModel source) => source.Volume, (source, value) => source.Volume = value, nameof(GameSettingsViewModel.Volume), BindingTargetAdapters.RangeValue);
        WireSettings(root);
    }

    private void WireHotReloadedResult(Control root)
    {
        GameViewPresentation.AttachResult(root);
        AttachText(root, RequireScope(root).Find<Label>("ResultText"), Presenter.Result, nameof(GameResultViewModel.ResultText), source => source.ResultText);
        WireResult(root);
    }

    private void ReplaceView<TViewModel>(ref Control current, Control expected, Control replacement, TViewModel viewModel, Action<Control> configure)
        where TViewModel : class
    {
        if (!ReferenceEquals(current, expected)) return;
        var index = Children.IndexOf(expected);
        RemoveChild(expected);
        replacement.DataContext = viewModel;
        configure(replacement);
        AddChild(replacement);
        if (index < Children.Count - 1) MoveChild(replacement, index);
        current = replacement;
        Arrange(Size);
        Present();
    }

    private static void AttachText<TViewModel>(Control root, Label target, TViewModel source, string propertyName, Func<TViewModel, string> read)
        where TViewModel : class
    {
        root.DataContext = source;
        CompiledBinding.AttachOneWay(root, target, read, propertyName, value => ((Label)value).Text, (value, text) => ((Label)value).Text = text);
    }

    private void TogglePause()
    {
        Presenter.Update(TimeSpan.Zero, new GameInput(default, false, true, false));
        Present();
    }

    private void Restart()
    {
        Presenter.Update(TimeSpan.Zero, new GameInput(default, false, false, true));
        Present();
    }

    private static void ArrangeOverlay(Control overlay, Microsoft.Xna.Framework.Vector2 viewport)
    {
        var size = overlay.GetMinimumSize();
        overlay.Size = size;
        overlay.Position = Microsoft.Xna.Framework.Vector2.Max(Microsoft.Xna.Framework.Vector2.Zero, (viewport - size) / 2);
    }

    private static NameScope RequireScope(Control root) => NameScope.GetNameScope(root) ?? throw new InvalidOperationException("The game view has no namescope.");

    public void Dispose()
    {
        foreach (var registration in _hotReloadRegistrations) registration.Dispose();
        _hotReloadRegistrations.Clear();
    }
}