#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) rid="osx-arm64"; abi_symbol='_forma_thorvg_abi_version' ;;
    Linux-x86_64) rid="linux-x64"; abi_symbol='forma_thorvg_abi_version' ;;
    *) printf 'ThorVG static managed reference host has no declared RID for %s-%s.\n' "$(uname -s)" "$(uname -m)" >&2; exit 2 ;;
esac
output="$repo_root/artifacts/nativeaot/thorvg-static/$rid"

bash "$repo_root/scripts/build-thorvg-spike.sh"
rm -rf "$output"
dotnet publish "$repo_root/tests/Forma.ThorVG.StaticHost/Forma.ThorVG.StaticHost.csproj" \
    --configuration Release --runtime "$rid" --self-contained true \
    -p:FormaRuntime=MonoGame -p:FormaThorvgStaticLink=true \
    --output "$output" --nologo

if find "$output" -type f \( -name 'libforma_thorvg.dylib' -o -name 'libforma_thorvg.so' \) -print -quit | grep -q .; then
    printf 'Static ThorVG host unexpectedly contains a dynamic Forma ThorVG library.\n' >&2
    exit 1
fi
"$output/Forma.ThorVG.StaticHost"
nm -g "$repo_root/artifacts/native/thorvg/$rid/output/libforma_thorvg.a" > "$output/static-symbols.txt"
grep -q "$abi_symbol" "$output/static-symbols.txt"
if { command -v otool >/dev/null && otool -L "$output/Forma.ThorVG.StaticHost" || ldd "$output/Forma.ThorVG.StaticHost"; } | grep -q 'forma_thorvg'; then
    printf 'Static ThorVG host retained a dynamic Forma ThorVG import.\n' >&2
    exit 1
fi