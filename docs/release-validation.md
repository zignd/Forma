# Release validation

## 2026-08-01 local candidate review

This review was performed on macOS Arm64 with .NET SDK 10.0.103. Linux and Windows runtime
validation, legal review, name and trademark clearance, publication, and tagging remain external
release gates.

### Baselines

- The source snapshot, commits, and 392-test pre-extraction baseline are recorded in
  `docs/provenance.md`.
- The normalized pre-extraction API baseline contains 185 types and 3,615 declaration lines.
- The current unit and catalog inventory suite discovers and passes 396 tests.
- The current render fixture contains five tests. It compiles on macOS, where NUnit excludes runtime
  execution before fixture setup because SDL graphics-device creation requires the process main
  thread.
- The catalog smoke host runs three frames, inventories 74 stories, selects the 2x density font, and
  records a 720x450 logical viewport.

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
This is compile validation, not Linux or Windows runtime validation.

The DesktopGL catalog smoke passed on macOS. A Native/Metal launch reached runtime initialization
but could not load `mgruntime.dylib`; no locally built MonoGame Native runtime was available. The
macOS Metal runtime gate therefore remains open alongside Linux Vulkan and Windows Direct3D.

The package check produced `Forma` and `Forma.Media` packages and symbol packages, verified their
assemblies, XML documentation, license, notice, README, third-party notices, and migration guide,
confirmed that neither package imposes a transitive MonoGame backend, and ran an isolated consumer
with a private package cache. The compliance scan classified all 18 implementation source files and
reported no pending source classification.

Source Link remains incomplete until the repository has its first commit. A public candidate must be
repacked and reinspected from committed source before release artifacts can pass the final technical
gate.

### Identity review

The package descriptions identify Forma as a retained-mode UI toolkit *for* MonoGame. `NOTICE.md`
states that Forma is independent and is not affiliated with or endorsed by Microsoft, the MonoGame
Foundation, or Godot. Package and migration documentation describe MonoGame as a separately licensed
dependency and describe Godot only as provenance for adapted behavior.

The candidate has no project logo or release screenshots to review. No copied Microsoft, MonoGame,
or Godot branding was found in package metadata or documentation. This engineering review does not
constitute name or trademark clearance; that external gate remains open.