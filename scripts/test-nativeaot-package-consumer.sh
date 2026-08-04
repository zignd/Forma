#!/usr/bin/env bash
set -euo pipefail

# Purpose: Publish and execute isolated trim-only and NativeAOT package consumers on macOS arm64.
# Usage: `make nativeaot`, optionally selecting NATIVEAOT_RUNTIME=MonoGame|FNA,
# NATIVEAOT_PROFILE=core|media|spritefont|dynamic, or NATIVEAOT_MODE=trimmed|aot.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repository_root/Artifacts/nativeaot"
package_root="$artifact_root/packages"
consumer_project="$repository_root/tests/Forma.PackageConsumer/Forma.PackageConsumer.csproj"
rid="osx-arm64"
runtime_selection="${NATIVEAOT_RUNTIME:-}"
profile_selection="${NATIVEAOT_PROFILE:-}"
mode_selection="${NATIVEAOT_MODE:-}"

case "$runtime_selection" in
  ""|MonoGame|FNA) ;;
  *) printf 'NATIVEAOT_RUNTIME must be MonoGame or FNA.\n' >&2; exit 2 ;;
esac
case "$profile_selection" in
  ""|core|media|spritefont|dynamic) ;;
  *) printf 'NATIVEAOT_PROFILE must be core, media, spritefont, or dynamic.\n' >&2; exit 2 ;;
esac
case "$mode_selection" in
  ""|trimmed|aot) ;;
  *) printf 'NATIVEAOT_MODE must be trimmed or aot.\n' >&2; exit 2 ;;
esac

runtimes=(MonoGame FNA)
profiles=(core media spritefont dynamic)
publish_modes=(trimmed aot)
if [[ -n "$runtime_selection" ]]; then runtimes=("$runtime_selection"); fi
if [[ -n "$profile_selection" ]]; then profiles=("$profile_selection"); fi
if [[ -n "$mode_selection" ]]; then publish_modes=("$mode_selection"); fi

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
  printf 'This gate currently validates only macOS arm64; host is %s %s.\n' "$(uname -s)" "$(uname -m)" >&2
  exit 2
fi

rm -rf "$artifact_root" \
  "$repository_root/tests/Forma.PackageConsumer/bin" \
  "$repository_root/tests/Forma.PackageConsumer/obj"
mkdir -p "$package_root"

for runtime in "${runtimes[@]}"; do
  runtime_packages="$package_root/$runtime"
  mkdir -p "$runtime_packages"
  dotnet pack "$repository_root/src/Forma/Forma.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
  dotnet pack "$repository_root/src/Forma.DynamicText/Forma.DynamicText.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
  dotnet pack "$repository_root/src/Forma.Media/Forma.Media.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
  dotnet pack "$repository_root/src/Forma.Xaml.Build/Forma.Xaml.Build.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --output "$runtime_packages" --nologo
done

assert_no_xaml_development_artifacts() {
  local output_dir="$1"
  if find "$output_dir" -type f \( \
    -iname '*.xaml' -o \
    -iname 'XamlX*' -o \
    -iname 'Mono.Cecil*' -o \
    -iname 'Forma.Xaml.Build*' -o \
    -iname 'Forma.Xaml.Compiler*' -o \
    -iname 'Forma.Xaml.HotReload*' \) -print -quit | grep -q .; then
    printf 'Trimmed/AOT output contains a Forma XAML development artifact: %s\n' "$output_dir" >&2
    exit 1
  fi
}

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
  if grep -Eq 'Forma(\.DynamicText|\.Media)?\.dll : warning IL[0-9]+' "$log_file"; then
    printf 'Forma-owned trim/AOT warning in %s:\n' "$log_file" >&2
    grep -E 'Forma(\.DynamicText|\.Media)?\.dll : warning IL[0-9]+' "$log_file" >&2
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

validate_missing_native_font_failure() {
  local output_dir="$1"
  local runtime="$2"
  local probe_dir="$artifact_root/.missing-native-$runtime"
  local failure_log="$artifact_root/diagnostics/$runtime-dynamic-aot-missing-native.txt"
  local removed_libraries
  local exit_code
  rm -rf "$probe_dir"
  mkdir -p "$probe_dir" "$(dirname "$failure_log")"
  cp -R "$output_dir/." "$probe_dir/"
  removed_libraries="$(find "$probe_dir" \( -type f -o -type l \) -iname '*freetype*' -print -delete)"
  if [[ -z "$removed_libraries" ]]; then
    printf 'Dynamic AOT failure probe found no FreeType library in %s.\n' "$output_dir" >&2
    exit 1
  fi
  set +e
  "$probe_dir/Forma.PackageConsumer" >"$failure_log" 2>&1
  exit_code=$?
  set -e
  rm -rf "$probe_dir"
  if [[ "$exit_code" -eq 0 ]]; then
    printf 'Dynamic AOT consumer unexpectedly started without packaged FreeType for %s.\n' "$runtime" >&2
    exit 1
  fi
  grep -Fq 'A native font dependency is unavailable or incompatible.' "$failure_log"
}

for runtime in "${runtimes[@]}"; do
  for profile in "${profiles[@]}"; do
    case "$profile" in
      core)
        profile_args=(-p:IncludeFormaMedia=false -p:IncludeDynamicText=false -p:ExerciseSpriteFont=false)
        ;;
      media)
        profile_args=(-p:IncludeFormaMedia=true -p:IncludeDynamicText=false -p:ExerciseSpriteFont=false)
        ;;
      spritefont)
        profile_args=(-p:IncludeFormaMedia=false -p:IncludeDynamicText=false -p:ExerciseSpriteFont=true)
        ;;
      dynamic)
        profile_args=(-p:IncludeFormaMedia=false -p:IncludeDynamicText=true -p:ExerciseSpriteFont=true)
        ;;
    esac
    for publish_mode in "${publish_modes[@]}"; do
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
      native_manifest="$artifact_root/native-manifests/$runtime-$profile-$publish_mode.txt"
      mkdir -p "$(dirname "$native_manifest")"
      find "$output_dir" -maxdepth 1 -type f \( -name '*.dylib' -o -name '*.so' -o -name '*.dll' \) -print \
        | sed "s|$output_dir/||" | sort > "$native_manifest"
      diagnostics_file="$artifact_root/diagnostics/$runtime-$profile-$publish_mode.txt"
      mkdir -p "$(dirname "$diagnostics_file")"
      if [[ "$profile" == "dynamic" ]]; then
        FORMA_DYNAMIC_TEXT_DIAGNOSTICS="$diagnostics_file" "$output_dir/Forma.PackageConsumer"
        if [[ "$publish_mode" == "aot" && -z "$mode_selection" ]]; then
          jit_diagnostics="$artifact_root/diagnostics/$runtime-$profile-trimmed.txt"
          if ! cmp -s "$jit_diagnostics" "$diagnostics_file"; then
            printf 'JIT/AOT dynamic-text diagnostics differ for %s.\n' "$runtime" >&2
            diff -u "$jit_diagnostics" "$diagnostics_file" >&2 || true
            exit 1
          fi
        fi
      else
        "$output_dir/Forma.PackageConsumer"
      fi
      file "$output_dir/Forma.PackageConsumer" >> "$native_manifest"
      assert_no_xaml_development_artifacts "$output_dir"
      if [[ "$profile" == "dynamic" && "$publish_mode" == "aot" ]]; then
        validate_missing_native_font_failure "$output_dir" "$runtime"
      fi

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

printf 'All selected macOS arm64 trim and NativeAOT package consumers passed.\n'