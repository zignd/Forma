#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-clean.XXXXXX")"
trap 'rm -rf "$stage_directory"' EXIT

(
  cd "$repository_root"
  git ls-files --cached --others --exclude-standard -z |
    tar --null -T - -cf -
) | tar -xf - -C "$stage_directory"

dotnet tool restore --tool-manifest "$stage_directory/.config/dotnet-tools.json"
dotnet restore "$stage_directory/Forma.slnx" --disable-parallel
dotnet build "$stage_directory/Forma.slnx" \
  --configuration Release \
  --no-restore \
  --nologo \
  --maxcpucount:1
dotnet test "$stage_directory/tests/Forma.Tests/Forma.Tests.csproj" \
  --configuration Release \
  --no-build \
  --nologo

printf 'Clean source validation succeeded without sibling repository output.\n'