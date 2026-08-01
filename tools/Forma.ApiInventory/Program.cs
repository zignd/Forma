// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Reflection;
using PublicApiGenerator;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine(
        "Usage: Forma.ApiInventory <assembly> <source-namespace> [normalized-namespace]");
    return 2;
}

var assemblyPath = Path.GetFullPath(args[0]);
var sourceNamespace = args[1];
var normalizedNamespace = args.Length == 3 ? args[2] : sourceNamespace;

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 2;
}

var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
{
    var dependencyName = new AssemblyName(eventArgs.Name).Name + ".dll";
    var dependencyPath = Path.Combine(assemblyDirectory, dependencyName);
    return File.Exists(dependencyPath) ? Assembly.LoadFrom(dependencyPath) : null;
};

var assembly = Assembly.LoadFrom(assemblyPath);
var publicTypes = assembly.GetExportedTypes()
    .Where(type => !type.IsNested)
    .Where(type => type.Namespace == sourceNamespace ||
        type.Namespace?.StartsWith(sourceNamespace + ".", StringComparison.Ordinal) == true)
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .ToArray();

if (publicTypes.Length == 0)
{
    Console.Error.WriteLine(
        $"No public types found in namespace '{sourceNamespace}' in {assemblyPath}.");
    return 1;
}

var publicApi = publicTypes.GeneratePublicApi(new ApiGeneratorOptions
{
    AllowNamespacePrefixes = [sourceNamespace],
    IncludeAssemblyAttributes = false,
});
if (sourceNamespace != normalizedNamespace)
{
    publicApi = publicApi.Replace(sourceNamespace, normalizedNamespace, StringComparison.Ordinal);
}

Console.Write(publicApi);
return 0;