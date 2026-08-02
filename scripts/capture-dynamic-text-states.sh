#!/usr/bin/env bash
# Purpose: Capture post-migration dynamic text states for both peer catalog hosts.
# Usage: `bash scripts/capture-dynamic-text-states.sh` on a graphical desktop host.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_directory="$repository_root/docs/baselines/dynamic-text-after"
mkdir -p "$output_directory"

if ! command -v jq >/dev/null; then
  printf 'Dynamic text state capture requires jq.\n' >&2
  exit 2
fi
if ! command -v sips >/dev/null; then
  printf 'Dynamic text state capture requires sips on macOS.\n' >&2
  exit 2
fi

states=(
  $'desktop-1x\tDynamic Sizes\t1\t1440\t900'
  $'retina-2x\tFallback Chain\t2\t1440\t900'
  $'narrow\tWrapping and Selection\t1\t720\t900'
  $'rtl\tBidirectional Text\t1\t1000\t700'
)

for runtime in MonoGame FNA; do
  project="$repository_root/samples/Forma.Catalog.$runtime/Forma.Catalog.$runtime.csproj"
  dotnet build "$project" --configuration Release -p:FormaRuntime="$runtime" --nologo
  runtime_name="$(tr '[:upper:]' '[:lower:]' <<<"$runtime")"
  for state in "${states[@]}"; do
    IFS=$'\t' read -r state_name story_name scale width height <<<"$state"
    metrics="$output_directory/$runtime_name-$state_name.json"
    screenshot="$output_directory/$runtime_name-$state_name.png"
    dotnet run \
      --no-build \
      --project "$project" \
      --configuration Release \
      -p:FormaRuntime="$runtime" \
      -- \
      --metrics "$metrics" \
      --screenshot "$screenshot" \
      --frames 8 \
      --display-scale "$scale" \
      --viewport-width "$width" \
      --viewport-height "$height" \
      --story "$story_name"

    jq -e --arg story "$story_name" --argjson scale "$scale" --argjson width "$width" --argjson height "$height" '
      .renderedFrames == 8 and
      .displayScale == $scale and
      .selectedStory == $story and
      .physicalViewportWidth == $width and
      .physicalViewportHeight == $height
    ' "$metrics" >/dev/null
    test -s "$screenshot"
    actual_width="$(sips -g pixelWidth "$screenshot" | awk '/pixelWidth/ { print $2 }')"
    actual_height="$(sips -g pixelHeight "$screenshot" | awk '/pixelHeight/ { print $2 }')"
    if [[ "$actual_width" != "$width" || "$actual_height" != "$height" ]]; then
      printf '%s has dimensions %sx%s; expected %sx%s.\n' "$screenshot" "$actual_width" "$actual_height" "$width" "$height" >&2
      exit 1
    fi
  done
done

printf 'Dynamic text states: captured desktop 1x, Retina 2x, narrow, and RTL screenshots for both peers.\n'