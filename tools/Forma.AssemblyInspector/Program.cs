// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Mono.Cecil;
using System.Globalization;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

return args.Length == 0 ? Usage() : args[0] switch
{
    "references" => CheckReferences(args),
    "forbid-references" => ForbidReferences(args),
    "compare-api" => CompareApi(args),
    "docs-coverage" => CheckDocumentationCoverage(args),
    "normalize-source-links" => NormalizeSourceLinks(args),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Forma.AssemblyInspector references <assembly> <assembly-name> <runtime> <required-reference> <forbidden-reference>");
    Console.Error.WriteLine("  Forma.AssemblyInspector forbid-references <assembly> <forbidden-reference> [<forbidden-reference> ...]");
    Console.Error.WriteLine("  Forma.AssemblyInspector compare-api <left-assembly> <right-assembly>");
    Console.Error.WriteLine("  Forma.AssemblyInspector docs-coverage <api-yaml-directory> <site-directory> <control-story-directory> <minimum-type-percent> <minimum-member-percent> <report-path>");
    Console.Error.WriteLine("  Forma.AssemblyInspector normalize-source-links <api-yaml-directory> <repository-url> <revision>");
    return 2;
}

static int NormalizeSourceLinks(string[] arguments)
{
    if (arguments.Length != 4 || arguments[3].Length != 40 || arguments[3].Any(character => !Uri.IsHexDigit(character)))
        return Usage();

    var apiDirectory = Path.GetFullPath(arguments[1]);
    var repositoryUrl = arguments[2].TrimEnd('/');
    var expectedPrefix = $"{repositoryUrl}/blob/";
    var replacementPrefix = $"{expectedPrefix}{arguments[3]}/";
    var updatedLinks = 0;
    foreach (var yamlPath in Directory.EnumerateFiles(apiDirectory, "*.yml", SearchOption.TopDirectoryOnly))
    {
        var firstLine = File.ReadLines(yamlPath).FirstOrDefault();
        using var input = File.OpenText(yamlPath);
        var yaml = new YamlStream();
        yaml.Load(input);
        var changed = false;
        foreach (var scalar in yaml.Documents.SelectMany(document => DescendantScalars(document.RootNode)))
        {
            if (scalar.Value is not { } value || !value.StartsWith(expectedPrefix, StringComparison.Ordinal)) continue;
            var sourcePathIndex = value.IndexOf("/src/", expectedPrefix.Length, StringComparison.Ordinal);
            if (sourcePathIndex < 0) continue;
            scalar.Value = replacementPrefix + value[(sourcePathIndex + 1)..];
            changed = true;
            updatedLinks++;
        }
        if (!changed) continue;
        using var output = File.CreateText(yamlPath);
        if (firstLine?.StartsWith("### YamlMime:", StringComparison.Ordinal) == true)
            output.WriteLine(firstLine);
        yaml.Save(output, false);
    }

    if (updatedLinks == 0)
    {
        Console.Error.WriteLine($"No {repositoryUrl} source links were found in {apiDirectory}.");
        return 1;
    }
    Console.WriteLine($"Normalized {updatedLinks} API source links to revision {arguments[3]}.");
    return 0;
}

static IEnumerable<YamlScalarNode> DescendantScalars(YamlNode node)
{
    if (node is YamlScalarNode scalar) yield return scalar;
    else if (node is YamlSequenceNode sequence)
        foreach (var child in sequence.Children.SelectMany(DescendantScalars)) yield return child;
    else if (node is YamlMappingNode mapping)
        foreach (var child in mapping.Children.SelectMany(pair => DescendantScalars(pair.Key).Concat(DescendantScalars(pair.Value)))) yield return child;
}

