using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Forma.Xaml.Game;

public abstract class ObservableViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class GameHudViewModel : ObservableViewModel
{
    private string _scoreText = "Score 0";
    private string _remainingText = "15.0 s";
    private string _statusText = "Move to begin";
    private bool _isLowTime;

    public string ScoreText { get => _scoreText; set => Set(ref _scoreText, value); }
    public string RemainingText { get => _remainingText; set => Set(ref _remainingText, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public bool IsLowTime { get => _isLowTime; set => Set(ref _isLowTime, value); }
}

public sealed class GameSettingsViewModel : ObservableViewModel
{
    private string _playerName = "Player One";
    private string _difficulty = "Normal";
    private bool _soundEnabled = true;
    private float _volume = 70;

    public string PlayerName { get => _playerName; set => Set(ref _playerName, value); }
    public string Difficulty { get => _difficulty; set => Set(ref _difficulty, value); }
    public bool SoundEnabled { get => _soundEnabled; set => Set(ref _soundEnabled, value); }
    public float Volume { get => _volume; set => Set(ref _volume, value); }
}

public sealed class GameResultViewModel : ObservableViewModel
{
    private string _resultText = "Score 0";
    public string ResultText { get => _resultText; set => Set(ref _resultText, value); }
}

public sealed class GamePresenter
{
    public GamePresenter(GameSession session, GameHudViewModel hud, GameSettingsViewModel settings, GameResultViewModel result)
    {
        Session = session;
        Hud = hud;
        Settings = settings;
        Result = result;
        Project();
    }

    public GameSession Session { get; }
    public GameHudViewModel Hud { get; }
    public GameSettingsViewModel Settings { get; }
    public GameResultViewModel Result { get; }

    public void Update(TimeSpan elapsed, GameInput input)
    {
        Session.Update(elapsed, input);
        Project();
    }

    private void Project()
    {
        Hud.ScoreText = $"Score {Session.Score}";
        Hud.RemainingText = string.Create(CultureInfo.InvariantCulture, $"{Session.Remaining.TotalSeconds:0.0} s");
        Hud.StatusText = Session.Phase switch
        {
            GamePhase.Ready => "Move to begin",
            GamePhase.Playing when Session.IsLowTime => "Low time",
            GamePhase.Playing => "Collect the signal",
            GamePhase.Paused => "Paused",
            _ => "Round complete",
        };
        Hud.IsLowTime = Session.IsLowTime;
        Result.ResultText = $"{Settings.PlayerName} scored {Session.Score}";
    }
}