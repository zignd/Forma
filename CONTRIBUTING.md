# Contributing to Forma

Forma accepts focused bug fixes, documentation improvements, tests, and proposals that preserve the
public behavior of both runtime peers. Discuss broad API or architecture changes in an issue before
investing in an implementation.

## Set Up

Prerequisites:

- .NET SDK 10.0.x;
- Git with submodule support;
- platform graphics/native dependencies required by MonoGame or FNA for graphical tests.

```sh
git clone --recurse-submodules https://github.com/zigrok/Forma.git
cd Forma
make setup
make build-monogame
make test-unit-monogame
```

Use `make build-fna` and `make test-unit-fna` for the FNA peer. Run `make help` for the complete,
current command list.

## Repository Map

- `src/`: runtime-neutral libraries compiled once per runtime peer;
- `samples/`: Catalog and end-to-end XAML applications with thin MonoGame/FNA hosts;
- `tests/`: unit, compiler, package-consumer, rendering, and integration fixtures;
- `native/`: reviewed native backend integration;
- `tools/`: deterministic asset and validation utilities;
- `scripts/`: parity, packaging, smoke, compliance, and documentation gates;
- `docs/`: user guides, support contracts, ADRs, and the Docfx site;
- `plans/`: implementation plans and historical readiness evidence, not user documentation.

See [Contributor Architecture](docs/contributor-architecture.md) for ownership boundaries and focused
validation commands.

## Make a Focused Change

Keep one pull request centered on one behavior or tightly related set of behaviors. Preserve existing
public APIs unless the change has an approved compatibility plan. Do not edit generated outputs when
the owning manifest or generator can produce them.

Runtime-neutral code must remain API-equivalent across MonoGame and FNA. Do not reference both
framework peers in one output assembly. Run the narrowest relevant check while iterating, then the
broader owner gate before opening a pull request.

```sh
# Runtime API and dependency isolation
make parity

# Core unit tests
make test-unit

# XAML compiler, runtime, tooling, and fixtures
make test-xaml

# XAML source formatting
make format-xaml-check

# Documentation, API parity, and links
make docs-check

# Compliance and generated default icons
make compliance
make icons-verify
```

Graphics tests may require the operating-system setup used by CI. Document any test you could not
run and why.

## Contribution Labels

Maintainers apply `good first issue` only when the issue:

- has one bounded owner area and no unresolved API, architecture, security, or release decision;
- states the expected behavior, relevant files or starting point, and acceptance checks;
- can be completed with project-owned prerequisites and a focused validation command;
- does not require private platform access, unpublished credentials, or native artifact signing;
- has a maintainer available to answer repository-specific questions and review the result.

Maintainers apply `help wanted` when the scope and desired outcome are accepted, but implementation
would benefit from contributor time or domain expertise. The issue must identify runtime/backend
impact, evidence required for both peers, and any prerequisite design or upstream work. It may be
larger than a first contribution, but it must not be an unreviewed feature request.

Remove either label when new investigation reveals a blocked design decision, confidential platform
dependency, stale reproduction, or materially larger scope. Contributors may ask for an issue to be
split before claiming it; assignment does not reserve an inactive issue indefinitely.

## Generated Assets and Dependencies

Use `make icons-import` only with the reviewed upstream Godot checkout, then run `make icons-verify`.
Use `make unicode` and `make unicode-verify` for Unicode tables. Changes to third-party code, native
artifacts, fonts, icons, or licenses must update `NOTICE.md` or `THIRD-PARTY-NOTICES.md` when
applicable and include provenance in the pull request.

Do not commit build output under `Artifacts/`, generated Docfx metadata, package caches, credentials,
or local SDK paths.

## Documentation

User-facing behavior changes require the corresponding guide, API comment, support matrix, or
release note. Build the site with `make docs`; preview it with `make docs-serve`. Keep volatile
versions and counts tied to the canonical sources listed in
[Documentation Inventory](docs/documentation-inventory.md).

## Commits and Pull Requests

Use imperative, contextual commit messages and avoid unrelated formatting churn. A pull request must
explain runtime/backend impact, tests, public API or XAML changes, documentation, visual evidence for
rendering changes, and dependency/license impact. Complete the repository pull-request template;
maintainers may ask for a smaller scope when independent changes cannot be reviewed safely together.

Release notes are expected for user-visible behavior, compatibility changes, new packages or native
assets, and deprecations. Public API changes require parity validation and explicit compatibility
review.
