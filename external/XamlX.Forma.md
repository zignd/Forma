# XamlX Dependency Record

## Selected Revision

Forma consumes XamlX only in compiler and development tooling through the Git submodule at
`external/XamlX`.

- Upstream: `https://github.com/kekekeks/XamlX`
- Evaluated NuGet packages: `XamlX` and `XamlX.IL.Cecil` 1.0.0
- NuGet source commit: `5da4e1d570a13ee270fafccd3778d29fc6ddc7f2`
- Audited upstream base: `a4e6be2d1407abec4f35fcb208848830ce513ead`
- Maintained fork: `https://github.com/zignd/XamlX`
- Fork branch: `forma/nativeaot-compat`
- Pinned fork commit: `0337e9b2f6450ac90cb988a3fac61f36f58c4fcc`
- License: MIT, preserved in the submodule as `LICENSE`

The NuGet 1.0.0 source commit restores and loads on .NET 10 but does not expose configurable
`IAddChild`/`IAddChild<T>` mappings. The audited base is the upstream commit that introduced those
mappings. Forma does not follow XamlX `master`.

## Fork Delta

The pinned fork commit contains two focused changes:

1. Deduplicate the type and inherited-interface list before `IAddChild` discovery. A normal
   `IAddChild<T> : IAddChild` hierarchy otherwise presents the non-generic interface more than once
   and the upstream `SingleOrDefault` lookup throws during content transformation.
2. Update the compiler-only `Mono.Cecil` dependency from 0.10.3 to 0.11.6. Cecil 0.10.3 calls an
   obsolete runtime detector and cannot create an assembly when the Forma compiler runs on .NET
   10.

The `tests/Forma.XamlSpike` executable is the regression gate for this delta. It proves SRE build
and populate, generic child insertion, CLR events, markup extensions, line-aware custom AST
diagnostics, Cecil IL emission, portable PDB writing, and that emitted assemblies do not reference
XamlX or Mono.Cecil.

## Runtime and NativeAOT Boundary

XamlX and Mono.Cecil are implementation dependencies of the build-time compiler and opt-in debug
hot-reload package. Generated views and the Forma runtime assembly must not reference or package
XamlX, Mono.Cecil, SRE, the runtime compiler, or source XAML. Release trimming and macOS arm64
NativeAOT consumer tests enforce that boundary.

`scripts/test-xaml-spike.sh` emits a generated view, compiles a direct consumer with macOS arm64
NativeAOT, executes the resulting Mach-O binary, and rejects XamlX, Mono.Cecil, or source XAML in
the publish directory. The gate passed against the pinned fork commit on .NET 10.

NativeAOT incompatibility in a compiler dependency is not an automatic no-go when a bounded patch
can remove the incompatible runtime surface. Any additional Forma-specific changes belong in the
maintained fork, require a focused regression in Forma, and must be recorded here before the
submodule pointer advances.

## Clone and Update Procedure

Initialize the pinned dependency after cloning Forma:

```sh
git submodule update --init --recursive
```

To update the fork, start from the audited fork branch, make and validate the smallest compatible
change in `zignd/XamlX`, then update Forma to an exact reviewed commit:

```sh
git -C external/XamlX fetch origin forma/nativeaot-compat
git -C external/XamlX checkout <reviewed-commit>
make xaml-spike
```

Record the new commit and delta in this file. Review the submodule diff with
`git diff --submodule=log -- external/XamlX` before committing the Forma gitlink. Do not use
`git submodule update --remote` in CI or release builds.