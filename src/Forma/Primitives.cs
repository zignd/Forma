// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// UI enums map concepts and values from Godot Engine's control.h, scroll_container.h,
// input_enums.h, and math_defs.h; see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Forma
{
    /// <summary>Specifies how a control participates in pointer hit testing.</summary>
    public enum MouseFilter { Stop, Pass, Ignore }
    /// <summary>Specifies whether a control can receive keyboard focus.</summary>
    public enum FocusMode { None, Click, All }
    /// <summary>Physical pointer buttons using Godot's <c>MouseButton</c> numeric values. Wheel directions are routed through <see cref="Control.PointerWheel"/> instead.</summary>
    public enum PointerButton { None = 0, Left = 1, Right = 2, Middle = 3, XButton1 = 8, XButton2 = 9 }
    /// <summary>Godot-style button mouse mask flags for controls that activate from pointer buttons.</summary>
    [Flags]
    public enum ButtonMouseMask { None = 0, Left = 1, Right = 2, Middle = 4 }
    public enum HorizontalAlignment { Left, Center, Right, Fill }
    public enum VerticalAlignment { Top, Center, Bottom, Fill }
    public enum Orientation { Horizontal, Vertical }
    /// <summary>Godot ScrollContainer display/interaction modes for an axis.</summary>
    public enum ScrollBarVisibility { Auto, Always, Never, Disabled, Reserve, MaximizeFirst }
    public enum Side { Left, Top, Right, Bottom }

    [Flags]
    public enum SizeFlags
    {
        ShrinkBegin = 0,
        Fill = 1,
        Expand = 2,
        ShrinkCenter = 4,
        ShrinkEnd = 8
    }

    /// <summary>Margins, padding and separation values used by UI controls.</summary>
    public readonly struct Thickness : IEquatable<Thickness>
    {
        public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }
        public Thickness(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
        public float Horizontal => Left + Right;
        public float Vertical => Top + Bottom;
        public static Thickness Zero => default;
        public bool Equals(Thickness other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        public override bool Equals(object obj) => obj is Thickness other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
        public static bool operator ==(Thickness left, Thickness right) => left.Equals(right);
        public static bool operator !=(Thickness left, Thickness right) => !left.Equals(right);
    }

    /// <summary>A small, deterministic theme used when an application does not provide custom drawing.</summary>
    public sealed class Theme
    {
        private readonly Dictionary<string, StyleBox> _styleBoxes = new Dictionary<string, StyleBox>(StringComparer.Ordinal);
        private Color? _panelColor, _panelBorderColor, _textColor, _disabledTextColor, _accentColor, _hoverColor, _pressedColor, _focusColor, _backgroundColor, _connectionActivityColor;
        private float? _separation, _borderWidth;
        private Theme _parent;

        /// <summary>Optional parent. Unspecified colors, constants and style items resolve through it.</summary>
        public Theme Parent
        {
            get => _parent;
            set
            {
                if (ReferenceEquals(value, this)) throw new ArgumentException("A theme cannot inherit from itself.", nameof(value));
                for (var ancestor = value; ancestor != null; ancestor = ancestor.Parent)
                    if (ReferenceEquals(ancestor, this)) throw new ArgumentException("Theme inheritance cannot contain a cycle.", nameof(value));
                _parent = value;
            }
        }
        public Color PanelColor { get => _panelColor ?? Parent?.PanelColor ?? new Color(52, 58, 70); set => _panelColor = value; }
        public Color PanelBorderColor { get => _panelBorderColor ?? Parent?.PanelBorderColor ?? new Color(92, 101, 119); set => _panelBorderColor = value; }
        public Color TextColor { get => _textColor ?? Parent?.TextColor ?? Color.White; set => _textColor = value; }
        public Color DisabledTextColor { get => _disabledTextColor ?? Parent?.DisabledTextColor ?? new Color(160, 166, 178); set => _disabledTextColor = value; }
        public Color AccentColor { get => _accentColor ?? Parent?.AccentColor ?? new Color(70, 145, 235); set => _accentColor = value; }
        public Color HoverColor { get => _hoverColor ?? Parent?.HoverColor ?? new Color(73, 82, 99); set => _hoverColor = value; }
        public Color PressedColor { get => _pressedColor ?? Parent?.PressedColor ?? new Color(42, 48, 58); set => _pressedColor = value; }
        public Color FocusColor { get => _focusColor ?? Parent?.FocusColor ?? new Color(112, 178, 255); set => _focusColor = value; }
        public Color BackgroundColor { get => _backgroundColor ?? Parent?.BackgroundColor ?? new Color(35, 39, 47); set => _backgroundColor = value; }
        /// <summary>Godot-style GraphEdit activity tint applied to active connection lines.</summary>
        public Color ConnectionActivityColor { get => _connectionActivityColor ?? Parent?.ConnectionActivityColor ?? new Color(255, 190, 86); set => _connectionActivityColor = value; }
        public float Separation { get => _separation ?? Parent?.Separation ?? 4; set => _separation = value; }
        public float BorderWidth { get => _borderWidth ?? Parent?.BorderWidth ?? 1; set => _borderWidth = value; }
        /// <summary>Assigns a style item, optionally scoped to a control type name.</summary>
        public void SetStyleBox(string itemName, StyleBox styleBox, string typeName = null)
        {
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("A theme item name is required.", nameof(itemName));
            var key = MakeStyleKey(typeName, itemName);
            if (styleBox == null) _styleBoxes.Remove(key); else _styleBoxes[key] = styleBox;
        }
        /// <summary>Gets a style item, first considering the requested control type then the shared item.</summary>
        public StyleBox GetStyleBox(string itemName, string typeName = null)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return null;
            if (!string.IsNullOrEmpty(typeName) && _styleBoxes.TryGetValue(MakeStyleKey(typeName, itemName), out var typed)) return typed;
            if (_styleBoxes.TryGetValue(MakeStyleKey(null, itemName), out var shared)) return shared;
            return Parent?.GetStyleBox(itemName, typeName);
        }
        internal StyleBox GetStyleBox(string itemName, IEnumerable<string> typeNames)
        {
            if (string.IsNullOrWhiteSpace(itemName)) return null;
            if (typeNames != null)
                foreach (var typeName in typeNames)
                    if (!string.IsNullOrEmpty(typeName) && _styleBoxes.TryGetValue(MakeStyleKey(typeName, itemName), out var typed)) return typed;
            if (_styleBoxes.TryGetValue(MakeStyleKey(null, itemName), out var shared)) return shared;
            return Parent?.GetStyleBox(itemName, typeNames);
        }
        private static string MakeStyleKey(string typeName, string itemName) => (typeName ?? string.Empty) + ":" + itemName;
    }
}
