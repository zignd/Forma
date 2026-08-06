// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Catalog;
using Forma;

#if FORMA_CATALOG_FNA
Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
#endif

#if THORVG
_ = SvgThorvgBackendDefaults.Verify();
#else
_ = SvgSkiaBackendDefaults.Verify();
#endif
using var game = new CatalogGame(CatalogMetricsOptions.Parse(args));
game.Run();
