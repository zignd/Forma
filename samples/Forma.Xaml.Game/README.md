# Signal Run XAML sample

Signal Run is one shared collect-and-score game with thin MonoGame and FNA executables. Move the teal marker into gold targets before the 15-second timer expires.

```bash
dotnet run --project samples/Forma.Xaml.Game.MonoGame/Forma.Xaml.Game.MonoGame.csproj -p:FormaRuntime=MonoGame
dotnet run --project samples/Forma.Xaml.Game.FNA/Forma.Xaml.Game.FNA.csproj -p:FormaRuntime=FNA
```

Use WASD or the arrow keys to move, `P` to pause, `R` to restart, and Escape to quit. The pause overlay edits the retained player name, difficulty, sound setting, and volume.

`GameSession` owns deterministic simulation and collision. `GamePresenter` projects it into focused `INotifyPropertyChanged` view models. `GameHudView.xaml`, `GameSettingsView.xaml`, and `GameResultView.xaml` own the UI trees, typed bindings, and keyed action `ControlTemplate` resources. Their real pause, resume, restart, and result workflows project content through named `ContentPresenter` parts. `GameViewPresentation` attaches resource dictionaries, static/dynamic resource use, selector styles, and state/event storyboards through Forma's runtime APIs. The game remains a core-only example and does not require DynamicText.

Debug builds enable hot reload by default. Run either host, edit one of the three XAML files, and save it. The host compiles the file off-thread and replaces only that view during `UIContext.Update`, retaining its current view model, score, timer, and settings. Invalid XAML reports diagnostics and leaves the live view intact. Set `-p:FormaXamlHotReload=false` to disable the development service; Release builds exclude it automatically.