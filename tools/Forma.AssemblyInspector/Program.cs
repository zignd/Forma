// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Mono.Cecil;

return args.Length == 0 ? Usage() : args[0] switch
{
    "references" => CheckReferences(args),
    "forbid-references" => ForbidReferences(args),
    "compare-api" => CompareApi(args),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  Forma.AssemblyInspector references <assembly> <assembly-name> <runtime> <required-reference> <forbidden-reference>");
    Console.Error.WriteLine("  Forma.AssemblyInspector forbid-references <assembly> <forbidden-reference> [<forbidden-reference> ...]");
    Console.Error.WriteLine("  Forma.AssemblyInspector compare-api <left-assembly> <right-assembly>");
    return 2;
}

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