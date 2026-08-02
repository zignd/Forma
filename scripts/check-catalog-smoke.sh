#!/usr/bin/env bash
set -euo pipefail

# Purpose: Run the catalog for three frames and compare its 2x metrics with the approved baseline.
# Usage: `bash scripts/check-catalog-smoke.sh` from any directory; a graphical environment and `jq`
# are required. Set `FormaRuntime=FNA` for the FNA host; MonoGame is the default.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline="$repository_root/samples/Forma.Catalog/catalog-metrics-baseline.json"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-catalog.XXXXXX")"
actual="$stage_directory/catalog.json"
stable_baseline="$stage_directory/baseline-stable.json"
stable_actual="$stage_directory/actual-stable.json"
runtime="${FormaRuntime:-MonoGame}"
catalog_options=()
if [[ -n "${FormaCatalogViewportWidth:-}" && -n "${FormaCatalogViewportHeight:-}" ]]; then
  catalog_options+=(--viewport-width "$FormaCatalogViewportWidth" --viewport-height "$FormaCatalogViewportHeight")
fi
msbuild_options=(-p:FormaRuntime="$runtime")
for property_name in FormaNativeRuntime MonoGamePlatform CatalogBackend; do
  if [[ -n "${!property_name:-}" ]]; then
    msbuild_options+=(-p:"$property_name=${!property_name}")
  fi
done
case "$runtime" in
  MonoGame)
    project="$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj"
    expected_backend="${CatalogBackend:-${MonoGamePlatform:-DesktopGL}}"
    ;;
  FNA)
    project="$repository_root/samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj"
    expected_backend="FNA"
    ;;
  *)
    printf 'FormaRuntime must be either MonoGame or FNA.\n' >&2
    exit 2
    ;;
esac
trap 'rm -rf "$stage_directory"' EXIT

if ! command -v jq >/dev/null; then
  printf 'Catalog smoke requires jq for structured metrics validation.\n' >&2
  exit 2
fi

dotnet build "$project" \
  --configuration Release \
  "${msbuild_options[@]}" \
  --nologo \
  -m:1

dotnet run --project "$project" \
  --configuration Release \
  "${msbuild_options[@]}" \
  --no-build \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
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

for story_name in "Complete icon inventory" "Override and suppression" "Atlas diagnostics"; do
  story_actual="$stage_directory/$(tr ' ' '-' <<<"$story_name").json"
  dotnet run --project "$project" \
    --configuration Release \
    "${msbuild_options[@]}" \
    --no-build \
    -- \
    ${catalog_options[@]+"${catalog_options[@]}"} \
    --metrics "$story_actual" \
    --frames 3 \
    --display-scale 2 \
    --story "$story_name"
  jq -e '(.themeIconDensity == 2) and (.themeIconAtlasCount >= 1) and (.themeIconTextureBytes > 0) and (.themeIconMissingCount == 0)' \
    "$story_actual" >/dev/null
done

for story_name in "Dynamic Sizes" "Display Density" "Fallback Chain" "Shaping and Features" "Bidirectional Text" "Wrapping and Selection" "SpriteFont Compatibility" "Atlas Inspector" "Failure States"; do
  story_actual="$stage_directory/$(tr ' ' '-' <<<"$story_name").json"
  dotnet run --project "$project" \
    --configuration Release \
    "${msbuild_options[@]}" \
    --no-build \
    -- \
    ${catalog_options[@]+"${catalog_options[@]}"} \
    --metrics "$story_actual" \
    --frames 3 \
    --display-scale 2 \
    --story "$story_name"
  jq -e --arg story "$story_name" '
    .selectedStory == $story and
    .dynamicGlyphPageCount <= 8 and
    .dynamicGlyphBytes <= 33554432 and
    .dynamicGlyphPendingUploads == 0 and
    .dynamicGlyphFailures == 0
  ' "$story_actual" >/dev/null
done

inventory_1x="$stage_directory/icon-inventory-1x.json"
dotnet run --project "$project" \
  --configuration Release \
  "${msbuild_options[@]}" \
  --no-build \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
  --metrics "$inventory_1x" \
  --frames 3 \
  --display-scale 1 \
  --story "Complete icon inventory"
jq -e '(.themeIconDensity == 1) and (.themeIconAtlasCount == 1) and (.themeIconTextureBytes > 0) and (.themeIconMissingCount == 0)' \
  "$inventory_1x" >/dev/null

jq -S 'del(.backend, .physicalViewportWidth, .physicalViewportHeight, .logicalViewportWidth, .logicalViewportHeight, .startupMilliseconds, .steadyStateMeasuredFrames, .steadyStateAllocatedBytes, .steadyStateAllocatedBytesPerFrame, .fontXnbBytes, .spriteFontTextureBytes, .steadyStateTextureBytes, .dynamicGlyphPageCount, .dynamicGlyphCount, .dynamicGlyphBytes, .dynamicGlyphPendingUploads, .dynamicGlyphFailures, .dynamicGlyphLastFailure)' "$baseline" > "$stable_baseline"
jq -S 'del(.backend, .physicalViewportWidth, .physicalViewportHeight, .logicalViewportWidth, .logicalViewportHeight, .startupMilliseconds, .steadyStateMeasuredFrames, .steadyStateAllocatedBytes, .steadyStateAllocatedBytesPerFrame, .fontXnbBytes, .spriteFontTextureBytes, .steadyStateTextureBytes, .dynamicGlyphPageCount, .dynamicGlyphCount, .dynamicGlyphBytes, .dynamicGlyphPendingUploads, .dynamicGlyphFailures, .dynamicGlyphLastFailure)' "$actual" > "$stable_actual"
if ! cmp -s "$stable_baseline" "$stable_actual"; then
  printf 'Catalog metrics differ from the approved 2x baseline.\n' >&2
  diff -u "$stable_baseline" "$stable_actual" || true
  exit 1
fi

viewport="$(jq -r '"\(.logicalViewportWidth)x\(.logicalViewportHeight)"' "$actual")"
story_count="$(jq -r '.storyCount' "$actual")"
printf 'Catalog smoke: %s, 3 frames, %s stories, 2x density font, %s logical viewport.\n' \
  "$expected_backend" "$story_count" "$viewport"