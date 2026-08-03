// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Xaml.Compiler;

public sealed record FormaXamlMember(
    string Namespace,
    string Name,
    string Value,
    FormaSourceLocation Location,
    bool IsDirective);

public sealed class FormaXamlObject
{
    public FormaXamlObject(string xmlNamespace, string typeName, FormaSourceLocation location)
    {
        XmlNamespace = xmlNamespace;
        TypeName = typeName;
        Location = location;
    }

    public string XmlNamespace { get; }
    public string TypeName { get; }
    public FormaSourceLocation Location { get; }
    public List<FormaXamlMember> Members { get; } = [];
    public List<FormaXamlObject> Children { get; } = [];

    public string? FindDirective(string name) =>
        Members.FirstOrDefault(member => member.IsDirective && member.Name == name)?.Value;

    public string? FindMember(string name) =>
        Members.FirstOrDefault(member => !member.IsDirective && member.Name == name)?.Value;
}

public sealed class FormaXamlDocument
{
    public FormaXamlDocument(string sourcePath, FormaXamlObject root, IReadOnlyDictionary<string, string> namespaces)
    {
        SourcePath = sourcePath;
        Root = root;
        Namespaces = namespaces;
    }

    public string SourcePath { get; }
    public FormaXamlObject Root { get; }
    public IReadOnlyDictionary<string, string> Namespaces { get; }
    public string? RootClass => Root.FindDirective("Class");
    public string? DataType => Root.FindDirective("DataType");
    public IEnumerable<FormaXamlObject> DescendantsAndSelf()
    {
        var stack = new Stack<FormaXamlObject>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            for (var index = current.Children.Count - 1; index >= 0; index--) stack.Push(current.Children[index]);
        }
    }
}

public sealed class FormaXamlParseOptions
{
    public bool RequireCompiledBindings { get; init; }
}

public sealed class FormaXamlParseResult
{
    public FormaXamlParseResult(FormaXamlDocument? document, IReadOnlyList<FormaDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public FormaXamlDocument? Document { get; }
    public IReadOnlyList<FormaDiagnostic> Diagnostics { get; }
    public bool Success => Document != null && !Diagnostics.Any(diagnostic => diagnostic.Severity == FormaDiagnosticSeverity.Error);
}