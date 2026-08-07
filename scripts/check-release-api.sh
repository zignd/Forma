#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_manifest="$repository_root/scripts/release-packages.json"
baseline_path="$repository_root/scripts/public-api-baseline.json"
migration_manifest="$repository_root/scripts/api-migrations.json"
inspector_project="$repository_root/tools/Forma.AssemblyInspector/Forma.AssemblyInspector.csproj"
create_baseline="${FORMA_API_BASELINE_CREATE:-false}"

command -v jq >/dev/null || { printf 'jq is required to read %s.\n' "$release_manifest" >&2; exit 1; }

assemblies=()
while IFS= read -r project; do
  dotnet build "$repository_root/$project" --configuration Release \
    -p:FormaRuntime=MonoGame --nologo
  target_path="$(dotnet msbuild "$repository_root/$project" \
    -p:FormaRuntime=MonoGame -p:Configuration=Release -getProperty:TargetPath -nologo)"
  [[ -f "$target_path" ]] || { printf 'Release assembly does not exist: %s\n' "$target_path" >&2; exit 1; }
  assemblies+=("$target_path")
done < <(jq -r '.packages[] | select(.runtime == "MonoGame") | .project' "$release_manifest")

if [[ "$create_baseline" == "true" ]]; then
  dotnet run --project "$inspector_project" --configuration Release -- \
    create-api-baseline "$baseline_path" "${assemblies[@]}"
else
  dotnet run --project "$inspector_project" --configuration Release -- \
    review-api "$baseline_path" "$migration_manifest" "$repository_root/RELEASE_NOTES.md" \
    "$repository_root" "${assemblies[@]}"
fi