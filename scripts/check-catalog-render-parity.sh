#!/usr/bin/env bash
set -euo pipefail

# Purpose: Render the shared catalog through both peer hosts and compare deterministic image
# statistics. Exact hashes are diagnostic; aggregate values allow at most 1% rasterizer variance.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-render-parity.XXXXXX")"
monogame_report="$stage_directory/monogame.json"
fna_report="$stage_directory/fna.json"
trap 'rm -rf "$stage_directory"' EXIT

if ! command -v jq >/dev/null; then
  printf 'Catalog render parity requires jq.\n' >&2
  exit 2
fi

dotnet run --project "$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj" \
  --configuration Release \
  -p:FormaRuntime=MonoGame \
  -- \
  --render-output "$monogame_report" \
  --frames 3 \
  --display-scale 2

dotnet run --project "$repository_root/samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj" \
  --configuration Release \
  -p:FormaRuntime=FNA \
  -- \
  --render-output "$fna_report" \
  --frames 3 \
  --display-scale 2

jq -e --slurpfile peer "$fna_report" '
  def relative_difference(left; right):
    ((left - right) | fabs) / ([left, right] | max);
  .width == $peer[0].width and
  .height == $peer[0].height and
  .alphaTotal == $peer[0].alphaTotal and
  relative_difference(.nonBackgroundPixels; $peer[0].nonBackgroundPixels) <= 0.01 and
  relative_difference(.redTotal; $peer[0].redTotal) <= 0.01 and
  relative_difference(.greenTotal; $peer[0].greenTotal) <= 0.01 and
  relative_difference(.blueTotal; $peer[0].blueTotal) <= 0.01
' "$monogame_report" >/dev/null || {
  printf 'Catalog peer render statistics exceeded the 1%% tolerance.\n' >&2
  jq -S . "$monogame_report" >&2
  jq -S . "$fna_report" >&2
  exit 1
}

monogame_hash="$(jq -r .pixelHash "$monogame_report")"
fna_hash="$(jq -r .pixelHash "$fna_report")"
printf 'Catalog render parity: 1440x900 within 1%%; hashes MonoGame=%s FNA=%s.\n' \
  "$monogame_hash" "$fna_hash"