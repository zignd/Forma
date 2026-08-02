#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! -d "$1" ]]; then
  printf 'Usage: %s <publish-directory>\n' "$0" >&2
  exit 2
fi

publish_dir="$1"
inspected=0
while IFS= read -r -d '' native_file; do
  dependencies=""
  case "$native_file" in
    *.dylib)
      dependencies="$(otool -L "$native_file" 2>/dev/null || true)"
      ;;
    *.so|*.so.*|*.dll|*.exe)
      dependencies="$(objdump -p "$native_file" 2>/dev/null || true)"
      ;;
    *)
      if [[ -x "$native_file" ]]; then
        dependencies="$(otool -L "$native_file" 2>/dev/null || objdump -p "$native_file" 2>/dev/null || true)"
      fi
      ;;
  esac
  if [[ -z "$dependencies" ]]; then
    continue
  fi
  inspected=$((inspected + 1))
  if grep -Eiq 'free(type|type6)|harfbuzz' <<<"$dependencies"; then
    printf 'Forbidden native text dependency imported by %s:\n%s\n' "$native_file" "$dependencies" >&2
    exit 1
  fi
done < <(find "$publish_dir" -type f \( -name '*.dylib' -o -name '*.so' -o -name '*.so.*' -o -name '*.dll' -o -name '*.exe' -o -perm -111 \) -print0)

if [[ $inspected -eq 0 ]]; then
  printf 'No native dependency tables were inspected in %s.\n' "$publish_dir" >&2
  exit 1
fi

printf 'Inspected %d native dependency tables in %s.\n' "$inspected" "$publish_dir"