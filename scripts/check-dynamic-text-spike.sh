#!/usr/bin/env bash
set -euo pipefail

# Purpose: Prove peer-runtime parity for byte-backed shaping, FreeType rasterization, and Alpha8
# coverage upload/rendering. Usage: `bash scripts/check-dynamic-text-spike.sh` from any directory on
# a graphical desktop host. Set FNA_PROJECT to include a local FNA project in the comparison.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
spike_project="$repository_root/tests/Forma.DynamicTextSpike/Forma.DynamicTextSpike.csproj"

run_spike() {
  local runtime="$1"
  shift
  dotnet run \
    --project "$spike_project" \
    --configuration Release \
    -p:FormaRuntime="$runtime" \
    --nologo \
    "$@"
}

fingerprint() {
  sed -nE 's/.*SHA256=([0-9a-f]{64}).*/\1/p' | tail -n 1
}

monogame_output="$(run_spike MonoGame)"
printf '%s\n' "$monogame_output"
monogame_hash="$(printf '%s\n' "$monogame_output" | fingerprint)"

fna_output="$(run_spike FNA)"
printf '%s\n' "$fna_output"
fna_hash="$(printf '%s\n' "$fna_output" | fingerprint)"

if [[ -z "$monogame_hash" || -z "$fna_hash" || "$monogame_hash" != "$fna_hash" ]]; then
  printf 'Dynamic text package fingerprints differ: MonoGame=%s FNA=%s\n' "$monogame_hash" "$fna_hash" >&2
  exit 1
fi

if [[ -n "${FNA_PROJECT:-}" ]]; then
  if [[ ! -f "$FNA_PROJECT" ]]; then
    printf 'FNA_PROJECT does not exist: %s\n' "$FNA_PROJECT" >&2
    exit 2
  fi
  local_fna_output="$(run_spike FNA -p:FnaProjectPath="$FNA_PROJECT")"
  printf '%s\n' "$local_fna_output"
  local_fna_hash="$(printf '%s\n' "$local_fna_output" | fingerprint)"
  if [[ "$local_fna_hash" != "$monogame_hash" ]]; then
    printf 'Local FNA dynamic text fingerprint differs: expected=%s actual=%s\n' "$monogame_hash" "$local_fna_hash" >&2
    exit 1
  fi
fi

printf 'Dynamic text spike parity: %s\n' "$monogame_hash"