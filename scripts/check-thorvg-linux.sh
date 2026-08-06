#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image="forma-thorvg-linux:local"

docker build --platform linux/amd64 --file "$repo_root/scripts/Dockerfile.thorvg-linux" --tag "$image" "$repo_root"
docker run --rm \
    --platform linux/amd64 \
    --volume "$repo_root:/repo" \
    --env MESON=/usr/local/bin/meson \
    --env THORVG_ARTIFACTS_DIR=/tmp/forma-thorvg-linux \
    "$image" \
    bash scripts/run-thorvg-linux-container.sh