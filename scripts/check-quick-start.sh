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
xaml_screenshot="$stage_directory/xaml-quick-start.png"
rm -rf \
  "$repository_root/samples/Forma.QuickStart/bin" \
  "$repository_root/samples/Forma.QuickStart/obj" \
  "$repository_root/samples/Forma.QuickStart.$runtime/bin" \
  "$repository_root/samples/Forma.QuickStart.$runtime/obj"
dotnet restore "$project" -p:FormaRuntime="$runtime" -p:Configuration=Release --nologo
dotnet build "$project" --configuration Release -p:FormaRuntime="$runtime" --no-restore --nologo
dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime" --no-build -- \
  --frames 3 --screenshot "$screenshot"
dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime" --no-build -- \
  --xaml --frames 3 --screenshot "$xaml_screenshot"

for image in "$screenshot" "$xaml_screenshot"; do
  signature="$(od -An -tx1 -N8 "$image" | tr -d ' \n')"
  [[ "$signature" == "89504e470d0a1a0a" ]] || {
    printf 'Quick-start screenshot is not a valid PNG: %s\n' "$image" >&2
    exit 1
  }
done

release_output="$repository_root/samples/Forma.QuickStart.$runtime/bin/$runtime/Release/net10.0"
for forbidden in Forma.Xaml.HotReload.dll Forma.Xaml.Compiler.dll XamlX.dll XamlX.IL.Cecil.dll; do
  [[ ! -e "$release_output/$forbidden" ]] || {
    printf 'Release quick start contains development assembly: %s\n' "$forbidden" >&2
    exit 1
  }
done

dotnet restore "$project" -p:FormaRuntime="$runtime" -p:Configuration=Debug --nologo
dotnet build "$project" --configuration Debug -p:FormaRuntime="$runtime" --no-restore --nologo
dotnet run --project "$project" --configuration Debug -p:FormaRuntime="$runtime" --no-build -- \
  --xaml --frames 3

printf 'C# and XAML quick starts passed for %s from an empty package cache.\n' "$runtime"
