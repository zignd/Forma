# ThorVG Build, Provenance, and Host Integration

## Source Pin

- Upstream: `https://github.com/thorvg/thorvg.git`
- Release: `v1.1.0`
- Commit: `1b3aed2c188571f49ef4d87062b9f92ff1426c57`
- License: MIT, Copyright (c) 2020-2026 ThorVG Project
- Reproducible `git archive --format=tar v1.1.0` SHA-256:
  `5511d2412a2dc88e420bb90590e842a6e70e0c339c76f7f735e524e3b8434a20`
- License SHA-256: `74242cdc4ccaebc73fa04fbd52a538429eae2be7a231f9eaf1dbf59d2bedc16b`
- Build system: Meson 1.9.1 and Ninja

ThorVG is a pinned Git submodule because source and history must be available to private restricted
builds without a runtime download. Update by reviewing a signed/tagged upstream release, changing the
submodule commit and checksums, rebuilding every declared RID, running sanitizers/profile/lifetime
checks, reviewing output differences, and only then updating package/version evidence.

## Minimal Configuration

`scripts/build-thorvg-spike.sh` configures a static ThorVG core with CPU engine and SVG loader only.
C API bindings are compiled for upstream verification, while managed code calls the narrower Forma
ABI. File I/O, threads, SIMD, partial rendering, logs, tools, tests, savers, image/font/Lottie loaders,
OpenMP, exceptions, RTTI, and sized deallocation are disabled. Sized deallocation is disabled because
ThorVG 1.1.0 pairs its `malloc`-backed global `new` with unsized `free`-backed `delete`; GCC otherwise
selects an unmatched sized runtime delete. A build needs only the pinned source, C++ compiler,
Meson, Ninja, and platform system libraries; ThorVG itself fetches no dependency.

```sh
make thorvg-spike
make thorvg-linux
THORVG_SANITIZE=true THORVG_ARTIFACTS_DIR="$PWD/artifacts/native/thorvg/sanitized" \
  bash scripts/build-thorvg-spike.sh
```

Validated source builds are macOS arm64 with Apple Clang 21 and Linux x64 with GCC 13.3. Windows is
not in the initial declared matrix. ASan+UBSan passed the native ABI smoke. Raw malformed/truncated
mutation seeds exercise bounded parse and ownership only because the ABI requires core-validated
input; well-formed bounded profile seeds exercise parse plus rasterization. macOS Leaks reported
zero leaks after 1,000 parse/raster/dispose lifetimes and a 2,976 KiB final / 8,304 KiB peak process
footprint for the static smoke host.

## Forma ABI 1

`native/Forma.ThorVG/include/forma_thorvg.h` is the authority. The dynamic library exports only:

- ABI/backend version and bounded last-error retrieval;
- explicit engine initialize/terminate;
- document create/destroy and intrinsic size;
- exact-size rasterization into caller-owned memory.

No C++ type, exception, allocator, callback, file path, URI, ThorVG object, or graphics-device handle
crosses the boundary. Source is copied in-memory. Output is exactly `width * height * 4`, top-left,
premultiplied sRGB RGBA8. Managed ownership uses source-generated `LibraryImport` and `SafeHandle`.
ABI mismatch and missing entry points fail health probing before parsing.

Desktop packages place dynamic libraries under NuGet RID native paths. Restricted hosts compile the
same adapter source and link the same ABI symbols statically. The checked-in static smoke host proves
symbol behavior and dead stripping, but does not qualify any console. Private adapters must not
commit SDK details and must record only policy-approved target ID, ABI/profile/source versions,
manifest hash, pass/fail counts, budget status, and approval date.

Official package maintainers own public desktop binaries. Source customers or engine integrations
own compilation/final linking for unshipped targets. Console platform owners/integrators own all
proprietary compilation and qualification evidence.
