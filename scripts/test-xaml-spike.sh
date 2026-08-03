#!/usr/bin/env bash
set -euo pipefail

# Purpose: Validate XamlX SRE/Cecil behavior and a compiler-free macOS arm64 NativeAOT consumer.
# Usage: `bash scripts/test-xaml-spike.sh` from any directory.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repository_root/Artifacts/xaml-spike"
generated_dir="$artifact_root/generated"
publish_dir="$artifact_root/nativeaot"
spike_project="$repository_root/tests/Forma.XamlSpike/Forma.XamlSpike.csproj"
consumer_project="$repository_root/tests/Forma.XamlSpike.Aot/Forma.XamlSpike.Aot.csproj"

if [[ "$(uname -s)" != "Darwin" || "$(uname -m)" != "arm64" ]]; then
  printf 'This gate currently validates only macOS arm64; host is %s %s.\n' "$(uname -s)" "$(uname -m)" >&2
  exit 2
fi

mkdir -p "$generated_dir" "$publish_dir"

dotnet run --project "$spike_project" --configuration Debug -- --emit "$generated_dir"
dotnet publish "$consumer_project" --configuration Release -r osx-arm64 \
  -p:PublishAot=true -p:SelfContained=true -p:PublishDir="$publish_dir" --nologo

file "$publish_dir/Forma.XamlSpike.Aot" | grep -Fq 'Mach-O 64-bit executable arm64'
"$publish_dir/Forma.XamlSpike.Aot"

if find "$publish_dir" -type f \( -iname 'XamlX*' -o -iname 'Mono.Cecil*' -o -iname '*.xaml' \) -print -quit | grep -q .; then
  printf 'NativeAOT output contains a compiler dependency or source XAML.\n' >&2
  exit 1
fi

printf 'Forma XAML compiler and NativeAOT feasibility: PASS\n'