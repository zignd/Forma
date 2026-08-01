# Release validation

## 2026-08-01 local candidate review

This review began on macOS Arm64 with .NET SDK 10.0.103 and was extended through hosted Linux,
Windows, and macOS Intel CI. Legal review, name and trademark clearance, publication, and tagging
remain external release gates.

### Baselines

- The source snapshot, commits, and 392-test pre-extraction baseline are recorded in
  `docs/provenance.md`.
- The normalized pre-extraction API baseline contains 185 types and 3,615 declaration lines.
- The current unit and catalog inventory suite discovers and passes 396 tests.
- The current render fixture contains five tests. Four execute on hosted Linux DesktopGL and Windows
  Direct3D; one interactive-only test remains explicitly ignored. On macOS, NUnit excludes the
  fixture before setup because SDL graphics-device creation requires the process main thread.
- The catalog smoke host runs three frames, inventories 74 stories, selects the 2x density font, and
  records a 720x450 logical viewport on hosted Linux DesktopGL and Windows Direct3D.

### Candidate validation

The following commands passed:

```sh
bash scripts/check-compliance.sh
dotnet build Forma.slnx --configuration Release
bash scripts/check-backend-references.sh
bash scripts/check-api-compatibility.sh
dotnet test tests/Forma.Tests/Forma.Tests.csproj --configuration Release
bash scripts/check-catalog-smoke.sh
bash scripts/check-clean-source.sh
bash scripts/test-package-consumer.sh
```

`check-backend-references.sh` compiled the DesktopGL, WindowsDX, and Native reference surfaces.
GitHub Actions run `30687738568` additionally passed the catalog smoke and four executable render
tests on Linux DesktopGL and WindowsDX, alongside the macOS DesktopGL catalog smoke and documented
render-test isolation policy.

The DesktopGL catalog smoke passed on macOS and hosted Linux, and the WindowsDX catalog smoke passed
on hosted Windows. Native Vulkan passed locally on macOS using the published
`MonoGame.Runtime.Mac.Vulkan` 3.8.5 package and on hosted Linux using SwiftShader. Native Metal
passed locally and on hosted macOS Intel after building MonoGame's official `Build Native Metal`
target at commit `99716f1b02ba9db2130c754606d6b0303d039d15` and staging its
`libmgruntime.dylib` beside the catalog output. Native catalog assets are compiled with MGCB's
supported `DesktopVK` content profile because MGCB 3.8.5 does not define a `Native` target. All
Native smoke runs rendered three frames, inventoried 74 stories, selected the 2x density font, and
recorded a 720x450 logical viewport. Clean CI run `30692342601` passed the Linux Vulkan, macOS Intel
Metal, WindowsDX, package-consumer, unit, render, compliance, API, and backend-reference gates
without debugger or validation-layer instrumentation.

The package check produced `Forma` and `Forma.Media` packages and symbol packages, verified their
assemblies, XML documentation, license, notice, README, third-party notices, and migration guide,
confirmed that neither package imposes a transitive MonoGame backend, and ran an isolated consumer
with a private package cache. The compliance scan classified all 18 implementation source files and
reported no pending source classification.

Both package manifests identify the exact repository commit, and both portable PDBs contain Source
Link URLs rooted at that commit. Public artifacts must still be rebuilt from the published commit
before release.

Release workflow run `30688898501` rebuilt and validated both packages from published commit
`a5febeffe3d1df4b80c28c37897df785ad62cc2e`, uploaded the two `.nupkg` and two `.snupkg` files as
the `nuget-packages` artifact, and skipped publication because the run was manually dispatched from
`main`. Tag-triggered publication requires approval through the protected `nuget.org` environment
and authenticates with NuGet trusted publishing instead of a stored API key.

### Identity review

The package descriptions identify Forma as a retained-mode UI toolkit *for* MonoGame. `NOTICE.md`
states that Forma is independent and is not affiliated with or endorsed by Microsoft, the MonoGame
Foundation, or Godot. Package and migration documentation describe MonoGame as a separately licensed
dependency and describe Godot only as provenance for adapted behavior.

The candidate has no project logo or release screenshots to review. No copied Microsoft, MonoGame,
or Godot branding was found in package metadata or documentation. This engineering review does not
constitute name or trademark clearance; that external gate remains open.