#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
api_project="$repository_root/tools/Forma.ApiInventory/Forma.ApiInventory.csproj"
api_tool="$repository_root/tools/Forma.ApiInventory/bin/Release/net10.0/Forma.ApiInventory.dll"
forma_assembly="$repository_root/src/Forma/bin/Release/net10.0/Forma.dll"
media_assembly="$repository_root/src/Forma.Media/bin/Release/net10.0/Forma.Media.dll"
baseline="$repository_root/docs/api-baseline.txt"
approved="$repository_root/docs/api-core.approved.txt"
approved_media="$repository_root/docs/api-media.approved.txt"
approved_delta="$repository_root/docs/api-compatibility.diff"

dotnet build "$repository_root/src/Forma/Forma.csproj" --configuration Release --nologo
dotnet build "$repository_root/src/Forma.Media/Forma.Media.csproj" --configuration Release --nologo
dotnet build "$api_project" --configuration Release --nologo

global_packages="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: //p')"
monogame_assembly="$global_packages/monogame.framework.desktopgl/3.8.5/lib/net8.0/MonoGame.Framework.dll"
if [[ ! -f "$monogame_assembly" ]]; then
  printf 'MonoGame dependency not found: %s\n' "$monogame_assembly" >&2
  exit 2
fi

stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-api.XXXXXX")"
trap 'rm -rf "$stage_directory"' EXIT
cp "$forma_assembly" "$media_assembly" "$monogame_assembly" "$stage_directory"

actual_api="$stage_directory/api-current.txt"
(
  cd "$stage_directory"
  dotnet "$api_tool" Forma.dll Forma > "$actual_api"
)

if ! cmp -s "$approved" "$actual_api"; then
  printf 'Forma public API differs from the approved core inventory.\n' >&2
  diff -u -L 'Approved Forma core' -L 'Current Forma core' "$approved" "$actual_api" || true
  exit 1
fi

actual_media_api="$stage_directory/api-media-current.txt"
(
  cd "$stage_directory"
  dotnet "$api_tool" Forma.Media.dll Forma > "$actual_media_api"
)
if ! cmp -s "$approved_media" "$actual_media_api"; then
  printf 'Forma.Media public API differs from its approved inventory.\n' >&2
  diff -u -L 'Approved Forma.Media' -L 'Current Forma.Media' \
    "$approved_media" "$actual_media_api" || true
  exit 1
fi

baseline_copy="$stage_directory/Phase 0 normalized baseline"
approved_copy="$stage_directory/Approved Forma core"
raw_delta="$stage_directory/api-compatibility.raw.diff"
actual_delta="$stage_directory/api-compatibility.diff"
cp "$baseline" "$baseline_copy"
cp "$approved" "$approved_copy"
delta_status=0
(
  cd "$stage_directory"
  git diff --no-index --text --no-prefix --unified=0 \
    'Phase 0 normalized baseline' 'Approved Forma core'
) > "$raw_delta" || delta_status=$?
tail -n +3 "$raw_delta" |
  sed -e '1,2s/[[:space:]]*$//' > "$actual_delta"
if ((delta_status != 1)) || ! cmp -s "$approved_delta" "$actual_delta"; then
  printf 'The documented baseline-to-core API delta is stale.\n' >&2
  diff -u "$approved_delta" "$actual_delta" || true
  exit 1
fi

baseline_types="$(grep -Ec '^    public (sealed |abstract |readonly )?(class|enum|struct|interface|delegate)' "$baseline")"
core_types="$(grep -Ec '^    public (sealed |abstract |readonly )?(class|enum|struct|interface|delegate)' "$approved")"
printf 'API compatibility: %s baseline types, %s core types; only VideoStreamPlayer is excluded.\n' \
  "$baseline_types" "$core_types"