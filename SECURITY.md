# Security Policy

## Supported Versions

Forma is preparing its first public preview and does not yet have a stable support line. Security
fixes are made on the default branch and, after public packages exist, on the latest published
preview when a correction can be shipped safely. Older previews may be unlisted instead of patched.
The support status for runtimes, platforms, native backends, and package families is documented in
[Runtime Support](docs/runtime-support.md).

## Report a Vulnerability

Use [GitHub private vulnerability reporting](https://github.com/zigrok/Forma/security/advisories/new)
for suspected vulnerabilities. This route is enabled for the repository and keeps the report,
attachments, and remediation discussion private. If GitHub reporting is unavailable, email
`igor@zigrok.com` with `Forma security` in the subject.

Include the affected package/version or commit, runtime and platform, impact, reproduction steps, and
whether the issue is already public. Do not include credentials or personal data that are not needed
to reproduce the problem.

Expect acknowledgement within five business days and an initial severity/status assessment within
ten business days. Complex native, parser, or upstream-runtime issues may take longer; maintainers
will provide status updates at least every ten business days while investigation remains active.

## Disclosure and Correction

Please allow maintainers reasonable time to reproduce, coordinate upstream fixes, prepare packages,
and notify affected users before public disclosure. Forma will credit reporters who request credit.
A correction may use a new package version, unlisting of an affected preview, release notes, and a
GitHub security advisory. Published NuGet versions are immutable and will never be overwritten.

Report MonoGame, FNA, Skia, ThorVG, XamlX, or other upstream defects to Forma first when Forma's
packaging or integration may affect exploitability. Maintainers will coordinate an upstream report
without disclosing the reporter's identity unless permission is given.
