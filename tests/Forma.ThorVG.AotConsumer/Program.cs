// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

var health = SvgThorvgBackendDefaults.Verify();
if (!health.IsAvailable || health.BackendId != "thorvg" || health.ProfileVersion != "1")
    throw new InvalidOperationException(health.Diagnostic);

Console.WriteLine($"ThorVG AOT verification passed: {health.Version}, ABI/profile {health.ProfileVersion}.");