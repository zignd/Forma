#!/usr/bin/env bash
set -euo pipefail

# Purpose: Pack and inspect all peer artifacts, run isolated consumers from empty package caches,
# and prove mixed runtime variants fail before reference resolution.
# Usage: `bash scripts/test-package-consumer.sh` from any directory.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_root="$repository_root/Artifacts/packages"
repro_root="$repository_root/Artifacts/package-repro"
consumer_project="$repository_root/tests/Forma.PackageConsumer/Forma.PackageConsumer.csproj"
version="0.1.0-alpha.1"
commit_sha="$(git -C "$repository_root" rev-parse HEAD)"

rm -rf "$package_root" "$repro_root" \
  "$repository_root/tests/Forma.PackageConsumer/bin" \
  "$repository_root/tests/Forma.PackageConsumer/obj"
dotnet tool restore
dotnet build "$repository_root/tools/Forma.AssemblyInspector/Forma.AssemblyInspector.csproj" --configuration Release --nologo
assembly_inspector="$repository_root/tools/Forma.AssemblyInspector/bin/MonoGame/Release/net10.0/Forma.AssemblyInspector.dll"

for runtime in MonoGame FNA; do
  if [[ "$runtime" == "MonoGame" ]]; then
    catalog_project="$repository_root/samples/Forma.Catalog.MonoGame/Forma.Catalog.MonoGame.csproj"
  else
    catalog_project="$repository_root/samples/Forma.Catalog.FNA/Forma.Catalog.FNA.csproj"
  fi
  dotnet build "$catalog_project" --configuration Release -p:FormaRuntime="$runtime" --nologo
  for font_name in Catalog CatalogCode; do
    cmp \
      "$repository_root/tests/Assets/Fonts/$font_name.xnb" \
      "$repository_root/samples/Forma.Catalog.$runtime/bin/$runtime/Release/net10.0/Content/Fonts/$font_name.xnb"
  done
done

for runtime in MonoGame FNA; do
  dotnet pack "$repository_root/src/Forma/Forma.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --nologo
  dotnet pack "$repository_root/src/Forma.DynamicText/Forma.DynamicText.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --nologo
  dotnet pack "$repository_root/src/Forma.Media/Forma.Media.csproj" \
    --configuration Release -p:FormaRuntime="$runtime" --nologo
done

inspect_package() {
  local runtime="$1"
  local package_id="$2"
  local assembly_name="$3"
  local guard_name="$4"
  local package_path="$package_root/$runtime/$package_id.$version.nupkg"
  local symbol_path="$package_root/$runtime/$package_id.$version.snupkg"
  local entries

  entries="$(unzip -Z1 "$package_path")"
  for required_entry in \
    "lib/net10.0/$assembly_name.dll" \
    "lib/net10.0/$assembly_name.xml" \
    "buildTransitive/$guard_name" \
    LICENSE \
    NOTICE.md \
    README.md \
    THIRD-PARTY-NOTICES.md; do
    grep -Fxq "$required_entry" <<<"$entries"
  done

  if [[ "$assembly_name" == "Forma" ]]; then
    assembly_bytes="$(unzip -p "$package_path" "lib/net10.0/$assembly_name.dll" | wc -c | tr -d ' ')"
    if (( assembly_bytes > 2 * 1024 * 1024 )); then
      printf '%s exceeds the 2 MiB core managed assembly budget (%s bytes).\n' "$package_id" "$assembly_bytes" >&2
      exit 1
    fi
    grep -Fxq "licenses/theme-icons/LICENSE.Godot.txt" <<<"$entries"
    strings_output="$(unzip -p "$package_path" "lib/net10.0/$assembly_name.dll" | strings)"
    for resource_name in theme-icons-1x.png theme-icons-2x.png theme-icons.json; do
      grep -Fq "Forma.ThemeIcons.$resource_name" <<<"$strings_output"
    done
  fi

  unzip -p "$package_path" "$package_id.nuspec" |
    grep -Fq "repository type=\"git\" url=\"https://github.com/zignd/Forma\" commit=\"$commit_sha\""
  unzip -p "$symbol_path" "lib/net10.0/$assembly_name.pdb" |
    strings |
    grep -F "raw.githubusercontent.com/zignd/Forma/$commit_sha" >/dev/null
}

