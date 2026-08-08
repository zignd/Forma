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
screenshot_directory="${FORMA_SCREENSHOT_OUTPUT:-$stage_directory/screenshots}"
mkdir -p "$screenshot_directory"
rm -rf \
  "$repository_root/samples/Forma.QuickStart/bin" \
  "$repository_root/samples/Forma.QuickStart/obj" \
  "$repository_root/samples/Forma.QuickStart.$runtime/bin" \
  "$repository_root/samples/Forma.QuickStart.$runtime/obj"
dotnet restore "$project" -p:FormaRuntime="$runtime" -p:Configuration=Release --nologo
dotnet build "$project" --configuration Release -p:FormaRuntime="$runtime" --no-restore --nologo
example_names=(
  csharp xaml settings-form responsive-hud inventory-list dialog-workflow data-grid theme-control
  dynamic-text runtime-svg
)
example_selectors=(
  "" --xaml --settings-form --responsive-hud --inventory-list --dialog-workflow --data-grid
  --theme-control --dynamic-text --runtime-svg
)
for ((index = 0; index < ${#example_names[@]}; index++)); do
  image="$screenshot_directory/${example_names[$index]}-$runtime.png"
  selector="${example_selectors[$index]}"
  frames=3
  [[ "$selector" == "--runtime-svg" ]] && frames=6
  if [[ -n "$selector" ]]; then
    dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime" --no-build -- \
      "$selector" --frames "$frames" --screenshot "$image"
  else
    dotnet run --project "$project" --configuration Release -p:FormaRuntime="$runtime" --no-build -- \
      --frames "$frames" --screenshot "$image"
  fi
  signature="$(od -An -tx1 -N8 "$image" | tr -d ' \n')"
  [[ "$signature" == "89504e470d0a1a0a" ]] || {
    printf 'Quick-start screenshot is not a valid PNG: %s\n' "$image" >&2
    exit 1
  }
  [[ "$(wc -c < "$image")" -gt 1024 ]] || {
    printf 'Quick-start screenshot is unexpectedly small: %s\n' "$image" >&2
    exit 1
  }
done

target_framework="$(dotnet msbuild "$project" -getProperty:TargetFramework -p:FormaRuntime="$runtime" --nologo)"
release_output="$repository_root/samples/Forma.QuickStart.$runtime/bin/$runtime/Release/$target_framework"
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

printf 'Quick starts and focused examples passed for %s from an empty package cache.\n' "$runtime"
