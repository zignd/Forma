// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Forma.Xaml;

namespace Forma.Xaml.Compiler;

public sealed class FormaXamlParser
{
    private static readonly HashSet<string> SupportedDirectives = new(StringComparer.Ordinal)
    {
        "Class", "Name", "Key", "DataType",
    };
    private static readonly HashSet<string> BindingOptions = new(StringComparer.Ordinal)
    {
        "Mode", "FallbackValue", "TargetNullValue", "StringFormat", "Converter", "ConverterParameter", "UpdateSourceTrigger",
    };
    private static readonly Regex NamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public FormaXamlParseResult Parse(string source, string sourcePath, FormaXamlParseOptions? options = null)
    {
        options ??= new FormaXamlParseOptions();
        var diagnostics = new List<FormaDiagnostic>();
        XDocument xml;
        try
        {
            xml = XDocument.Parse(source, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.XmlSyntax, exception.Message, sourcePath, exception.LineNumber, exception.LinePosition));
            return new FormaXamlParseResult(null, diagnostics);
        }

        if (xml.Root == null)
        {
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.XmlSyntax, "A XAML document requires one root element.", sourcePath, 1, 1));
            return new FormaXamlParseResult(null, diagnostics);
        }

        var namespaces = xml.Root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration)
            .ToDictionary(attribute => attribute.Name.LocalName == "xmlns" ? string.Empty : attribute.Name.LocalName, attribute => attribute.Value, StringComparer.Ordinal);
        var root = BuildObject(xml.Root, sourcePath, diagnostics, true);
        var document = new FormaXamlDocument(sourcePath, root, namespaces);
        Validate(document, options, diagnostics);
        return new FormaXamlParseResult(document, diagnostics);
    }

    private static FormaXamlObject BuildObject(XElement element, string sourcePath, List<FormaDiagnostic> diagnostics, bool isRoot)
    {
        var location = Location(element, sourcePath);
        var result = new FormaXamlObject(element.Name.NamespaceName, element.Name.LocalName, location);
        foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
        {
            var directive = attribute.Name.NamespaceName == XamlNamespaces.Xaml2006;
            result.Members.Add(new FormaXamlMember(attribute.Name.NamespaceName, attribute.Name.LocalName, attribute.Value, Location(attribute, sourcePath), directive));
            if (directive && !SupportedDirectives.Contains(attribute.Name.LocalName))
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.UnsupportedDirective, $"Directive 'x:{attribute.Name.LocalName}' is not supported in Forma XAML v1.", Location(attribute, sourcePath)));
            if (directive && attribute.Name.LocalName == "Class" && !isRoot)
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.InvalidDirective, "x:Class is valid only on the document root.", Location(attribute, sourcePath)));
        }
        foreach (var child in element.Elements()) result.Children.Add(BuildObject(child, sourcePath, diagnostics, false));
        return result;
    }

    private static void Validate(FormaXamlDocument document, FormaXamlParseOptions options, List<FormaDiagnostic> diagnostics)
    {
        if (document.Root.XmlNamespace != XamlNamespaces.Forma && !document.Root.XmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.RootNamespace, $"Root namespace '{document.Root.XmlNamespace}' is not a Forma or CLR namespace.", document.Root.Location));

        var names = new Dictionary<string, FormaSourceLocation>(StringComparer.Ordinal);
        var dataTypes = new Stack<string?>();
        ValidateObject(document.Root, document, options, diagnostics, names, dataTypes, document.DataType);

        foreach (var timeline in document.DescendantsAndSelf().Where(node => node.TypeName.EndsWith("Timeline", StringComparison.Ordinal)))
        {
            var target = timeline.FindMember("TargetName");
            var property = timeline.FindMember("Property");
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(property))
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Storyboard, "A timeline requires TargetName and Property.", timeline.Location));
            else if (!names.ContainsKey(target))
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Storyboard, $"Storyboard target '{target}' was not found in the local namescope.", timeline.Location));
            if (!timeline.Children.Any(child => child.TypeName == "KeyFrame"))
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Storyboard, "A timeline requires at least one KeyFrame.", timeline.Location));
        }
    }

    private static void ValidateObject(
        FormaXamlObject node,
        FormaXamlDocument document,
        FormaXamlParseOptions options,
        List<FormaDiagnostic> diagnostics,
        Dictionary<string, FormaSourceLocation> names,
        Stack<string?> dataTypes,
        string? inheritedDataType)
    {
        var name = node.FindDirective("Name");
        if (name != null)
        {
            if (!NamePattern.IsMatch(name)) diagnostics.Add(Diagnostic(FormaDiagnosticCodes.InvalidName, $"'{name}' is not a valid XAML name.", node.Location));
            else if (!names.TryAdd(name, node.Location)) diagnostics.Add(Diagnostic(FormaDiagnosticCodes.DuplicateName, $"Name '{name}' is already registered in this namescope.", node.Location));
        }

        var dataType = node.FindDirective("DataType") ?? inheritedDataType;
        foreach (var member in node.Members.Where(member => !member.IsDirective && member.Value.StartsWith("{Binding", StringComparison.Ordinal)))
            ValidateBinding(member, dataType, options, diagnostics);

        if (node.TypeName == "Style")
        {
            var selector = node.FindMember("Selector");
            if (string.IsNullOrWhiteSpace(selector)) diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Selector, "Style requires a Selector.", node.Location));
            else
            {
                try { _ = StyleSelector.Parse(selector); }
                catch (FormatException exception) { diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Selector, exception.Message, node.Location)); }
            }
        }

        if (node.TypeName is "PropertyTrigger" && node.FindMember("Binding") == null)
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Trigger, "PropertyTrigger requires a Binding.", node.Location));
        if (node.TypeName is "EventTrigger" && (node.FindMember("Event") == null || node.FindMember("SourceName") == null))
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Trigger, "EventTrigger requires SourceName and Event.", node.Location));

        foreach (var child in node.Children) ValidateObject(child, document, options, diagnostics, names, dataTypes, dataType);
    }

    private static void ValidateBinding(FormaXamlMember member, string? dataType, FormaXamlParseOptions options, List<FormaDiagnostic> diagnostics)
    {
        var expression = member.Value;
        if (!expression.EndsWith('}'))
        {
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Binding, "Binding markup extension is missing its closing brace.", member.Location));
            return;
        }
        var body = expression.Substring("{Binding".Length, expression.Length - "{Binding".Length - 1).Trim();
        var parts = SplitArguments(body);
        for (var index = 1; index < parts.Count; index++)
        {
            var separator = parts[index].IndexOf('=');
            if (separator <= 0 || !BindingOptions.Contains(parts[index].Substring(0, separator).Trim()))
                diagnostics.Add(Diagnostic(FormaDiagnosticCodes.Binding, $"Unknown or malformed binding option '{parts[index]}'.", member.Location));
        }
        if (options.RequireCompiledBindings && string.IsNullOrWhiteSpace(dataType))
            diagnostics.Add(Diagnostic(FormaDiagnosticCodes.CompiledBinding, "Compiled bindings require x:DataType on this element or an ancestor.", member.Location));
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        var start = 0;
        var quote = '\0';
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (quote != '\0') { if (character == quote) quote = '\0'; continue; }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character == '{') depth++;
            else if (character == '}') depth--;
            else if (character == ',' && depth == 0) { result.Add(value.Substring(start, index - start).Trim()); start = index + 1; }
        }
        result.Add(value.Substring(start).Trim());
        return result;
    }

    private static FormaDiagnostic Diagnostic(string code, string message, FormaSourceLocation location) =>
        new(code, FormaDiagnosticSeverity.Error, message, location);

    private static FormaDiagnostic Diagnostic(string code, string message, string sourcePath, int line, int column) =>
        Diagnostic(code, message, new FormaSourceLocation(sourcePath, line, column));

    private static FormaSourceLocation Location(XObject value, string sourcePath)
    {
        var info = (IXmlLineInfo)value;
        return new FormaSourceLocation(sourcePath, info.HasLineInfo() ? info.LineNumber : 1, info.HasLineInfo() ? info.LinePosition : 1);
    }
}