#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest_path="$repository_root/scripts/release-packages.json"
package_source="https://api.nuget.org/v3/index.json"
registration_root="https://api.nuget.org/v3-flatcontainer"
verification_root="${FORMA_PUBLISHED_PACKAGE_CHECK_DIR:-$repository_root/Artifacts/published-package-check}"
version="${1:-$(dotnet msbuild "$repository_root/src/Forma/Forma.csproj" -p:FormaRuntime=MonoGame -getProperty:Version -nologo)}"

command -v jq >/dev/null || { printf 'jq is required to read %s.\n' "$manifest_path" >&2; exit 1; }
rm -rf "$verification_root"
mkdir -p "$verification_root"

while IFS= read -r package_id; do
  normalized_id="$(printf '%s' "$package_id" | tr '[:upper:]' '[:lower:]')"
  package_url="$registration_root/$normalized_id/$version/$normalized_id.$version.nupkg"
  curl --fail --silent --show-error --location \
    --retry 20 --retry-delay 15 --retry-all-errors \
    --output /dev/null "$package_url"

  consumer_root="$verification_root/$package_id"
  mkdir -p "$consumer_root"
  cat >"$consumer_root/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestorePackagesPath>$consumer_root/packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$package_id" Version="$version" />
  </ItemGroup>
</Project>
EOF
  dotnet restore "$consumer_root/Consumer.csproj" \
    --source "$package_source" --force --no-cache --nologo
  printf 'Restored %s %s from NuGet.org.\n' "$package_id" "$version"
done < <(jq -r '.packages[].id' "$manifest_path")
