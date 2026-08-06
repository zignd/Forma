#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repo_root/artifacts/svg-comparison"
rm -rf "$artifact_root"
mkdir -p "$artifact_root"

dotnet run --project "$repo_root/tests/Forma.Svg.Benchmark.Skia/Forma.Svg.Benchmark.Skia.csproj" \
    --configuration Release -- --raster-output "$artifact_root/skia"
dotnet run --project "$repo_root/tests/Forma.Svg.Benchmark.ThorVG/Forma.Svg.Benchmark.ThorVG.csproj" \
    --configuration Release -- --raster-output "$artifact_root/thorvg"
dotnet run --project "$repo_root/tests/Forma.Svg.Compare/Forma.Svg.Compare.csproj" \
    --configuration Release -- "$artifact_root/skia" "$artifact_root/thorvg" "$artifact_root/report"