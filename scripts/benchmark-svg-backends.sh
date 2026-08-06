#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet run --project "$repo_root/tests/Forma.Svg.Benchmark.Skia/Forma.Svg.Benchmark.Skia.csproj" --configuration Release --no-launch-profile
dotnet run --project "$repo_root/tests/Forma.Svg.Benchmark.ThorVG/Forma.Svg.Benchmark.ThorVG.csproj" --configuration Release --no-launch-profile