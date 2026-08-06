// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

#if SVG_THORVG
var expectedFailure = Environment.GetEnvironmentVariable("FORMA_EXPECT_THORVG_FAILURE");
if (!string.IsNullOrEmpty(expectedFailure))
{
    var failureHealth = SvgThorvgBackendDefaults.Health;
    if (failureHealth.IsNativeAvailable || !failureHealth.Diagnostic.Contains(expectedFailure, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected ThorVG failure containing '{expectedFailure}', got: {failureHealth.Diagnostic}");
    Console.WriteLine($"Expected ThorVG failure: {failureHealth.Diagnostic}");
    return;
}
#endif

#if SVG_SKIA
var health = SvgSkiaBackendDefaults.Verify();
const string expectedBackend = "skia";
const string expectedAssembly = "Forma.Svg.Skia";
#elif SVG_THORVG
var health = SvgThorvgBackendDefaults.Verify();
const string expectedBackend = "thorvg";
const string expectedAssembly = "Forma.Svg.ThorVG";
#else
var health = SvgRuntime.Health;
if (health.IsRegistered || !health.Diagnostic.Contains("SVG backend", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"No-backend diagnostic was not actionable: {health.Diagnostic}");
Console.WriteLine("No SVG backend is installed.");
return;
#endif

#if SVG_SKIA || SVG_THORVG
if (!health.IsAvailable || health.BackendId != expectedBackend || health.ProfileVersion != "1")
    throw new InvalidOperationException($"Unexpected {expectedBackend} health: {health.Diagnostic}");
if (health.GetType().Assembly.GetName().Name != "Forma")
    throw new InvalidOperationException("Backend health escaped the core assembly.");

#if SVG_SKIA
if (typeof(SvgSkiaBackendDefaults).Assembly.GetName().Name != expectedAssembly)
    throw new InvalidOperationException("Skia backend was loaded from an unexpected assembly.");
#elif SVG_THORVG
if (typeof(SvgThorvgBackendDefaults).Assembly.GetName().Name != expectedAssembly)
    throw new InvalidOperationException("ThorVG backend was loaded from an unexpected assembly.");
#endif

Console.WriteLine($"{health.BackendId} package verification passed: {health.Version}, {health.LinkMode}.");
#endif