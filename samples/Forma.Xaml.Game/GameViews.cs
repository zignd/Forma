using Forma.Xaml;

namespace Forma.Xaml.Game;

public sealed class GameHudView : BoxContainer
{
    public GameHudView() : this(new GameHudViewModel()) { }
    public GameHudView(GameHudViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);
        GameViewPresentation.AttachHud(this, viewModel);
    }
}

public sealed class GameSettingsView : BoxContainer
{
    public GameSettingsView() : this(new GameSettingsViewModel()) { }
    public GameSettingsView(GameSettingsViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);
        GameViewPresentation.AttachSettings(this);
    }
}

public sealed class GameResultView : BoxContainer
{
    public GameResultView() : this(new GameResultViewModel()) { }
    public GameResultView(GameResultViewModel viewModel) : base(Orientation.Vertical)
    {
        DataContext = viewModel;
        FormaXamlLoader.Load(this);
        GameViewPresentation.AttachResult(this);
    }
}