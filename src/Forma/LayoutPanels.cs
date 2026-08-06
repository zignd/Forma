// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace Forma
{
    internal interface IDisplayScaleLayout
    {
    }

    internal static class LayoutMath
    {
        public static float Snap(Control owner, float value)
        {
            var scale = owner?.Context?.DisplayScale ?? 1;
            return MathF.Round(value * scale) / scale;
        }

        public static void Arrange(Control owner, Control child, float x, float y, float width, float height)
        {
            if (!child.IsPixelSnappingEnabled)
            {
                child.Position = new Vector2(x, y);
                child.Size = new Vector2(Math.Max(0, width), Math.Max(0, height));
                return;
            }
            var left = Snap(owner, x);
            var top = Snap(owner, y);
            var right = Snap(owner, x + Math.Max(0, width));
            var bottom = Snap(owner, y + Math.Max(0, height));
            child.Position = new Vector2(left, top);
            child.Size = new Vector2(Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
    }

    public enum CrossAxisAlignment { Start, Center, End, Stretch }
    public enum LayoutLengthUnit { Auto, Pixel, Percent, Content, Star }

    public readonly struct LayoutLength : IEquatable<LayoutLength>
    {
        private LayoutLength(float value, LayoutLengthUnit unit) { Value = value; Unit = unit; }
        public float Value { get; }
        public LayoutLengthUnit Unit { get; }
        public static LayoutLength Auto => new LayoutLength(0, LayoutLengthUnit.Auto);
        public static LayoutLength Content => new LayoutLength(0, LayoutLengthUnit.Content);
        public static LayoutLength Pixels(float value) => new LayoutLength(ValidateNonnegative(value, nameof(value)), LayoutLengthUnit.Pixel);
        public static LayoutLength Percent(float value) => new LayoutLength(ValidateNonnegative(value, nameof(value)), LayoutLengthUnit.Percent);
        public static LayoutLength Star(float value = 1) => new LayoutLength(ValidatePositive(value, nameof(value)), LayoutLengthUnit.Star);
        public float Resolve(float available, float content)
        {
            return Unit switch
            {
                LayoutLengthUnit.Pixel => Value,
                LayoutLengthUnit.Percent when float.IsFinite(available) => available * Value,
                _ => content,
            };
        }
        public bool Equals(LayoutLength other) => Value == other.Value && Unit == other.Unit;
        public override bool Equals(object obj) => obj is LayoutLength other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, Unit);
        public static bool operator ==(LayoutLength left, LayoutLength right) => left.Equals(right);
        public static bool operator !=(LayoutLength left, LayoutLength right) => !left.Equals(right);
        private static float ValidateNonnegative(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
        private static float ValidatePositive(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public enum GridTrackUnit { Auto, Pixel, Percent, Star, MinMax, FitContent }

    public readonly struct GridTrackSize : IEquatable<GridTrackSize>
    {
        private GridTrackSize(GridTrackUnit unit, float value, LayoutLength minimum, LayoutLength maximum)
        {
            Unit = unit;
            Value = value;
            Minimum = minimum;
            Maximum = maximum;
        }
        public GridTrackUnit Unit { get; }
        public float Value { get; }
        public LayoutLength Minimum { get; }
        public LayoutLength Maximum { get; }
        public static GridTrackSize Auto => new GridTrackSize(GridTrackUnit.Auto, 0, LayoutLength.Auto, LayoutLength.Auto);
        public static GridTrackSize Pixels(float value) => new GridTrackSize(GridTrackUnit.Pixel, ValidateNonnegative(value, nameof(value)), LayoutLength.Auto, LayoutLength.Auto);
        public static GridTrackSize Percent(float value) => new GridTrackSize(GridTrackUnit.Percent, ValidateNonnegative(value, nameof(value)), LayoutLength.Auto, LayoutLength.Auto);
        public static GridTrackSize Star(float value = 1) => new GridTrackSize(GridTrackUnit.Star, ValidatePositive(value, nameof(value)), LayoutLength.Auto, LayoutLength.Auto);
        public static GridTrackSize MinMax(LayoutLength minimum, LayoutLength maximum)
        {
            if (minimum.Unit == LayoutLengthUnit.Star) throw new ArgumentException("Grid track minimum cannot use star units.", nameof(minimum));
            if (minimum.Unit == LayoutLengthUnit.Pixel && maximum.Unit == LayoutLengthUnit.Pixel && maximum.Value < minimum.Value)
                throw new ArgumentException("Grid track maximum cannot be smaller than its minimum.", nameof(maximum));
            return new GridTrackSize(GridTrackUnit.MinMax, 0, minimum, maximum);
        }
        public static GridTrackSize FitContent(float maximum) => new GridTrackSize(GridTrackUnit.FitContent, ValidateNonnegative(maximum, nameof(maximum)), LayoutLength.Auto, LayoutLength.Auto);
        public bool Equals(GridTrackSize other) => Unit == other.Unit && Value == other.Value && Minimum == other.Minimum && Maximum == other.Maximum;
        public override bool Equals(object obj) => obj is GridTrackSize other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Unit, Value, Minimum, Maximum);
        public static bool operator ==(GridTrackSize left, GridTrackSize right) => left.Equals(right);
        public static bool operator !=(GridTrackSize left, GridTrackSize right) => !left.Equals(right);
        private static float ValidateNonnegative(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
        private static float ValidatePositive(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public sealed class ColumnDefinition
    {
        private GridTrackSize _width = GridTrackSize.Star();
        public GridTrackSize Width { get => _width; set { if (_width == value) return; _width = value; Changed?.Invoke(); } }
        internal event Action Changed;
    }
    public sealed class RowDefinition
    {
        private GridTrackSize _height = GridTrackSize.Auto;
        public GridTrackSize Height { get => _height; set { if (_height == value) return; _height = value; Changed?.Invoke(); } }
        internal event Action Changed;
    }

    public sealed class CanvasPanel : Container, IDisplayScaleLayout
    {
        private sealed class Placement
        {
            public float Left = float.NaN;
            public float Top = float.NaN;
            public float Right = float.NaN;
            public float Bottom = float.NaN;
            public Vector2 Anchor;
            public Vector2 AnchorPosition;
            public bool HasAnchorPosition;
            public Vector2 LastArrangedPosition;
            public bool HasArrangedPosition;
        }

        private static readonly ConditionalWeakTable<Control, Placement> Placements = new ConditionalWeakTable<Control, Placement>();

        public static float GetLeft(Control child) => GetPlacement(child).Left;
        public static void SetLeft(Control child, float value) { ValidateOffset(value, nameof(value)); GetPlacement(child).Left = value; child.VisualParent?.QueueLayout(); }
        public static float GetTop(Control child) => GetPlacement(child).Top;
        public static void SetTop(Control child, float value) { ValidateOffset(value, nameof(value)); GetPlacement(child).Top = value; child.VisualParent?.QueueLayout(); }
        public static float GetRight(Control child) => GetPlacement(child).Right;
        public static void SetRight(Control child, float value) { ValidateOffset(value, nameof(value)); GetPlacement(child).Right = value; child.VisualParent?.QueueLayout(); }
        public static float GetBottom(Control child) => GetPlacement(child).Bottom;
        public static void SetBottom(Control child, float value) { ValidateOffset(value, nameof(value)); GetPlacement(child).Bottom = value; child.VisualParent?.QueueLayout(); }
        public static Vector2 GetAnchor(Control child) => GetPlacement(child).Anchor;
        public static void SetAnchor(Control child, Vector2 value)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || value.X < 0 || value.X > 1 || value.Y < 0 || value.Y > 1) throw new ArgumentOutOfRangeException(nameof(value));
            var placement = GetPlacement(child);
            placement.Anchor = value;
            placement.AnchorPosition = child.Position;
            placement.HasAnchorPosition = true;
            child.VisualParent?.QueueLayout();
        }
        public static int GetZIndex(Control child) => child?.ZIndex ?? throw new ArgumentNullException(nameof(child));
        public static void SetZIndex(Control child, int value)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            child.ZIndex = value;
        }

        public override Vector2 GetMinimumSize()
        {
            var minimum = CustomMinimumSize;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var placement = GetPlacement(child);
                var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                var width = desired.X + (float.IsNaN(placement.Left) ? 0 : placement.Left) + (float.IsNaN(placement.Right) ? 0 : placement.Right);
                var height = desired.Y + (float.IsNaN(placement.Top) ? 0 : placement.Top) + (float.IsNaN(placement.Bottom) ? 0 : placement.Bottom);
                minimum = Vector2.Max(minimum, new Vector2(width, height));
            }
            return minimum;
        }

        protected override void ArrangeChildren()
        {
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var placement = GetPlacement(child);
                var desired = child.GetBoundDesiredSize();
                var width = !float.IsNaN(placement.Left) && !float.IsNaN(placement.Right)
                    ? Math.Max(0, Size.X - placement.Left - placement.Right - child.Margins.Horizontal)
                    : desired.X;
                var height = !float.IsNaN(placement.Top) && !float.IsNaN(placement.Bottom)
                    ? Math.Max(0, Size.Y - placement.Top - placement.Bottom - child.Margins.Vertical)
                    : desired.Y;
                if (placement.HasAnchorPosition && ((placement.HasArrangedPosition && child.Position != placement.LastArrangedPosition)
                    || (!placement.HasArrangedPosition && child.Position != placement.AnchorPosition)))
                    placement.AnchorPosition = child.Position;
                var anchorPosition = placement.HasAnchorPosition ? placement.AnchorPosition : child.Position;
                var x = !float.IsNaN(placement.Left) ? placement.Left + child.Margins.Left
                    : !float.IsNaN(placement.Right) ? Size.X - placement.Right - width - child.Margins.Right
                    : anchorPosition.X - width * placement.Anchor.X;
                var y = !float.IsNaN(placement.Top) ? placement.Top + child.Margins.Top
                    : !float.IsNaN(placement.Bottom) ? Size.Y - placement.Bottom - height - child.Margins.Bottom
                    : anchorPosition.Y - height * placement.Anchor.Y;
                LayoutMath.Arrange(this, child, x, y, width, height);
                placement.LastArrangedPosition = child.Position;
                placement.HasArrangedPosition = true;
            }
        }

        private static Placement GetPlacement(Control child) => Placements.GetOrCreateValue(child ?? throw new ArgumentNullException(nameof(child)));
        private static void ValidateOffset(float value, string parameterName)
        {
            if (!float.IsNaN(value) && !float.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class OverlayPanel : Container, IDisplayScaleLayout
    {
        public static HorizontalAlignment GetHorizontalAlignment(Control child) => child?.HorizontalAlignment ?? throw new ArgumentNullException(nameof(child));
        public static void SetHorizontalAlignment(Control child, HorizontalAlignment value)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (!Enum.IsDefined(typeof(HorizontalAlignment), value)) throw new ArgumentOutOfRangeException(nameof(value));
            child.HorizontalAlignment = value;
            child.VisualParent?.QueueLayout();
        }
        public static VerticalAlignment GetVerticalAlignment(Control child) => child?.VerticalAlignment ?? throw new ArgumentNullException(nameof(child));
        public static void SetVerticalAlignment(Control child, VerticalAlignment value)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (!Enum.IsDefined(typeof(VerticalAlignment), value)) throw new ArgumentOutOfRangeException(nameof(value));
            child.VerticalAlignment = value;
            child.VisualParent?.QueueLayout();
        }
        public static int GetZIndex(Control child) => child?.ZIndex ?? throw new ArgumentNullException(nameof(child));
        public static void SetZIndex(Control child, int value)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            child.ZIndex = value;
        }

        public override Vector2 GetMinimumSize()
        {
            var minimum = CustomMinimumSize;
            foreach (var child in VisualChildren)
                if (child.Visible) minimum = Vector2.Max(minimum, child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical));
            return minimum;
        }

        protected override void ArrangeChildren()
        {
            var rtl = IsLayoutRtl();
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var horizontal = rtl ? child.HorizontalAlignment switch { HorizontalAlignment.Left => HorizontalAlignment.Right, HorizontalAlignment.Right => HorizontalAlignment.Left, _ => child.HorizontalAlignment } : child.HorizontalAlignment;
                var desired = child.GetBoundDesiredSize();
                var available = Vector2.Max(Vector2.Zero, Size - new Vector2(child.Margins.Horizontal, child.Margins.Vertical));
                var width = horizontal == HorizontalAlignment.Fill ? available.X : Math.Min(available.X, desired.X);
                var height = child.VerticalAlignment == VerticalAlignment.Fill ? available.Y : Math.Min(available.Y, desired.Y);
                var x = horizontal == HorizontalAlignment.Center ? (available.X - width) / 2 : horizontal == HorizontalAlignment.Right ? available.X - width : 0;
                var y = child.VerticalAlignment == VerticalAlignment.Center ? (available.Y - height) / 2 : child.VerticalAlignment == VerticalAlignment.Bottom ? available.Y - height : 0;
                LayoutMath.Arrange(this, child, child.Margins.Left + x, child.Margins.Top + y, width, height);
            }
        }
    }

    public sealed class StackPanel : Container, IDisplayScaleLayout
    {
        private Orientation _orientation = Orientation.Vertical;
        private float _gap;
        private CrossAxisAlignment _crossAxisAlignment = CrossAxisAlignment.Stretch;

        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (!Enum.IsDefined(typeof(Orientation), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (_orientation == value) return;
                _orientation = value;
                QueueLayout();
            }
        }

        public float Gap
        {
            get => _gap;
            set
            {
                if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (_gap == value) return;
                _gap = value;
                QueueLayout();
            }
        }

        public CrossAxisAlignment CrossAxisAlignment
        {
            get => _crossAxisAlignment;
            set
            {
                if (!Enum.IsDefined(typeof(CrossAxisAlignment), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (_crossAxisAlignment == value) return;
                _crossAxisAlignment = value;
                QueueLayout();
            }
        }

        public override Vector2 GetMinimumSize()
        {
            var minimum = CustomMinimumSize;
            var visibleCount = 0;
            var content = Vector2.Zero;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var childSize = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                if (Orientation == Orientation.Horizontal)
                {
                    content.X += childSize.X;
                    content.Y = Math.Max(content.Y, childSize.Y);
                }
                else
                {
                    content.X = Math.Max(content.X, childSize.X);
                    content.Y += childSize.Y;
                }
                visibleCount++;
            }
            if (visibleCount > 1)
            {
                if (Orientation == Orientation.Horizontal) content.X += Gap * (visibleCount - 1);
                else content.Y += Gap * (visibleCount - 1);
            }
            return Vector2.Max(minimum, content);
        }

        protected override void ArrangeChildren()
        {
            var children = new List<Control>();
            foreach (var child in VisualChildren) if (child.Visible) children.Add(child);
            var cursor = 0f;
            foreach (var child in children)
            {
                var desired = child.GetBoundDesiredSize();
                if (Orientation == Orientation.Horizontal)
                {
                    var availableCross = Math.Max(0, Size.Y - child.Margins.Vertical);
                    var cross = CrossAxisAlignment == CrossAxisAlignment.Stretch ? availableCross : Math.Min(availableCross, desired.Y);
                    var crossOffset = GetCrossOffset(availableCross, cross);
                    var x = cursor + (IsLayoutRtl() ? child.Margins.Right : child.Margins.Left);
                    if (IsLayoutRtl()) x = Size.X - x - desired.X;
                    LayoutMath.Arrange(this, child, x, child.Margins.Top + crossOffset, desired.X, cross);
                    cursor += desired.X + child.Margins.Horizontal + Gap;
                }
                else
                {
                    var availableCross = Math.Max(0, Size.X - child.Margins.Horizontal);
                    var cross = CrossAxisAlignment == CrossAxisAlignment.Stretch ? availableCross : Math.Min(availableCross, desired.X);
                    var crossOffset = GetCrossOffset(availableCross, cross);
                    if (IsLayoutRtl()) crossOffset = availableCross - crossOffset - cross;
                    LayoutMath.Arrange(this, child, child.Margins.Left + crossOffset, cursor + child.Margins.Top, cross, desired.Y);
                    cursor += desired.Y + child.Margins.Vertical + Gap;
                }
            }
        }

        private float GetCrossOffset(float available, float size) => CrossAxisAlignment switch
        {
            CrossAxisAlignment.Center => (available - size) / 2,
            CrossAxisAlignment.End => available - size,
            _ => 0,
        };
    }

    public sealed class WrapPanel : Container, IDisplayScaleLayout
    {
        private Orientation _orientation = Orientation.Horizontal;
        private float _itemGap;
        private float _lineGap;
        private CrossAxisAlignment _crossAxisAlignment = CrossAxisAlignment.Start;

        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (!Enum.IsDefined(typeof(Orientation), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (_orientation == value) return;
                _orientation = value;
                QueueLayout();
            }
        }
        public float ItemGap { get => _itemGap; set { ValidateGap(value, nameof(value)); if (_itemGap == value) return; _itemGap = value; QueueLayout(); } }
        public float LineGap { get => _lineGap; set { ValidateGap(value, nameof(value)); if (_lineGap == value) return; _lineGap = value; QueueLayout(); } }
        public CrossAxisAlignment CrossAxisAlignment
        {
            get => _crossAxisAlignment;
            set
            {
                if (!Enum.IsDefined(typeof(CrossAxisAlignment), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (_crossAxisAlignment == value) return;
                _crossAxisAlignment = value;
                QueueLayout();
            }
        }
        public int LineCount { get; private set; }

        public override Vector2 GetMinimumSize()
        {
            var largestMain = 0f;
            var largestCross = 0f;
            var availableMain = Orientation == Orientation.Horizontal ? Size.X : Size.Y;
            var lineMain = 0f;
            var lineCross = 0f;
            var totalCross = 0f;
            var lineCount = 0;
            var lineItems = 0;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                var childMain = Orientation == Orientation.Horizontal ? desired.X : desired.Y;
                var childCross = Orientation == Orientation.Horizontal ? desired.Y : desired.X;
                largestMain = Math.Max(largestMain, childMain);
                largestCross = Math.Max(largestCross, childCross);
                if (availableMain > 0 && lineItems > 0 && lineMain + ItemGap + childMain > availableMain)
                {
                    totalCross += lineCross;
                    lineCount++;
                    lineMain = 0;
                    lineCross = 0;
                    lineItems = 0;
                }
                if (lineItems > 0) lineMain += ItemGap;
                lineMain += childMain;
                lineCross = Math.Max(lineCross, childCross);
                lineItems++;
            }
            if (lineItems > 0)
            {
                totalCross += lineCross;
                lineCount++;
            }
            if (lineCount > 1) totalCross += LineGap * (lineCount - 1);
            if (availableMain <= 0) totalCross = largestCross;
            var content = Orientation == Orientation.Horizontal ? new Vector2(largestMain, totalCross) : new Vector2(totalCross, largestMain);
            return Vector2.Max(CustomMinimumSize, content);
        }

        protected override void ArrangeChildren()
        {
            var lines = new List<List<Control>>();
            var current = new List<Control>();
            var currentMain = 0f;
            var availableMain = Math.Max(0, Orientation == Orientation.Horizontal ? Size.X : Size.Y);
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                var childMain = Orientation == Orientation.Horizontal ? desired.X : desired.Y;
                if (current.Count > 0 && currentMain + ItemGap + childMain > availableMain)
                {
                    lines.Add(current);
                    current = new List<Control>();
                    currentMain = 0;
                }
                if (current.Count > 0) currentMain += ItemGap;
                current.Add(child);
                currentMain += childMain;
            }
            if (current.Count > 0) lines.Add(current);
            LineCount = lines.Count;

            var crossCursor = 0f;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineCross = 0f;
                foreach (var child in line)
                {
                    var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                    lineCross = Math.Max(lineCross, Orientation == Orientation.Horizontal ? desired.Y : desired.X);
                }
                var mainCursor = 0f;
                foreach (var child in line)
                {
                    var desired = child.GetBoundDesiredSize();
                    var marginMainStart = Orientation == Orientation.Horizontal ? (IsLayoutRtl() ? child.Margins.Right : child.Margins.Left) : child.Margins.Top;
                    var marginMainEnd = Orientation == Orientation.Horizontal ? (IsLayoutRtl() ? child.Margins.Left : child.Margins.Right) : child.Margins.Bottom;
                    var marginCrossStart = Orientation == Orientation.Horizontal ? child.Margins.Top : (IsLayoutRtl() ? child.Margins.Right : child.Margins.Left);
                    var marginCrossEnd = Orientation == Orientation.Horizontal ? child.Margins.Bottom : (IsLayoutRtl() ? child.Margins.Left : child.Margins.Right);
                    var desiredCross = Orientation == Orientation.Horizontal ? desired.Y : desired.X;
                    var availableCross = Math.Max(0, lineCross - marginCrossStart - marginCrossEnd);
                    var childCross = CrossAxisAlignment == CrossAxisAlignment.Stretch ? availableCross : Math.Min(availableCross, desiredCross);
                    var crossOffset = CrossAxisAlignment == CrossAxisAlignment.Center ? (availableCross - childCross) / 2
                        : CrossAxisAlignment == CrossAxisAlignment.End ? availableCross - childCross : 0;
                    if (Orientation == Orientation.Horizontal)
                    {
                        LayoutMath.Arrange(this, child, mainCursor + marginMainStart, crossCursor + marginCrossStart + crossOffset, desired.X, childCross);
                        mainCursor += marginMainStart + desired.X + marginMainEnd + ItemGap;
                    }
                    else
                    {
                        LayoutMath.Arrange(this, child, crossCursor + marginCrossStart + crossOffset, mainCursor + marginMainStart, childCross, desired.Y);
                        mainCursor += marginMainStart + desired.Y + marginMainEnd + ItemGap;
                    }
                }
                crossCursor += lineCross + (lineIndex + 1 < lines.Count ? LineGap : 0);
            }
            if (!IsLayoutRtl()) return;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var position = child.Position;
                position.X = Size.X - position.X - child.Size.X;
                child.Position = position;
            }
        }

        private static void ValidateGap(float value, string parameterName)
        {
            if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public enum FlexDirection { Row, RowReverse, Column, ColumnReverse }
    public enum FlexWrap { NoWrap, Wrap, WrapReverse }
    public enum FlexJustify { Start, Center, End, SpaceBetween, SpaceAround, SpaceEvenly }
    public enum FlexAlign { Auto, Start, Center, End, Stretch }
    public enum FlexAlignContent { Start, Center, End, Stretch, SpaceBetween, SpaceAround, SpaceEvenly }

    public sealed class FlexPanel : Container, IDisplayScaleLayout
    {
        private sealed class Item
        {
            public int Order;
            public float Grow;
            public float Shrink = 1;
            public LayoutLength Basis = LayoutLength.Auto;
            public FlexAlign AlignSelf = FlexAlign.Auto;
        }
        private sealed class Entry
        {
            public Control Child;
            public Item Item;
            public float Main;
            public float Cross;
            public float MainStartMargin;
            public float MainEndMargin;
            public float CrossStartMargin;
            public float CrossEndMargin;
            public float MinimumMain;
            public float MaximumMain;
            public int SourceIndex;
        }

        private static readonly ConditionalWeakTable<Control, Item> Items = new ConditionalWeakTable<Control, Item>();
        private FlexDirection _direction;
        private FlexWrap _wrap;
        private FlexJustify _justifyContent;
        private FlexAlign _alignItems = FlexAlign.Stretch;
        private FlexAlignContent _alignContent = FlexAlignContent.Stretch;
        private float _rowGap;
        private float _columnGap;

        public FlexDirection Direction { get => _direction; set { ValidateEnum(value, nameof(value)); if (_direction == value) return; _direction = value; QueueLayout(); } }
        public FlexWrap Wrap { get => _wrap; set { ValidateEnum(value, nameof(value)); if (_wrap == value) return; _wrap = value; QueueLayout(); } }
        public FlexJustify JustifyContent { get => _justifyContent; set { ValidateEnum(value, nameof(value)); if (_justifyContent == value) return; _justifyContent = value; QueueLayout(); } }
        public FlexAlign AlignItems { get => _alignItems; set { ValidateEnum(value, nameof(value)); if (value == FlexAlign.Auto) throw new ArgumentOutOfRangeException(nameof(value)); if (_alignItems == value) return; _alignItems = value; QueueLayout(); } }
        public FlexAlignContent AlignContent { get => _alignContent; set { ValidateEnum(value, nameof(value)); if (_alignContent == value) return; _alignContent = value; QueueLayout(); } }
        public float RowGap { get => _rowGap; set { ValidateGap(value, nameof(value)); if (_rowGap == value) return; _rowGap = value; QueueLayout(); } }
        public float ColumnGap { get => _columnGap; set { ValidateGap(value, nameof(value)); if (_columnGap == value) return; _columnGap = value; QueueLayout(); } }
        public float Gap { set { RowGap = value; ColumnGap = value; } }

        public static int GetOrder(Control child) => GetItem(child).Order;
        public static void SetOrder(Control child, int value) { GetItem(child).Order = value; child.VisualParent?.QueueLayout(); }
        public static float GetGrow(Control child) => GetItem(child).Grow;
        public static void SetGrow(Control child, float value) { ValidateNonnegative(value, nameof(value)); GetItem(child).Grow = value; child.VisualParent?.QueueLayout(); }
        public static float GetShrink(Control child) => GetItem(child).Shrink;
        public static void SetShrink(Control child, float value) { ValidateNonnegative(value, nameof(value)); GetItem(child).Shrink = value; child.VisualParent?.QueueLayout(); }
        public static LayoutLength GetBasis(Control child) => GetItem(child).Basis;
        public static void SetBasis(Control child, LayoutLength value)
        {
            if (value.Unit == LayoutLengthUnit.Star) throw new ArgumentException("Flex basis cannot use star units.", nameof(value));
            GetItem(child).Basis = value;
            child.VisualParent?.QueueLayout();
        }
        public static FlexAlign GetAlignSelf(Control child) => GetItem(child).AlignSelf;
        public static void SetAlignSelf(Control child, FlexAlign value) { ValidateEnum(value, nameof(value)); GetItem(child).AlignSelf = value; child.VisualParent?.QueueLayout(); }

        public override Vector2 GetMinimumSize()
        {
            var horizontal = Direction is FlexDirection.Row or FlexDirection.RowReverse;
            var main = 0f;
            var cross = 0f;
            var count = 0;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                var childMain = horizontal ? desired.X : desired.Y;
                if (Wrap == FlexWrap.NoWrap) main += childMain;
                else main = Math.Max(main, childMain);
                cross = Math.Max(cross, horizontal ? desired.Y : desired.X);
                count++;
            }
            var mainGap = horizontal ? ColumnGap : RowGap;
            if (Wrap == FlexWrap.NoWrap) main += mainGap * Math.Max(0, count - 1);
            var content = horizontal ? new Vector2(main, cross) : new Vector2(cross, main);
            return Vector2.Max(CustomMinimumSize, content);
        }

        protected override void ArrangeChildren()
        {
            var horizontal = Direction is FlexDirection.Row or FlexDirection.RowReverse;
            var reverseMain = Direction is FlexDirection.RowReverse or FlexDirection.ColumnReverse;
            if (horizontal && IsLayoutRtl()) reverseMain = !reverseMain;
            var availableMain = horizontal ? Size.X : Size.Y;
            var availableCross = horizontal ? Size.Y : Size.X;
            var mainGap = horizontal ? ColumnGap : RowGap;
            var lineGap = horizontal ? RowGap : ColumnGap;
            var reverseCross = Wrap == FlexWrap.WrapReverse;
            var entries = new List<Entry>();
            var sourceIndex = 0;
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var item = GetItem(child);
                var desired = child.GetBoundDesiredSize();
                var minimum = child.GetMinimumSize();
                var maximum = child.GetCombinedMaximumSize();
                var intrinsicMain = horizontal ? desired.X : desired.Y;
                var minimumMain = horizontal ? minimum.X : minimum.Y;
                var maximumMain = horizontal ? maximum.X : maximum.Y;
                var hypotheticalMain = Math.Max(minimumMain, item.Basis.Resolve(availableMain, intrinsicMain));
                if (maximumMain >= 0) hypotheticalMain = Math.Min(hypotheticalMain, maximumMain);
                entries.Add(new Entry
                {
                    Child = child,
                    Item = item,
                    Main = hypotheticalMain,
                    Cross = horizontal ? desired.Y : desired.X,
                    MainStartMargin = horizontal ? (reverseMain ? child.Margins.Right : child.Margins.Left) : (reverseMain ? child.Margins.Bottom : child.Margins.Top),
                    MainEndMargin = horizontal ? (reverseMain ? child.Margins.Left : child.Margins.Right) : (reverseMain ? child.Margins.Top : child.Margins.Bottom),
                    CrossStartMargin = horizontal ? (reverseCross ? child.Margins.Bottom : child.Margins.Top) : (reverseCross != IsLayoutRtl() ? child.Margins.Right : child.Margins.Left),
                    CrossEndMargin = horizontal ? (reverseCross ? child.Margins.Top : child.Margins.Bottom) : (reverseCross != IsLayoutRtl() ? child.Margins.Left : child.Margins.Right),
                    MinimumMain = minimumMain,
                    MaximumMain = maximumMain,
                    SourceIndex = sourceIndex++,
                });
            }
            entries.Sort((left, right) =>
            {
                var order = left.Item.Order.CompareTo(right.Item.Order);
                return order != 0 ? order : left.SourceIndex.CompareTo(right.SourceIndex);
            });
            var lines = new List<List<Entry>>();
            var current = new List<Entry>();
            var used = 0f;
            foreach (var entry in entries)
            {
                var outer = entry.Main + entry.MainStartMargin + entry.MainEndMargin;
                if (Wrap != FlexWrap.NoWrap && current.Count > 0 && used + mainGap + outer > availableMain)
                {
                    lines.Add(current);
                    current = new List<Entry>();
                    used = 0;
                }
                if (current.Count > 0) used += mainGap;
                current.Add(entry);
                used += outer;
            }
            if (current.Count > 0) lines.Add(current);
            if (lines.Count == 0) return;

            var lineCrosses = new float[lines.Count];
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                var occupied = mainGap * Math.Max(0, line.Count - 1);
                foreach (var entry in line)
                {
                    occupied += entry.Main + entry.MainStartMargin + entry.MainEndMargin;
                    lineCrosses[lineIndex] = Math.Max(lineCrosses[lineIndex], entry.Cross + entry.CrossStartMargin + entry.CrossEndMargin);
                }
                var free = availableMain - occupied;
                ResolveFlexibleMainSizes(line, free);
            }

            if (Wrap == FlexWrap.NoWrap) lineCrosses[0] = availableCross;

            var totalCross = lineGap * Math.Max(0, lines.Count - 1);
            foreach (var value in lineCrosses) totalCross += value;
            var crossFree = Math.Max(0, availableCross - totalCross);
            if (AlignContent == FlexAlignContent.Stretch)
            {
                var addition = crossFree / lines.Count;
                for (var index = 0; index < lineCrosses.Length; index++) lineCrosses[index] += addition;
                crossFree = 0;
            }
            GetDistribution(AlignContent, crossFree, lines.Count, out var crossCursor, out var extraLineGap);

            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineOccupied = mainGap * Math.Max(0, line.Count - 1);
                foreach (var entry in line) lineOccupied += entry.Main + entry.MainStartMargin + entry.MainEndMargin;
                GetDistribution(JustifyContent, Math.Max(0, availableMain - lineOccupied), line.Count, out var mainCursor, out var extraItemGap);
                foreach (var entry in line)
                {
                    var alignment = entry.Item.AlignSelf == FlexAlign.Auto ? AlignItems : entry.Item.AlignSelf;
                    var crossAvailable = Math.Max(0, lineCrosses[lineIndex] - entry.CrossStartMargin - entry.CrossEndMargin);
                    var childCross = alignment == FlexAlign.Stretch ? crossAvailable : Math.Min(crossAvailable, entry.Cross);
                    var crossOffset = alignment == FlexAlign.Center ? (crossAvailable - childCross) / 2 : alignment == FlexAlign.End ? crossAvailable - childCross : 0;
                    var logicalCross = crossCursor + entry.CrossStartMargin + crossOffset;
                    if (Wrap == FlexWrap.WrapReverse) logicalCross = availableCross - logicalCross - childCross;
                    var logicalMain = mainCursor + entry.MainStartMargin;
                    var finalMain = reverseMain ? availableMain - logicalMain - entry.Main : logicalMain;
                    if (horizontal)
                    {
                        LayoutMath.Arrange(this, entry.Child, finalMain, logicalCross, entry.Main, childCross);
                    }
                    else
                    {
                        var finalCross = logicalCross;
                        if (IsLayoutRtl()) finalCross = availableCross - finalCross - childCross;
                        LayoutMath.Arrange(this, entry.Child, finalCross, finalMain, childCross, entry.Main);
                    }
                    mainCursor += entry.MainStartMargin + entry.Main + entry.MainEndMargin + mainGap + extraItemGap;
                }
                crossCursor += lineCrosses[lineIndex] + lineGap + extraLineGap;
            }
        }

        private static Item GetItem(Control child) => Items.GetOrCreateValue(child ?? throw new ArgumentNullException(nameof(child)));
        private static void ResolveFlexibleMainSizes(List<Entry> line, float free)
        {
            var active = new List<Entry>(line);
            while (active.Count > 0 && Math.Abs(free) > .0001f)
            {
                var weight = 0f;
                foreach (var entry in active) weight += free > 0 ? entry.Item.Grow : entry.Item.Shrink * entry.Main;
                if (weight <= 0) break;
                var froze = false;
                foreach (var entry in active)
                {
                    var entryWeight = free > 0 ? entry.Item.Grow : entry.Item.Shrink * entry.Main;
                    var candidate = entry.Main + free * entryWeight / weight;
                    var constrained = Math.Max(entry.MinimumMain, candidate);
                    if (entry.MaximumMain >= 0) constrained = Math.Min(constrained, entry.MaximumMain);
                    if (Math.Abs(constrained - candidate) <= .0001f) continue;
                    free -= constrained - entry.Main;
                    entry.Main = constrained;
                    active.Remove(entry);
                    froze = true;
                    break;
                }
                if (froze) continue;
                foreach (var entry in active)
                {
                    var entryWeight = free > 0 ? entry.Item.Grow : entry.Item.Shrink * entry.Main;
                    entry.Main += free * entryWeight / weight;
                }
                break;
            }
            foreach (var entry in line)
            {
                entry.Main = Math.Max(entry.MinimumMain, entry.Main);
                if (entry.MaximumMain >= 0) entry.Main = Math.Min(entry.Main, entry.MaximumMain);
            }
        }
        private static void GetDistribution(FlexJustify value, float free, int count, out float offset, out float gap)
        {
            offset = 0; gap = 0;
            if (value == FlexJustify.End) offset = free;
            else if (value == FlexJustify.Center) offset = free / 2;
            else if (value == FlexJustify.SpaceBetween && count > 1) gap = free / (count - 1);
            else if (value == FlexJustify.SpaceAround && count > 0) { gap = free / count; offset = gap / 2; }
            else if (value == FlexJustify.SpaceEvenly && count > 0) { gap = free / (count + 1); offset = gap; }
        }
        private static void GetDistribution(FlexAlignContent value, float free, int count, out float offset, out float gap)
        {
            offset = 0; gap = 0;
            if (value == FlexAlignContent.End) offset = free;
            else if (value == FlexAlignContent.Center) offset = free / 2;
            else if (value == FlexAlignContent.SpaceBetween && count > 1) gap = free / (count - 1);
            else if (value == FlexAlignContent.SpaceAround && count > 0) { gap = free / count; offset = gap / 2; }
            else if (value == FlexAlignContent.SpaceEvenly && count > 0) { gap = free / (count + 1); offset = gap; }
        }
        private static void ValidateGap(float value, string parameterName) => ValidateNonnegative(value, parameterName);
        private static void ValidateNonnegative(float value, string parameterName) { if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName); }
        private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum { if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(parameterName); }
    }

    public sealed class GridPanel : Container, IDisplayScaleLayout
    {
        private sealed class Placement
        {
            public int Row;
            public int Column;
            public int RowSpan = 1;
            public int ColumnSpan = 1;
            public HorizontalAlignment Horizontal = HorizontalAlignment.Fill;
            public VerticalAlignment Vertical = VerticalAlignment.Fill;
        }
        private sealed class DefinitionCollection<T> : Collection<T> where T : class
        {
            private readonly Action _changed;
            public DefinitionCollection(Action changed) { _changed = changed; }
            protected override void InsertItem(int index, T item) { if (item == null) throw new ArgumentNullException(nameof(item)); base.InsertItem(index, item); Subscribe(item); _changed(); }
            protected override void SetItem(int index, T item) { if (item == null) throw new ArgumentNullException(nameof(item)); Unsubscribe(this[index]); base.SetItem(index, item); Subscribe(item); _changed(); }
            protected override void RemoveItem(int index) { Unsubscribe(this[index]); base.RemoveItem(index); _changed(); }
            protected override void ClearItems() { foreach (var item in this) Unsubscribe(item); base.ClearItems(); _changed(); }
            private void Subscribe(T item) { if (item is ColumnDefinition column) column.Changed += _changed; else if (item is RowDefinition row) row.Changed += _changed; }
            private void Unsubscribe(T item) { if (item is ColumnDefinition column) column.Changed -= _changed; else if (item is RowDefinition row) row.Changed -= _changed; }
        }

        private static readonly ConditionalWeakTable<Control, Placement> Placements = new ConditionalWeakTable<Control, Placement>();
        private float _columnGap;
        private float _rowGap;

        public GridPanel()
        {
            ColumnDefinitions = new DefinitionCollection<ColumnDefinition>(QueueLayout);
            RowDefinitions = new DefinitionCollection<RowDefinition>(QueueLayout);
        }

        public Collection<ColumnDefinition> ColumnDefinitions { get; }
        public Collection<RowDefinition> RowDefinitions { get; }
        public float ColumnGap { get => _columnGap; set { ValidateGap(value, nameof(value)); if (_columnGap == value) return; _columnGap = value; QueueLayout(); } }
        public float RowGap { get => _rowGap; set { ValidateGap(value, nameof(value)); if (_rowGap == value) return; _rowGap = value; QueueLayout(); } }

        public static int GetRow(Control child) => GetPlacement(child).Row;
        public static void SetRow(Control child, int value) { ValidateIndex(value, nameof(value)); GetPlacement(child).Row = value; child.VisualParent?.QueueLayout(); }
        public static int GetColumn(Control child) => GetPlacement(child).Column;
        public static void SetColumn(Control child, int value) { ValidateIndex(value, nameof(value)); GetPlacement(child).Column = value; child.VisualParent?.QueueLayout(); }
        public static int GetRowSpan(Control child) => GetPlacement(child).RowSpan;
        public static void SetRowSpan(Control child, int value) { ValidateSpan(value, nameof(value)); GetPlacement(child).RowSpan = value; child.VisualParent?.QueueLayout(); }
        public static int GetColumnSpan(Control child) => GetPlacement(child).ColumnSpan;
        public static void SetColumnSpan(Control child, int value) { ValidateSpan(value, nameof(value)); GetPlacement(child).ColumnSpan = value; child.VisualParent?.QueueLayout(); }
        public static HorizontalAlignment GetHorizontalAlignment(Control child) => GetPlacement(child).Horizontal;
        public static void SetHorizontalAlignment(Control child, HorizontalAlignment value) { ValidateEnum(value, nameof(value)); GetPlacement(child).Horizontal = value; child.VisualParent?.QueueLayout(); }
        public static VerticalAlignment GetVerticalAlignment(Control child) => GetPlacement(child).Vertical;
        public static void SetVerticalAlignment(Control child, VerticalAlignment value) { ValidateEnum(value, nameof(value)); GetPlacement(child).Vertical = value; child.VisualParent?.QueueLayout(); }

        public override Vector2 GetMinimumSize()
        {
            ResolveTracks(float.PositiveInfinity, float.PositiveInfinity, out var columns, out var rows);
            return Vector2.Max(CustomMinimumSize, new Vector2(Sum(columns) + ColumnGap * Math.Max(0, columns.Length - 1), Sum(rows) + RowGap * Math.Max(0, rows.Length - 1)));
        }

        protected override void ArrangeChildren()
        {
            ResolveTracks(Size.X, Size.Y, out var columns, out var rows);
            var columnOffsets = GetOffsets(columns, ColumnGap);
            var rowOffsets = GetOffsets(rows, RowGap);
            var rtl = IsLayoutRtl();
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var placement = GetPlacement(child);
                var column = Math.Min(placement.Column, columns.Length - 1);
                var row = Math.Min(placement.Row, rows.Length - 1);
                var columnSpan = Math.Min(placement.ColumnSpan, columns.Length - column);
                var rowSpan = Math.Min(placement.RowSpan, rows.Length - row);
                var cellWidth = Sum(columns, column, columnSpan) + ColumnGap * Math.Max(0, columnSpan - 1);
                var cellHeight = Sum(rows, row, rowSpan) + RowGap * Math.Max(0, rowSpan - 1);
                var cellX = rtl ? Size.X - columnOffsets[column] - cellWidth : columnOffsets[column];
                var desired = child.GetBoundDesiredSize();
                var availableWidth = Math.Max(0, cellWidth - child.Margins.Horizontal);
                var availableHeight = Math.Max(0, cellHeight - child.Margins.Vertical);
                var width = placement.Horizontal == HorizontalAlignment.Fill ? availableWidth : Math.Min(availableWidth, desired.X);
                var height = placement.Vertical == VerticalAlignment.Fill ? availableHeight : Math.Min(availableHeight, desired.Y);
                var x = placement.Horizontal == HorizontalAlignment.Center ? (availableWidth - width) / 2 : placement.Horizontal == HorizontalAlignment.Right ? availableWidth - width : 0;
                var y = placement.Vertical == VerticalAlignment.Center ? (availableHeight - height) / 2 : placement.Vertical == VerticalAlignment.Bottom ? availableHeight - height : 0;
                if (rtl && placement.Horizontal is HorizontalAlignment.Left or HorizontalAlignment.Right) x = availableWidth - x - width;
                LayoutMath.Arrange(this, child, cellX + child.Margins.Left + x, rowOffsets[row] + child.Margins.Top + y, width, height);
            }
        }

        private void ResolveTracks(float availableWidth, float availableHeight, out float[] columns, out float[] rows)
        {
            var columnCount = Math.Max(1, ColumnDefinitions.Count);
            var rowCount = Math.Max(1, RowDefinitions.Count);
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                var placement = GetPlacement(child);
                columnCount = Math.Max(columnCount, placement.Column + placement.ColumnSpan);
                rowCount = Math.Max(rowCount, placement.Row + placement.RowSpan);
            }
            var columnTracks = new GridTrackSize[columnCount];
            var rowTracks = new GridTrackSize[rowCount];
            for (var index = 0; index < columnCount; index++) columnTracks[index] = index < ColumnDefinitions.Count ? ColumnDefinitions[index].Width : GridTrackSize.Auto;
            for (var index = 0; index < rowCount; index++) rowTracks[index] = index < RowDefinitions.Count ? RowDefinitions[index].Height : GridTrackSize.Auto;
            var columnContent = new float[columnCount];
            var rowContent = new float[rowCount];
            for (var span = 1; span <= columnCount; span++)
            {
                foreach (var child in VisualChildren)
                {
                    if (!child.Visible) continue;
                    var placement = GetPlacement(child);
                    if (placement.ColumnSpan != span) continue;
                    var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                    Contribute(columnTracks, columnContent, placement.Column, placement.ColumnSpan, desired.X, availableWidth, ColumnGap);
                }
            }
            for (var span = 1; span <= rowCount; span++)
            {
                foreach (var child in VisualChildren)
                {
                    if (!child.Visible) continue;
                    var placement = GetPlacement(child);
                    if (placement.RowSpan != span) continue;
                    var desired = child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical);
                    Contribute(rowTracks, rowContent, placement.Row, placement.RowSpan, desired.Y, availableHeight, RowGap);
                }
            }
            columns = ResolveAxis(columnTracks, columnContent, availableWidth, ColumnGap);
            rows = ResolveAxis(rowTracks, rowContent, availableHeight, RowGap);
        }

        private static float[] ResolveAxis(GridTrackSize[] tracks, float[] content, float available, float gap)
        {
            var values = new float[tracks.Length];
            var weights = new float[tracks.Length];
            for (var index = 0; index < tracks.Length; index++)
            {
                var track = tracks[index];
                values[index] = track.Unit switch
                {
                    GridTrackUnit.Pixel => track.Value,
                    GridTrackUnit.Percent when float.IsFinite(available) => available * track.Value,
                    GridTrackUnit.FitContent => Math.Min(content[index], track.Value),
                    GridTrackUnit.MinMax => Math.Max(content[index], track.Minimum.Resolve(available, content[index])),
                    _ => content[index],
                };
                if (track.Unit == GridTrackUnit.MinMax && track.Maximum.Unit is LayoutLengthUnit.Pixel or LayoutLengthUnit.Percent)
                {
                    var minimum = track.Minimum.Resolve(available, content[index]);
                    values[index] = Math.Min(values[index], Math.Max(minimum, track.Maximum.Resolve(available, content[index])));
                }
                if (track.Unit == GridTrackUnit.Star) weights[index] = track.Value;
                else if (track.Unit == GridTrackUnit.MinMax && track.Maximum.Unit == LayoutLengthUnit.Star) weights[index] = track.Maximum.Value;
            }
            if (!float.IsFinite(available)) return values;
            var remaining = available - gap * Math.Max(0, tracks.Length - 1);
            var activeWeight = 0f;
            var active = new bool[tracks.Length];
            for (var index = 0; index < tracks.Length; index++)
            {
                if (weights[index] > 0)
                {
                    active[index] = true;
                    activeWeight += weights[index];
                }
                else remaining -= values[index];
            }
            remaining = Math.Max(0, remaining);
            while (activeWeight > 0)
            {
                var unit = remaining / activeWeight;
                var froze = false;
                for (var index = 0; index < tracks.Length; index++)
                {
                    if (!active[index] || values[index] <= unit * weights[index]) continue;
                    active[index] = false;
                    activeWeight -= weights[index];
                    remaining = Math.Max(0, remaining - values[index]);
                    froze = true;
                    break;
                }
                if (froze) continue;
                for (var index = 0; index < tracks.Length; index++)
                    if (active[index]) values[index] = unit * weights[index];
                break;
            }
            return values;
        }

        private static void Contribute(GridTrackSize[] tracks, float[] values, int start, int span, float desired, float available, float gap)
        {
            start = Math.Min(start, values.Length - 1);
            span = Math.Min(span, values.Length - start);
            var current = gap * Math.Max(0, span - 1);
            var flexible = new List<int>();
            for (var index = start; index < start + span; index++)
            {
                var track = tracks[index];
                current += track.Unit switch
                {
                    GridTrackUnit.Pixel => track.Value,
                    GridTrackUnit.Percent when float.IsFinite(available) => available * track.Value,
                    _ => values[index],
                };
                if (track.Unit != GridTrackUnit.Pixel && (track.Unit != GridTrackUnit.Percent || !float.IsFinite(available))) flexible.Add(index);
            }
            var deficit = desired - current;
            while (flexible.Count > 0 && deficit > .0001f)
            {
                var addition = deficit / flexible.Count;
                var distributed = 0f;
                for (var candidate = flexible.Count - 1; candidate >= 0; candidate--)
                {
                    var index = flexible[candidate];
                    var capacity = GetContributionCapacity(tracks[index], values[index], available);
                    var applied = Math.Min(addition, capacity);
                    values[index] += applied;
                    distributed += applied;
                    if (capacity <= addition + .0001f) flexible.RemoveAt(candidate);
                }
                if (distributed <= .0001f) break;
                deficit -= distributed;
            }
        }
        private static float GetContributionCapacity(GridTrackSize track, float value, float available)
        {
            if (track.Unit == GridTrackUnit.FitContent) return Math.Max(0, track.Value - value);
            if (track.Unit == GridTrackUnit.MinMax && track.Maximum.Unit == LayoutLengthUnit.Pixel)
                return Math.Max(0, track.Maximum.Value - value);
            if (track.Unit == GridTrackUnit.MinMax && track.Maximum.Unit == LayoutLengthUnit.Percent && float.IsFinite(available))
                return Math.Max(0, available * track.Maximum.Value - value);
            return float.PositiveInfinity;
        }
        private static float[] GetOffsets(float[] sizes, float gap)
        {
            var offsets = new float[sizes.Length];
            for (var index = 1; index < sizes.Length; index++) offsets[index] = offsets[index - 1] + sizes[index - 1] + gap;
            return offsets;
        }
        private static float Sum(float[] values, int start = 0, int count = -1)
        {
            if (count < 0) count = values.Length - start;
            var result = 0f;
            for (var index = start; index < start + count; index++) result += values[index];
            return result;
        }
        private static Placement GetPlacement(Control child) => Placements.GetOrCreateValue(child ?? throw new ArgumentNullException(nameof(child)));
        private static void ValidateGap(float value, string parameterName) { if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(parameterName); }
        private static void ValidateIndex(int value, string parameterName) { if (value < 0) throw new ArgumentOutOfRangeException(parameterName); }
        private static void ValidateSpan(int value, string parameterName) { if (value <= 0) throw new ArgumentOutOfRangeException(parameterName); }
        private static void ValidateEnum<T>(T value, string parameterName) where T : struct, Enum { if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(parameterName); }
    }

    public sealed class Viewbox : Container
    {
        private ImageStretch _stretch = ImageStretch.Contain;
        private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Center;
        private VerticalAlignment _verticalAlignment = VerticalAlignment.Center;

        public ImageStretch Stretch
        {
            get => _stretch;
            set { if (!Enum.IsDefined(typeof(ImageStretch), value)) throw new ArgumentOutOfRangeException(nameof(value)); if (_stretch == value) return; _stretch = value; QueueLayout(); }
        }
        public new HorizontalAlignment HorizontalAlignment
        {
            get => _horizontalAlignment;
            set { if (!Enum.IsDefined(typeof(HorizontalAlignment), value) || value == HorizontalAlignment.Fill) throw new ArgumentOutOfRangeException(nameof(value)); if (_horizontalAlignment == value) return; _horizontalAlignment = value; QueueLayout(); }
        }
        public new VerticalAlignment VerticalAlignment
        {
            get => _verticalAlignment;
            set { if (!Enum.IsDefined(typeof(VerticalAlignment), value) || value == VerticalAlignment.Fill) throw new ArgumentOutOfRangeException(nameof(value)); if (_verticalAlignment == value) return; _verticalAlignment = value; QueueLayout(); }
        }
        public ImageSamplingMode SamplingMode { get; set; } = ImageSamplingMode.Linear;

        public override void AddChild(Control child)
        {
            if (VisualChildren.Count != 0) throw new InvalidOperationException("Viewbox accepts exactly one visual child.");
            base.AddChild(child);
        }

        public override Vector2 GetMinimumSize()
        {
            foreach (var child in VisualChildren)
                if (child.Visible) return Vector2.Max(CustomMinimumSize, child.GetBoundDesiredSize() + new Vector2(child.Margins.Horizontal, child.Margins.Vertical));
            return CustomMinimumSize;
        }

        protected override void ArrangeChildren()
        {
            foreach (var child in VisualChildren)
            {
                if (!child.Visible) continue;
                child.Position = new Vector2(child.Margins.Left, child.Margins.Top);
                child.Size = child.GetBoundDesiredSize();
            }
        }

        internal override void Draw(UIRenderContext context)
        {
            Control child = null;
            foreach (var candidate in VisualChildren) if (candidate.Visible) { child = candidate; break; }
            if (child == null) return;
            var source = child.Bounds;
            var destination = GetDestinationBounds(new Point(source.Width, source.Height));
            context.PushClip(Bounds);
            try { context.DrawScaled(source, destination, SamplingMode, () => child.DrawTree(context)); }
            finally { context.PopClip(); }
        }

        public Rectangle GetDestination(Point sourceSize)
        {
            var destination = GetDestinationBounds(sourceSize);
            return new Rectangle(
                (int)MathF.Round(destination.X),
                (int)MathF.Round(destination.Y),
                Math.Max(0, (int)MathF.Round(destination.Z)),
                Math.Max(0, (int)MathF.Round(destination.W)));
        }

        private Vector4 GetDestinationBounds(Point sourceSize)
        {
            if (sourceSize.X <= 0 || sourceSize.Y <= 0 || Size.X <= 0 || Size.Y <= 0) return Vector4.Zero;
            var destinationSize = new Vector2(sourceSize.X, sourceSize.Y);
            if (Stretch == ImageStretch.Fill) destinationSize = Size;
            else if (Stretch is ImageStretch.Contain or ImageStretch.Cover or ImageStretch.ScaleDown)
            {
                var scale = Stretch == ImageStretch.Cover ? Math.Max(Size.X / sourceSize.X, Size.Y / sourceSize.Y) : Math.Min(Size.X / sourceSize.X, Size.Y / sourceSize.Y);
                if (Stretch == ImageStretch.ScaleDown) scale = Math.Min(1, scale);
                destinationSize = new Vector2(sourceSize.X * scale, sourceSize.Y * scale);
            }
            var horizontal = IsLayoutRtl() ? HorizontalAlignment switch { HorizontalAlignment.Left => HorizontalAlignment.Right, HorizontalAlignment.Right => HorizontalAlignment.Left, _ => HorizontalAlignment } : HorizontalAlignment;
            var x = horizontal == HorizontalAlignment.Center ? (Size.X - destinationSize.X) / 2 : horizontal == HorizontalAlignment.Right ? Size.X - destinationSize.X : 0;
            var y = VerticalAlignment == VerticalAlignment.Center ? (Size.Y - destinationSize.Y) / 2 : VerticalAlignment == VerticalAlignment.Bottom ? Size.Y - destinationSize.Y : 0;
            return new Vector4(Bounds.X + x, Bounds.Y + y, destinationSize.X, destinationSize.Y);
        }
    }
}