#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/tests/Forma.ThorVG.AotConsumer/Forma.ThorVG.AotConsumer.csproj"
if [[ -n "${THORVG_AOT_RID:-}" ]]; then
    rid="$THORVG_AOT_RID"
elif [[ "$(uname -s)-$(uname -m)" == "Darwin-arm64" ]]; then
    rid="osx-arm64"
elif [[ "$(uname -s)-$(uname -m)" == "Linux-x86_64" ]]; then
    rid="linux-x64"
else
    printf 'No declared ThorVG NativeAOT RID for %s-%s.\n' "$(uname -s)" "$(uname -m)" >&2
    exit 2
fi

for runtime in MonoGame FNA; do
    output="$repo_root/artifacts/nativeaot/thorvg/$rid/$runtime"
    rm -rf "$output"
    dotnet publish "$project" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        -p:PublishAot=true \
        -p:FormaRuntime="$runtime" \
        --output "$output" \
        --nologo

    "$output/Forma.ThorVG.AotConsumer"
done