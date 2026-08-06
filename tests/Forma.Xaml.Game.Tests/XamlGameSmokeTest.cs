// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;
using Forma.Xaml.Game;
using Forma.Xaml.HotReload;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Reflection;

namespace Forma.Xaml.Game.Tests;

public sealed class XamlGameSmokeTest
{
    [Test]
    public void SessionAdvancesThroughCollectPauseLowTimeResultAndRestart()
    {
        var presenter = new GamePresenter(new GameSession(), new GameHudViewModel(), new GameSettingsViewModel(), new GameResultViewModel());
        Assert.Multiple(() =>
        {
            Assert.That(presenter.Session.Phase, Is.EqualTo(GamePhase.Ready));
            Assert.That(presenter.Hud.ScoreText, Is.EqualTo("Score 0"));
            Assert.That(presenter.Hud.RemainingText, Is.EqualTo("15.0 s"));
            Assert.That(presenter.Hud.StatusText, Is.EqualTo("Move to begin"));
        });

        presenter.Update(TimeSpan.Zero, new GameInput(default, true, false, false));
        Assert.Multiple(() =>
        {
            Assert.That(presenter.Session.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(presenter.Hud.StatusText, Is.EqualTo("Collect the signal"));
        });

        for (var step = 0; step < 40 && presenter.Session.Score == 0; step++)
        {
            var movement = presenter.Session.TargetPosition - presenter.Session.PlayerPosition;
            if (movement != Vector2.Zero) movement.Normalize();
            presenter.Update(TimeSpan.FromMilliseconds(100), new GameInput(movement, false, false, false));
        }
        var score = presenter.Session.Score;
        presenter.Update(TimeSpan.Zero, new GameInput(default, false, true, false));
        var pausedRemaining = presenter.Session.Remaining;
        Assert.Multiple(() =>
        {
            Assert.That(presenter.Session.Phase, Is.EqualTo(GamePhase.Paused));
            Assert.That(presenter.Hud.StatusText, Is.EqualTo("Paused"));
        });
        presenter.Update(TimeSpan.FromSeconds(2), default);
        var pausedWasStable = presenter.Session.Remaining == pausedRemaining;
        presenter.Update(TimeSpan.Zero, new GameInput(default, false, true, false));
        presenter.Update(presenter.Session.Remaining - TimeSpan.FromSeconds(4), default);

        Assert.Multiple(() =>
        {
            Assert.That(score, Is.GreaterThan(0));
            Assert.That(pausedWasStable, Is.True);
            Assert.That(presenter.Session.IsLowTime, Is.True);
            Assert.That(presenter.Hud.StatusText, Is.EqualTo("Low time"));
        });

        presenter.Update(TimeSpan.FromSeconds(5), default);
        Assert.Multiple(() =>
        {
            Assert.That(presenter.Session.Phase, Is.EqualTo(GamePhase.Result));
            Assert.That(presenter.Hud.StatusText, Is.EqualTo("Round complete"));
            Assert.That(presenter.Result.ResultText, Is.EqualTo($"Player One scored {score}"));
        });
        presenter.Update(TimeSpan.Zero, new GameInput(default, false, false, true));
        Assert.Multiple(() =>
        {
            Assert.That(presenter.Session.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(presenter.Session.Score, Is.Zero);
            Assert.That(presenter.Session.Remaining, Is.EqualTo(GameSession.RoundDuration));
        });
    }

    [Test]
    public void CompiledViewsApplyOneWayTwoWayResourcesSelectorsAndAnimations()
    {
        var hudModel = new GameHudViewModel();
        var settingsModel = new GameSettingsViewModel();
        var hud = new GameHudView(hudModel);
        var settings = new GameSettingsView(settingsModel);
        var hudScope = NameScope.GetNameScope(hud);
        var settingsScope = NameScope.GetNameScope(settings);
        using var context = new UIContext();
        context.Add(hud);
        context.Add(settings);

        hudModel.ScoreText = "Score 7";
        settingsScope.Find<LineEdit>("PlayerName").Text = "Ada";
        settingsScope.Find<LineEdit>("Difficulty").Text = "Hard";
        settingsScope.Find<CheckBox>("SoundEnabled").ButtonPressed = false;
        settingsScope.Find<HSlider>("Volume").Value = 42;
        var soundToggle = settingsScope.Find<CheckBox>("SoundEnabled");
        soundToggle.ButtonPressed = true;
        var checkedMargins = soundToggle.Margins;
        soundToggle.ButtonPressed = false;
        var restoredMargins = soundToggle.Margins;
        GameViewPresentation.SetLowTimeResource(hud, true);
        var initialTimerSize = hudScope.Find<Label>("RemainingText").CustomMinimumSize;
        hudModel.IsLowTime = true;
        context.Update(new GameTime(TimeSpan.FromMilliseconds(70), TimeSpan.FromMilliseconds(70)), new MouseState(), new KeyboardState());
        var intermediateTimerSize = hudScope.Find<Label>("RemainingText").CustomMinimumSize;
        context.Update(new GameTime(TimeSpan.FromMilliseconds(140), TimeSpan.FromMilliseconds(70)), new MouseState(), new KeyboardState());
        var progressedTimerSize = hudScope.Find<Label>("RemainingText").CustomMinimumSize;

        Assert.Multiple(() =>
        {
            Assert.That(hudScope.Find<Label>("ScoreText").Text, Is.EqualTo("Score 7"));
            Assert.That(settingsModel.PlayerName, Is.EqualTo("Ada"));
            Assert.That(settingsModel.Difficulty, Is.EqualTo("Hard"));
            Assert.That(settingsModel.SoundEnabled, Is.False);
            Assert.That(settingsModel.Volume, Is.EqualTo(42));
            Assert.That(hudScope.Find<Label>("RemainingText").FontColor, Is.EqualTo(new Color(246, 185, 73)));
            Assert.That(intermediateTimerSize, Is.Not.EqualTo(initialTimerSize));
            Assert.That(progressedTimerSize, Is.Not.EqualTo(intermediateTimerSize));
            Assert.That(checkedMargins, Is.EqualTo(new Thickness(4, 0, 4, 0)));
            Assert.That(restoredMargins, Is.EqualTo(new Thickness(0)));
        });
    }

    [Test]
    public void GameScreenPreservesHudSettingsAndResultWorkflowsWithTemplatedControls()
    {
        using var screen = new GameScreen();
        using var context = new UIContext { ViewportSize = new Vector2(960, 540) };
        screen.Arrange(context.ViewportSize);
        context.Add(screen);
        context.Layout();
        var hudScope = NameScope.GetNameScope(screen.HudView);
        var settingsScope = NameScope.GetNameScope(screen.SettingsView);
        var resultScope = NameScope.GetNameScope(screen.ResultView);
        var pause = hudScope.Find<Button>("PauseButton");
        var resume = settingsScope.Find<Button>("ResumeButton");
        var playAgain = resultScope.Find<Button>("PlayAgainButton");
        var sound = settingsScope.Find<CheckBox>("SoundEnabled");
        var volume = settingsScope.Find<HSlider>("Volume");

        screen.Update(TimeSpan.Zero, new GameInput(Vector2.UnitX, true, false, false));
        Click(context, pause);
        context.Layout();
        sound.ButtonPressed = false;

        Assert.Multiple(() =>
        {
            Assert.That(screen.Presenter.Session.Phase, Is.EqualTo(GamePhase.Paused));
            Assert.That(screen.SettingsView.Visible, Is.True);
            Assert.That(screen.ResultView.Visible, Is.False);
            Assert.That(volume.Enabled, Is.False);
            Assert.That(pause.TemplateRoot, Is.TypeOf<Border>());
            Assert.That(pause.GetTemplateChild(ContentControl.ContentPresenterPartName), Is.TypeOf<ContentPresenter>());
            Assert.That(resume.TemplateRoot, Is.TypeOf<Border>());
            Assert.That(resume.GetTemplateChild(ContentControl.ContentPresenterPartName), Is.TypeOf<ContentPresenter>());
            Assert.That(playAgain.TemplateRoot, Is.TypeOf<Border>());
            Assert.That(playAgain.GetTemplateChild(ContentControl.ContentPresenterPartName), Is.TypeOf<ContentPresenter>());
            Assert.That(sound.TemplateRoot, Is.Not.Null);
            Assert.That(settingsScope.Find<LineEdit>("PlayerName").TemplateRoot, Is.Not.Null);
        });

        Click(context, resume);
        screen.Update(GameSession.RoundDuration + TimeSpan.FromSeconds(1), default);
        context.Layout();

        Assert.Multiple(() =>
        {
            Assert.That(screen.Presenter.Session.Phase, Is.EqualTo(GamePhase.Result));
            Assert.That(screen.SettingsView.Visible, Is.False);
            Assert.That(screen.ResultView.Visible, Is.True);
            Assert.That(resultScope.Find<Label>("ResultText").Text, Does.Contain("scored"));
        });

        Click(context, playAgain);
        Assert.Multiple(() =>
        {
            Assert.That(screen.Presenter.Session.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(screen.Presenter.Session.Score, Is.Zero);
            Assert.That(screen.ResultView.Visible, Is.False);
        });
    }

    private static void Click(UIContext context, Control control)
    {
        var point = control.VisualBounds.Center;
        var pressed = new MouseState(point.X, point.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        var released = new MouseState(point.X, point.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        context.Update(new GameTime(), pressed, new KeyboardState());
        context.Update(new GameTime(), released, new KeyboardState());
    }

    [Test]
    public async Task HotReloadKeepsCurrentGameViewModelsSessionAndSettings()
    {
        var sourceRoot = typeof(GameHudView).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(metadata => metadata.Key == "FormaXamlGameSourceRoot")?.Value;
        if (sourceRoot == null) Assert.Ignore("Game XAML source metadata is intentionally Debug-only.");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"forma-xaml-game-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        var viewSources = new[] { "GameHudView.xaml", "GameSettingsView.xaml", "GameResultView.xaml" };
        foreach (var viewSource in viewSources)
            File.Copy(Path.Combine(sourceRoot, viewSource), Path.Combine(temporaryRoot, viewSource));
        try
        {
            using var context = new UIContext();
            using var screen = new GameScreen();
            context.Add(screen);
            var session = screen.Presenter.Session;
            var movement = session.TargetPosition - session.PlayerPosition;
            var travelTime = TimeSpan.FromSeconds(movement.Length() / 260f);
            screen.Update(travelTime, new GameInput(movement, true, false, false));
            Assert.That(session.Score, Is.EqualTo(1));
            screen.Presenter.Settings.PlayerName = "Retained Pilot";
            screen.Presenter.Settings.Difficulty = "Expert";
            screen.Presenter.Settings.SoundEnabled = false;
            screen.Presenter.Settings.Volume = 23;
            var remaining = session.Remaining;
            var hudModel = screen.Presenter.Hud;
            var settingsModel = screen.Presenter.Settings;
            var resultModel = screen.Presenter.Result;
            var oldHud = screen.HudView;
            var oldSettings = screen.SettingsView;
            var oldResult = screen.ResultView;
            using var service = new FormaXamlHotReloadService(context, temporaryRoot, watchFiles: false);
            var enableHotReload = typeof(GameScreen).GetMethod("EnableHotReload");
            if (enableHotReload == null) Assert.Ignore("Signal Run hot-reload registration is intentionally Debug-only.");
            enableHotReload.Invoke(screen, new object[] { service });

            foreach (var viewSource in viewSources) await service.RequestReloadAsync(viewSource);
            context.Update(new GameTime(), new MouseState(), new KeyboardState());

            Assert.Multiple(() =>
            {
                Assert.That(screen.HudView, Is.Not.SameAs(oldHud));
                Assert.That(screen.SettingsView, Is.Not.SameAs(oldSettings));
                Assert.That(screen.ResultView, Is.Not.SameAs(oldResult));
                Assert.That(screen.HudView.DataContext, Is.SameAs(hudModel));
                Assert.That(screen.SettingsView.DataContext, Is.SameAs(settingsModel));
                Assert.That(screen.ResultView.DataContext, Is.SameAs(resultModel));
                Assert.That(screen.Presenter.Session, Is.SameAs(session));
                Assert.That(session.Score, Is.EqualTo(1));
                Assert.That(session.Remaining, Is.EqualTo(remaining));
                Assert.That(settingsModel.PlayerName, Is.EqualTo("Retained Pilot"));
                Assert.That(settingsModel.Difficulty, Is.EqualTo("Expert"));
                Assert.That(settingsModel.SoundEnabled, Is.False);
                Assert.That(settingsModel.Volume, Is.EqualTo(23));
            });
        }
        finally
        {
            Directory.Delete(temporaryRoot, true);
        }
    }
}