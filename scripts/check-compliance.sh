#!/usr/bin/env bash
set -euo pipefail

# Purpose: Verify required license files, third-party notices, per-file SPDX identifiers, and the
# explicit MonoGame and ok_color attributions. Usage: `bash scripts/check-compliance.sh` from any
# directory. The script exits nonzero on the first compliance failure and otherwise prints the
# number of tracked C# files checked.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for required_file in LICENSE NOTICE.md THIRD-PARTY-NOTICES.md \
  tests/Assets/Fonts/LICENSE.Inter.txt \
  tests/Assets/Fonts/LICENSE.JetBrainsMono.txt; do
  test -s "$repository_root/$required_file"
done

for required_notice in "Godot Engine" "Bjorn Ottosson" "MonoGame" "Inter" "JetBrains Mono"; do
  grep -Fq "$required_notice" "$repository_root/THIRD-PARTY-NOTICES.md"
done

source_count=0
while IFS= read -r source_file; do
  grep -Eq '^// SPDX-License-Identifier: (MIT|MS-PL)$' "$repository_root/$source_file"
  source_count=$((source_count + 1))
done < <(git -C "$repository_root" ls-files '*.cs')

graphics_fixture="tests/Forma.RenderTests/GraphicsDeviceTestFixtureBase.cs"
grep -Fq "MonoGame Foundation" "$repository_root/$graphics_fixture"
grep -Fq "SPDX-License-Identifier: MS-PL" "$repository_root/$graphics_fixture"
grep -Fq "Microsoft Public License (Ms-PL)" "$repository_root/THIRD-PARTY-NOTICES.md"

grep -Fq "Copyright (c) 2021 Björn Ottosson" "$repository_root/src/Forma/OkColor.cs"

printf 'Compliance: %d tracked C# files carry SPDX license identifiers.\n' "$source_count"