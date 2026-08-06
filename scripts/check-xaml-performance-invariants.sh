#!/usr/bin/env bash
# Purpose: Run deterministic template, collection, selector, and virtualization performance invariants.
# Usage: `bash scripts/check-xaml-performance-invariants.sh` from any directory.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repository_root/tests/Forma.Tests/Forma.Tests.csproj"
filter='(FullyQualifiedName~Forma.Tests.VirtualizingPanelsTest|FullyQualifiedName~Forma.Tests.DataGridTest|FullyQualifiedName~Forma.Tests.ItemsControlTest.ListBoxItem_PseudoStatesInvalidateOnlyMatchingTemplateParts|FullyQualifiedName~Forma.Tests.XamlStyleTest.Selectors_|FullyQualifiedName~Forma.Tests.XamlBindingTest.Control_InheritedEffectivePropertiesNotifyOnlyAffectedDescendants|FullyQualifiedName~Forma.Tests.XamlBindingTest.Control_ComputedGeometryNotifiesAffectedVisualSubtree)'

for runtime in MonoGame FNA; do
  dotnet test "$project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    --filter "$filter" \
    --nologo \
    -m:1
done
