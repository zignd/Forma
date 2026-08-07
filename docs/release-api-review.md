---
title: Release API review
description: Review Forma public API additions, removals, and signature changes before release.
---

# Release API review

Forma snapshots normalized public signatures for the seven logical packages in
`scripts/release-packages.json`. The MonoGame assemblies are canonical because the runtime-parity
gate first requires their FNA peers to expose the same API. Explicitly excluded compatibility
packages are not part of the snapshot.

Run the release review from the repository root:

```sh
bash scripts/check-release-api.sh
```

The check treats a new signature as a compatible addition and reports it for review. A removed
signature fails. A return type, visibility, base type, interface, generic constraint, field,
property, event, or method-signature change appears as a removal plus an addition and therefore also
fails.

## Acknowledge a breaking change

Do not edit `scripts/public-api-baseline.json` to make a pending change pass. Add one sorted entry to
`scripts/api-migrations.json` for every removed normalized signature:

```json
{
  "assembly": "Forma",
  "removedSignature": "method public System.Void Forma.Example::OldName`0()",
  "releaseNoteText": "Replace Example.OldName with Example.NewName.",
  "referencePath": "docs/example-migration.md",
  "referenceText": "Example.NewName"
}
```

`releaseNoteText` must occur verbatim in `RELEASE_NOTES.md`. `referencePath` must stay inside the
repository, and that file must contain `referenceText`. This makes the release impact and updated
reference independently reviewable. Stale or duplicate acknowledgements fail.

## Roll the baseline

Only roll the baseline after the corresponding package version has been published and its migration
notes are live. Build the release commit, then run:

```sh
FORMA_API_BASELINE_CREATE=true bash scripts/check-release-api.sh
```

Review the complete baseline diff, remove acknowledgements now incorporated into the published
baseline, rerun the normal check, and commit both changes with the next development-version update.
The Release workflow runs the normal review before packaging and never creates a baseline.
