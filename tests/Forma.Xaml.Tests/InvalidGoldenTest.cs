// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml.Compiler;

namespace Forma.Xaml.Tests;

public sealed class InvalidGoldenTest
{
    [TestCase("unknown-type.xaml")]
    [TestCase("unknown-member.xaml")]
    [TestCase("constructor-content.xaml")]
    [TestCase("binding-path-mode.xaml")]
    [TestCase("selector.xaml")]
    [TestCase("animation-mismatch.xaml")]
    [TestCase("datagrid-contract.xaml")]
    [TestCase("duplicate-names-keys.xaml")]
    [TestCase("unsupported-directive.xaml")]
    public void InvalidGoldenIsRejectedWithSourceLocation(string fileName)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Goldens", fileName);
        var source = File.ReadAllText(path);
        var parse = new FormaXamlParser().Parse(source, fileName, new FormaXamlParseOptions { RequireCompiledBindings = true });
        if (!parse.Success)
        {
            Assert.That(parse.Diagnostics, Has.All.Matches<FormaDiagnostic>(diagnostic => diagnostic.Location.Line > 0 && diagnostic.Location.Column > 0));
            return;
        }

        Assert.That(
            () => FormaXamlCompiler.CreateSre().CompileSre(source, fileName),
            Throws.Exception,
            $"{fileName} must fail either semantic validation or typed emission.");
    }
}