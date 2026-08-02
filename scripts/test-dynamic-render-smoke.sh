#!/usr/bin/env bash
# Purpose: Run Alpha8, warm-cache, and device-reset graphics assertions on the process main thread.
# Usage: `bash scripts/test-dynamic-render-smoke.sh` from any directory.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repository_root/tests/Forma.RenderSmoke/Forma.RenderSmoke.csproj"

dotnet run --project "$project" -p:FormaRuntime=MonoGame
dotnet run --project "$project" -p:FormaRuntime=FNA