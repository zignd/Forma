# Authorized Host Checklist

Use this NDA-neutral checklist inside the authorized integration repository for each distinct
platform, runtime port, graphics backend, and packaging toolchain. Keep SDK names, paths, headers,
libraries, logs, symbols, device identifiers, and platform-holder requirements out of public Forma
source, issues, CI, and artifacts. Report only disclosure-approved capability results publicly.

## Integration Identity

- [ ] Record the Forma version and commit, package lock, .NET/runtime port revision, graphics
  backend, compiler/linker revisions, and private evidence location.
- [ ] Assign an owner and separate approval for this exact platform/runtime/backend combination.
- [ ] Confirm the integration repository, CI runners, caches, logs, and artifacts meet the
  platform-holder's confidentiality requirements.

## Build and Runtime Assembly

- [ ] Run Forma XAML injection on an approved build host before trimming, platform AOT compilation,
  linking, signing, and final packaging.
- [ ] Reject signed intermediate assemblies unless an approved post-injection re-signing stage is
  configured.
- [ ] Verify the runtime port preserves every statically referenced Forma member and generated XAML
  member without whole-assembly roots or reflection-binding fallback.
- [ ] Verify deployed output contains no source XAML, XamlX, Mono.Cecil, MSBuild task, Forma compiler
  or build assembly, hot reload, file watcher, or dynamic-code path.

## Graphics, Input, and Layout

- [ ] Validate graphics-device creation, theme/resource loading, compiled-XAML rendering, resize,
  display scale, safe areas, device loss/reset, and clean graphics shutdown.
- [ ] Validate controller-only focus traversal, activation, cancellation, disconnection,
  reconnection, user switching, and capability absence for pointer, keyboard, and text input.
- [ ] Exercise representative menus, HUDs, settings, localization, and lifecycle restoration.

## Filesystem and Platform Services

- [ ] Start with no writable application directory and no system-font assumption.
- [ ] Supply explicit filesystem, URI, clipboard, text-input, and font-source capabilities where
  permitted; verify stable unavailable behavior everywhere else.
- [ ] Validate save-data/user-storage boundaries, mount changes, quota/full-storage behavior, and
  asynchronous platform-service failures without exposing confidential error details publicly.

## Lifecycle and Threading

- [ ] Validate startup, suspend, resume, backgrounding, memory pressure, user switching, graphics
  reset, repeated initialization, and clean shutdown.
- [ ] Verify shaping and file work remain on permitted worker threads and glyph atlas creation,
  upload, reset, and disposal remain on the required graphics thread.
- [ ] Record bounded memory, glyph atlas, fallback depth, and allocation limits for representative
  localized screens.

## Native Fonts and Media

- [ ] Supply approved FreeType/HarfBuzz dynamic, static, or platform-adapter implementations and
  verify architecture, imports/exports, callbacks, ownership, and disposal.
- [ ] Validate multilingual shaping, fallback, rasterization, atlas upload/draw, missing glyphs,
  malformed fonts, missing native libraries, and rejected native libraries.
- [ ] Package optional media only when its backend, codecs, licenses, and platform policy are
  approved; otherwise verify a stable unavailable capability.

## Diagnostics, Packaging, and Deployment

- [ ] Verify warning-free trim/AOT compilation, package size, forbidden imports, native symbols,
  stripping, signing, manifest generation, installation, launch, update, and uninstall.
- [ ] Retain exact private build inputs, linker/AOT logs, symbols, native dependency manifests,
  crash evidence, and hardware smoke results according to approved retention policy.
- [ ] Confirm diagnostics are bounded and disclose no confidential paths, SDK identities, symbols,
  device identifiers, or platform-holder data.
- [ ] Deploy the final signed package through the approved channel and execute startup, rendering,
  input, lifecycle, dynamic-text, and shutdown smokes on hardware or an approved equivalent.

## Approval

- [ ] Complete security, legal, native redistribution, accessibility, localization, performance,
  submission, and platform-holder reviews required for this target.
- [ ] Record private sign-off and expiry/revalidation conditions.
- [ ] Publish only an approved support level and limitations summary in the public runtime matrix;
  do not infer support for another runtime, backend, toolchain, or target.
