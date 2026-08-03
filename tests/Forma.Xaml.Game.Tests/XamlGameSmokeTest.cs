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
    public async Task HotReloadKeepsCurrentGameViewModel()
    {
        var sourceRoot = typeof(GameHudView).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(metadata => metadata.Key == "FormaXamlGameSourceRoot").Value;
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"forma-xaml-game-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        File.Copy(Path.Combine(sourceRoot, "GameHudView.xaml"), Path.Combine(temporaryRoot, "GameHudView.xaml"));
        try
        {
            using var context = new UIContext();
            var viewModel = new GameHudViewModel { ScoreText = "Score 9", RemainingText = "3.2 s" };
            Control current = new GameHudView(viewModel);
            context.Add(current);
            using var service = new FormaXamlHotReloadService(context, temporaryRoot, watchFiles: false);
            using var registration = service.Register("GameHudView.xaml", () => current, (oldView, replacement) =>
            {
                context.Remove(oldView);
                context.Add(replacement);
                current = replacement;
            });

            await service.RequestReloadAsync("GameHudView.xaml");
            context.Update(new GameTime(), new MouseState(), new KeyboardState());

            Assert.Multiple(() =>
            {
                Assert.That(current, Is.Not.TypeOf<GameHudView>());
                Assert.That(current.DataContext, Is.SameAs(viewModel));
                Assert.That(((GameHudViewModel)current.DataContext).ScoreText, Is.EqualTo("Score 9"));
                Assert.That(((GameHudViewModel)current.DataContext).RemainingText, Is.EqualTo("3.2 s"));
            });
        }
        finally
        {
            Directory.Delete(temporaryRoot, true);
        }
    }
}