#!/usr/bin/env bash
set -euo pipefail

# Purpose: Play the repository Theora fixture through Forma.Media and FNA until completion.
# Usage: `bash scripts/check-fna-video-smoke.sh` from any directory on a graphical desktop host.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture="$repository_root/tests/Assets/Video/forma-video-smoke.ogv"

if [[ ! -f "$fixture" ]]; then
  printf 'FNA video fixture is missing: %s\n' "$fixture" >&2
  exit 2
fi

dotnet run \
  --project "$repository_root/tests/Forma.FnaVideoSmoke/Forma.FnaVideoSmoke.csproj" \
  --configuration Release \
  -p:FormaRuntime=FNA \
  -- \
  "$fixture"