inspect_package MonoGame Forma.MonoGame Forma Forma.MonoGame.targets
inspect_package MonoGame Forma.Media.MonoGame Forma.Media Forma.Media.MonoGame.targets
inspect_package FNA Forma.FNA Forma Forma.FNA.targets
inspect_package FNA Forma.Media.FNA Forma.Media Forma.Media.FNA.targets

inspect_dynamic_package() {
  local runtime="$1"
  local opposite_runtime="$2"
  local package_id="Forma.DynamicText.$runtime"
  local package_path="$package_root/$runtime/$package_id.$version.nupkg"
  local symbol_path="$package_root/$runtime/$package_id.$version.snupkg"
  local entries
  local manifest

  entries="$(unzip -Z1 "$package_path")"
  for required_entry in lib/net10.0/Forma.DynamicText.dll lib/net10.0/Forma.DynamicText.xml "buildTransitive/$package_id.targets" buildTransitive/Forma.DynamicText.PackageInitializer.cs.txt buildTransitive/assets/Inter_Regular.ttf licenses/fonts/LICENSE.Inter.txt docs/dynamic-text.md examples/DynamicTextQuickStart.cs LICENSE NOTICE.md README.md THIRD-PARTY-NOTICES.md; do
    grep -Fxq "$required_entry" <<<"$entries"
  done
  assembly_bytes="$(unzip -p "$package_path" lib/net10.0/Forma.DynamicText.dll | wc -c | tr -d ' ')"
  if (( assembly_bytes > 256 * 1024 )); then
    printf '%s exceeds the 256 KiB dynamic-text managed assembly budget (%s bytes).\n' "$package_id" "$assembly_bytes" >&2
    exit 1
  fi
  unzip -p "$symbol_path" lib/net10.0/Forma.DynamicText.pdb |
    strings |
    grep -F "raw.githubusercontent.com/zignd/Forma/$commit_sha" >/dev/null
  manifest="$(unzip -p "$package_path" "$package_id.nuspec")"
  for dependency in "Forma.$runtime" FreeTypeSharp HarfBuzzSharp HarfBuzzSharp.NativeAssets.Linux; do
    grep -Fq "dependency id=\"$dependency\"" <<<"$manifest"
  done
  if grep -Fq "dependency id=\"Forma.$opposite_runtime\"" <<<"$manifest"; then
    printf '%s must not depend on Forma.%s.\n' "$package_id" "$opposite_runtime" >&2
    exit 1
  fi
}

inspect_dynamic_package MonoGame FNA
inspect_dynamic_package FNA MonoGame

for runtime in MonoGame FNA; do
  dotnet "$assembly_inspector" forbid-references \
    "$repository_root/src/Forma/bin/$runtime/Release/net10.0/Forma.dll" \
    FreeTypeSharp HarfBuzzSharp
done

