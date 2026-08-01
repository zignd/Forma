#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
provenance="$repository_root/docs/provenance.md"
file_manifest="$repository_root/docs/provenance-files.tsv"

for required_file in LICENSE NOTICE.md THIRD-PARTY-NOTICES.md docs/provenance.md docs/provenance-files.tsv; do
  test -s "$repository_root/$required_file"
done

while IFS= read -r repository_file; do
  grep -Fq "$repository_file"$'\t' "$file_manifest"
done < <(git -C "$repository_root" ls-files --cached --others --exclude-standard)

for required_notice in "Godot Engine" "Bjorn Ottosson" "MonoGame" "IBM Plex Sans"; do
  grep -Fq "$required_notice" "$repository_root/THIRD-PARTY-NOTICES.md"
done

pending_count=0
classified_count=0
for source_file in "$repository_root"/src/Forma/*.cs; do
  source_name="$(basename "$source_file")"
  manifest_row="$(grep -F "| \`$source_name\` |" "$provenance")"

  if grep -Fq "MonoGame Foundation" "$source_file"; then
    grep -Fqi "pending" <<<"$manifest_row"
    pending_count=$((pending_count + 1))
    continue
  fi

  if [[ "$source_name" == "OkColor.cs" ]]; then
    grep -Fq "Copyright (c) 2021 Björn Ottosson" "$source_file"
  else
    grep -Fq "SPDX-License-Identifier: MIT" "$source_file"
  fi
  grep -Fqi "classified" <<<"$manifest_row"
  classified_count=$((classified_count + 1))
done

printf 'Compliance manifest: %d classified source files, %d pending source files.\n' \
  "$classified_count" "$pending_count"

for test_file in \
  tests/Forma.Tests/UITest.cs \
  tests/Forma.Tests/CatalogInventoryTest.cs \
  tests/Forma.RenderTests/UIRenderTest.cs; do
  grep -Fq "SPDX-License-Identifier: MIT" "$repository_root/$test_file"
  grep -Fqi "classified" <<<"$(grep -F "| \`$test_file\` |" "$provenance")"
done

graphics_fixture="tests/Forma.RenderTests/GraphicsDeviceTestFixtureBase.cs"
grep -Fq "MonoGame Foundation" "$repository_root/$graphics_fixture"
grep -Fq "SPDX-License-Identifier: MS-PL" "$repository_root/$graphics_fixture"
grep -Fqi "classified" <<<"$(grep -F "| \`$graphics_fixture\` |" "$provenance")"
grep -Fq "Microsoft Public License (Ms-PL)" "$repository_root/THIRD-PARTY-NOTICES.md"

for catalog_file in "$repository_root"/samples/Forma.Catalog/*.cs; do
  grep -Fq "SPDX-License-Identifier: MIT" "$catalog_file"
done
grep -Fqi "classified" <<<"$(grep -F '| `samples/Forma.Catalog/CatalogGame.cs`, `CatalogShell.cs`, `Program.cs`, `StoryCatalog.cs` |' "$provenance")"
grep -Fqi "classified" <<<"$(grep -F '| `samples/Forma.Catalog/CatalogBackend.cs` |' "$provenance")"

package_consumer="tests/Forma.PackageConsumer/Program.cs"
grep -Fq "SPDX-License-Identifier: MIT" "$repository_root/$package_consumer"
grep -Fqi "classified" <<<"$(grep -F "| \`$package_consumer\` |" "$provenance")"