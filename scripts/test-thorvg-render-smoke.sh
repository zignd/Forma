#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/tests/Forma.SvgRenderSmoke/Forma.SvgRenderSmoke.csproj"

for runtime in MonoGame FNA; do
    if [[ "$runtime" == "FNA" && "$(uname -s)" == "Linux" ]]; then
        SDL_VIDEODRIVER=offscreen FNA3D_FORCE_DRIVER=OpenGL FNA3D_OPENGL_WINDOW_DEPTHSTENCILFORMAT=None \
            dotnet run --project "$project" --configuration Release \
            -p:FormaRuntime="$runtime" -p:SvgBackend=ThorVG
    else
        dotnet run --project "$project" --configuration Release \
            -p:FormaRuntime="$runtime" -p:SvgBackend=ThorVG
    fi
done