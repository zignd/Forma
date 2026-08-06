# Forma Runtime SVG Profile v1

Profile v1 is the behavior intersection required from every production Forma SVG backend. The
machine fixture source is `tests/Forma.Svg.Conformance/SvgProfileV1Fixtures.cs`; the policy manifest
is `tests/Forma.Svg.Conformance/runtime-svg-profile-v1.json`.

## Included

- finite positive root dimensions and view boxes;
- `preserveAspectRatio` meet, slice, and none;
- paths, rectangles, rounded rectangles, circles, ellipses, lines, polylines, and polygons;
- fills, fill rules, strokes, opacity, caps, joins, miter limits, and dash arrays;
- nested groups and affine transforms;
- linear/radial gradients, stop opacity, supported spread, and gradient transforms;
- local clipping, masks, `defs`, `use`, and fragment references;
- bounded presentation attributes, inline class styles, inheritance, and `currentColor`.

Every fixture must produce non-empty, exact-size, tightly packed, top-left, premultiplied sRGB RGBA8
at 1x, 1.25x, 1.5x, 1.75x, 2x, and 2.5x. Every pixel must satisfy `R <= A`, `G <= A`, and `B <= A`.

## Excluded

Core validation rejects scripts/events, animation, foreign objects, image/data/external resources,
text and arbitrary fonts, external stylesheets, filters/blur, blend modes, ICC profiles, duplicate
IDs, cyclic references, and non-finite or over-budget documents before native parsing. Rejection is
part of the profile and may not become an empty successful image or trigger fallback to another
backend.

## Comparison Rules

Backend-local hashes detect changes within a pinned backend. Cross-backend output is not required to
be byte-identical. A comparison passes only when all semantic samples pass and:

- dimensions and buffer length are exact;
- transparent/opaque interior sample points agree exactly where antialiasing is irrelevant;
- non-transparent bounds differ by no more than `ceil(scale)` pixels per edge;
- alpha coverage differs by no more than 4 percentage points of total image area for production
	theme icons and 8 percentage points for tiny semantic fixtures;
- production theme icons have mean premultiplied channel error at most 12/255 and 95th percentile
	error at most 48/255;
- tiny semantic profile fixtures have mean error at most 26/255 and 95th percentile error at most
	128/255, alongside their exact semantic sample assertions.

These measured limits account for a one-pixel antialiasing phase in 8-10 px fixtures; they are not
relative-to-object percentages, which distort one-pixel lines. Tolerance never permits empty output,
a missing shape, wrong clipping, straight alpha, shifted
geometry beyond the bound, or an out-of-bounds write. For example, dropping a thin stroke fails its
semantic sample/bounds even if mean image error stays low; translating an otherwise identical icon
two pixels at 1x fails the geometry rule.

## Change Control

Adding a feature requires a positive fixture, deterministic excluded-feature behavior where
applicable, both isolated backend runs, visual review at all approved scales, and an updated profile
version if public behavior changes. First-party theme/Catalog assets may only use profile features.
