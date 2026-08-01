// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Catalog;

using var game = new CatalogGame(CatalogMetricsOptions.Parse(args));
game.Run();
