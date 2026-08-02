#!/usr/bin/env bash
set -euo pipefail

# Purpose: Publish and execute isolated trim-only and NativeAOT package consumers on macOS arm64.
# Usage: `bash scripts/test-nativeaot-package-consumer.sh` from any directory.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repository_root/Artifacts/nativeaot"
package_root="$artifact_root/packages"
consumer_project="$repository_root/tests/Forma.PackageConsumer/Forma.PackageConsumer.csproj"
rid="osx-arm64"

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
  printf 'This gate currently validates only macOS arm64; host is %s %s.\n' "$(uname -s)" "$(uname -m)" >&2
  exit 2
fi

rm -rf "$artifact_root" \
  "$repository_root/tests/Forma.PackageConsumer/bin" \
  "$repository_root/tests/Forma.PackageConsumer/obj"
mkdir -p "$package_root"

for runtime in MonoGame FNA; do
  runtime_packages="$package_root/$runtime"
  mkdir -p "$runtime_packages"
  dotnet pack "$repository_root/src/Forma/Forma.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
  dotnet pack "$repository_root/src/Forma.DynamicText/Forma.DynamicText.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
done

stage_fna_native_aliases() {
  local output_dir="$1"
  local versioned_name
  local alias_name
  while IFS=' ' read -r versioned_name alias_name; do
    if [[ -f "$output_dir/$versioned_name" ]]; then
      ln -sf "$versioned_name" "$output_dir/$alias_name"
    fi
  done <<'ALIASES'
libSDL3.0.dylib libSDL3.dylib
libFNA3D.0.dylib libFNA3D.dylib
libFAudio.0.dylib libFAudio.dylib
libdav1dfile.1.dylib libdav1dfile.dylib
ALIASES
}

validate_warnings() {
  local log_file="$1"
  local warning_line
  if grep -Eq 'Forma(\.DynamicText)?\.dll : warning IL[0-9]+' "$log_file"; then
    printf 'Forma-owned trim/AOT warning in %s:\n' "$log_file" >&2
    grep -E 'Forma(\.DynamicText)?\.dll : warning IL[0-9]+' "$log_file" >&2
    exit 1
  fi
  while IFS= read -r warning_line; do
    if [[ "$warning_line" != *"FNA.NET.dll : warning IL2104:"* && \
          "$warning_line" != *"MonoGame.Framework.dll : warning IL2104:"* ]]; then
      printf 'Unclassified trim/AOT warning in %s:\n%s\n' "$log_file" "$warning_line" >&2
      exit 1
    fi
  done < <(grep -E 'warning IL[0-9]+' "$log_file" || true)
}

for runtime in MonoGame FNA; do
  for profile in core spritefont dynamic; do
    case "$profile" in
      core)
        profile_args=(-p:IncludeDynamicText=false -p:ExerciseSpriteFont=false)
        ;;
      spritefont)
        profile_args=(-p:IncludeDynamicText=false -p:ExerciseSpriteFont=true)
        ;;
      dynamic)
        profile_args=(-p:IncludeDynamicText=true -p:ExerciseSpriteFont=true)
        ;;
    esac
    for publish_mode in trimmed aot; do
      cache_dir="$artifact_root/cache/$runtime/$profile-$publish_mode"
      output_dir="$artifact_root/publish/$runtime/$profile-$publish_mode"
      log_file="$artifact_root/logs/$runtime-$profile-$publish_mode.log"
      rm -rf "$cache_dir" "$output_dir" \
        "$repository_root/tests/Forma.PackageConsumer/bin" \
        "$repository_root/tests/Forma.PackageConsumer/obj"
      mkdir -p "$output_dir" "$(dirname "$log_file")"

      if [[ "$publish_mode" == "aot" ]]; then
        mode_args=(-p:PublishAot=true -p:EnableAotAnalyzer=true)
      else
        mode_args=(-p:PublishTrimmed=true -p:TrimMode=link)
      fi
      common_args=(
        -r "$rid"
        -p:FormaRuntime="$runtime"
        -p:IncludeFormaMedia=false
        -p:FormaPackageSource="$package_root/$runtime"
        -p:SelfContained=true
        -p:EnableTrimAnalyzer=true
      )

      NUGET_PACKAGES="$cache_dir" dotnet restore "$consumer_project" \
        "${common_args[@]}" "${profile_args[@]}" "${mode_args[@]}" \
        -p:RestoreNoCache=true --nologo
      NUGET_PACKAGES="$cache_dir" dotnet publish "$consumer_project" \
        --configuration Release "${common_args[@]}" "${profile_args[@]}" "${mode_args[@]}" \
        -p:PublishDir="$output_dir" --no-restore --nologo 2>&1 | tee "$log_file"

      validate_warnings "$log_file"
      file "$output_dir/Forma.PackageConsumer" | grep -Fq 'Mach-O 64-bit executable arm64'
      if [[ "$runtime" == "FNA" ]]; then
        stage_fna_native_aliases "$output_dir"
      fi
      "$output_dir/Forma.PackageConsumer"

      if [[ "$profile" != "dynamic" ]]; then
        if find "$output_dir" -type f \( -iname '*freetype*' -o -iname '*harfbuzz*' \) -print -quit | grep -q .; then
          printf 'Native-free %s/%s/%s output contains a dynamic-text native library.\n' \
            "$runtime" "$profile" "$publish_mode" >&2
          exit 1
        fi
        bash "$repository_root/scripts/inspect-native-imports.sh" "$output_dir"
      fi
      printf 'Passed %s / %s / %s.\n' "$runtime" "$profile" "$publish_mode"
    done
  done
done

printf 'All macOS arm64 trim and NativeAOT package consumers passed.\n'