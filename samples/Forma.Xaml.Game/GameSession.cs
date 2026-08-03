// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;

namespace Forma.Xaml.Game;

public enum GamePhase
{
    Ready,
    Playing,
    Paused,
    Result,
}

public readonly record struct GameInput(Vector2 Movement, bool Start, bool TogglePause, bool Restart);

public sealed class GameSession
{
    public static readonly TimeSpan RoundDuration = TimeSpan.FromSeconds(15);
    private static readonly Vector2[] Targets =
    {
        new(180, 150), new(520, 210), new(350, 390), new(690, 460), new(120, 500),
    };

    private int _targetIndex;

    public GameSession() => Reset();

    public GamePhase Phase { get; private set; }
    public int Score { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public Vector2 PlayerPosition { get; private set; }
    public Vector2 TargetPosition => Targets[_targetIndex % Targets.Length];
    public bool IsLowTime => Phase == GamePhase.Playing && Remaining <= TimeSpan.FromSeconds(5);

    public void Update(TimeSpan elapsed, GameInput input)
    {
        if (input.Restart)
        {
            Reset();
            Phase = GamePhase.Playing;
            return;
        }
        if (Phase == GamePhase.Ready && (input.Start || input.Movement != Vector2.Zero)) Phase = GamePhase.Playing;
        if (input.TogglePause && Phase is GamePhase.Playing or GamePhase.Paused)
        {
            Phase = Phase == GamePhase.Playing ? GamePhase.Paused : GamePhase.Playing;
            return;
        }
        if (Phase != GamePhase.Playing) return;

        var movement = input.Movement;
        if (movement.LengthSquared() > 1) movement.Normalize();
        PlayerPosition += movement * 260f * (float)Math.Max(0, elapsed.TotalSeconds);
        PlayerPosition = Vector2.Clamp(PlayerPosition, new Vector2(24), new Vector2(776, 536));
        if (Vector2.DistanceSquared(PlayerPosition, TargetPosition) <= 34 * 34)
        {
            Score++;
            _targetIndex++;
        }

        Remaining -= elapsed;
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            Phase = GamePhase.Result;
        }
    }

    public void Reset()
    {
        Phase = GamePhase.Ready;
        Score = 0;
        Remaining = RoundDuration;
        PlayerPosition = new Vector2(400, 300);
        _targetIndex = 0;
    }
}