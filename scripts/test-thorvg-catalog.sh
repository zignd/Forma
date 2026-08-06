#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="$repo_root/artifacts/catalog/thorvg"
rm -rf "$artifact_root"
mkdir -p "$artifact_root"

for runtime in MonoGame FNA; do
    slug="$(printf '%s' "$runtime" | tr '[:upper:]' '[:lower:]')"
    project="$repo_root/samples/Forma.Catalog.$runtime/Forma.Catalog.$runtime.csproj"
    dotnet run --project "$project" --configuration Release \
        -p:FormaRuntime="$runtime" -p:SvgBackend=ThorVG -- \
        --story 'Runtime SVG' --frames 20 --display-scale 1.25 \
        --viewport-width 1024 --viewport-height 720 --theme-icon-policy RuntimeSvg \
        --metrics "$artifact_root/$slug.metrics.json" \
        --render-output "$artifact_root/$slug.render.json"
    jq -e '
        .svgBackendId == "thorvg" and
        .svgBackendProfile == "1" and
        .svgBackendNativeAvailability == "Packaged" and
        .svgBackendLinkMode == "Dynamic" and
        .svgBackendAvailable == true and
        .themeIconRuntimeSvgCount == 67 and
        .themeIconBitmapFallbackCount == 0 and
        .svgRasterEntries > 0 and
        .svgRasterBytes > 0
    ' "$artifact_root/$slug.metrics.json" >/dev/null
done

printf 'ThorVG Runtime SVG Catalog story passed on MonoGame and FNA.\n'