static int CheckDocumentationCoverage(string[] arguments)
{
    if (arguments.Length != 7 ||
        !double.TryParse(arguments[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimumTypePercent) ||
        !double.TryParse(arguments[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimumMemberPercent))
        return Usage();

    var apiDirectory = Path.GetFullPath(arguments[1]);
    var siteDirectory = Path.GetFullPath(arguments[2]);
    var storyDirectory = Path.GetFullPath(arguments[3]);
    var reportPath = Path.GetFullPath(arguments[6]);
    var types = new List<DocumentationItem>();
    var members = new List<DocumentationItem>();

    foreach (var yamlPath in Directory.EnumerateFiles(apiDirectory, "*.yml", SearchOption.TopDirectoryOnly))
    {
        using var input = File.OpenText(yamlPath);
        var yaml = new YamlStream();
        yaml.Load(input);
        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root ||
            !root.Children.TryGetValue(new YamlScalarNode("items"), out var itemsNode) || itemsNode is not YamlSequenceNode items)
            continue;

        foreach (var itemNode in items.Children.OfType<YamlMappingNode>())
        {
            var uid = Scalar(itemNode, "uid");
            var kind = Scalar(itemNode, "type");
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(kind)) continue;
            var item = new DocumentationItem(uid, !string.IsNullOrWhiteSpace(Scalar(itemNode, "summary")));
            if (kind is "Class" or "Struct" or "Interface" or "Enum" or "Delegate") types.Add(item);
            else if (kind is "Constructor" or "Method" or "Property" or "Field" or "Event" or "Operator") members.Add(item);
        }
    }

    var typeCoverage = Percentage(types.Count(item => item.Documented), types.Count);
    var memberCoverage = Percentage(members.Count(item => item.Documented), members.Count);
    var missingMappings = new List<string>();
    var mappings = new List<ControlMapping>();
    foreach (var storyPath in Directory.EnumerateFiles(storyDirectory, "*.xaml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
    {
        var controlName = Path.GetFileNameWithoutExtension(storyPath);
        var uid = $"Forma.{controlName}";
        var yamlExists = File.Exists(Path.Combine(apiDirectory, $"{uid}.yml"));
        var htmlExists = File.Exists(Path.Combine(siteDirectory, "api", $"{uid}.html"));
        mappings.Add(new ControlMapping(controlName, Path.GetRelativePath(Directory.GetParent(storyDirectory)!.Parent!.Parent!.FullName, storyPath), uid, yamlExists, htmlExists));
        if (!yamlExists || !htmlExists) missingMappings.Add($"{controlName}: metadata={yamlExists}, page={htmlExists}");
    }

    var report = new DocumentationCoverageReport(
        types.Count, types.Count(item => item.Documented), typeCoverage,
        members.Count, members.Count(item => item.Documented), memberCoverage,
        minimumTypePercent, minimumMemberPercent, mappings);
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

    foreach (var missing in missingMappings) Console.Error.WriteLine($"Missing control mapping: {missing}");
    if (typeCoverage < minimumTypePercent)
        Console.Error.WriteLine($"Type documentation coverage {typeCoverage:F1}% is below {minimumTypePercent:F1}%.");
    if (memberCoverage < minimumMemberPercent)
        Console.Error.WriteLine($"Member documentation coverage {memberCoverage:F1}% is below {minimumMemberPercent:F1}%.");
    Console.WriteLine($"Documentation coverage: {report.DocumentedTypes}/{report.PublicTypes} types ({typeCoverage:F1}%), {report.DocumentedMembers}/{report.PublicMembers} members ({memberCoverage:F1}%), {mappings.Count} control story/reference mappings.");
    return missingMappings.Count == 0 && typeCoverage >= minimumTypePercent && memberCoverage >= minimumMemberPercent ? 0 : 1;
}

static string? Scalar(YamlMappingNode node, string key) =>
    node.Children.TryGetValue(new YamlScalarNode(key), out var value) && value is YamlScalarNode scalar ? scalar.Value : null;

static double Percentage(int documented, int total) => total == 0 ? 100 : documented * 100.0 / total;

static int CheckReferences(string[] arguments)
{
    if (arguments.Length != 6) return Usage();
    using var assembly = AssemblyDefinition.ReadAssembly(Path.GetFullPath(arguments[1]));
    var references = assembly.MainModule.AssemblyReferences.Select(reference => reference.Name).ToHashSet(StringComparer.Ordinal);
    var runtime = assembly.CustomAttributes
        .Where(attribute => attribute.AttributeType.FullName == typeof(System.Reflection.AssemblyMetadataAttribute).FullName)
        .Where(attribute => attribute.ConstructorArguments.Count == 2 && (string)attribute.ConstructorArguments[0].Value == "FormaRuntime")
        .Select(attribute => (string)attribute.ConstructorArguments[1].Value)
        .SingleOrDefault();
    var errors = new List<string>();
    if (assembly.Name.Name != arguments[2]) errors.Add($"Expected assembly name '{arguments[2]}', found '{assembly.Name.Name}'.");
    if (runtime != arguments[3]) errors.Add($"Expected FormaRuntime metadata '{arguments[3]}', found '{runtime ?? "<missing>"}'.");
    if (!references.Contains(arguments[4])) errors.Add($"{assembly.Name.Name} does not reference required framework assembly '{arguments[4]}'.");
    if (references.Contains(arguments[5])) errors.Add($"{assembly.Name.Name} unexpectedly references framework assembly '{arguments[5]}'.");
    foreach (var type in assembly.MainModule.Types.SelectMany(FlattenTypes).Where(IsPublicType))
    {
        if (type.Namespace == "Forma.MonoGame" || type.Namespace.StartsWith("Forma.MonoGame.", StringComparison.Ordinal) ||
            type.Namespace == "Forma.FNA" || type.Namespace.StartsWith("Forma.FNA.", StringComparison.Ordinal))
            errors.Add($"Runtime-branded public namespace is not allowed: {type.FullName}.");
    }
    foreach (var error in errors) Console.Error.WriteLine(error);
    Console.WriteLine($"{assembly.Name.Name} [{runtime}]: {string.Join(", ", references.OrderBy(reference => reference, StringComparer.Ordinal))}");
    return errors.Count == 0 ? 0 : 1;
}

static int ForbidReferences(string[] arguments)
{
    if (arguments.Length < 3) return Usage();
    using var assembly = AssemblyDefinition.ReadAssembly(Path.GetFullPath(arguments[1]));
    var references = assembly.MainModule.AssemblyReferences.Select(reference => reference.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var forbidden = arguments.Skip(2).Where(references.Contains).OrderBy(reference => reference, StringComparer.OrdinalIgnoreCase).ToArray();
    foreach (var reference in forbidden) Console.Error.WriteLine($"{assembly.Name.Name} unexpectedly references '{reference}'.");
    Console.WriteLine($"{assembly.Name.Name}: {string.Join(", ", references.OrderBy(reference => reference, StringComparer.Ordinal))}");
    return forbidden.Length == 0 ? 0 : 1;
}

static int CompareApi(string[] arguments)
{
    if (arguments.Length != 3) return Usage();
    using var left = AssemblyDefinition.ReadAssembly(Path.GetFullPath(arguments[1]));
    using var right = AssemblyDefinition.ReadAssembly(Path.GetFullPath(arguments[2]));
    var leftApi = GetPublicApi(left);
    var rightApi = GetPublicApi(right);
    var leftOnly = leftApi.Except(rightApi, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    var rightOnly = rightApi.Except(leftApi, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    foreach (var value in leftOnly) Console.Error.WriteLine($"Only in left: {value}");
    foreach (var value in rightOnly) Console.Error.WriteLine($"Only in right: {value}");
    if (leftOnly.Length != 0 || rightOnly.Length != 0) return 1;
    Console.WriteLine($"Public API parity: {leftApi.Count} normalized signatures match.");
    return 0;
}

static HashSet<string> GetPublicApi(AssemblyDefinition assembly)
{
    var api = new HashSet<string>(StringComparer.Ordinal);
    foreach (var type in assembly.MainModule.Types.SelectMany(FlattenTypes).Where(IsPublicType))
    {
        api.Add($"type {TypeKind(type)} {type.FullName} : {type.BaseType?.FullName ?? "<none>"}");
        foreach (var generic in type.GenericParameters) api.Add($"type-generic {type.FullName} {GenericSignature(generic)}");
        foreach (var contract in type.Interfaces) api.Add($"interface {type.FullName} {contract.InterfaceType.FullName}");
        foreach (var field in type.Fields.Where(IsVisibleField)) api.Add($"field {FieldVisibility(field)} {(field.IsStatic ? "static " : "")}{field.FieldType.FullName} {type.FullName}::{field.Name}");
        foreach (var method in type.Methods.Where(IsVisibleMethod)) api.Add(MethodSignature(type, method));
        foreach (var property in type.Properties.Where(property => IsVisibleMethod(property.GetMethod) || IsVisibleMethod(property.SetMethod)))
            api.Add($"property {property.PropertyType.FullName} {type.FullName}::{property.Name}[{string.Join(",", property.Parameters.Select(parameter => parameter.ParameterType.FullName))}] get:{MethodVisibility(property.GetMethod)} set:{MethodVisibility(property.SetMethod)}");
        foreach (var eventDefinition in type.Events.Where(eventDefinition => IsVisibleMethod(eventDefinition.AddMethod) || IsVisibleMethod(eventDefinition.RemoveMethod)))
            api.Add($"event {eventDefinition.EventType.FullName} {type.FullName}::{eventDefinition.Name} add:{MethodVisibility(eventDefinition.AddMethod)} remove:{MethodVisibility(eventDefinition.RemoveMethod)}");
    }
    return api;
}

static IEnumerable<TypeDefinition> FlattenTypes(TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes.SelectMany(FlattenTypes)) yield return nested;
}

static bool IsPublicType(TypeDefinition type) => type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly;
static bool IsVisibleField(FieldDefinition field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
static bool IsVisibleMethod(MethodDefinition? method) => method != null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
static string FieldVisibility(FieldDefinition field) => field.IsPublic ? "public" : field.IsFamily ? "protected" : "protected-internal";
static string MethodVisibility(MethodDefinition? method) => method == null ? "none" : method.IsPublic ? "public" : method.IsFamily ? "protected" : method.IsFamilyOrAssembly ? "protected-internal" : "private";
static string TypeKind(TypeDefinition type) => type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : type.IsClass ? "class" : "type";
static string GenericSignature(GenericParameter parameter) => $"{parameter.Position}:{parameter.Name}:{parameter.Attributes}:{string.Join(",", parameter.Constraints.Select(constraint => constraint.ConstraintType.FullName))}";
static string MethodSignature(TypeDefinition type, MethodDefinition method) =>
    $"method {MethodVisibility(method)} {(method.IsStatic ? "static " : "")}{(method.IsAbstract ? "abstract " : "")}{method.ReturnType.FullName} {type.FullName}::{method.Name}`{method.GenericParameters.Count}({string.Join(",", method.Parameters.Select(parameter => $"{parameter.ParameterType.FullName} {parameter.Name}"))})";

sealed record DocumentationItem(string Uid, bool Documented);
sealed record ControlMapping(string Control, string Story, string ApiUid, bool MetadataExists, bool PageExists);
sealed record DocumentationCoverageReport(
    int PublicTypes,
    int DocumentedTypes,
    double TypeCoveragePercent,
    int PublicMembers,
    int DocumentedMembers,
    double MemberCoveragePercent,
    double MinimumTypeCoveragePercent,
    double MinimumMemberCoveragePercent,
    IReadOnlyList<ControlMapping> Controls);