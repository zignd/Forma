// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Xaml.Compiler;

public enum FormaDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public static class FormaDiagnosticCodes
{
    public const string XmlSyntax = "FXAML1001";
    public const string RootNamespace = "FXAML1002";
    public const string UnsupportedDirective = "FXAML1003";
    public const string InvalidDirective = "FXAML1004";
    public const string DuplicateName = "FXAML2001";
    public const string InvalidName = "FXAML2002";
    public const string Template = "FXAML2501";
    public const string ContentModel = "FXAML2502";
    public const string AttachedProperty = "FXAML2503";
    public const string DataGrid = "FXAML2601";
    public const string Binding = "FXAML3001";
    public const string CompiledBinding = "FXAML3002";
    public const string RelativeSource = "FXAML3003";
    public const string Resource = "FXAML3501";
    public const string SvgAssetMissing = "FXAML3601";
    public const string SvgAssetInvalid = "FXAML3602";
    public const string SvgAssetDuplicate = "FXAML3603";
    public const string Selector = "FXAML4001";
    public const string Trigger = "FXAML5001";
    public const string Storyboard = "FXAML6001";
    public const string Emission = "FXAML7001";
    public const string DuplicateRootClass = "FXAML7002";
}

public sealed record FormaSourceLocation(string FilePath, int Line, int Column, int EndLine = 0, int EndColumn = 0);

public sealed record FormaDiagnostic(
    string Code,
    FormaDiagnosticSeverity Severity,
    string Message,
    FormaSourceLocation Location)
{
    public override string ToString() => $"{Location.FilePath}({Location.Line},{Location.Column}): {Severity.ToString().ToLowerInvariant()} {Code}: {Message}";
}

public sealed class FormaXamlCompilationException : Exception
{
    public FormaXamlCompilationException(IReadOnlyList<FormaDiagnostic> diagnostics)
        : base(diagnostics.Count == 0 ? "Forma XAML compilation failed." : diagnostics[0].ToString())
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<FormaDiagnostic> Diagnostics { get; }
}