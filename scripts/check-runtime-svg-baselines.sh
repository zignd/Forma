#!/usr/bin/env bash
# Purpose: Capture and compare the approved Runtime SVG Catalog matrix across runtime peers and PNG fallback.
# Usage: `bash scripts/check-runtime-svg-baselines.sh` from any directory.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repository_root/Artifacts/runtime-svg-baselines"
image_root="$repository_root/docs/images"
mkdir -p "$artifact_root" "$image_root"

if ! command -v jq >/dev/null 2>&1; then
  printf 'Runtime SVG baseline validation requires jq.\n' >&2
  exit 1
fi

compare_metric() {
  local first_file="$1"
  local second_file="$2"
  local field="$3"
  local tolerance="$4"
  local relative_difference
  relative_difference="$(jq -n --slurpfile first "$first_file" --slurpfile second "$second_file" --arg field "$field" '
    ($first[0][$field] | tonumber) as $a |
    ($second[0][$field] | tonumber) as $b |
    (($a - $b) | if . < 0 then -. else . end) / ([($a | if . < 0 then -. else . end), ($b | if . < 0 then -. else . end), 1] | max)
  ')"
  if ! awk -v difference="$relative_difference" -v limit="$tolerance" 'BEGIN { exit !(difference <= limit) }'; then
    printf '%s differs by %s between %s and %s (limit %s).\n' "$field" "$relative_difference" "$first_file" "$second_file" "$tolerance" >&2
    exit 1
  fi
}

capture() {
  local runtime_name="$1"
  local cell_name="$2"
  local display_scale="$3"
  local viewport_width="$4"
  local viewport_height="$5"
  local layout_direction="$6"
  local policy="$7"
  local project="$repository_root/samples/Forma.Catalog.$runtime_name/Forma.Catalog.$runtime_name.csproj"
  local runtime_slug
  runtime_slug="$(printf '%s' "$runtime_name" | tr '[:upper:]' '[:lower:]')"
  local prefix="runtime-svg-$runtime_slug-$cell_name"
  if [[ "$policy" == "RuntimeSvg" ]]; then
    dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime_name" --no-build -- \
      --story 'Runtime SVG' --frames 20 --display-scale "$display_scale" \
      --viewport-width "$viewport_width" --viewport-height "$viewport_height" \
      --layout-direction "$layout_direction" --theme-icon-policy "$policy" \
      --render-output "$artifact_root/$prefix.${policy}.json" \
      --screenshot "$image_root/$prefix.png" --metrics "$artifact_root/$prefix.metrics.json"
  else
    dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime_name" --no-build -- \
      --story 'Runtime SVG' --frames 20 --display-scale "$display_scale" \
      --viewport-width "$viewport_width" --viewport-height "$viewport_height" \
      --layout-direction "$layout_direction" --theme-icon-policy "$policy" \
      --render-output "$artifact_root/$prefix.${policy}.json"
  fi
}

for runtime_name in MonoGame FNA; do
  dotnet build "$repository_root/samples/Forma.Catalog.$runtime_name/Forma.Catalog.$runtime_name.csproj" \
    --configuration Release -p:FormaRuntime="$runtime_name" --nologo
  while IFS=: read -r cell_name display_scale viewport_width viewport_height layout_direction; do
    capture "$runtime_name" "$cell_name" "$display_scale" "$viewport_width" "$viewport_height" "$layout_direction" RuntimeSvg
    capture "$runtime_name" "$cell_name" "$display_scale" "$viewport_width" "$viewport_height" "$layout_direction" BitmapAtlas
  done <<'CELLS'
1x:1:1440:900:LTR
1_25x:1.25:1440:900:LTR
1_5x:1.5:1440:900:LTR
1_75x:1.75:1440:900:LTR
2x:2:1800:1200:LTR
2_5x:2.5:2160:1300:LTR
rtl:1.25:1440:900:RTL
narrow:1:1024:720:LTR
CELLS
done

# Machine assertions: healthy backend, all 67 icons, zero fallback, cache entries.
# Warm cache hits are visible in the story's status label (raster.Hits) and are validated
# by SvgRasterCacheTest unit tests; they are not exported to the metrics JSON.
for runtime_name in monogame fna; do
  for cell_name in 1x 1_25x 1_5x 1_75x 2x 2_5x rtl narrow; do
    svg_report="$artifact_root/runtime-svg-$runtime_name-$cell_name.RuntimeSvg.json"
    png_report="$artifact_root/runtime-svg-$runtime_name-$cell_name.BitmapAtlas.json"
    for field in nonBackgroundPixels redTotal greenTotal blueTotal edgeTransitions edgeStrength; do
      compare_metric "$svg_report" "$png_report" "$field" 0.03
    done
    jq -e '
      .svgBackendAvailable == true and
      .svgBackendName == "Svg.Skia" and
      .themeIconPolicy == "RuntimeSvg" and
      .themeIconRuntimeSvgCount == 67 and
      .themeIconBitmapFallbackCount == 0 and
      .svgRasterEntries > 0
    ' "$artifact_root/runtime-svg-$runtime_name-$cell_name.metrics.json" >/dev/null
  done
done

# Cross-peer pixel parity: MonoGame and FNA RuntimeSvg output must agree within 1%.
for cell_name in 1x 1_25x 1_5x 1_75x 2x 2_5x rtl narrow; do
  monogame_report="$artifact_root/runtime-svg-monogame-$cell_name.RuntimeSvg.json"
  fna_report="$artifact_root/runtime-svg-fna-$cell_name.RuntimeSvg.json"
  for field in nonBackgroundPixels redTotal greenTotal blueTotal edgeTransitions edgeStrength; do
    compare_metric "$monogame_report" "$fna_report" "$field" 0.01
  done
done

printf 'Runtime SVG baselines passed for 1x, 1.25x, 1.5x, 1.75x, 2x, 2.5x, RTL, and narrow cells on MonoGame and FNA.\n'
