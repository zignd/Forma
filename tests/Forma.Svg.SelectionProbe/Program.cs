// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

if (args.Length != 1) return Fail("Expected one selection probe mode.");

switch (args[0])
{
    case "none":
        if (SvgRuntime.Health.IsRegistered) return Fail("Empty process unexpectedly has a backend.");
        return ExpectFailure(() => _ = SvgBackendRegistry.Backend, "No SVG backend is registered");
    case "repeated":
        var repeated = new ProbeBackend("skia", true);
        SvgBackendRegistry.Register(repeated);
        SvgBackendRegistry.Register(repeated);
        return SvgRuntime.Health.BackendId == "skia" ? 0 : Fail("Repeated installation changed the backend.");
    case "conflict":
        SvgBackendRegistry.Register(new ProbeBackend("skia", true));
        return ExpectFailure(
            () => SvgBackendRegistry.Register(new ProbeBackend("thorvg", true)),
            "'skia' is already registered; cannot install 'thorvg'");
    case "late":
        _ = ExpectFailure(() => _ = SvgBackendRegistry.Backend, "No SVG backend is registered");
        return ExpectFailure(
            () => SvgBackendRegistry.Register(new ProbeBackend("thorvg", true)),
            "'thorvg' cannot be installed after SVG parsing has started");
    case "unavailable":
        SvgBackendRegistry.Register(new ProbeBackend("thorvg", false));
        return ExpectFailure(() => _ = SvgBackendRegistry.Backend, "ThorVG native library is unavailable");
    default:
        return Fail($"Unknown selection probe mode '{args[0]}'.");
}

static int ExpectFailure(Action action, string expected)
{
    try
    {
        action();
    }
    catch (InvalidOperationException exception) when (exception.Message.Contains(expected, StringComparison.Ordinal))
    {
        return 0;
    }
    return Fail($"Expected failure containing: {expected}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

internal sealed class ProbeBackend : ISvgRasterizerBackend
{
    internal ProbeBackend(string id, bool available)
    {
        Health = new SvgBackendHealth(
            true,
            available,
            id,
            id,
            "test",
            "1",
            SvgBackendFeatures.None,
            available ? SvgNativeAvailability.Managed : SvgNativeAvailability.Unavailable,
            SvgBackendLinkMode.Managed,
            available ? "available" : "ThorVG native library is unavailable");
    }

    public SvgBackendHealth Health { get; }
    public ISvgBackendDocument Parse(byte[] source) => throw new NotSupportedException();
    public SvgRasterData Rasterize(ISvgBackendDocument document, int width, int height) => throw new NotSupportedException();
}