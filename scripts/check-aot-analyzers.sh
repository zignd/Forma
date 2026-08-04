#!/usr/bin/env bash
set -euo pipefail

# Purpose: Build a minimal source-linked consumer with trim/AOT analyzers for both peer runtimes.
# Usage: `bash scripts/check-aot-analyzers.sh` from any directory.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
consumer_project="$repository_root/tests/Forma.AotAnalyzerConsumer/Forma.AotAnalyzerConsumer.csproj"

for runtime in MonoGame FNA; do
  dotnet build "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:EnableTrimAnalyzer=true \
    -p:EnableAotAnalyzer=true \
    --warnaserror \
    --nologo
done

printf 'Source-linked trim/AOT analyzer consumers passed for MonoGame and FNA.\n'