#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repository_root/tests/Forma.StaticFontBackendSpike/Forma.StaticFontBackendSpike.csproj"
artifact_root="$repository_root/Artifacts/static-font-backend"
runtime_selection="${FORMA_RUNTIME:-}"
runtimes=(MonoGame FNA)
if [[ -n "$runtime_selection" ]]; then runtimes=("$runtime_selection"); fi

rm -rf "$artifact_root"
for runtime_name in "${runtimes[@]}"; do
  output_dir="$artifact_root/$runtime_name"
  backend_source="$repository_root/tests/Forma.StaticFontBackendSpike/PlatformDynamicTextBackend.cs"
  rm -rf "$repository_root/src/Forma.DynamicText/obj/$runtime_name" \
    "$repository_root/tests/Forma.StaticFontBackendSpike/obj/$runtime_name"
  dotnet publish "$project" --configuration Release --runtime osx-arm64 --self-contained true \
    -p:FormaRuntime="$runtime_name" -p:FormaDynamicTextBackend=External \
    -p:FormaDynamicTextBackendSource="$backend_source" \
    -p:PublishAot=true -p:EnableAotAnalyzer=true \
    -p:PublishDir="$output_dir" --nologo
  "$output_dir/Forma.StaticFontBackendSpike"
  if find "$output_dir" -type f \( -iname '*freetype*' -o -iname '*harfbuzz*' \) -print -quit | grep -q .; then
    printf 'External backend output contains a FreeType/HarfBuzz artifact: %s\n' "$output_dir" >&2
    exit 1
  fi
  if grep -Eq 'FreeTypeSharp|HarfBuzzSharp' "$output_dir/Forma.StaticFontBackendSpike.deps.json" 2>/dev/null; then
    printf 'External backend output retains a FreeType/HarfBuzz managed dependency: %s\n' "$output_dir" >&2
    exit 1
  fi
  printf 'Passed static/platform font backend spike for %s.\n' "$runtime_name"
done