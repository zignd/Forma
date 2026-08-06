#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/tests/Forma.Svg.SelectionProbe/Forma.Svg.SelectionProbe.csproj"

for mode in none repeated conflict late unavailable; do
    dotnet run --project "$project" --configuration Release -p:FormaRuntime=MonoGame -- "$mode"
done

printf 'SVG backend selection contract passed all isolated modes.\n'