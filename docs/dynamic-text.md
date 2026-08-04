# Dynamic Text

Forma exposes the same text contracts in its MonoGame and FNA variants. Runtime-loaded text uses
`UIFontFace`, `DynamicUIFont`, `TextLayoutEngine`, and device-scoped glyph atlases. Existing
`SpriteFont` applications remain supported through `SpriteFontAdapter`.

Installing the runtime-matched dynamic package gives each new `UIContext` a packaged Inter
`DynamicUIFont` at 16 logical pixels. Set `FormaDynamicTextDefaultEnabled=false` in the application
project to disable this initializer. Core-only and opted-out applications continue to resolve only
explicitly assigned fonts. Applications can replace the default through `UIContext.Theme.FontFamily`
or call `DynamicTextDefaults.Install` before constructing contexts.

## Packages and Deployment

Use the package matching the application's framework:

| Runtime | Native-free compatibility | Dynamic text |
| --- | --- | --- |
| MonoGame | `Forma.MonoGame` | `Forma.DynamicText.MonoGame` |
| FNA | `Forma.FNA` | `Forma.DynamicText.FNA` |

The core package is the native-free compatibility profile for restricted platforms and authorized
console ports. It does not include FreeType, HarfBuzz, or their native libraries. Trim-only and
NativeAOT package consumers are validated for both peers on macOS arm64, including native-free
SpriteFont and optional dynamic-text graphical profiles. Run
`bash scripts/test-nativeaot-package-consumer.sh` to reproduce the packed-artifact gate. Other RIDs
remain unsupported until equivalent executable gates pass. Actual console support remains
conditional on the selected MonoGame/FNA platform port, platform-holder approval, and validation on
authorized hardware.

The optional dynamic package resolves FreeType and HarfBuzz native assets for its declared runtime
identifiers. Publish and test every target RID in a clean environment; do not rely on system-installed
libraries. Ship font licenses and the repository third-party notices with redistributed fonts.
Forma selects the permissive FreeType License and HarfBuzz's MIT license. Binary redistribution
requires retaining their acknowledgments and notices, which `make compliance` enforces; neither
selected license requires a source offer. Modified or source redistribution must be reviewed against
the corresponding upstream terms rather than inferred from this binary-package conclusion.

### Internal Backend Boundary

`UIFontFace`, `DynamicUIFont`, and `TextLayoutEngine` do not expose FreeTypeSharp, HarfBuzzSharp,
native handles, or platform font types. `UIFontFace` delegates face metadata, character/glyph lookup,
metrics, variations, shaping, rasterization, diagnostics, and disposal to an internal backend
contract. The normal `Forma.DynamicText.<Runtime>` build selects `FreeTypeHarfBuzz` and preserves
the existing desktop package behavior.

Authorized source builds may set `FormaDynamicTextBackend=External` and provide
`FormaDynamicTextBackendSource` pointing to a source file compiled into `Forma.DynamicText`. That
file defines the internal `ExternalDynamicTextBackend` implementation. Selection is compile-time:
there is no assembly scanning, reflection activation, runtime generic construction, or public
backend API. The source remains in authorized infrastructure when it contains platform SDK details.

Run `make static-font-backend` to publish and execute the NDA-neutral platform-adapter spike for both
runtime peers. The gate verifies the unchanged public face API and rejects FreeType/HarfBuzz managed
dependencies and sidecar libraries from the spike output. This proves the replacement boundary and
packaging shape; target-specific font quality, lifecycle, and policy remain platform validation work.

`DynamicTextNativeDiagnostics.Current` reports the target RID, logical native library names, and
NuGet packaging sources without scanning loaded modules or exposing handles. Forma owns one direct
entry point, `FT_Set_Var_Design_Coordinates`, against FreeTypeSharp's `freetype` library name. All
other FreeType calls use FreeTypeSharp; shaping uses HarfBuzzSharp and its `libHarfBuzzSharp` native
assets. Forma owns pinned-memory, FreeType-library, and FreeType-face safe handles. HarfBuzzSharp owns
its blob, face, font, and buffer handles. This path registers no unmanaged callbacks and uses no
runtime-generated marshalling.

Missing, incompatible, or rejected native font libraries fail face creation with
`FontLoadException` and `FontLoadErrorCode.NativeFailure`. The public message is bounded and stable;
loader details remain available through `InnerException` for host diagnostics. Run
`make native-font-failures` to exercise missing files, invalid binaries, and valid libraries with
missing FreeType exports in fresh processes for both peers. Packed dynamic consumers also require
exactly one loaded FreeType and HarfBuzz module from their publish directory.

MGCB/XNB is not required for dynamic text. MonoGame MGCB SpriteFonts and FNA-compatible XNB
SpriteFonts are optional offline compatibility routes.

## Release Budgets

The dual-runtime render smoke enforces these deliberately conservative ceilings on supported
graphical CI hosts. The August 2026 Apple M4 Max baseline measured MonoGame/FNA respectively at
1.9/9.7 ms cold face load, 0.19/0.24 ms first shape, 0.24/0.27 ms first raster plus upload,
1.0/1.1 ms per 1,000 warm layout lookups, 6.4/2.5 ms per 100 warm draws, 4.6/5.5 ms fallback-heavy
layout, and 2.5/3.3 ms atlas churn.

| Operation | Release ceiling |
| --- | ---: |
| Cold face load | 1,000 ms |
| First shape | 500 ms |
| First glyph raster and upload | 500 ms |
| 1,000 warm layout lookups | 500 ms |
| Warm layout cache hit rate | at least 99% |
| 100 unchanged warm draws | 1,000 ms and zero managed allocation |
| Fallback-heavy layout | 500 ms |
| One-page atlas churn | 2,000 ms |
| Core managed assembly | 2 MiB |
| Dynamic-text managed assembly | 256 KiB |

