#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(grep -oE '<FormaVersion>[^<]+</FormaVersion>' "$repo_root/Directory.Build.props" | sed -E 's/<\/?FormaVersion>//g')"
package_root="$repo_root/artifacts/svg-package-consumers/packages"
consumer_root="$repo_root/artifacts/svg-package-consumers/consumers"
project="$repo_root/tests/Forma.Svg.PackageConsumer/Forma.Svg.PackageConsumer.csproj"

case "$(uname -s)-$(uname -m)" in
    Darwin-arm64) rid="osx-arm64"; native_name="libforma_thorvg.dylib"; shared_flags=(-dynamiclib) ;;
    Linux-x86_64) rid="linux-x64"; native_name="libforma_thorvg.so"; shared_flags=(-shared -fPIC) ;;
    *) rid=""; native_name=""; shared_flags=() ;;
esac

rm -rf "$package_root" "$consumer_root" "$repo_root/tests/Forma.Svg.PackageConsumer/bin" "$repo_root/tests/Forma.Svg.PackageConsumer/obj"

for runtime in MonoGame FNA; do
    runtime_packages="$package_root/$runtime"
    mkdir -p "$runtime_packages"
    for package_project in Forma Forma.Svg Forma.Svg.ThorVG Forma.Svg.Compatibility; do
        dotnet pack "$repo_root/src/$package_project/$package_project.csproj" \
            --configuration Release -p:FormaRuntime="$runtime" -p:PackageOutputPath="$runtime_packages" --nologo
    done

    for backend in None Skia ThorVG Compatibility; do
        package_cache="$consumer_root/$runtime/$backend/packages"
        publish_dir="$consumer_root/$runtime/$backend/publish"
        rm -rf "$package_cache" "$publish_dir" "$repo_root/tests/Forma.Svg.PackageConsumer/bin" "$repo_root/tests/Forma.Svg.PackageConsumer/obj"
        NUGET_PACKAGES="$package_cache" dotnet restore "$project" \
            -p:FormaRuntime="$runtime" -p:SvgBackend="$backend" -p:FormaPackageSource="$runtime_packages" \
            -p:RestoreNoCache=true --nologo
        NUGET_PACKAGES="$package_cache" dotnet publish "$project" \
            --configuration Release --self-contained false \
            -p:FormaRuntime="$runtime" -p:SvgBackend="$backend" -p:FormaPackageSource="$runtime_packages" \
            -p:PublishDir="$publish_dir" --no-restore --nologo
        "$publish_dir/Forma.Svg.PackageConsumer"

        case "$backend" in
            None)
                find "$publish_dir" -type f \( -iname '*skia*' -o -iname '*thorvg*' \) -print -quit | grep -q . && exit 1
                ;;
            Skia|Compatibility)
                [[ -f "$publish_dir/Forma.Svg.Skia.dll" ]]
                [[ ! -f "$publish_dir/Forma.Svg.ThorVG.dll" ]]
                [[ ! -f "$publish_dir/libforma_thorvg.dylib" && ! -f "$publish_dir/libforma_thorvg.so" ]]
                ;;
            ThorVG)
                [[ -f "$publish_dir/Forma.Svg.ThorVG.dll" ]]
                [[ ! -f "$publish_dir/Forma.Svg.Skia.dll" ]]
                find "$publish_dir" -type f -iname '*skia*' -print -quit | grep -q . && exit 1
                ;;
        esac
    done

    if [[ -n "$rid" ]]; then
        thorvg_publish="$consumer_root/$runtime/ThorVG/publish"
        thorvg_native="$thorvg_publish/runtimes/$rid/native/$native_name"
        original_native="$consumer_root/$runtime/ThorVG/$native_name.original"
        cp "$thorvg_native" "$original_native"

        rm "$thorvg_native"
        FORMA_EXPECT_THORVG_FAILURE=DllNotFoundException "$thorvg_publish/Forma.Svg.PackageConsumer"

        cc "${shared_flags[@]}" -DFORMA_THORVG_FAKE_ABI=2 \
            "$repo_root/native/Forma.ThorVG/tests/fake_abi.c" -o "$thorvg_native"
        FORMA_EXPECT_THORVG_FAILURE='ABI mismatch' "$thorvg_publish/Forma.Svg.PackageConsumer"

        cc "${shared_flags[@]}" -DFORMA_THORVG_FAKE_ABI=1 \
            "$repo_root/native/Forma.ThorVG/tests/fake_abi.c" -o "$thorvg_native"
        FORMA_EXPECT_THORVG_FAILURE=EntryPointNotFoundException "$thorvg_publish/Forma.Svg.PackageConsumer"
        mv "$original_native" "$thorvg_native"

        single_cache="$consumer_root/$runtime/ThorVG-single/packages"
        single_publish="$consumer_root/$runtime/ThorVG-single/publish"
        rm -rf "$single_cache" "$single_publish" "$repo_root/tests/Forma.Svg.PackageConsumer/bin" "$repo_root/tests/Forma.Svg.PackageConsumer/obj"
        NUGET_PACKAGES="$single_cache" dotnet restore "$project" -r "$rid" \
            -p:FormaRuntime="$runtime" -p:SvgBackend=ThorVG -p:FormaPackageSource="$runtime_packages" \
            -p:RestoreNoCache=true --nologo
        NUGET_PACKAGES="$single_cache" dotnet publish "$project" \
            --configuration Release --runtime "$rid" --self-contained true \
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:FormaRuntime="$runtime" -p:SvgBackend=ThorVG -p:FormaPackageSource="$runtime_packages" \
            -p:PublishDir="$single_publish" --no-restore --nologo
        "$single_publish/Forma.Svg.PackageConsumer"
        find "$single_publish" -type f -iname '*skia*' -print -quit | grep -q . && exit 1
    fi

    mixed_output="$(dotnet build "$project" --configuration Release \
        -p:FormaRuntime="$runtime" -p:IncludeMixedBackends=true -p:FormaPackageSource="$runtime_packages" \
        --nologo 2>&1)" && {
        printf 'Mixed SVG backends unexpectedly built for %s.\n' "$runtime" >&2
        exit 1
    }
    grep -Fq 'cannot be referenced by the same project' <<<"$mixed_output"
done

printf 'SVG clean-package consumers passed for no backend, Skia, ThorVG, compatibility, native failures, single-file, and mixed rejection.\n'