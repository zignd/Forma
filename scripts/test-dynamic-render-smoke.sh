#!/usr/bin/env bash
# Purpose: Run Alpha8, warm-cache, and device-reset graphics assertions on the process main thread.
# Usage: `bash scripts/test-dynamic-render-smoke.sh` from any directory.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repository_root/tests/Forma.RenderSmoke/Forma.RenderSmoke.csproj"
output_directory="$(mktemp -d)"
trap 'rm -rf "$output_directory"' EXIT

dotnet run --project "$project" -p:FormaRuntime=MonoGame | tee "$output_directory/monogame.log"
dotnet run --project "$project" -p:FormaRuntime=FNA | tee "$output_directory/fna.log"

monogame_svg="$(grep '^SVG scale hashes:' "$output_directory/monogame.log")"
fna_svg="$(grep '^SVG scale hashes:' "$output_directory/fna.log")"
if [[ "$monogame_svg" != "$fna_svg" ]]; then
	printf 'MonoGame and FNA SVG scale pixels differ.\nMonoGame: %s\nFNA: %s\n' "$monogame_svg" "$fna_svg" >&2
	exit 1
fi