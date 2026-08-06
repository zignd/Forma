// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using Microsoft.Xna.Framework;

namespace Forma
{
    public sealed class ScrollViewportMetricsChangedEventArgs : EventArgs
    {
        public ScrollViewportMetricsChangedEventArgs(ScrollMetrics previous, ScrollMetrics current)
        {
            Previous = previous;
            Current = current;
        }

        public ScrollMetrics Previous { get; }
        public ScrollMetrics Current { get; }
        public bool ViewportChanged => Previous.Viewport != Current.Viewport;
        public bool ExtentChanged => Previous.Extent != Current.Extent;
        public bool OffsetChanged => Previous.Offset != Current.Offset;
    }

    public readonly struct ScrollAnchor
    {
        public ScrollAnchor(object token, Vector2 viewportOffset)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            ViewportOffset = viewportOffset;
        }

        public object Token { get; }
        public Vector2 ViewportOffset { get; }
    }

    public sealed class ScrollViewportController
    {
        private const float DragDeceleration = 1000;
        private Vector2 _offset;
        private ScrollMetrics _metrics;
        private bool _dragTouching;
        private bool _dragDecelerating;
        private bool _beyondDeadzone;
        private Vector2 _dragSpeed;
        private Vector2 _dragAccum;
        private Vector2 _lastDragAccum;
        private Vector2 _dragFrom;
        private float _timeSinceDragMotion;
        private bool _horizontalEnabled = true;
        private bool _verticalEnabled = true;
        private ScrollAnchor? _anchor;

        public Vector2 Offset
        {
            get => _offset;
            set => SetOffset(value);
        }

        public ScrollMetrics Metrics => _metrics;
        public Vector2 Viewport => _metrics.Viewport;
        public Vector2 Extent => _metrics.Extent;
        public Vector2 MaxOffset
        {
            get
            {
                var maximum = _metrics.MaxOffset;
                if (!HorizontalEnabled) maximum.X = 0;
                if (!VerticalEnabled) maximum.Y = 0;
                return maximum;
            }
        }
        public bool HorizontalEnabled
        {
            get => _horizontalEnabled;
            set
            {
                if (_horizontalEnabled == value) return;
                _horizontalEnabled = value;
                SetOffset(_offset);
            }
        }
        public bool VerticalEnabled
        {
            get => _verticalEnabled;
            set
            {
                if (_verticalEnabled == value) return;
                _verticalEnabled = value;
                SetOffset(_offset);
            }
        }
        public int ScrollDeadzone { get; set; }
        public bool IsTouchDragging => _dragTouching;
        public bool IsTouchDragDecelerating => _dragDecelerating;
        public bool IsBeyondScrollDeadzone => _beyondDeadzone;
        public Vector2 TouchDragSpeed => _dragSpeed;
        public ScrollAnchor? Anchor => _anchor;

        public event EventHandler ScrollStarted;
        public event EventHandler ScrollEnded;
        public event EventHandler<ScrollViewportMetricsChangedEventArgs> MetricsChanged;

        public void UpdateMetrics(Vector2 viewport, Vector2 extent)
        {
            var previous = _metrics;
            var metrics = new ScrollMetrics(viewport, extent, _offset);
            _metrics = metrics;
            _offset = Clamp(metrics.Offset);
            _metrics = new ScrollMetrics(metrics.Viewport, metrics.Extent, _offset);
            NotifyMetricsChanged(previous);
        }

        public ScrollAnchor CaptureAnchor(object token, Vector2 contentPosition)
        {
            _anchor = new ScrollAnchor(token, contentPosition - _offset);
            return _anchor.Value;
        }

        public bool RestoreAnchor(object token, Vector2 contentPosition)
        {
            if (!_anchor.HasValue || !ReferenceEquals(_anchor.Value.Token, token)) return false;
            Offset = contentPosition - _anchor.Value.ViewportOffset;
            return true;
        }

        public void ClearAnchor() => _anchor = null;

        public bool ScrollWheel(int delta, bool horizontal, float horizontalPage, float verticalPage)
        {
            if (delta == 0) return false;
            var before = _offset;
            var direction = -Math.Sign(delta);
            if (horizontal && HorizontalEnabled)
                Offset += new Vector2(direction * Math.Max(0, horizontalPage) / 8f, 0);
            else if (VerticalEnabled)
                Offset += new Vector2(0, direction * Math.Max(0, verticalPage) / 8f);
            return before != _offset;
        }

        public void BringIntoView(Rectangle viewportBounds, Rectangle targetBounds)
        {
            Offset += new Vector2(
                GetVisibilityScrollDelta(viewportBounds.Left, viewportBounds.Width, targetBounds.Left, targetBounds.Width),
                GetVisibilityScrollDelta(viewportBounds.Top, viewportBounds.Height, targetBounds.Top, targetBounds.Height));
            CancelTouchDrag();
        }

        public void BeginTouchDrag()
        {
            if (_dragTouching) CancelTouchDrag();
            _dragSpeed = Vector2.Zero;
            _dragAccum = Vector2.Zero;
            _lastDragAccum = Vector2.Zero;
            _dragFrom = _offset;
            _dragTouching = true;
            _dragDecelerating = false;
            _beyondDeadzone = false;
            _timeSinceDragMotion = 0;
        }

        public void TouchDragBy(Vector2 relativeMotion)
        {
            if (!_dragTouching || _dragDecelerating) return;
            _dragAccum -= relativeMotion;
            if (!_beyondDeadzone && !(HorizontalEnabled && Math.Abs(_dragAccum.X) > ScrollDeadzone) &&
                !(VerticalEnabled && Math.Abs(_dragAccum.Y) > ScrollDeadzone)) return;
            if (!_beyondDeadzone)
            {
                _beyondDeadzone = true;
                ScrollStarted?.Invoke(this, EventArgs.Empty);
                _dragAccum = -relativeMotion;
            }
            var requested = _dragFrom + _dragAccum;
            var next = _offset;
            if (HorizontalEnabled) next.X = requested.X; else _dragAccum.X = 0;
            if (VerticalEnabled) next.Y = requested.Y; else _dragAccum.Y = 0;
            Offset = next;
            _timeSinceDragMotion = 0;
        }

        public void EndTouchDrag()
        {
            if (!_dragTouching) return;
            if (_dragSpeed == Vector2.Zero) CancelTouchDrag();
            else _dragDecelerating = true;
        }

        public void CancelTouchDrag()
        {
            _dragTouching = false;
            _dragDecelerating = false;
            _dragSpeed = Vector2.Zero;
            _dragAccum = Vector2.Zero;
            _lastDragAccum = Vector2.Zero;
            _dragFrom = Vector2.Zero;
            if (_beyondDeadzone)
            {
                ScrollEnded?.Invoke(this, EventArgs.Empty);
                _beyondDeadzone = false;
            }
        }

        public void Process(float delta)
        {
            if (!_dragTouching || delta <= 0) return;
            if (_dragDecelerating)
            {
                var position = Clamp(_offset + _dragSpeed * delta);
                var stoppedHorizontal = position.X == 0 || position.X == MaxOffset.X;
                var stoppedVertical = position.Y == 0 || position.Y == MaxOffset.Y;
                Offset = position;
                _dragSpeed.X = Decelerate(_dragSpeed.X, delta, out var speedStoppedHorizontal);
                _dragSpeed.Y = Decelerate(_dragSpeed.Y, delta, out var speedStoppedVertical);
                if ((stoppedHorizontal || speedStoppedHorizontal) && (stoppedVertical || speedStoppedVertical)) CancelTouchDrag();
                return;
            }
            if (_timeSinceDragMotion == 0 || _timeSinceDragMotion > .1f)
            {
                var difference = _dragAccum - _lastDragAccum;
                _lastDragAccum = _dragAccum;
                _dragSpeed = difference / delta;
            }
            _timeSinceDragMotion += delta;
        }

        private Vector2 Clamp(Vector2 offset)
        {
            return Vector2.Min(Vector2.Max(Vector2.Zero, offset), MaxOffset);
        }

        private void SetOffset(Vector2 value)
        {
            var next = Clamp(value);
            if (_offset == next && _metrics.Offset == next) return;
            var previous = _metrics;
            _offset = next;
            _metrics = new ScrollMetrics(_metrics.Viewport, _metrics.Extent, _offset);
            NotifyMetricsChanged(previous);
        }

        private void NotifyMetricsChanged(ScrollMetrics previous)
        {
            if (previous == _metrics) return;
            MetricsChanged?.Invoke(this, new ScrollViewportMetricsChangedEventArgs(previous, _metrics));
        }

        private static float GetVisibilityScrollDelta(float visibleStart, float visibleSize, float targetStart, float targetSize)
        {
            var begin = targetStart - visibleStart;
            var end = targetStart + targetSize - visibleStart - visibleSize;
            if (visibleSize > targetSize) return begin <= 0 ? begin : end <= 0 ? 0 : end;
            return begin >= 0 ? begin : end >= 0 ? 0 : end;
        }

        private static float Decelerate(float speed, float delta, out bool stopped)
        {
            var sign = speed < 0 ? -1 : 1;
            var value = MathF.Abs(speed) - DragDeceleration * delta;
            stopped = value < 0;
            return sign * value;
        }
    }
}