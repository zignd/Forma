#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest_path="$repository_root/scripts/release-packages.json"
output_root="${FORMA_RELEASE_PACKAGE_DIR:-$repository_root/Artifacts/release-packages}"
configuration="${CONFIGURATION:-Release}"
validate_only="${FORMA_RELEASE_VALIDATE_ONLY:-false}"

command -v jq >/dev/null || { printf 'jq is required to read %s.\n' "$manifest_path" >&2; exit 1; }

discovered_manifest="$(mktemp)"
declared_manifest="$(mktemp)"
trap 'rm -f "$discovered_manifest" "$declared_manifest"' EXIT

for project_path in "$repository_root"/src/*/*.csproj; do
  project="${project_path#"$repository_root/"}"
  for runtime in MonoGame FNA; do
    is_packable="$(dotnet msbuild "$project_path" -p:FormaRuntime="$runtime" -getProperty:IsPackable -nologo)"
    if [[ "$is_packable" == "true" || "$is_packable" == "True" ]]; then
      package_id="$(dotnet msbuild "$project_path" -p:FormaRuntime="$runtime" -getProperty:PackageId -nologo)"
      printf '%s\t%s\t%s\n' "$project" "$runtime" "$package_id"
    fi
  done
done | sort -u >"$discovered_manifest"

jq -r '(.packages + .excludedPackages)[] | [.project, .runtime, .id] | @tsv' \
  "$manifest_path" | sort >"$declared_manifest"
if ! diff -u "$discovered_manifest" "$declared_manifest"; then
  printf 'Release manifest and explicit exclusions do not cover every packable source/runtime peer.\n' >&2
  exit 1
fi

version="$(dotnet msbuild "$repository_root/src/Forma/Forma.csproj" -p:FormaRuntime=MonoGame -getProperty:Version -nologo)"
[[ "$version" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?(\+[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$ ]] || {
  printf 'Forma version is not SemVer-compatible: %s\n' "$version" >&2
  exit 1
}

if [[ "$validate_only" != "true" ]]; then
  rm -rf "$output_root"
  mkdir -p "$output_root"
else
  [[ -d "$output_root" ]] || { printf 'Release package directory does not exist: %s\n' "$output_root" >&2; exit 1; }
fi

while IFS=$'\t' read -r project runtime package_id; do
  evaluated_id="$(dotnet msbuild "$repository_root/$project" -p:FormaRuntime="$runtime" -getProperty:PackageId -nologo)"
  evaluated_version="$(dotnet msbuild "$repository_root/$project" -p:FormaRuntime="$runtime" -getProperty:Version -nologo)"
  [[ "$evaluated_id" == "$package_id" ]] || {
    printf 'Manifest package ID mismatch for %s (%s): expected %s, got %s.\n' \
      "$project" "$runtime" "$package_id" "$evaluated_id" >&2
    exit 1
  }
  [[ "$evaluated_version" == "$version" ]] || {
    printf 'Manifest version mismatch for %s: expected %s, got %s.\n' \
      "$package_id" "$version" "$evaluated_version" >&2
    exit 1
  }

  if [[ "$validate_only" != "true" ]]; then
    dotnet pack "$repository_root/$project" --configuration "$configuration" \
      -p:FormaRuntime="$runtime" -p:PackageOutputPath="$output_root" --nologo
  fi

done < <(jq -r '.packages[] | [.project, .runtime, .id] | @tsv' "$manifest_path")

expected_paths="$output_root/.expected-packages"
actual_paths="$output_root/.actual-packages"
{
  jq -r --arg version "$version" '.packages[].id + "." + $version + ".nupkg"' "$manifest_path"
  jq -r --arg version "$version" '.packages[] | select(.symbols != false) | .id + "." + $version + ".snupkg"' "$manifest_path"
} | sort >"$expected_paths"
find "$output_root" -maxdepth 1 -type f \( -name '*.nupkg' -o -name '*.snupkg' \) -exec basename {} \; |
  sort >"$actual_paths"

if ! diff -u "$expected_paths" "$actual_paths"; then
  printf 'Release package output does not exactly match the approved manifest.\n' >&2
  exit 1
fi

while IFS=$'\t' read -r _ _ package_id; do
  package_path="$output_root/$package_id.$version.nupkg"
  nuspec="$(unzip -p "$package_path" "$package_id.nuspec")"
  grep -Fq "<version>$version</version>" <<<"$nuspec"
  grep -Fq 'repository type="git" url="https://github.com/zigrok/Forma"' <<<"$nuspec"
done < <(jq -r '.packages[] | [.project, .runtime, .id] | @tsv' "$manifest_path")

for runtime in MonoGame FNA; do
  package_id="Forma.Xaml.HotReload.$runtime"
  package_path="$output_root/$package_id.$version.nupkg"
  entries="$(unzip -Z1 "$package_path")"
  nuspec="$(unzip -p "$package_path" "$package_id.nuspec")"
  for required_entry in \
    lib/net10.0/Forma.Xaml.HotReload.dll \
    lib/net10.0/Forma.Xaml.Compiler.dll \
    lib/net10.0/XamlX.dll \
    lib/net10.0/XamlX.IL.Cecil.dll \
    licenses/XamlX/LICENSE; do
    grep -Fxq "$required_entry" <<<"$entries"
  done
  grep -Fq 'dependency id="Mono.Cecil" version="0.11.6"' <<<"$nuspec"
  if grep -Fq 'dependency id="Forma.Xaml.Compiler"' <<<"$nuspec"; then
    printf '%s must embed the private compiler instead of depending on an unpublished package.\n' "$package_id" >&2
    exit 1
  fi
done

for runtime in MonoGame FNA; do
  package_id="Forma.Svg.ThorVG.$runtime"
  package_path="$output_root/$package_id.$version.nupkg"
  entries="$(unzip -Z1 "$package_path")"
  for required_entry in \
    runtimes/linux-x64/native/libforma_thorvg.so \
    runtimes/osx-arm64/native/libforma_thorvg.dylib; do
    grep -Fxq "$required_entry" <<<"$entries" || {
      printf '%s is missing required native release asset %s.\n' "$package_id" "$required_entry" >&2
      exit 1
    }
  done
  if unzip -p "$package_path" "$package_id.nuspec" | grep -Fq 'SkiaSharp'; then
    printf '%s must not depend on SkiaSharp.\n' "$package_id" >&2
    exit 1
  fi
done

if [[ "$validate_only" != "true" ]]; then
for runtime in MonoGame FNA; do
  consumer_cache="$output_root/.hot-reload-consumer/$runtime/packages"
  consumer_output="$output_root/.hot-reload-consumer/$runtime/output"
  rm -rf "$consumer_cache" "$consumer_output" \
    "$repository_root/tests/Forma.Xaml.HotReload.PackageConsumer/bin" \
    "$repository_root/tests/Forma.Xaml.HotReload.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet run \
    --project "$repository_root/tests/Forma.Xaml.HotReload.PackageConsumer/Forma.Xaml.HotReload.PackageConsumer.csproj" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:FormaPackageSource="$output_root" \
    -p:BaseOutputPath="$consumer_output/" \
    --nologo
done
fi

rm "$expected_paths" "$actual_paths"
printf 'Validated %s release packages and symbol packages at version %s in %s.\n' \
  "$(jq '.packages | length' "$manifest_path")" "$version" "$output_root"
