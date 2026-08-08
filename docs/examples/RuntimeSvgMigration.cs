// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
//
// Runtime SVG Migration Examples
//
// This file demonstrates three common migration paths to Forma's bounded runtime SVG rendering:
//
//   1. Migrating a Texture2D atlas icon loaded at startup to an SvgImageSource with Image.
//   2. Migrating a ThemeIcon bitmap-atlas consumer to the RuntimeSvg theme policy.
//   3. Migrating a DrawingImage / ImageDrawing surface to an SvgImageSource.
//
// Prerequisites: add the runtime-matched companion alongside the core package.
//
//   MonoGame:
//     <PackageReference Include="Forma.MonoGame" Version="0.1.0-alpha.2" />
//     <PackageReference Include="Forma.Svg.MonoGame" Version="0.1.0-alpha.2" />
//
//   FNA:
//     <PackageReference Include="Forma.FNA" Version="0.1.0-alpha.2" />
//     <PackageReference Include="Forma.Svg.FNA" Version="0.1.0-alpha.2" />
//
// The package module initializer installs the backend automatically. Call
// SvgBackendDefaults.Verify() for an explicit startup probe.

using System;
using System.IO;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Examples;

// ---------------------------------------------------------------------------
// Example 1: Migrating a Texture2D atlas icon to SvgImageSource + Image
//
// Before: load a pre-rasterized PNG at a fixed physical size.
// After:  load an SVG source once; the cache rasterizes it at the exact
//         physical size required by the current display scale.
// ---------------------------------------------------------------------------

public sealed class TextureIconMigrationExample : IDisposable
{
    // Before: callers owned a Texture2D and drew it at a fixed logical size.
    // private Texture2D _iconTexture;

    // After: one immutable source shared across all consumers.
    private readonly SvgImageSource _iconSource;

    public UIContext Context { get; }

    public TextureIconMigrationExample(string contentDirectory)
    {
        // FromFile validates the SVG immediately and stores an immutable private copy.
        // The source lifetime is independent of the graphics device.
        _iconSource = SvgImageSource.FromFile(Path.Combine(contentDirectory, "Icons/status.svg"));

        Context = new UIContext();

        // Before: Image with a Texture2D source at a fixed logical size.
        //   var icon = new Image { Texture = _iconTexture, CustomMinimumSize = new Vector2(32, 32) };
        //
        // After: Image with a ScalableImageSource at the same logical size.
        //   The renderer derives the exact physical dimensions from the current display scale and
        //   the complete logical transform, producing a crisp raster at every fractional DPI.
        var icon = new Image
        {
            ScalableSource = _iconSource,
            Stretch = ImageStretch.Contain,
            CustomMinimumSize = new Vector2(32, 32),
        };

        var root = new VBoxContainer { Size = new Vector2(480, 240) };
        root.AddChild(icon);
        Context.Add(root);
    }

    public void Dispose()
    {
        Context.Dispose();
        // SvgImageSource is a plain immutable object; no Dispose is required.
    }
}

// ---------------------------------------------------------------------------
// Example 2: Migrating a ThemeIcon bitmap-atlas consumer to RuntimeSvg policy
//
// Before: ThemeIcon always draws from the embedded 1x/2x PNG atlas.
// After:  RuntimeSvg policy renders authoritative companion SVG sources when the
//         backend is healthy, with the PNG atlas retained as a per-icon fallback.
// ---------------------------------------------------------------------------

public sealed class ThemeIconPolicyMigrationExample : IDisposable
{
    public UIContext Context { get; }

    public ThemeIconPolicyMigrationExample()
    {
        Context = new UIContext();

        // Before: policy was always BitmapAtlas (the shipped default).
        //   Context.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.BitmapAtlas;
        //
        // After: switch to RuntimeSvg. A missing companion or unhealthy backend never
        //   removes a default control icon; every icon carries its PNG atlas region as a
        //   per-icon fallback. ThemeIconDiagnostics distinguishes SVG sources, PNG
        //   fallbacks, and missing names.
        //
        // Note: BitmapAtlas remains the shipped default until the full release matrix
        //   (Phase 8) is approved. Set RuntimeSvg explicitly to opt in during development.
        Context.ThemeIconRenderingPolicy = ThemeIconRenderingPolicy.RuntimeSvg;

        // ThemeIcon usage is unchanged; policy selection happens at the context level.
        var icon = new ThemeIconRect
        {
            ThemeTypeName = nameof(OptionButton),
            ThemeItemName = "arrow",
            CustomMinimumSize = new Vector2(32, 32),
        };

        var root = new HBoxContainer { Size = new Vector2(480, 64) };
        root.AddChild(icon);
        Context.Add(root);
    }

    public void Dispose() => Context.Dispose();
}

// ---------------------------------------------------------------------------
// Example 3: Migrating a DrawingImage / ImageDrawing surface to SvgImageSource
//
// Before: a DrawingImage assembled programmatically from GeometryDrawing primitives,
//         then set as the source of an Image control.
// After:  an SvgImageSource loaded from an authored SVG file that expresses the same
//         geometry, giving the design team a standard authoring workflow.
//
// Source precedence: bitmap content wins over DrawingImage, which wins over scalable
// (SVG) content. Remove any Texture or DrawingImage assignment when migrating to SVG.
// ---------------------------------------------------------------------------

public sealed class DrawingImageMigrationExample : IDisposable
{
    private readonly SvgImageSource _badgeSource;

    public UIContext Context { get; }

    public DrawingImageMigrationExample(ReadOnlyMemory<byte> svgBytes)
    {
        // Before: build a DrawingImage and assign it to Image.Drawing.
        //   var circle = new EllipseGeometry { Center = new Vector2(16, 16), RadiusX = 14, RadiusY = 14 };
        //   var drawing = new GeometryDrawing { Geometry = circle, Brush = new SolidColorBrush(Color.Green) };
        //   var drawingImage = new DrawingImage { Drawing = drawing };
        //   var badge = new Image { Drawing = drawingImage, CustomMinimumSize = new Vector2(32, 32) };
        //
        // After: load the equivalent authored SVG. FromMemory accepts a ReadOnlyMemory<byte>
        //   from a caller-owned buffer; the source copies the bytes on construction.
        _badgeSource = SvgImageSource.FromMemory(svgBytes);

        var badge = new Image
        {
            ScalableSource = _badgeSource,
            Stretch = ImageStretch.Contain,
            CustomMinimumSize = new Vector2(32, 32),
        };

        Context = new UIContext();
        var root = new VBoxContainer { Size = new Vector2(480, 240) };
        root.AddChild(badge);
        Context.Add(root);
    }

    public void Dispose() => Context.Dispose();
}