The retained layout cache is bounded at 512 entries. Device-scoped Alpha8 atlases are bounded at
eight 2048x2048 pages and 32 MiB. Budget failures block release; measurements are performance gates,
not cross-machine throughput promises.

## Loading and Ownership

```csharp
using var latinFace = UIFontFace.FromProjectFile(projectDirectory, "Fonts/Inter-Regular.ttf");
using var arabicFace = UIFontFace.FromStream(File.OpenRead("Fonts/NotoSansArabic.ttf"));
var font = new DynamicUIFont(latinFace, 18, UIFontHinting.Default, arabicFace);

var label = new Label
{
    Text = "Forma مرحبا",
    UIFont = font,
    Language = "ar",
};
```

Faces can also be loaded from `ReadOnlyMemory<byte>`. Forma copies and pins bounded source bytes for
the native face lifetime. The application owns faces and must keep them alive while fonts or layouts
can use them, then dispose them idempotently. Controls, `DynamicUIFont`, and immutable `TextLayout`
instances do not own faces.

Fallback order is deterministic and resolved per grapheme cluster. Put the normal UI face first,
then script and emoji faces. Unsupported input reaches glyph 0 (`.notdef`) after the chain is
exhausted; it is not replaced with `?` and does not throw.

Use `UIFontHinting.Light` for small grayscale UI text that needs vertical pixel alignment while
preserving fractional horizontal advances and inter-glyph spacing.

## Layout and Display Density

Font sizes and all `TextLayout` geometry use logical UI units. `UIContext.DisplayScale` controls the
physical glyph raster size. Moving from 1x to 2x rerasterizes or reuses density-specific cache entries
without changing line breaks, caret positions, or logical bounds.

```csharp
var options = new TextLayoutOptions(
    maxWidth: 320,
    wrapping: TextWrapping.Word,
    direction: TextDirection.Auto,
    locale: "he");
var layout = ui.TextLayoutEngine.Layout(font, text, options);
var caret = layout.GetCaretPosition(utf16Index);
var hit = layout.HitTest(pointerInLayout);
var selection = layout.GetSelectionRectangles(startUtf16, lengthUtf16);
```

UTF-16 offsets, grapheme clusters, visual clusters, and glyph IDs are distinct. Use layout movement,
hit-testing, word-boundary, and selection APIs instead of incrementing code units or measuring
substrings.

## OpenType and Variable Fonts

`TextLayoutOptions.OpenTypeFeatures` accepts immutable four-character OpenType tags. Label forwards
features with `SetOpenTypeFeatures`. Variable coordinates belong to `DynamicUIFont` identity:

```csharp
var variable = new DynamicUIFont(
    face,
    24,
    UIFontHinting.Default,
    new[] { new UIFontVariationCoordinate("wght", 650) });
label.UIFont = variable;
label.SetOpenTypeFeatures(new[]
{
    new UIFontOpenTypeFeature("liga", 1),
    new UIFontOpenTypeFeature("kern", 1),
});
```

## Cache Budget and Recovery

Each `UIContext` owns a glyph cache per `GraphicsDevice`. The default hard limits are eight
2048x2048 Alpha8 pages and 32 MB. `UIContext.DynamicGlyphDiagnostics` reports pages, glyphs,
occupancy, hits, misses, uploads, evictions, failures, and bytes. Immutable page snapshots are
available through `GetDynamicGlyphAtlasPages`; `ClearDynamicGlyphCache` clears pages between draws.
Device reset recreates textures from retained grayscale pages. Active-frame budget exhaustion skips
the unavailable glyph, records a diagnostic, and retries normally on later frames instead of
allocating beyond the budget or terminating the process.

## SpriteFont Compatibility

Keep `SpriteFont` when the application needs a fixed glyph set, pixel-art sampling, a deterministic
offline atlas, minimal native dependencies, or legacy XNA-compatible deployment. Assigning a
control's existing `Font` property installs a cached `SpriteFontAdapter`; no source rewrite is
required.

Migrate without changing layout intent:

```csharp
// Before: offline SpriteFont.
var column = new VBoxContainer { Separation = 8 };
column.AddChild(new Label { Font = content.Load<SpriteFont>("UI"), Text = "Settings" });
column.AddChild(new Button { Font = content.Load<SpriteFont>("UI"), Text = "Apply" });

// After: retain the same controls and layout properties; change only font selection.
using var face = UIFontFace.FromProjectFile(projectDirectory, "Fonts/Inter-Regular.ttf");
var uiFont = new DynamicUIFont(face, 16);
column.Children.OfType<Label>().Single().UIFont = uiFont;
column.Children.OfType<Button>().Single().UIFont = uiFont;
```

During rollout, keep the original SpriteFont loaded and expose an application switch:

```csharp
void SelectCompatibility(bool compatibility)
{
    label.UIFont = compatibility ? new SpriteFontAdapter(spriteFont, 16) : dynamicFont;
}
```

The catalog header demonstrates this rollback path in both runtime hosts.

## Compatibility Policy

Forma 0.x preserves the parallel `Font` and `UIFont` properties during migration. Additive text
contracts are minor-version changes. Existing `Font` members will not be silently reinterpreted or
removed; any future obsoletion forwards through `SpriteFontAdapter` for at least one minor release,
and removal requires a documented major-version decision. Assigning both properties remains
last-assignment-wins.
