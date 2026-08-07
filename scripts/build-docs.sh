#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mode="${1:-build}"
docs_port="${DOCS_PORT:-8080}"
generated_root="$repository_root/docs/_generated"
api_root="$repository_root/docs/api"
site_root="$repository_root/Artifacts/docs/site"
docs_revision="$(git -C "$repository_root" rev-parse HEAD)"

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
    --configuration Release -p:FormaRuntime=MonoGame -p:SourceRevisionId="$docs_revision" \
    --no-incremental --nologo
done

rm -rf "$generated_root" "$api_root" "$site_root"
mkdir -p "$generated_root/examples" "$generated_root/references"
cp samples/Forma.QuickStart/FirstView.xaml "$generated_root/examples/FirstView.xaml"
cp samples/Forma.QuickStart/QuickStartGame.cs "$generated_root/examples/QuickStartGame.cs"
cp samples/Forma.Catalog/StylesStoryView.xaml "$generated_root/examples/StylesStoryView.xaml"
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

dotnet docfx metadata docs/docfx.json --warningsAsErrors
dotnet run --project tools/Forma.AssemblyInspector/Forma.AssemblyInspector.csproj -- \
  normalize-source-links "$api_root" https://github.com/zigrok/Forma "$docs_revision"
dotnet docfx build docs/docfx.json --warningsAsErrors

dotnet run --project tools/Forma.AssemblyInspector/Forma.AssemblyInspector.csproj -- \
  docs-coverage "$api_root" "$site_root" samples/Forma.Catalog/Stories/Controls \
  27.65 11.57 docs/reference/documentation-baseline.json "$site_root/control-coverage.json"
dotnet run --project tools/Forma.AssemblyInspector/Forma.AssemblyInspector.csproj -- \
  control-families "$api_root" "$site_root" docs/reference/control-families.json

test -f "$site_root/index.html"
test -f "$site_root/api/Forma.Control.html"
test -f "$site_root/xrefmap.yml"
source_link_pattern='github\.com/zigrok/Forma/blob/[0-9a-f]{40}/src/Forma/'
if ! grep -Eq "$source_link_pattern" "$site_root/api/Forma.Control.html"; then
  printf 'Generated Control API page does not contain an immutable zigrok/Forma Source Link.\n' >&2
  grep -Eo 'github\.com/[^"< ]+/blob/[^"< ]+' "$site_root/api/Forma.Control.html" | head -5 >&2 || true
  exit 1
fi

printf 'Built Forma documentation at %s.\n' "$site_root"
if [[ "$mode" == "serve" ]]; then
  exec dotnet docfx serve "$site_root" --hostname localhost --port "$docs_port"
fi
