---
title: Release operations
description: Publish, verify, correct, and recover Forma releases on NuGet.org.
---

# Release operations

This runbook covers public Forma package releases. NuGet.org versions are immutable, so the release
workflow treats creation of a `v*` tag as authorization to publish one already validated version.
Ordinary branch pushes and manual workflow runs cannot publish.

## Tagged release

Before creating a tag:

1. Confirm CI and the non-publishing Release dry run pass on the exact commit.
2. Confirm the version in `Directory.Build.props` matches the intended tag without the leading `v`.
3. Recheck every ID in `scripts/release-packages.json` on NuGet.org.
4. Review `RELEASE_NOTES.md`, the API compatibility report, package contents, checksums, and the
   complete runtime-peer manifest.
5. Create and push the tag only after those checks pass. The tag starts protected OIDC publication;
   no long-lived NuGet credential is involved.

The workflow publishes the validated artifact without rebuilding, waits for every primary package
to become available, restores every package from an empty cache, and then creates the matching
GitHub Release. A failed verification must prevent GitHub Release creation.

## Correct or unlist a release

Never overwrite an existing package version. If a published package is incorrect:

1. Stop further release automation and determine whether the defect affects one package or the
   shared peer version.
2. Unlist every affected package through the Zigrok organization on NuGet.org. Unlist the complete
   fourteen-package version when runtime pairing or shared-version integrity is uncertain.
3. Add a prominent notice to the GitHub Release and release notes describing impact, affected
   package IDs, and the replacement version. Use a security advisory instead when disclosure must
   remain private until a fix is ready.
4. Correct the source, increment the version, rerun the complete release matrix, and publish a new
   tag. Do not reuse or move the old tag.
5. Keep the unlisted version and its evidence for diagnosis; deletion is reserved for NuGet.org
   policy or legal requirements.

## Symbol packages

The release manifest controls symbol publication. Tagged releases publish a `.snupkg` beside every
package except entries that explicitly set `"symbols": false`; currently the two XAML build packages
are the only exceptions. Symbol packages must come from the same validated artifact and version as
their `.nupkg`.

After publication, confirm NuGet.org accepts each expected symbol package and that Source Link points
to the tagged `zigrok/Forma` revision. If a symbol package alone is rejected, retain the workflow log
and correct its metadata. Push the matching validated `.snupkg` only when NuGet.org permits completing
that existing version; otherwise publish the correction under a new package version.

## Ownership and account recovery

The Zigrok NuGet.org organization is intentionally administered by the project owner while Forma is
a single-maintainer project. Keep Microsoft account recovery methods, MFA recovery material, and
NuGet.org recovery information outside the repository.

If account access is lost, recover the Microsoft account first and then contact NuGet.org support
with organization and package ownership evidence. For a future maintainer transfer, add the
successor to Zigrok, transfer package ownership and trusted-publishing administration, verify a
tagged release, and only then remove the original administrator.

## Credential or workflow compromise

Trusted publishing creates a short-lived credential for one bound repository, workflow, and
environment. There is no persistent NuGet API key to rotate. If the maintainer account, workflow,
or OIDC policy may be compromised:

1. Disable the Release workflow and remove or disable the Forma trusted-publishing policy on
   NuGet.org.
2. Revoke active GitHub and Microsoft sessions, rotate affected credentials, replace MFA recovery
   material, and review organization, repository, environment, tag, workflow, and NuGet audit logs.
3. Remove unauthorized tags or releases only after preserving evidence. Unlist unauthorized package
   versions; immutable versions cannot be repaired in place.
4. Review workflow and action changes from a known-good commit, restore the tag-restricted
   `nuget-production` policy, and run a non-publishing Release dry run before re-enabling tags.
5. Publish any correction under a new version and document the incident at the appropriate public
   disclosure level.
