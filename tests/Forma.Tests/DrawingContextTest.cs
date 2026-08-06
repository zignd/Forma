// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace Forma.Tests
{
    public sealed class DrawingContextTest
    {
        [Test]
        public void CubicPathFlattensDeterministicallyAfterTransform()
        {
            var path = new DrawingPath()
                .MoveTo(new Vector2(0, 0))
                .CubicTo(new Vector2(0, 10), new Vector2(10, 10), new Vector2(10, 0))
                .LineTo(new Vector2(0, 0))
                .Close();
            var transform = Matrix.CreateScale(2, 3, 1) * Matrix.CreateTranslation(5, 7, 0);

            var first = DrawingPathFlattener.Flatten(path, transform, .25f);
            var second = DrawingPathFlattener.Flatten(path, transform, .25f);

            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(first[0].Count, Is.GreaterThan(4));
            Assert.That(first[0].Count, Is.LessThanOrEqualTo((1 << DrawingPathFlattener.MaximumSubdivisionDepth) + 3));
            Assert.That(first[0][0], Is.EqualTo(new Vector2(5, 7)));
            Assert.That(first[0][^1], Is.EqualTo(new Vector2(5, 7)));
            Assert.That(second[0], Is.EqualTo(first[0]));
            Assert.That(first[0], Has.Some.Matches<Vector2>(point => point.Y > 7));
            Assert.That(path.ContainsPoint(new Vector2(15, 20), transform), Is.True);
            Assert.That(path.ContainsPoint(new Vector2(15, 38), transform), Is.False);
            Assert.That(path.ContainsPoint(new Vector2(2, 2), transform), Is.False);
        }

        [Test]
        public void SegmentBeforeMoveIsRejected()
        {
            Assert.That(
                () => new DrawingPath().LineTo(Vector2.One),
                Throws.InvalidOperationException.With.Message.Contains("MoveTo"));
        }

        [Test]
        public void ExtendedPathCommandsNormalizeIntoDeterministicCubics()
        {
            var path = new DrawingPath()
                .MoveTo(new Vector2(5, 5))
                .HorizontalToRelative(10)
                .QuadraticToRelative(new Vector2(5, 10), new Vector2(10, 0))
                .SmoothQuadraticToRelative(new Vector2(10, 0))
                .ArcToRelative(new Vector2(8, 6), 20, false, true, new Vector2(16, 8));

            var contour = DrawingPathFlattener.Flatten(path, Matrix.Identity, .1f)[0];

            Assert.That(path.Commands, Has.Some.Matches<DrawingPathCommand>(command => command.Kind == DrawingPathCommandKind.Cubic));
            Assert.That(contour[0], Is.EqualTo(new Vector2(5, 5)));
            Assert.That(contour[^1].X, Is.EqualTo(51).Within(.001f));
            Assert.That(contour[^1].Y, Is.EqualTo(13).Within(.001f));
            Assert.That(contour.Count, Is.LessThanOrEqualTo((1 << DrawingPathFlattener.MaximumSubdivisionDepth) * 5));
        }

        [Test]
        public void SvgPathDataParsesAllCommandsWithInvariantNumbers()
        {
            var geometry = new PathGeometry
            {
                Data = "M1.5 2.5 h3 v4 l2,-1 C8 5 9 6 10 7 s2 2 3 0 Q14 5 15 7 t2 0 A3 2 15 0 1 21 9 z",
            };

            var contours = DrawingPathFlattener.Flatten(geometry.Path, Matrix.Identity, .1f);

            Assert.That(contours, Has.Count.EqualTo(1));
            Assert.That(contours[0][0], Is.EqualTo(new Vector2(1.5f, 2.5f)));
            Assert.That(contours[0][^1], Is.EqualTo(new Vector2(1.5f, 2.5f)));
            Assert.Throws<FormatException>(() => DrawingPath.Parse("M0 0 A2 2 0 2 0 4 4"));
            Assert.Throws<FormatException>(() => DrawingPath.Parse("M0,0 L"));
        }

        [Test]
        public void StrokeTessellationExpandsEveryFlattenedSegment()
        {
            var path = new DrawingPath()
                .MoveTo(Vector2.Zero)
                .CubicTo(new Vector2(0, 10), new Vector2(10, 10), new Vector2(10, 0));

            var mesh = DrawingPathTessellator.TessellateStroke(path, Matrix.Identity, 4, .25f);

            Assert.That(mesh.Vertices.Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(mesh.Indices.Length % 3, Is.Zero);
            Assert.That(mesh.Indices.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(mesh.Vertices[0].Length(), Is.EqualTo(2).Within(.001f));
        }

        [Test]
        public void StrokeStyleBuildsDashesCapsAndOneSidedAlignment()
        {
            var line = new DrawingPath().MoveTo(Vector2.Zero).LineTo(new Vector2(20, 0));
            var dashed = DrawingPathTessellator.TessellateStroke(line, Matrix.Identity, 4, .25f, new StrokeStyle
            {
                DashArray = new[] { 5f, 5f },
                StartLineCap = StrokeLineCap.Square,
                EndLineCap = StrokeLineCap.Square,
            });
            Assert.That(dashed.Vertices.Length, Is.EqualTo(8));
            Assert.That(dashed.Vertices[0].X, Is.EqualTo(-2).Within(.001f));
            Assert.That(dashed.Vertices[4].X, Is.EqualTo(8).Within(.001f));

            var rectangle = DrawingPath.Parse("M0 0 H20 V10 H0 Z");
            var inside = DrawingPathTessellator.TessellateStroke(rectangle, Matrix.Identity, 2, .25f, new StrokeStyle { Alignment = StrokeAlignment.Inside });
            Assert.That(inside.Vertices, Has.All.Matches<Vector2>(point => point.X >= 0 && point.X <= 20 && point.Y >= 0 && point.Y <= 10));
        }

        [Test]
        public void GeometryGroupsAndBooleanGeometryShareFillAndHitTestRules()
        {
            var outer = new RectangleGeometry();
            var inner = new RectangleGeometry
            {
                Transform = new TranslateTransform { X = 5, Y = 5 },
            };
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(outer);
            group.Children.Add(inner);
            var groupedPath = group.CreatePath(new Vector2(20, 20));
            Assert.That(groupedPath.ContainsPoint(new Vector2(2, 2), Matrix.Identity, FillRule.EvenOdd), Is.True);
            Assert.That(groupedPath.ContainsPoint(new Vector2(10, 10), Matrix.Identity, FillRule.EvenOdd), Is.False);
            Assert.DoesNotThrow(() => DrawingPathTessellator.TessellateFill(groupedPath, Matrix.Identity, .25f, FillRule.EvenOdd));

            var overlap = new CombinedGeometry
            {
                Mode = GeometryCombineMode.Intersect,
                Geometry1 = new RectangleGeometry(),
                Geometry2 = new RectangleGeometry { Transform = new TranslateTransform { X = 10 } },
            }.CreatePath(new Vector2(20, 10));
            Assert.That(overlap.ContainsPoint(new Vector2(15, 5), Matrix.Identity), Is.True);
            Assert.That(overlap.ContainsPoint(new Vector2(5, 5), Matrix.Identity), Is.False);
            Assert.DoesNotThrow(() => DrawingPathTessellator.TessellateFill(overlap, Matrix.Identity, .25f));
        }

        [Test]
        public void GeometryClipsSupportConcaveContoursAndEvenOddHoles()
        {
            var sourcePath = DrawingPath.Parse("M0 0 H20 V20 H0 Z");
            var source = DrawingPathTessellator.TessellateFill(sourcePath, Matrix.Identity, .25f);
            var concavePath = DrawingPath.Parse("M0 0 H20 V10 H10 V20 H0 Z");
            var concaveContours = DrawingPathFlattener.Flatten(concavePath, Matrix.Identity, .25f);
            var concave = DrawingPathClipper.Clip(source, DrawingPathClipper.NormalizeContours(concaveContours, FillRule.NonZero));
            Assert.That(MeshArea(concave), Is.EqualTo(300).Within(.01f));

            var holePath = DrawingPath.Parse("M0 0 H20 V20 H0 Z M5 5 H15 V15 H5 Z");
            var holeContours = DrawingPathFlattener.Flatten(holePath, Matrix.Identity, .25f);
            var withHole = DrawingPathClipper.Clip(source, DrawingPathClipper.NormalizeContours(holeContours, FillRule.EvenOdd));
            Assert.That(MeshArea(withHole), Is.EqualTo(300).Within(.01f));
        }

        [Test]
        public void FrozenGeometryRecursivelyRejectsEveryMutationRoute()
        {
            var path = DrawingPath.Parse("M0 0 H10 V10 H0 Z");
            var transform = new TransformGroup();
            var translation = new TranslateTransform { X = 2 };
            transform.Children.Add(translation);
            var child = new PathGeometry(path) { Transform = transform };
            var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
            group.Children.Add(child);

            group.Freeze();

            Assert.That(group.IsFrozen, Is.True);
            Assert.That(child.IsFrozen, Is.True);
            Assert.That(path.IsFrozen, Is.True);
            Assert.That(transform.IsFrozen, Is.True);
            Assert.That(translation.IsFrozen, Is.True);
            Assert.Throws<InvalidOperationException>(() => group.FillRule = FillRule.NonZero);
            Assert.Throws<InvalidOperationException>(() => group.Children.Add(new EllipseGeometry()));
            Assert.Throws<InvalidOperationException>(() => child.Path = new DrawingPath());
            Assert.Throws<InvalidOperationException>(() => path.LineTo(Vector2.One));
            Assert.Throws<InvalidOperationException>(() => transform.Children.Clear());
            Assert.Throws<InvalidOperationException>(() => translation.X = 4);
            Assert.DoesNotThrow(() => DrawingPathTessellator.TessellateFill(group.CreatePath(new Vector2(10)), Matrix.Identity, .25f, group.FillRule));
        }

        [Test]
        public void ImageBrushCalculatesStretchAlignmentAndTileCoordinates()
        {
            var brush = new ImageBrush
            {
                Stretch = ImageStretch.Contain,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                TileMode = ImageTileMode.TileX,
                Transform = new TranslateTransform { X = 2, Y = 3 },
            };
            var placement = new Rectangle(10, 20, 40, 20);
            Assert.That(brush.GetTextureCoordinate(new Vector2(30, 30), placement), Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(brush.ToBrushSpace(new Vector2(12, 23)), Is.EqualTo(new Vector2(10, 20)));
            var paint = brush.GetPaintBounds(new Rectangle(0, 0, 80, 60), placement);
            Assert.That(paint[0], Is.EqualTo(new Vector2(2, 23)));
            Assert.That(paint[2], Is.EqualTo(new Vector2(82, 43)));
        }

        [Test]
        public void PathShapeStretchComposesBoundsScaleAndAlignment()
        {
            var shape = new PathShape
            {
                Size = new Vector2(40, 40),
                Data = new PathGeometry(DrawingPath.Parse("M10 20 H20 V40 H10 Z")),
                Stretch = ShapeStretch.Uniform,
                GeometryHorizontalAlignment = HorizontalAlignment.Right,
                GeometryVerticalAlignment = VerticalAlignment.Bottom,
            };
            var path = shape.Data.CreatePath(shape.Size);
            var uniform = DrawingPathFlattener.Flatten(path, shape.GetStretchTransform(path), .25f)[0];
            Assert.That(uniform[0], Is.EqualTo(new Vector2(20, 0)));
            Assert.That(uniform[2], Is.EqualTo(new Vector2(40, 40)));

            shape.Stretch = ShapeStretch.Fill;
            var filled = DrawingPathFlattener.Flatten(path, shape.GetStretchTransform(path), .25f)[0];
            Assert.That(filled[0], Is.EqualTo(Vector2.Zero));
            Assert.That(filled[2], Is.EqualTo(new Vector2(40, 40)));

            shape.Position = new Vector2(10, 20);
            shape.Fill = new SolidColorBrush(Color.White);
            Assert.That(shape.ContainsPoint(new Point(15, 25)), Is.True);
            Assert.That(shape.ContainsPoint(new Point(45, 25)), Is.True);
            Assert.That(shape.ContainsPoint(new Point(5, 25)), Is.False);

            var line = new LineShape
            {
                Position = new Vector2(5, 6),
                StartPoint = Vector2.Zero,
                EndPoint = new Vector2(20, 10),
                Stroke = new SolidColorBrush(Color.White),
                StrokeThickness = 4,
            };
            Assert.That(line.GetMinimumSize(), Is.EqualTo(new Vector2(24, 14)));
            Assert.That(line.ContainsPoint(new Point(15, 11)), Is.True);
            Assert.That(line.ContainsPoint(new Point(15, 18)), Is.False);
        }

        [Test]
        public void ImageUsesVectorIntrinsicSizeWithoutChangingBitmapCompatibility()
        {
            var image = new Image
            {
                Position = new Vector2(4, 6),
                Size = new Vector2(100, 100),
                ExpandMode = TextureRectExpandMode.KeepSize,
                VectorSource = new DrawingImage { IntrinsicSize = new Vector2(24, 12) },
            };
            Assert.That(image.GetMinimumSize(), Is.EqualTo(new Vector2(24, 12)));
            Assert.That(image.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(4, 31, 100, 50)));
            image.Stretch = ImageStretch.Cover;
            Assert.That(image.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(-46, 6, 200, 100)));
            image.Stretch = ImageStretch.Fill;
            Assert.That(image.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(4, 6, 100, 100)));
            image.Stretch = ImageStretch.None;
            image.ImageHorizontalAlignment = HorizontalAlignment.Right;
            image.ImageVerticalAlignment = VerticalAlignment.Bottom;
            Assert.That(image.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(64, 86, 40, 20)));
            image.Stretch = ImageStretch.ScaleDown;
            Assert.That(image.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(64, 86, 40, 20)));
            Assert.That(new Image { Size = new Vector2(20, 10), Stretch = ImageStretch.ScaleDown }.GetImageLayout(new Vector2(40, 20)).Destination, Is.EqualTo(new Rectangle(0, 0, 20, 10)));
            image.ExpandMode = TextureRectExpandMode.IgnoreSize;
            Assert.That(image.GetMinimumSize(), Is.EqualTo(Vector2.Zero));
        }

        [Test]
        public void ScalableImageSurfacesUseIntrinsicSizeAndPreserveExistingPrecedence()
        {
            var scalable = SvgImageSource.FromMemory(System.Text.Encoding.UTF8.GetBytes(
                "<svg xmlns='http://www.w3.org/2000/svg' width='30' height='18'><rect width='30' height='18'/></svg>"));
            var vector = new DrawingImage { IntrinsicSize = new Vector2(24, 12) };
            var image = new Image { ScalableSource = scalable };
            var inline = new InlineImage { ScalableSource = scalable };
            var icon = new ThemeIcon(scalable, new Point(15, 9));
            var drawing = new ImageDrawing { ScalableSource = scalable };

            Assert.Multiple(() =>
            {
                Assert.That(image.GetMinimumSize(), Is.EqualTo(new Vector2(30, 18)));
                Assert.That(inline.ScalableSource, Is.SameAs(scalable));
                Assert.That(icon.ScalableSource, Is.SameAs(scalable));
                Assert.That(icon.LogicalSize, Is.EqualTo(new Point(15, 9)));
                Assert.That(drawing.ScalableSource, Is.SameAs(scalable));
            });

            image.Size = new Vector2(100, 100);
            image.Position = new Vector2(4, 6);
            image.Stretch = ImageStretch.Contain;
            Assert.That(image.GetImageLayout(scalable.IntrinsicSize).Destination, Is.EqualTo(new Rectangle(4, 26, 100, 60)));
            image.Stretch = ImageStretch.Cover;
            Assert.That(image.GetImageLayout(scalable.IntrinsicSize).Destination, Is.EqualTo(new Rectangle(-29, 6, 167, 100)));
            image.Stretch = ImageStretch.Fill;
            Assert.That(image.GetImageLayout(scalable.IntrinsicSize).Destination, Is.EqualTo(new Rectangle(4, 6, 100, 100)));
            image.Stretch = ImageStretch.None;
            image.ImageHorizontalAlignment = HorizontalAlignment.Right;
            image.ImageVerticalAlignment = VerticalAlignment.Bottom;
            Assert.That(image.GetImageLayout(scalable.IntrinsicSize).Destination, Is.EqualTo(new Rectangle(74, 88, 30, 18)));

            image.VectorSource = vector;
            Assert.That(image.GetMinimumSize(), Is.EqualTo(new Vector2(24, 12)));
            image.AccessibilityLabel = "Application-provided image description";
            Assert.That(image.AccessibilityName, Is.EqualTo("Application-provided image description"));
        }

        [Test]
        public void FoundationalShapesBuildDeterministicGeometryAndRejectChildren()
        {
            var rectangle = new RectangleShape
            {
                Size = new Vector2(40, 20),
                RadiusX = 4,
                Fill = new LinearGradientBrush
                {
                    GradientStops = new[] { new GradientStop(0, Color.Red), new GradientStop(1, Color.Blue) },
                },
                Stroke = new SolidColorBrush(Color.White),
                StrokeThickness = 2,
            };
            var ellipse = new EllipseGeometry().CreatePath(new Vector2(30, 20));

            Assert.That(rectangle.Fill, Is.TypeOf<LinearGradientBrush>());
            Assert.That(ellipse.Commands.Count, Is.EqualTo(6));
            Assert.That(ellipse.ContainsPoint(new Vector2(15, 10), Matrix.Identity), Is.True);
            Assert.Throws<InvalidOperationException>(() => rectangle.AddChild(new Control()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GradientStop(-.1f, Color.White));

            var corners = new RectangleGeometry { CornerRadius = new CornerRadius(2, 4, 6, 8) }.CreatePath(new Vector2(40, 20));
            Assert.That(corners.Commands[0].First, Is.EqualTo(new Vector2(2, 0)));
            Assert.That(corners.Commands[1].First, Is.EqualTo(new Vector2(36, 0)));
            var linearGradient = new LinearGradientBrush
            {
                InterpolationSpace = GradientInterpolationSpace.LinearSrgb,
                GradientStops = new[] { new GradientStop(0, Color.Black), new GradientStop(1, Color.White) },
            };
            Assert.That(linearGradient.Sample(new Vector2(.5f, 0), new Rectangle(0, 0, 1, 1)).R, Is.GreaterThan(127));

            var border = new Border { BorderThickness = new Thickness(2), Padding = new Thickness(3) };
            border.AddChild(new Control { CustomMinimumSize = new Vector2(10, 8) });
            Assert.That(border.GetMinimumSize(), Is.EqualTo(new Vector2(20, 18)));
            Assert.Throws<InvalidOperationException>(() => border.AddChild(new Control()));

            var effects = new EffectGroup();
            effects.Add(new ColorMatrixEffect());
            effects.Add(new BlurEffect());
            effects.Add(new DropShadowEffect());
            effects.Add(new ColorMatrixEffect());
            Assert.That(effects.Children, Has.Count.EqualTo(DrawingContextLimits.MaximumEffectGroupLength));
            Assert.Throws<InvalidOperationException>(() => effects.Add(new BlurEffect()));
            Assert.Throws<InvalidOperationException>(() => new EffectGroup().Add(new EffectGroup()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BlurEffect { Radius = DrawingContextLimits.MaximumBlurRadius + 1 });

            var effectBounds = UIRenderContext.GetEffectProcessingBounds(
                new DropShadowEffect { BlurRadius = 3, Offset = new Vector2(4, -3) },
                new Rectangle(100, 100, 40, 20),
                new Rectangle(0, 0, 200, 200));
            Assert.That(effectBounds, Is.EqualTo(new Rectangle(97, 94, 50, 29)));

            var run = new Run("world");
            var span = new Span();
            span.Inlines.Add(new Run("hello "));
            span.Inlines.Add(run);
            var text = new TextBlock { Text = "plain" };
            text.Inlines.Add(span);
            text.Inlines.Add(new LineBreak());
            text.Inlines.Add(new InlineImage { AlternativeText = "[icon]" });
            Assert.That(((Label)text).Text, Is.EqualTo("hello world\n[icon]"));
            run.Text = "Forma";
            Assert.That(((Label)text).Text, Is.EqualTo("hello Forma\n[icon]"));
            Assert.That(text.Inlines, Has.None.InstanceOf<Control>());
            text.Inlines.Clear();
            Assert.That(((Label)text).Text, Is.EqualTo("plain"));
        }

        private static float MeshArea(DrawingMesh mesh)
        {
            var area = 0f;
            for (var index = 0; index < mesh.Indices.Length; index += 3)
            {
                var first = mesh.Vertices[mesh.Indices[index]];
                var second = mesh.Vertices[mesh.Indices[index + 1]];
                var third = mesh.Vertices[mesh.Indices[index + 2]];
                area += MathF.Abs((second.X - first.X) * (third.Y - first.Y) - (second.Y - first.Y) * (third.X - first.X)) * .5f;
            }
            return area;
        }
    }
}