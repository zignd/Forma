#!/usr/bin/env bash
set -euo pipefail

# Purpose: Run the catalog for three frames and compare its 2x metrics with the approved baseline.
# Usage: `bash scripts/check-catalog-smoke.sh` from any directory; a graphical environment and `jq`
# are required. DesktopGL is expected by default; set `CatalogBackend` or `MonoGamePlatform` to
# override the expected backend name.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline="$repository_root/samples/Forma.Catalog/catalog-metrics-baseline.json"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-catalog.XXXXXX")"
actual="$stage_directory/catalog.json"
stable_baseline="$stage_directory/baseline-stable.json"
stable_actual="$stage_directory/actual-stable.json"
expected_backend="${CatalogBackend:-${MonoGamePlatform:-DesktopGL}}"
trap 'rm -rf "$stage_directory"' EXIT

if ! command -v jq >/dev/null; then
  printf 'Catalog smoke requires jq for structured metrics validation.\n' >&2
  exit 2
fi

dotnet run --project "$repository_root/samples/Forma.Catalog/Forma.Catalog.csproj" \
  --configuration Release \
  -- \
  --metrics "$actual" \
  --frames 3 \
  --display-scale 2

if ! jq -e '(.logicalViewportWidth > 0) and (.logicalViewportHeight > 0)' \
  "$actual" >/dev/null; then
  printf 'Catalog metrics contain an invalid logical viewport.\n' >&2
  exit 1
fi

if ! jq -e --arg backend "$expected_backend" '.backend == $backend' "$actual" >/dev/null; then
  printf 'Catalog metrics reported an unexpected backend; expected %s.\n' "$expected_backend" >&2
  exit 1
fi

jq -S 'del(.backend, .logicalViewportWidth, .logicalViewportHeight)' "$baseline" > "$stable_baseline"
jq -S 'del(.backend, .logicalViewportWidth, .logicalViewportHeight)' "$actual" > "$stable_actual"
if ! cmp -s "$stable_baseline" "$stable_actual"; then
  printf 'Catalog metrics differ from the approved 2x baseline.\n' >&2
  diff -u "$stable_baseline" "$stable_actual" || true
  exit 1
fi

viewport="$(jq -r '"\(.logicalViewportWidth)x\(.logicalViewportHeight)"' "$actual")"
printf 'Catalog smoke: %s, 3 frames, 75 stories, 2x density font, %s logical viewport.\n' \
  "$expected_backend" "$viewport"