// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml.Game;

#if FORMA_XAML_GAME_FNA
Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
#endif

using var game = new XamlGame();
game.Run();