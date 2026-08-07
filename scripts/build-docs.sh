#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mode="${1:-build}"
docs_port="${DOCS_PORT:-8080}"
generated_root="$repository_root/docs/_generated"
api_root="$repository_root/docs/api"
site_root="$repository_root/Artifacts/docs/site"

case "$mode" in
  build|check|serve) ;;
  *) printf 'Usage: %s [build|check|serve]\n' "$0" >&2; exit 2 ;;
esac

cd "$repository_root"
dotnet tool restore
if [[ "$mode" == "check" ]]; then
  bash scripts/check-runtime-parity.sh
fi

projects=(Forma Forma.DynamicText Forma.Media Forma.Svg Forma.Svg.ThorVG Forma.Xaml.HotReload)
for project in "${projects[@]}"; do
  dotnet build "src/$project/$project.csproj" \
    --configuration Release -p:FormaRuntime=MonoGame --nologo
done

rm -rf "$generated_root" "$api_root" "$site_root"
mkdir -p "$generated_root/references"
for project in "${projects[@]}"; do
  find "src/$project/bin/MonoGame/Release/net10.0" -maxdepth 1 -type f -name '*.dll' \
    -exec cp {} "$generated_root/references/" \;
done

nuget_root="$(dotnet msbuild src/Forma/Forma.csproj \
  -p:FormaRuntime=MonoGame -getProperty:NuGetPackageRoot -nologo)"
monogame_version="$(dotnet msbuild src/Forma/Forma.csproj \
  -p:FormaRuntime=MonoGame -getProperty:MonoGameVersion -nologo)"
cp "$nuget_root/monogame.framework.desktopgl/$monogame_version/lib/net8.0/MonoGame.Framework.dll" \
  "$generated_root/references/"
nvorbis_version="$(jq -r '.targets[] | keys[] | select(startswith("NVorbis/")) | split("/")[1]' \
  src/Forma.Media/bin/MonoGame/Release/net10.0/Forma.Media.deps.json | head -1)"
cp "$nuget_root/nvorbis/$nvorbis_version/lib/netstandard2.0/NVorbis.dll" \
  "$generated_root/references/"

dotnet docfx docs/docfx.json --warningsAsErrors

test -f "$site_root/index.html"
test -f "$site_root/api/Forma.Control.html"
test -f "$site_root/xrefmap.yml"
rg -q "github.com/zigrok/Forma/blob/$(git rev-parse HEAD)/src/Forma/" \
  "$site_root/api/Forma.Control.html"

printf 'Built Forma documentation at %s.\n' "$site_root"
if [[ "$mode" == "serve" ]]; then
  exec dotnet docfx serve "$site_root" --hostname localhost --port "$docs_port"
fi
