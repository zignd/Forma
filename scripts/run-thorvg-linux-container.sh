#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

bash scripts/build-thorvg-spike.sh
mkdir -p artifacts/native/thorvg/linux-x64/output
cp /tmp/forma-thorvg-linux/output/libforma_thorvg.so /tmp/forma-thorvg-linux/output/libforma_thorvg.a artifacts/native/thorvg/linux-x64/output/

for runtime in MonoGame FNA; do
    dotnet test tests/Forma.ThorVG.Tests/Forma.ThorVG.Tests.csproj \
        --configuration Release -p:FormaRuntime="$runtime" --nologo
done

bash scripts/test-svg-package-consumers.sh
bash scripts/compare-svg-backends.sh
THORVG_AOT_RID=linux-x64 bash scripts/test-thorvg-nativeaot.sh
THORVG_ARTIFACTS_DIR="$repo_root/artifacts/native/thorvg/linux-x64" bash scripts/test-thorvg-static-host.sh

THORVG_SANITIZE=true THORVG_ARTIFACTS_DIR=/tmp/forma-thorvg-linux-sanitized \
    bash scripts/build-thorvg-spike.sh