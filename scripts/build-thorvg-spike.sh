#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_dir="$repo_root/external/ThorVG"
native_dir="$repo_root/native/Forma.ThorVG"
host_os="$(uname -s)"
host_arch="$(uname -m)"
case "$host_os-$host_arch" in
    Darwin-arm64) platform="osx-arm64"; library_name="libforma_thorvg.dylib"; shared_flags=(-dynamiclib); rpath="@loader_path" ;;
    Linux-x86_64) platform="linux-x64"; library_name="libforma_thorvg.so"; shared_flags=(-shared); rpath='$ORIGIN' ;;
    *) echo "Unsupported ThorVG spike host: $host_os-$host_arch" >&2; exit 2 ;;
esac
artifacts_dir="${THORVG_ARTIFACTS_DIR:-$repo_root/artifacts/native/thorvg/$platform}"
tools_dir="${THORVG_TOOLS_DIR:-$repo_root/artifacts/tools/meson}"
build_dir="$artifacts_dir/build"
output_dir="$artifacts_dir/output"
cxx="${CXX:-c++}"
sanitize="${THORVG_SANITIZE:-false}"
if [[ "$host_os" == "Darwin" ]]; then
    export_flags=(-Wl,-exported_symbols_list,"$native_dir/exports-macos.txt")
else
    export_flags=(-Wl,--version-script,"$native_dir/exports-linux.map")
fi

if [[ ! -f "$source_dir/meson.build" ]]; then
    echo "ThorVG source is missing. Run: git submodule update --init external/ThorVG" >&2
    exit 2
fi

if [[ -n "${MESON:-}" ]]; then
    meson_command="$MESON"
elif command -v meson >/dev/null 2>&1; then
    meson_command="$(command -v meson)"
elif [[ ! -x "$tools_dir/bin/meson" ]]; then
    python3 -m venv "$tools_dir"
    "$tools_dir/bin/python" -m pip install --disable-pip-version-check \
        --requirement "$native_dir/requirements.txt"
    meson_command="$tools_dir/bin/meson"
else
    meson_command="$tools_dir/bin/meson"
fi

rm -rf "$build_dir" "$output_dir"
mkdir -p "$output_dir"

meson_extra=(-Db_lundef=false -Dcpp_args=-fno-sized-deallocation)
compiler_extra=(-fno-exceptions -fno-rtti -fno-sized-deallocation)
if [[ "$sanitize" == "true" ]]; then
    meson_extra+=(-Db_sanitize=address,undefined)
    compiler_extra+=(-fsanitize=address,undefined -fno-omit-frame-pointer)
    export ASAN_OPTIONS="halt_on_error=1"
    export UBSAN_OPTIONS="halt_on_error=1:print_stacktrace=1"
fi

"$meson_command" setup "$build_dir" "$source_dir" \
    --buildtype=release \
    --default-library=static \
    -Dengines=cpu \
    -Dloaders=svg \
    -Dsavers= \
    -Dbindings=capi \
    -Dtools= \
    -Dtests=false \
    -Dthreads=false \
    -Dsimd=false \
    -Dpartial=false \
    -Dlog=false \
    -Dstatic=true \
    -Dfile=false \
    -Dextra= \
    "${meson_extra[@]}"

"$meson_command" compile -C "$build_dir"

"$cxx" -std=c++17 -fPIC -fvisibility=hidden "${compiler_extra[@]}" "${shared_flags[@]}" "${export_flags[@]}" \
    -I"$native_dir/include" \
    -I"$source_dir/inc" \
    "$native_dir/src/forma_thorvg.cpp" \
    "$build_dir/src/libthorvg-1.a" \
    -lpthread \
    -o "$output_dir/$library_name"

"$cxx" -std=c++17 "${compiler_extra[@]}" \
    -I"$native_dir/include" \
    "$native_dir/tests/smoke.cpp" \
    -L"$output_dir" \
    -lforma_thorvg \
    -Wl,-rpath,"$rpath" \
    -o "$output_dir/forma_thorvg_smoke"

"$output_dir/forma_thorvg_smoke"

static_strip_flags=()
if [[ "$host_os" == "Darwin" ]]; then
    static_strip_flags=(-Wl,-dead_strip)
else
    static_strip_flags=(-Wl,--gc-sections)
fi

"$cxx" -std=c++17 -fvisibility=hidden "${compiler_extra[@]}" \
    -I"$native_dir/include" \
    -I"$source_dir/inc" \
    "$native_dir/src/forma_thorvg.cpp" \
    "$native_dir/tests/smoke.cpp" \
    "$build_dir/src/libthorvg-1.a" \
    -lpthread \
    "${static_strip_flags[@]}" \
    -o "$output_dir/forma_thorvg_static_smoke"

"$output_dir/forma_thorvg_static_smoke"

"$cxx" -std=c++17 -fPIC -fvisibility=hidden -ffunction-sections -fdata-sections "${compiler_extra[@]}" \
    -I"$native_dir/include" \
    -I"$source_dir/inc" \
    -c "$native_dir/src/forma_thorvg.cpp" \
    -o "$output_dir/forma_thorvg.o"

if [[ "$host_os" == "Darwin" ]]; then
    libtool -static -o "$output_dir/libforma_thorvg.a" \
        "$output_dir/forma_thorvg.o" "$build_dir/src/libthorvg-1.a"
else
    ar -M <<EOF
CREATE $output_dir/libforma_thorvg.a
ADDMOD $output_dir/forma_thorvg.o
ADDLIB $build_dir/src/libthorvg-1.a
SAVE
END
EOF
fi