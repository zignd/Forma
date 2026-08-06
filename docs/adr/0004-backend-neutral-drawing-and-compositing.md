# ADR 0004: Backend-Neutral Drawing and Bounded Compositing

- Status: Accepted
- Date: 2026-08-04
- Owners: Forma maintainers
- Related plan: `plans/xaml-templates-items-and-virtualization-plan.md`, Phase 0 task 2

## Context

The template-first visual vocabulary needs paths, transforms, brushes, geometry clips, opacity
masks, and effects without exposing MonoGame/FNA types beyond the shared XNA-compatible surface.
The two runtime peers must produce equivalent pixels and hit geometry without reflection, runtime
XAML interpretation, or runtime shader compilation. Tessellation, offscreen work, and caches must
also have finite limits before public visual APIs are frozen.

## Decision

### Shared drawing pipeline

`UIRenderContext.Drawing` exposes a backend-neutral `DrawingContext`. A normalized `DrawingPath`
records move, line, cubic, and close commands. Curves are transformed and then flattened with a
fixed tolerance and bounded subdivision. The same transformed contours drive fill tessellation,
stroke tessellation, clipping, and nonzero-winding hit testing.

The CPU emits indexed triangle meshes. `UIRenderContext` converts them to vertex-colored triangles
and submits them with the peer-provided `BasicEffect`; no runtime-specific public type or runtime
shader compiler is required. Brushes and the first bounded effect are evaluated per mesh vertex
before submission. Existing `SpriteBatch` drawing and rectangular scissor clips remain available
and batch state is restored after neutral mesh submission.

Drawing state is explicit and stack-based. The proven composition slice applies:

1. transformed path flattening and tessellation;
2. one convex arbitrary-geometry clip by triangle/polygon intersection;
3. one two-stop linear opacity mask by vertex alpha;
4. one finite 4x5 color matrix;
5. final vertex-color triangle submission.

The feasibility fixture renders and pixel-checks a transformed cubic fill, gradient stroke,
geometry clip, opacity mask, and color matrix on MonoGame DesktopGL and FNA Metal. Neutral tests run
the same geometry and transformed hit-test contracts against both package peers.

### Finite limits

`DrawingContextLimits` is the runtime source of truth for the initial limits:

| Resource | Limit |
| --- | ---: |
| Saved drawing-state depth | 16 |
| Commands per path | 4,096 |
| Curve subdivision depth | 12 |
| Vertices in one clip contour | 256 |
| Vertices per generated mesh | 16,384 |
| Indices per generated mesh | 49,152 |
| Effect groups per composited element | 1 |
| Shadows per element | 4 |
| Blur radius | 64 logical units |
| Offscreen expansion per side | 128 logical units |
| Nested offscreen passes | 4 |
| Render-target width or height | 4,096 pixels |
| Render-target area | 16,777,216 pixels |
| Device-scoped render-target/effect cache | 64 MiB |

Path, state, clip, and mesh limits are enforced by the feasibility implementation. Phase 1's
compositor must enforce the reserved shadow, blur, expansion, offscreen, target, and cache limits
without increasing them. A statically known violation is a compiler diagnostic. A data-bound
runtime violation omits the affected effect and reports one bounded diagnostic; it may not allocate
past the budget. Cache eviction is least-recently-used with frame-safe disposal.

### Explicitly narrowed behavior

The Phase 0 implementation proves one simple contour per fill, segment-quad strokes, convex
single-contour clips, two-stop linear gradients/masks, and one color matrix. Phase 1 may broaden
path commands, fill rules, joins/caps, gradient families, geometry Boolean operations, shadows, and
blur only while preserving this architecture and the limits above.

Concave or multi-contour clips require deterministic CPU decomposition into convex pieces before
submission. Until that implementation lands, direct `DrawingContext.Clip` rejects them with
`NotSupportedException`; it does not silently approximate them. Blur and shadows require the
bounded offscreen scheduler and target pool and are not implemented by the Phase 0 immediate mesh
slice. Device loss recreates GPU effects, targets, and cached resources from retained neutral data;
controls never own backend resources.

Arbitrary shader/filter graphs, runtime shader source compilation, unbounded filter chains, SVG
filters, and backend-specific public drawing APIs are outside the contract. Custom rendering uses
the future non-templated `DrawingElement` extension point and must provide peer implementations
under the same budgets.

## Rejected Alternatives

- SpriteBatch-only shape approximation: rejected because arbitrary transformed paths, shared hit
  geometry, masks, and future vector assets need one retained geometry model.
- Backend-specific tessellation or effects: rejected because it permits peer drift and leaks
  implementation constraints into public APIs.
- Render-target composition for every primitive: rejected because it adds avoidable allocations and
  device-loss state; direct vertex geometry is the default and offscreen passes are effect-driven.
- Runtime shader compilation: rejected because it conflicts with deterministic packaging and
  NativeAOT guarantees.
- Unlimited adaptive tessellation or caches: rejected because malformed/data-bound content could
  create unbounded CPU, GPU, or memory work.

## Consequences

Forma gains one drawing and hit-test geometry contract shared by MonoGame and FNA. The initial
feature envelope is intentionally narrow, but failures are explicit and every future expansion has
a fixed resource budget. Phase 1 must add retained resource caches, offscreen scheduling, pooling,
and device recreation behind `DrawingContext`; foundational elements and templates do not select a
runtime backend or compile shaders.
