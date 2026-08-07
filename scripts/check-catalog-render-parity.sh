#!/usr/bin/env bash
set -euo pipefail

# Purpose: Render the shared catalog through both peer hosts and compare deterministic image
# statistics. Exact hashes are diagnostic; aggregate tolerances can be adjusted for software backends.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-render-parity.XXXXXX")"
monogame_report="$stage_directory/monogame.json"
fna_report="$stage_directory/fna.json"
template_monogame_report="$stage_directory/template-monogame.json"
template_fna_report="$stage_directory/template-fna.json"
tolerance="${FormaRenderParityTolerance:-0.01}"
coverage_tolerance="${FormaRenderParityCoverageTolerance:-$tolerance}"
color_tolerance="${FormaRenderParityColorTolerance:-$tolerance}"
catalog_options=()
if [[ -n "${FormaCatalogViewportWidth:-}" && -n "${FormaCatalogViewportHeight:-}" ]]; then
  catalog_options+=(--viewport-width "$FormaCatalogViewportWidth" --viewport-height "$FormaCatalogViewportHeight")
fi
svg_backend="${SvgBackend:-ThorVG}"
monogame_msbuild_options=(-p:FormaRuntime=MonoGame -p:SvgBackend="$svg_backend")
fna_environment=(env -u VK_ICD_FILENAMES -u VK_DRIVER_FILES)
if [[ "$(uname -s)" == "Linux" ]]; then
  fna_environment+=(SDL_VIDEODRIVER=offscreen FNA3D_FORCE_DRIVER=OpenGL FNA3D_OPENGL_WINDOW_DEPTHSTENCILFORMAT=None)
fi
for property_name in FormaNativeRuntime MonoGamePlatform CatalogBackend; do
  if [[ -n "${!property_name:-}" ]]; then
    monogame_msbuild_options+=(-p:"$property_name=${!property_name}")
  fi
done
trap 'rm -rf "$stage_directory"' EXIT

if ! command -v jq >/dev/null; then
  printf 'Catalog render parity requires jq.\n' >&2
  exit 2
fi

dotnet run --project "$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj" \
  --configuration Release \
  "${monogame_msbuild_options[@]}" \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
  --render-output "$monogame_report" \
  --frames 3 \
  --display-scale 2

"${fna_environment[@]}" \
  dotnet run --project "$repository_root/samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj" \
  --configuration Release \
  -p:FormaRuntime=FNA -p:SvgBackend="$svg_backend" \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
  --render-output "$fna_report" \
  --frames 3 \
  --display-scale 2

jq -e \
  --argjson coverageTolerance "$coverage_tolerance" \
  --argjson colorTolerance "$color_tolerance" \
  --slurpfile peer "$fna_report" '
  def relative_difference(left; right):
    ([left, right] | max) as $maximum |
    if $maximum == 0 then 0 else ((left - right) | fabs) / $maximum end;
  .width == $peer[0].width and
  .height == $peer[0].height and
  .alphaTotal == $peer[0].alphaTotal and
  .nonBackgroundPixels > 0 and
  $peer[0].nonBackgroundPixels > 0 and
  (.redTotal + .greenTotal + .blueTotal) > 0 and
  ($peer[0].redTotal + $peer[0].greenTotal + $peer[0].blueTotal) > 0 and
  relative_difference(.nonBackgroundPixels; $peer[0].nonBackgroundPixels) <= $coverageTolerance and
  relative_difference(.redTotal; $peer[0].redTotal) <= $colorTolerance and
  relative_difference(.greenTotal; $peer[0].greenTotal) <= $colorTolerance and
  relative_difference(.blueTotal; $peer[0].blueTotal) <= $colorTolerance
' "$monogame_report" >/dev/null || {
  printf 'Catalog peer render statistics exceeded coverage=%s color=%s tolerances.\n' \
    "$coverage_tolerance" "$color_tolerance" >&2
  jq -S . "$monogame_report" >&2
  jq -S . "$fna_report" >&2
  exit 1
}

dotnet run --project "$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj" \
  --configuration Release \
  "${monogame_msbuild_options[@]}" \
  --no-build \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
  --render-output "$template_monogame_report" \
  --frames 3 \
  --display-scale 2 \
  --story "Template Systems"

"${fna_environment[@]}" \
  dotnet run --project "$repository_root/samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj" \
  --configuration Release \
  -p:FormaRuntime=FNA -p:SvgBackend="$svg_backend" \
  --no-build \
  -- \
  ${catalog_options[@]+"${catalog_options[@]}"} \
  --render-output "$template_fna_report" \
  --frames 3 \
  --display-scale 2 \
  --story "Template Systems"

jq -e \
  --argjson coverageTolerance "$coverage_tolerance" \
  --argjson colorTolerance "$color_tolerance" \
  --slurpfile peer "$template_fna_report" '
  def relative_difference(left; right):
    ([left, right] | max) as $maximum |
    if $maximum == 0 then 0 else ((left - right) | fabs) / $maximum end;
  .width == $peer[0].width and
  .height == $peer[0].height and
  .alphaTotal == $peer[0].alphaTotal and
  .nonBackgroundPixels > 0 and
  $peer[0].nonBackgroundPixels > 0 and
  relative_difference(.nonBackgroundPixels; $peer[0].nonBackgroundPixels) <= $coverageTolerance and
  relative_difference(.redTotal; $peer[0].redTotal) <= $colorTolerance and
  relative_difference(.greenTotal; $peer[0].greenTotal) <= $colorTolerance and
  relative_difference(.blueTotal; $peer[0].blueTotal) <= $colorTolerance
' "$template_monogame_report" >/dev/null || {
  printf 'Template Systems peer render statistics exceeded coverage=%s color=%s tolerances.\n' \
    "$coverage_tolerance" "$color_tolerance" >&2
  jq -S . "$template_monogame_report" >&2
  jq -S . "$template_fna_report" >&2
  exit 1
}

monogame_hash="$(jq -r .pixelHash "$monogame_report")"
fna_hash="$(jq -r .pixelHash "$fna_report")"
printf 'Catalog render parity: %sx%s within coverage=%s color=%s; hashes MonoGame=%s FNA=%s.\n' \
  "${FormaCatalogViewportWidth:-1440}" "${FormaCatalogViewportHeight:-900}" \
  "$coverage_tolerance" "$color_tolerance" "$monogame_hash" "$fna_hash"
printf 'Template Systems render parity: hashes MonoGame=%s FNA=%s.\n' \
  "$(jq -r .pixelHash "$template_monogame_report")" "$(jq -r .pixelHash "$template_fna_report")"