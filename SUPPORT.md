# Forma Support

Use the route that matches the request so maintainers receive the information needed to respond.

| Need | Route |
| --- | --- |
| Reproducible Forma defect | Bug report issue form |
| Documentation error or missing explanation | Documentation issue form |
| New capability or API proposal | Feature request issue form |
| New platform, runtime, RID, or backend | Platform/backend issue form |
| Suspected vulnerability | Private route in `SECURITY.md` |
| Conduct concern | Private route in `CODE_OF_CONDUCT.md` |
| General usage question | GitHub Discussions when enabled; otherwise a focused question in an issue |

Before reporting a runtime-specific defect, check [Runtime Support](docs/runtime-support.md) and try
the same package family throughout the dependency graph. MonoGame and FNA packages are not binary
interchangeable. ThorVG support is currently limited to the RIDs explicitly marked as validated.

Include a minimal reproduction, Forma version or commit, .NET SDK, operating system/RID, runtime
package and version, graphics backend, build configuration, logs, and whether the Catalog reproduces
the problem. XAML reports should include the diagnostic code and the smallest relevant XAML/C# pair.
Rendering reports should include a screenshot or artifact when licensing permits.

Forma cannot provide platform-holder SDK access, console qualification, bespoke game integration,
or guaranteed response times for general support. Unsupported platform requests are welcome as
proposals when they include an owner, validation access, redistribution constraints, and a realistic
maintenance plan.
