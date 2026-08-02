#!/usr/bin/env bash
set -euo pipefail

# Purpose: Capture the canonical pre-dynamic-text MonoGame catalog screenshots and metrics at 1x
# and 2x. Usage: `bash scripts/capture-dynamic-text-baseline.sh` on a graphical desktop host.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj"
output_directory="$repository_root/docs/baselines"
mkdir -p "$output_directory"

if ! command -v jq >/dev/null; then
  printf 'Dynamic text baseline capture requires jq.\n' >&2
  exit 2
fi

dotnet build "$project" --configuration Release -p:FormaRuntime=MonoGame --nologo

for scale in 1 2; do
  metrics="$output_directory/dynamic-text-before-${scale}x.json"
  screenshot="$output_directory/dynamic-text-before-${scale}x.png"
  dotnet run \
    --no-build \
    --project "$project" \
    --configuration Release \
    -p:FormaRuntime=MonoGame \
    -- \
    --metrics "$metrics" \
    --screenshot "$screenshot" \
    --frames 120 \
    --display-scale "$scale"

  jq -e --argjson scale "$scale" '
    .renderedFrames == 120 and
    .displayScale == $scale and
    .startupMilliseconds > 0 and
    .steadyStateMeasuredFrames == 119 and
    .steadyStateAllocatedBytes >= 0 and
    .steadyStateAllocatedBytesPerFrame >= 0 and
    .fontXnbBytes > 0 and
    .spriteFontTextureBytes > 0 and
    .steadyStateTextureBytes > .spriteFontTextureBytes
  ' "$metrics" >/dev/null
  test -s "$screenshot"
done

printf 'Dynamic text baseline: captured 1x and 2x screenshots plus 120-frame metrics.\n'