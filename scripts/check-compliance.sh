#!/usr/bin/env bash
set -euo pipefail

# Purpose: Verify required license files, third-party notices, per-file SPDX identifiers, and the
# explicit MonoGame and ok_color attributions. Usage: `bash scripts/check-compliance.sh` from any
# directory. The script exits nonzero on the first compliance failure and otherwise prints the
# number of repository C# files checked.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for required_file in LICENSE NOTICE.md THIRD-PARTY-NOTICES.md \
  tests/Assets/Fonts/LICENSE.Inter.txt \
  tests/Assets/Fonts/LICENSE.JetBrainsMono.txt \
  tests/Assets/Fonts/LICENSE.NotoSansArabic.txt \
  tests/Assets/Fonts/NotoSansArabic_Variable.ttf \
  tests/Assets/Text/multilingual-corpus.json \
  tests/Assets/Text/README.md \
  tests/Assets/Video/README.md \
  tests/Assets/Video/forma-video-smoke.ogv \
  assets/unicode/manifest.json \
  assets/theme-icons/imports.json \
  assets/theme-icons/LICENSE.Godot.txt \
  src/Forma/Resources/ThemeIcons/theme-icons-1x.png \
  src/Forma/Resources/ThemeIcons/theme-icons-2x.png \
  src/Forma/Resources/ThemeIcons/theme-icons.json \
  docs/images/catalog-monogame.png \
  docs/images/catalog-fna.png; do
  test -s "$repository_root/$required_file"
done

icon_source_count="$(find "$repository_root/assets/theme-icons/svg" -type f -name '*.svg' | wc -l | tr -d ' ')"
icon_manifest_count="$(jq '.Icons | length' "$repository_root/assets/theme-icons/imports.json")"
test "$icon_source_count" -eq "$icon_manifest_count"
test "$icon_source_count" -gt 0
if find "$repository_root/assets/theme-icons/svg" -type f ! -name '*.svg' | grep -q .; then
  printf 'Theme icon input directory contains an unclassified file.\n' >&2
  exit 1
fi
jq -e 'all(.Icons[]; .License == "Godot-MIT" and (.Source | startswith("scene/theme/icons/")) and (.Sha256 | test("^[0-9a-f]{64}$")))' \
  "$repository_root/assets/theme-icons/imports.json" >/dev/null
grep -Fq 'Svg.Skia' "$repository_root/THIRD-PARTY-NOTICES.md"
grep -Fq 'Clipper2 2.0.0' "$repository_root/THIRD-PARTY-NOTICES.md"
jq -e '.unicodeVersion == "17.0.0" and (.files | length > 0) and all(.files[]; (.url | startswith("https://www.unicode.org/Public/17.0.0/")) and (.sha256 | test("^[0-9a-f]{64}$")))' \
  "$repository_root/assets/unicode/manifest.json" >/dev/null

for required_notice in "Godot Engine" "Bjorn Ottosson" "MonoGame" "FNA.NET" "Inter" "JetBrains Mono" "Noto Sans Arabic" "FreeType" "HarfBuzz" "Unicode Character Database" "Unicode License V3"; do
  grep -Fq "$required_notice" "$repository_root/THIRD-PARTY-NOTICES.md"
done
grep -Fq '<FreeTypeSharpVersion>3.1.0</FreeTypeSharpVersion>' "$repository_root/Directory.Build.props"
grep -Fq '<HarfBuzzSharpVersion>14.2.1.1</HarfBuzzSharpVersion>' "$repository_root/Directory.Build.props"
grep -Fq '<Clipper2Version>2.0.0</Clipper2Version>' "$repository_root/Directory.Build.props"
grep -Fq 'FreeType 2.13.2 through FreeTypeSharp 3.1.0' "$repository_root/THIRD-PARTY-NOTICES.md"
grep -Fq 'based in part on the work of the FreeType Team' "$repository_root/THIRD-PARTY-NOTICES.md"
grep -Fq 'HarfBuzz 14.2.1 through HarfBuzzSharp 14.2.1.1' "$repository_root/THIRD-PARTY-NOTICES.md"
grep -Fq 'Permission is hereby granted, without written agreement' "$repository_root/THIRD-PARTY-NOTICES.md"
grep -Fq 'THE COPYRIGHT HOLDER SPECIFICALLY DISCLAIMS ANY WARRANTIES' "$repository_root/THIRD-PARTY-NOTICES.md"

source_count=0
while IFS= read -r source_file; do
  grep -Eq '^// SPDX-License-Identifier: (MIT|MS-PL)$' "$repository_root/$source_file"
  source_count=$((source_count + 1))
done < <(git -C "$repository_root" ls-files --cached --others --exclude-standard '*.cs')

graphics_fixture="tests/Forma.RenderTests/GraphicsDeviceTestFixtureBase.cs"
grep -Fq "MonoGame Foundation" "$repository_root/$graphics_fixture"
grep -Fq "SPDX-License-Identifier: MS-PL" "$repository_root/$graphics_fixture"
grep -Fq "Microsoft Public License (Ms-PL)" "$repository_root/THIRD-PARTY-NOTICES.md"

grep -Fq "Copyright (c) 2021 Björn Ottosson" "$repository_root/src/Forma/OkColor.cs"

printf 'Compliance: %d repository C# files and %d classified theme icons validated.\n' "$source_count" "$icon_source_count"