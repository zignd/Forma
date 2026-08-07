#!/usr/bin/env bash
# Purpose: Format or check every repository source XAML document with XAML Styler.
# Usage: `bash scripts/format-xaml.sh [--write|--check]` from any directory.

set -euo pipefail

mode="${1:---write}"
if [[ "$mode" != "--write" && "$mode" != "--check" ]]; then
  printf 'Usage: %s [--write|--check]\n' "${BASH_SOURCE[0]}" >&2
  exit 2
fi

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
config="$repository_root/.xamlstyler.json"
dotnet="${DOTNET:-dotnet}"

cd "$repository_root"
xaml_files=()
while IFS= read -r -d '' xaml_file; do
  xaml_files+=("$xaml_file")
done < <(git ls-files --cached --others --exclude-standard -z -- '*.xaml')
if ((${#xaml_files[@]} == 0)); then
  printf 'No repository XAML files found.\n'
  exit 0
fi

for xaml_file in "${xaml_files[@]}"; do
  if [[ "$xaml_file" == *,* ]]; then
    printf 'XAML Styler cannot accept a file name containing a comma: %s\n' "$xaml_file" >&2
    exit 2
  fi
done

file_list="$(IFS=,; printf '%s' "${xaml_files[*]}")"
arguments=(tool run xstyler -- --file "$file_list" --config "$config")
if [[ "$mode" == "--check" ]]; then
  arguments+=(--passive)
fi

"$dotnet" "${arguments[@]}"