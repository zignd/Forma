# Documentation versions and link stability

Every generated page footer identifies the Forma version, support maturity, source commit, and
version index. Public documentation uses these channels:

- `/Forma/dev/` is replaced by each validated default-branch build and is always labeled
  **Development preview**.
- `/Forma/v<version>/` is created from the matching release tag only after package publication and
  clean NuGet.org restore checks succeed. A release path cannot be replaced from another commit.
- `/Forma/latest/` and `/Forma/` redirect to the newest published release, or to `dev/` before the
  first release.
- `/Forma/versions/` is the human-readable index. `/Forma/versions.json` is its generated,
  machine-readable contract.

The `gh-pages` branch retains earlier release directories while `dev/` advances. Pull requests
produce review artifacts but never mutate public channels.

## Redirect policy

Release URLs are immutable. Renamed pages keep an authored redirect at the old path for at least one
major release. Before an authored redirect exists, the root `404.html` preserves an unversioned
legacy path and resolves it beneath the current default channel. It does not redirect an unknown
path already scoped to `dev/`, `latest/`, `versions/`, or a release version, so broken versioned
links remain visible rather than silently landing on different content.

Changes to a public path must update this page, the generated redirect contract, and link checks in
the same pull request. Removing a release directory or changing a `v<version>/` path is not an
allowed correction; publish a new version instead.
