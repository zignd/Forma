# ADR 0003: Dynamic Text Security Limits

- Status: Accepted
- Date: 2026-08-01
- Owners: Forma maintainers

## Decision

Dynamic text rejects work before native calls or allocation when any limit below is exceeded. All
limits apply independently to MonoGame and FNA builds and produce the same Forma exception type and
error code.

| Resource | Initial limit |
| --- | ---: |
| Font source bytes | 64 MiB per source |
| Faces in a font collection | 256 |
| SFNT tables | 4,096 per face |
| Single SFNT table | 32 MiB |
| Aggregate declared SFNT table bytes | 64 MiB per face |
| Glyph bitmap dimension | 4,096 pixels per axis |
| Glyph bitmap area | 16,777,216 pixels |
| Alpha8 atlas page dimension | 2,048 pixels per axis |
| Alpha8 atlas pages | 8 per graphics device |
| Alpha8 atlas memory | 32 MiB per graphics device |
| Fallback faces examined | 16 per grapheme cluster |
| Layout input | 1,000,000 UTF-16 code units |
| Shaped glyphs | 1,000,000 per layout |
| Lines | 100,000 per layout |
| Raster requests | 65,536 distinct glyph keys per layout |
| Shaping budget | 500 ms per layout |
| Rasterization budget | 100 ms per glyph and 2 s per layout |

Checked arithmetic is mandatory for every byte count, row pitch, rectangle, and texture-size
calculation. Zero and negative dimensions, offsets outside the source buffer, overlapping SFNT
table ranges, and glyph bitmaps whose pitch cannot contain one row are invalid data.

The page and memory limits are both enforced. A future color atlas has a separate explicit budget;
it cannot consume the Alpha8 allowance implicitly. Eviction may recover from a full cache, but one
layout cannot bypass raster-request or fallback limits by causing repeated eviction.

## Time Budgets

The implementation checks a monotonic deadline before and after each HarfBuzz run and FreeType
glyph operation. Exceeding a budget aborts remaining work and returns a deterministic limit error.
Cancellation is cooperative between native calls; managed code cannot safely interrupt one active
FreeType or HarfBuzz call.

Consequently, arbitrary adversarial fonts are not accepted as a fully sandboxed in-process input.
Applications that ingest untrusted uploads must validate them out of process with operating-system
CPU and memory limits before passing bytes to Forma. Forma's structural, count, allocation, and
deadline checks remain defense in depth for trusted application assets and damaged files.

## Configuration

Public APIs may allow applications to lower limits. Raising hard font, bitmap, atlas, fallback, or
layout limits requires an explicit advanced configuration object created before the text service;
controls cannot mutate limits. Package defaults and error behavior remain identical between peers.

## Validation

Boundary tests cover exactly-at-limit and one-over-limit values without allocating the rejected
size. Malformed-font fuzzing records the seed, binding/native versions, runtime peer, platform, and
failure category. Time-budget tests use injected clocks around managed orchestration; native hangs
are covered by process-level CI watchdogs.
