// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

if (args.Length != 1 || args[0] is not ("missing" or "wrong-architecture" or "rejected")) return 2;

var failure = args[0];
try
{
    using var face = UIFontFace.FromProjectFile(AppContext.BaseDirectory, "Fonts/Inter_Regular.ttf");
    return 1;
}
catch (FontLoadException exception)
{
    if (exception.ErrorCode != FontLoadErrorCode.NativeFailure ||
        exception.Message != "A native font dependency is unavailable or incompatible." ||
        exception.InnerException == null)
        return 1;

    var expectedType = failure switch
    {
        "missing" => typeof(FileLoadException),
        "wrong-architecture" => typeof(FileLoadException),
        _ => typeof(EntryPointNotFoundException),
    };
    if (!expectedType.IsInstanceOfType(exception.InnerException)) return 1;
    Console.WriteLine($"{failure}: {exception.ErrorCode}");
    return 0;
}