package_fingerprint() {
  local package_path="$1"
  while IFS= read -r entry; do
    case "$entry" in
      _rels/.rels|package/services/metadata/core-properties/*) continue ;;
    esac
    local unzip_entry="$entry"
    if [[ "$entry" == '[Content_Types].xml' ]]; then unzip_entry='\[Content_Types\].xml'; fi
    printf '%s\0' "$entry"
    unzip -p "$package_path" "$unzip_entry" | shasum -a 256
  done < <(unzip -Z1 "$package_path" | LC_ALL=C sort)
}

for runtime in MonoGame FNA; do
  dotnet pack "$repository_root/src/Forma/Forma.csproj" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:PackageOutputPath="$repro_root/$runtime" \
    --nologo
  dotnet pack "$repository_root/src/Forma.DynamicText/Forma.DynamicText.csproj" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:PackageOutputPath="$repro_root/$runtime" \
    --nologo
  dotnet pack "$repository_root/src/Forma.Media/Forma.Media.csproj" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:PackageOutputPath="$repro_root/$runtime" \
    --nologo
  for package_id in "Forma.$runtime" "Forma.DynamicText.$runtime" "Forma.Media.$runtime"; do
    for extension in nupkg snupkg; do
      printf 'Comparing deterministic contents: %s.%s\n' "$package_id" "$extension"
      diff \
        <(package_fingerprint "$package_root/$runtime/$package_id.$version.$extension") \
        <(package_fingerprint "$repro_root/$runtime/$package_id.$version.$extension")
    done
  done
done

if unzip -p "$package_root/MonoGame/Forma.MonoGame.$version.nupkg" Forma.MonoGame.nuspec |
  grep -Eq 'FNA.NET|MonoGame.Framework'; then
  printf 'Forma.MonoGame must leave framework backend selection to the application.\n' >&2
  exit 1
fi
if unzip -p "$package_root/FNA/Forma.FNA.$version.nupkg" Forma.FNA.nuspec |
  grep -Eq 'FNA.NET|MonoGame.Framework'; then
  printf 'Forma.FNA must leave framework selection to the application.\n' >&2
  exit 1
fi

for runtime in MonoGame FNA; do
  consumer_cache="$package_root/.consumer-packages/$runtime"
  rm -rf "$consumer_cache" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -p:FormaRuntime="$runtime" \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet run --project "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:FormaPackageSource="$package_root/$runtime" \
    --no-restore \
    --nologo
  for forbidden_package in freetypesharp harfbuzzsharp harfbuzzsharp.nativeassets.linux; do
    if [[ -d "$consumer_cache/$forbidden_package" ]]; then
      printf 'Core-only %s consumer unexpectedly resolved %s.\n' "$runtime" "$forbidden_package" >&2
      exit 1
    fi
  done
  if find "$repository_root/tests/Forma.PackageConsumer/bin" -type f \
    \( -iname '*freetype*' -o -iname '*harfbuzz*' \) -print -quit | grep -q .; then
    printf 'Core-only %s consumer unexpectedly copied FreeType/HarfBuzz assets.\n' "$runtime" >&2
    exit 1
  fi
done

for runtime in MonoGame FNA; do
  consumer_cache="$package_root/.dynamic-consumer-packages/$runtime"
  rm -rf "$consumer_cache" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet run --project "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    --no-restore \
    --nologo
  for required_package in freetypesharp harfbuzzsharp; do
    if [[ ! -d "$consumer_cache/$required_package" ]]; then
      printf 'Dynamic-text %s consumer did not resolve %s.\n' "$runtime" "$required_package" >&2
      exit 1
    fi
  done
done

for runtime in MonoGame FNA; do
  consumer_cache="$package_root/.spritefont-consumer-packages/$runtime"
  rm -rf "$consumer_cache" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:ExerciseSpriteFont=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet run --project "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:ExerciseSpriteFont=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    --no-restore \
    --nologo
done

for runtime in MonoGame FNA; do
  consumer_cache="$package_root/.integration-consumer-packages/$runtime"
  rm -rf "$consumer_cache" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:ExerciseSpriteFont=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet run --project "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:ExerciseSpriteFont=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    --no-restore \
    --nologo
done

for runtime in MonoGame FNA; do
  consumer_cache="$package_root/.rollback-consumer-packages/$runtime"
  rm -rf "$consumer_cache" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:ExpectDynamicTextDefault=false \
    -p:FormaDynamicTextDefaultEnabled=false \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet run --project "$consumer_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:ExpectDynamicTextDefault=false \
    -p:FormaDynamicTextDefaultEnabled=false \
    -p:FormaPackageSource="$package_root/$runtime" \
    --no-restore \
    --nologo
done

validate_core_rid() {
  local runtime="$1"
  local rid="$2"
  local consumer_cache="$package_root/.core-consumer-packages/$runtime/$rid"
  local publish_dir="$package_root/.core-consumer-publish/$runtime/$rid"

  rm -rf "$consumer_cache" "$publish_dir" \
    "$repository_root/tests/Forma.PackageConsumer/bin" \
    "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -r "$rid" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet publish "$consumer_project" \
    --configuration Release \
    -r "$rid" \
    --self-contained false \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:PublishDir="$publish_dir" \
    --no-restore \
    --nologo
  for forbidden_package in freetypesharp harfbuzzsharp harfbuzzsharp.nativeassets.linux; do
    if [[ -d "$consumer_cache/$forbidden_package" ]]; then
      printf 'Core-only %s/%s consumer unexpectedly resolved %s.\n' "$runtime" "$rid" "$forbidden_package" >&2
      exit 1
    fi
  done
  if find "$publish_dir" -type f \( -iname '*freetype*' -o -iname '*harfbuzz*' \) -print -quit | grep -q .; then
    printf 'Core-only %s/%s consumer unexpectedly copied FreeType/HarfBuzz assets.\n' "$runtime" "$rid" >&2
    exit 1
  fi
  bash "$repository_root/scripts/inspect-native-imports.sh" "$publish_dir"
}

validate_dynamic_rid() {
  local runtime="$1"
  local rid="$2"
  local consumer_cache="$package_root/.dynamic-consumer-packages/$runtime/$rid"
  local publish_dir="$package_root/.dynamic-consumer-publish/$runtime/$rid"
  local freetype_asset
  local harfbuzz_asset

  case "$rid" in
    win-*) freetype_asset='freetype.dll'; harfbuzz_asset='libHarfBuzzSharp.dll' ;;
    linux-*) freetype_asset='libfreetype.so'; harfbuzz_asset='libHarfBuzzSharp.so' ;;
    osx-*) freetype_asset='libfreetype.dylib'; harfbuzz_asset='libHarfBuzzSharp.dylib' ;;
    *) printf 'No dynamic-text asset mapping for %s.\n' "$rid" >&2; exit 2 ;;
  esac

  rm -rf "$consumer_cache" "$publish_dir" \
    "$repository_root/tests/Forma.PackageConsumer/bin" \
    "$repository_root/tests/Forma.PackageConsumer/obj"
  NUGET_PACKAGES="$consumer_cache" dotnet restore "$consumer_project" \
    -r "$rid" \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:RestoreNoCache=true \
    --nologo
  NUGET_PACKAGES="$consumer_cache" dotnet publish "$consumer_project" \
    --configuration Release \
    -r "$rid" \
    --self-contained false \
    -p:FormaRuntime="$runtime" \
    -p:IncludeFormaMedia=false \
    -p:IncludeDynamicText=true \
    -p:FormaPackageSource="$package_root/$runtime" \
    -p:PublishDir="$publish_dir" \
    --no-restore \
    --nologo
  for native_asset in "$freetype_asset" "$harfbuzz_asset"; do
    if [[ ! -f "$publish_dir/$native_asset" ]]; then
      printf 'Dynamic-text %s/%s consumer did not publish %s.\n' "$runtime" "$rid" "$native_asset" >&2
      exit 1
    fi
  done
}

for rid in win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64; do
  validate_core_rid MonoGame "$rid"
done
for rid in win-x64 linux-x64 linux-arm64 osx-x64 osx-arm64; do
  validate_core_rid FNA "$rid"
done
for rid in win-x64 win-arm64 linux-x64 osx-x64 osx-arm64; do
  validate_dynamic_rid MonoGame "$rid"
done
for rid in win-x64 linux-x64 osx-x64 osx-arm64; do
  validate_dynamic_rid FNA "$rid"
done

mixed_output="$(dotnet build "$consumer_project" \
  --configuration Release \
  -p:FormaRuntime=MonoGame \
  -p:FormaMediaRuntime=FNA \
  -p:FormaPackageSource="$package_root/MonoGame" \
  -p:FormaAdditionalPackageSource="$package_root/FNA" \
  --nologo 2>&1)" && {
    printf 'Mixed Forma runtime variants unexpectedly built successfully.\n' >&2
    exit 1
}
grep -Eq 'cannot be referenced by the same project|cannot be combined with' <<<"$mixed_output"

printf 'Validated six peer packages, 11 native-free core publishes, nine native-complete dynamic publishes, isolated consumers, and mixed-variant rejection.\n'