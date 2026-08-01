// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Reflection;

namespace Forma.Catalog;

internal static class CatalogBackend
{
    public static string Name { get; } = typeof(CatalogBackend).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "FormaCatalogBackend")?.Value ?? "Unknown";
}