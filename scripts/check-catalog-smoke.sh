#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline="$repository_root/samples/Forma.Catalog/catalog-metrics-baseline.json"
actual="$(mktemp "${TMPDIR:-/tmp}/forma-catalog.XXXXXX.json")"
trap 'rm -f "$actual"' EXIT

dotnet run --project "$repository_root/samples/Forma.Catalog/Forma.Catalog.csproj" \
  --configuration Release \
  -- \
  --metrics "$actual" \
  --frames 3 \
  --display-scale 2

if ! cmp -s "$baseline" "$actual"; then
  printf 'Catalog metrics differ from the approved 2x baseline.\n' >&2
  diff -u "$baseline" "$actual" || true
  exit 1
fi

printf 'Catalog smoke: 3 frames, 74 stories, 2x density font, 720x450 logical viewport.\n'