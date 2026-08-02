#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_root="$repository_root/tests/Assets/Fonts"
commit="2796410152d4f9524b68ed46e69c1b60f8e0f7c3"
temporary_root="$(mktemp -d)"
trap 'rm -rf "$temporary_root"' EXIT

download() {
  local relative_url="$1"
  local output_name="$2"
  local expected_sha="$3"
  local output_file="$temporary_root/$output_name"
  curl -fsSL "https://raw.githubusercontent.com/google/fonts/$commit/$relative_url" -o "$output_file"
  printf '%s  %s\n' "$expected_sha" "$output_file" | shasum -a 256 -c -
}

subset() {
  local source_name="$1"
  local output_name="$2"
  local text="$3"
  pyftsubset "$temporary_root/$source_name" \
    --output-file="$output_root/$output_name" \
    --text="$text" \
    --layout-features='*' \
    --glyph-names \
    --symbol-cmap \
    --legacy-cmap \
    --notdef-glyph \
    --notdef-outline \
    --recommended-glyphs \
    --name-IDs='*' \
    --name-legacy \
    --name-languages='*'
}

command -v curl >/dev/null
command -v pyftsubset >/dev/null
mkdir -p "$output_root"

download 'ofl/notosansdevanagari/NotoSansDevanagari%5Bwdth%2Cwght%5D.ttf' devanagari.ttf 9ce7b04f60e363d8870e5997744cf85cf69d38a4d7d129d364d92a3b14b461d7
download 'ofl/notosansthai/NotoSansThai%5Bwdth%2Cwght%5D.ttf' thai.ttf 5a1c559bb539583c8a1fd99d1c5b9491e5e14478c9cd2bd0970d5c3096cc9ef8
download 'ofl/notosanshebrew/NotoSansHebrew%5Bwdth%2Cwght%5D.ttf' hebrew.ttf 7ef36a2c3593758cdb622e1bdef4f84523e92fbc3ccc667438dd80ff54c2de88
download 'ofl/notoemoji/NotoEmoji%5Bwght%5D.ttf' emoji.ttf de6c18832938afc99caf132b39d6a30a19bac7f2e812e28db2535b4608d27551
download 'ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf' cjk.ttf a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da
download 'ofl/notosansdevanagari/OFL.txt' OFL.txt a216f6f8d85c7228093e0ee5e258d9d377e6671f68acb4db1930b29583d0f331

subset devanagari.ttf NotoSansDevanagari_Subset.ttf 'नमस्ते दुनिया क्ष'
subset thai.ttf NotoSansThai_Subset.ttf 'สวัสดีชาวโลก'
subset hebrew.ttf NotoSansHebrew_Subset.ttf 'שלום עולם'
subset cjk.ttf NotoSansCJK_Subset.ttf '你好世界 日本語 한글 office'
subset emoji.ttf NotoEmoji_Subset.ttf '👩🏽‍💻 👨‍👩‍👧‍👦 🏳️‍🌈 1️⃣ 🇧🇷'
cp "$temporary_root/OFL.txt" "$output_root/LICENSE.NotoSubsets.txt"

printf 'Generated multilingual font subsets from google/fonts@%s.\n' "$commit"