#!/usr/bin/env bash
set -euo pipefail

# Purpose: Capture the pre-template Catalog metrics, screenshots, and deterministic render reports
# for MonoGame and FNA at 1x and 2x. Usage: run this script on a graphical macOS host.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_directory="$repository_root/Artifacts/xaml-templates-baseline"
mkdir -p "$output_directory"

for command_name in jq sips; do
  if ! command -v "$command_name" >/dev/null; then
    printf 'XAML template baseline capture requires %s.\n' "$command_name" >&2
    exit 2
  fi
done

for runtime in MonoGame FNA; do
  runtime_name="$(tr '[:upper:]' '[:lower:]' <<<"$runtime")"
  project="$repository_root/samples/Forma.Catalog.$runtime/Forma.Catalog.$runtime.csproj"

  dotnet build "$project" --configuration Release -p:FormaRuntime="$runtime" --nologo

  for scale in 1 2; do
    metrics="$output_directory/$runtime_name-${scale}x-metrics.json"
    screenshot="$output_directory/$runtime_name-${scale}x.png"
    render_report="$output_directory/$runtime_name-${scale}x-render.json"

    dotnet run \
      --no-build \
      --project "$project" \
      --configuration Release \
      -p:FormaRuntime="$runtime" \
      -- \
      --metrics "$metrics" \
      --screenshot "$screenshot" \
      --render-output "$render_report" \
      --frames 120 \
      --display-scale "$scale"

    jq -e --argjson scale "$scale" '
      .renderedFrames == 120 and
      (.backend | length) > 0 and
      .displayScale == $scale and
      .storyCount > 0 and
      .startupMilliseconds > 0 and
      .steadyStateMeasuredFrames == 119 and
      .steadyStateAllocatedBytes >= 0 and
      .steadyStateAllocatedBytesPerFrame >= 0 and
      .steadyStateTextureBytes > 0
    ' "$metrics" >/dev/null

    jq -e '
      .width > 0 and
      .height > 0 and
      (.pixelHash | length) > 0 and
      .nonBackgroundPixels > 0 and
      .alphaTotal > 0
    ' "$render_report" >/dev/null

    test -s "$screenshot"
    expected_width="$(jq -r .physicalViewportWidth "$metrics")"
    expected_height="$(jq -r .physicalViewportHeight "$metrics")"
    actual_width="$(sips -g pixelWidth "$screenshot" | awk '/pixelWidth/ { print $2 }')"
    actual_height="$(sips -g pixelHeight "$screenshot" | awk '/pixelHeight/ { print $2 }')"
    if [[ "$actual_width" != "$expected_width" || "$actual_height" != "$expected_height" ]]; then
      printf '%s has dimensions %sx%s; expected %sx%s.\n' \
        "$screenshot" "$actual_width" "$actual_height" "$expected_width" "$expected_height" >&2
      exit 1
    fi
  done
done

printf 'XAML template baseline: captured MonoGame and FNA metrics, screenshots, and render reports at 1x and 2x.\n'