#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtime="${FORMA_RUNTIME:-MonoGame}"
stage_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-quick-start.XXXXXX")"
trap 'rm -rf "$stage_directory"' EXIT

case "$runtime" in
  MonoGame) project="$repository_root/samples/Forma.QuickStart.MonoGame/Forma.QuickStart.MonoGame.csproj" ;;
  FNA) project="$repository_root/samples/Forma.QuickStart.FNA/Forma.QuickStart.FNA.csproj" ;;
  *) printf 'FORMA_RUNTIME must be either MonoGame or FNA.\n' >&2; exit 2 ;;
esac

export NUGET_PACKAGES="$stage_directory/packages"
screenshot="$stage_directory/quick-start.png"
dotnet restore "$project" -p:FormaRuntime="$runtime" --nologo
dotnet build "$project" --configuration Release -p:FormaRuntime="$runtime" --no-restore --nologo
dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime" --no-build -- \
  --frames 3 --screenshot "$screenshot"

signature="$(od -An -tx1 -N8 "$screenshot" | tr -d ' \n')"
[[ "$signature" == "89504e470d0a1a0a" ]] || {
  printf 'Quick-start screenshot is not a valid PNG: %s\n' "$screenshot" >&2
  exit 1
}
printf 'Quick start passed for %s from an empty package cache.\n' "$runtime"
