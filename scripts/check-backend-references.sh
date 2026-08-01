#!/usr/bin/env bash
set -euo pipefail

# Purpose: Compile Forma.Media against each supported MonoGame backend package to catch incompatible
# API references. Usage: `bash scripts/check-backend-references.sh` from any directory.
# Release build artifacts are isolated in a temporary directory and removed when the script exits.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifacts_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-backends.XXXXXX")"
trap 'rm -rf "$artifacts_directory"' EXIT

for backend in DesktopGL WindowsDX Native; do
  dotnet build "$repository_root/src/Forma.Media/Forma.Media.csproj" \
    --configuration Release \
    --artifacts-path "$artifacts_directory/$backend" \
    -p:MonoGamePlatform="$backend" \
    --nologo
  printf 'Backend reference: %s succeeded.\n' "$backend"
done