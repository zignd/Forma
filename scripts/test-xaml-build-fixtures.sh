#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
valid_project="$repo_root/tests/Forma.Xaml.Build.Integration/Forma.Xaml.Build.Integration.csproj"
invalid_project="$repo_root/tests/Forma.Xaml.Build.Invalid/Forma.Xaml.Build.Invalid.csproj"
template_invalid_project="$repo_root/tests/Forma.Xaml.Build.TemplateInvalid/Forma.Xaml.Build.TemplateInvalid.csproj"
temporary_directory="$(mktemp -d "${TMPDIR:-/tmp}/forma-xaml-build.XXXXXX")"
trap 'rm -rf "$temporary_directory"' EXIT

hash_outputs() {
  local runtime="$1"
  local output="$repo_root/tests/Forma.Xaml.Build.Integration/bin/$runtime/Debug/net10.0"
  shasum -a 256 "$output/Forma.Xaml.Build.Integration.dll" "$output/Forma.Xaml.Build.Integration.pdb" | awk '{print $1}'
}

for runtime in MonoGame FNA; do
  echo "Validating Forma XAML build fixtures for $runtime"
  MSBUILDDISABLENODEREUSE=1 dotnet clean "$valid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" >/dev/null
  MSBUILDDISABLENODEREUSE=1 dotnet build "$valid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" >/dev/null
  dotnet run --project "$valid_project" --configuration Debug --no-build -p:FormaRuntime="$runtime" | grep -q 'Forma XAML build integration: PASS'

  output="$repo_root/tests/Forma.Xaml.Build.Integration/bin/$runtime/Debug/net10.0"
  test "$(dd if="$output/Forma.Xaml.Build.Integration.pdb" bs=4 count=1 2>/dev/null)" = "BSJB"
  hash_outputs "$runtime" > "$temporary_directory/$runtime.first"

  MSBUILDDISABLENODEREUSE=1 dotnet build "$valid_project" --configuration Debug --nologo --verbosity:diagnostic -p:FormaRuntime="$runtime" > "$temporary_directory/$runtime.noop.log"
  grep -q 'Skipping target "FormaXamlCompile"' "$temporary_directory/$runtime.noop.log"

  MSBUILDDISABLENODEREUSE=1 dotnet clean "$valid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" >/dev/null
  MSBUILDDISABLENODEREUSE=1 dotnet build "$valid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" >/dev/null
  hash_outputs "$runtime" > "$temporary_directory/$runtime.second"
  cmp "$temporary_directory/$runtime.first" "$temporary_directory/$runtime.second"

  if MSBUILDDISABLENODEREUSE=1 dotnet build "$invalid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" > "$temporary_directory/$runtime.invalid.log" 2>&1; then
    echo "Invalid Forma XAML fixture unexpectedly built for $runtime" >&2
    exit 1
  fi
  grep -Eq 'InvalidView\.xaml\([0-9]+,[0-9]+(,[0-9]+,[0-9]+)?\).*FXAML1003' "$temporary_directory/$runtime.invalid.log"

  if MSBUILDDISABLENODEREUSE=1 dotnet build "$template_invalid_project" --configuration Debug --nologo -p:FormaRuntime="$runtime" > "$temporary_directory/$runtime.template-invalid.log" 2>&1; then
    echo "Invalid Forma XAML template fixture unexpectedly built for $runtime" >&2
    exit 1
  fi
  test "$(grep -Ec 'TemplateInvalidView\.xaml\([0-9]+,[0-9]+(,[0-9]+,[0-9]+)?\).*FXAML2501' "$temporary_directory/$runtime.template-invalid.log")" -ge 4
  grep -Eq 'TemplateInvalidView\.xaml\([0-9]+,[0-9]+(,[0-9]+,[0-9]+)?\).*FXAML4001' "$temporary_directory/$runtime.template-invalid.log"
  if grep -q 'FXAML7001' "$temporary_directory/$runtime.template-invalid.log"; then
    echo "Invalid Forma XAML template fixture degraded to FXAML7001 for $runtime" >&2
    exit 1
  fi
done

echo "Forma XAML build fixtures: PASS"