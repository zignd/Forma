#!/usr/bin/env bash
set -euo pipefail

# Purpose: Verify bounded native-font loader failures in fresh processes for both peer runtimes.
# Usage: `bash scripts/check-native-font-failures.sh` from any directory.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
probe_project="$repository_root/tests/Forma.NativeFontFailureProbe/Forma.NativeFontFailureProbe.csproj"

for runtime in MonoGame FNA; do
  dotnet build "$probe_project" --configuration Release -p:FormaRuntime="$runtime" --nologo
  output_dir="$repository_root/tests/Forma.NativeFontFailureProbe/bin/$runtime/Release/net10.0"
  probe="$output_dir/Forma.NativeFontFailureProbe.dll"
  if [[ "$(uname -s)" == "Darwin" ]]; then
    native_name="libfreetype.dylib"
    rejected_library="$(find "$output_dir/runtimes/osx/native" -type f -name 'libHarfBuzzSharp.dylib' -print -quit)"
  elif [[ "$(uname -s)" == "Linux" ]]; then
    native_name="libfreetype.so"
    rejected_library="$(find "$output_dir/runtimes" -type f -name 'libHarfBuzzSharp.so' -print -quit)"
  else
    native_name="freetype.dll"
    rejected_library="$(find "$output_dir/runtimes" -type f -name 'libHarfBuzzSharp.dll' -print -quit)"
  fi
  test -f "$rejected_library"
  find "$output_dir" -type f -name "$native_name" -delete
  for failure in missing wrong-architecture rejected; do
    rm -f "$output_dir/$native_name"
    if [[ "$failure" == "wrong-architecture" ]]; then
      cp "$probe" "$output_dir/$native_name"
    elif [[ "$failure" == "rejected" ]]; then
      cp "$rejected_library" "$output_dir/$native_name"
    fi
    dotnet "$probe" "$failure"
  done
done

printf 'Native font failure matrix passed for MonoGame and FNA.\n'