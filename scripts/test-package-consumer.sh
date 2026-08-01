#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_output="$repository_root/Artifacts/packages"
package_path="$package_output/Forma.0.1.0-alpha.1.nupkg"
media_package_path="$package_output/Forma.Media.0.1.0-alpha.1.nupkg"
consumer_packages="$package_output/.consumer-packages"

rm -rf "$package_output" "$repository_root/tests/Forma.PackageConsumer/bin" "$repository_root/tests/Forma.PackageConsumer/obj"
dotnet tool restore
dotnet build "$repository_root/samples/Forma.Catalog/Forma.Catalog.csproj" --configuration Release
cmp "$repository_root/tests/Assets/Fonts/Catalog.xnb" "$repository_root/samples/Forma.Catalog/bin/Release/net10.0/Content/Fonts/Catalog.xnb"
cmp "$repository_root/tests/Assets/Fonts/Catalog@2x.xnb" "$repository_root/samples/Forma.Catalog/bin/Release/net10.0/Content/Fonts/Catalog@2x.xnb"
dotnet pack "$repository_root/src/Forma/Forma.csproj" --configuration Release --output "$package_output"
dotnet pack "$repository_root/src/Forma.Media/Forma.Media.csproj" --configuration Release --output "$package_output"

package_entries="$(unzip -Z1 "$package_path")"
for required_entry in \
  lib/net10.0/Forma.dll \
  lib/net10.0/Forma.xml \
  LICENSE \
  NOTICE.md \
  README.md \
  THIRD-PARTY-NOTICES.md \
  docs/migration.md; do
  grep -Fxq "$required_entry" <<<"$package_entries"
done
if unzip -p "$package_path" Forma.nuspec | grep -Fq 'MonoGame.Framework.'; then
  printf 'Forma must not impose a transitive MonoGame backend package.\n' >&2
  exit 1
fi

media_package_entries="$(unzip -Z1 "$media_package_path")"
for required_entry in \
  lib/net10.0/Forma.Media.dll \
  lib/net10.0/Forma.Media.xml \
  LICENSE \
  NOTICE.md \
  README.md \
  THIRD-PARTY-NOTICES.md \
  docs/migration.md; do
  grep -Fxq "$required_entry" <<<"$media_package_entries"
done
if unzip -p "$media_package_path" Forma.Media.nuspec | grep -Fq 'MonoGame.Framework.'; then
  printf 'Forma.Media must not impose a transitive MonoGame backend package.\n' >&2
  exit 1
fi

test -f "$package_output/Forma.0.1.0-alpha.1.snupkg"
test -f "$package_output/Forma.Media.0.1.0-alpha.1.snupkg"
NUGET_PACKAGES="$consumer_packages" dotnet restore "$repository_root/tests/Forma.PackageConsumer/Forma.PackageConsumer.csproj" \
  -p:FormaPackageSource="$package_output" \
  -p:RestoreNoCache=true
NUGET_PACKAGES="$consumer_packages" dotnet run --project "$repository_root/tests/Forma.PackageConsumer/Forma.PackageConsumer.csproj" \
  --configuration Release \
  --no-restore