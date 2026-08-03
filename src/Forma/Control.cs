// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// Control geometry, layout-direction, sizing, and input semantics are adapted from
// Godot Engine's scene/gui/control.cpp, control.h, and container.cpp;
// see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma
{
    /// <summary>Godot-compatible layout direction, including application and system locale resolution through <see cref="UIContext"/>.</summary>
    public enum LayoutDirection { Inherited, ApplicationLocale, LeftToRight, RightToLeft, SystemLocale }

    /// <summary>Godot-compatible text shaping direction. Actual Unicode shaping is provided by the active text renderer.</summary>
    public enum TextDirection { Auto, LeftToRight, RightToLeft, Inherited }

    /// <summary>Matches Godot's Control.GrowDirection: how a control's position compensates when an anchor-resolved size is clamped up to its minimum size.</summary>
    public enum GrowDirection { Begin, End, Both }

    /// <summary>
    /// Retained-mode UI element modelled after Godot's Control. Coordinates are relative to the parent;
    /// anchors and offsets are resolved during layout.
    /// </summary>
    public class Control : IAddChild<Control>
    {
        private readonly List<Control> _children = new List<Control>();
        private readonly ReadOnlyCollection<Control> _readOnlyChildren;
        private Vector2 _position;
        private Vector2 _size;
        private Vector2 _customMinimumSize;
        private Vector2 _customMaximumSize = new Vector2(-1, -1);
        private LayoutDirection _layoutDirection;
        private bool _layoutDirty = true;
        private int _zIndex;
        private long _treeOrder;
        private long _nextChildOrder;
        private bool _childOrderDirty = true;
        private readonly List<Control> _childrenInDrawOrder = new List<Control>();
        private readonly Dictionary<string, StyleBox> _styleOverrides = new Dictionary<string, StyleBox>(StringComparer.Ordinal);
        private readonly Dictionary<string, ThemeIcon?> _iconOverrides = new Dictionary<string, ThemeIcon?>(StringComparer.Ordinal);
        private Theme _themeOverride;
        private object _dataContext;
        private bool _hasLocalDataContext;
        private bool _enabled = true;
        private string _name;

        public Control()
        {
            _readOnlyChildren = _children.AsReadOnly();
            MouseFilter = MouseFilter.Stop;
            FocusMode = FocusMode.None;
            Visible = true;
            HorizontalSizeFlags = SizeFlags.Fill;
            VerticalSizeFlags = SizeFlags.Fill;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                NameChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public Control Parent { get; private set; }
        public ReadOnlyCollection<Control> Children => _readOnlyChildren;
        public UIContext Context { get; private set; }
        public ResourceDictionary Resources { get; } = new ResourceDictionary();
        public ControlClassList Classes { get; } = new ControlClassList();
        public IDictionary<string, StyleBox> ThemeStyleOverrides => _styleOverrides;
        public bool HasLocalDataContext => _hasLocalDataContext;
        public object DataContext
        {
            get => _hasLocalDataContext ? _dataContext : Parent?.DataContext;
            set
            {
                var previous = DataContext;
                _dataContext = value;
                _hasLocalDataContext = true;
                NotifyDataContextChanged(previous, DataContext);
            }
        }
        /// <summary>Optional theme applied to this control and inherited by its descendants while drawing.</summary>
        public Theme ThemeOverride
        {
            get => _themeOverride;
            set
            {
                if (ReferenceEquals(_themeOverride, value)) return;
                if (_themeOverride != null) _themeOverride.Changed -= ThemeOverrideChanged;
                _themeOverride = value;
                if (_themeOverride != null) _themeOverride.Changed += ThemeOverrideChanged;
                MarkThemeDirty();
                QueueLayout();
            }
        }
        /// <summary>Text presented by <see cref="UIContext"/> after the pointer rests over this control.</summary>
        public string TooltipText { get; set; } = string.Empty;
        private bool _visible;
        /// <summary>Matches Godot's Control.visible: toggling it requeues the parent's layout, since Container wires each child's visibility_changed signal to queue_sort().</summary>
        public bool Visible { get => _visible; set { if (_visible == value) return; _visible = value; QueueLayout(); } }
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public MouseFilter MouseFilter { get; set; }
        public FocusMode FocusMode { get; set; }
        /// <summary>Optional explicit focus order used before tree traversal.</summary>
        public Control FocusNext { get; set; }
        /// <summary>Optional reverse focus order used before tree traversal.</summary>
        public Control FocusPrevious { get; set; }
        /// <summary>Optional directional focus neighbor.</summary>
        public Control FocusNeighborLeft { get; set; }
        /// <summary>Optional directional focus neighbor.</summary>
        public Control FocusNeighborTop { get; set; }
        /// <summary>Optional directional focus neighbor.</summary>
        public Control FocusNeighborRight { get; set; }
        /// <summary>Optional directional focus neighbor.</summary>
        public Control FocusNeighborBottom { get; set; }
        public bool ClipContents { get; set; }
        /// <summary>Controls bidirectional layout inheritance for containers and alignment-aware controls.</summary>
        public LayoutDirection LayoutDirection
        {
            get => _layoutDirection;
            set
            {
                if (_layoutDirection == value) return;
                _layoutDirection = value;
                QueueLayout();
                foreach (var child in _children) child.MarkInheritedLayoutDirectionDirty();
            }
        }
        /// <summary>Whether this control resolves to right-to-left layout.</summary>
        public bool IsLayoutRtl()
        {
            if (LayoutDirection == Forma.LayoutDirection.RightToLeft) return true;
            if (LayoutDirection == Forma.LayoutDirection.LeftToRight) return false;
            if (LayoutDirection == Forma.LayoutDirection.ApplicationLocale || LayoutDirection == Forma.LayoutDirection.SystemLocale)
                return Context?.ResolveLayoutDirection(LayoutDirection) ?? System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
            return Parent?.IsLayoutRtl() ?? (Context?.ResolveLayoutDirection(Forma.LayoutDirection.Inherited) ?? System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft);
        }
        /// <summary>
        /// Drawing and pointer-picking order. Higher values are painted above and receive input before
        /// lower values; equal values retain tree insertion order.
        /// </summary>
        public int ZIndex
        {
            get => _zIndex;
            set
            {
                if (_zIndex == value) return;
                _zIndex = value;
                if (Parent != null) Parent._childOrderDirty = true;
                else Context?.MarkRootOrderDirty();
            }
        }
        public SizeFlags HorizontalSizeFlags { get; set; }
        public SizeFlags VerticalSizeFlags { get; set; }
        public float SizeFlagsStretchRatio { get; set; } = 1f;
        public Thickness Margins { get; set; }
        public Vector2 CustomMinimumSize
        {
            get => _customMinimumSize;
            set { _customMinimumSize = Vector2.Max(Vector2.Zero, value); QueueLayout(); }
        }
        public Vector2 CustomMaximumSize
        {
            get => _customMaximumSize;
            set
            {
                if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) return;
                _customMaximumSize = new Vector2(value.X < 0 ? -1 : value.X, value.Y < 0 ? -1 : value.Y);
                QueueLayout();
            }
        }
        public Vector2 Position { get => _position; set { _position = value; QueueLayout(); } }
        public Vector2 Size { get => _size; set { _size = Vector2.Max(Vector2.Zero, value); QueueLayout(); } }
        public float AnchorLeft { get; private set; }
        public float AnchorTop { get; private set; }
        public float AnchorRight { get; private set; }
        public float AnchorBottom { get; private set; }
        public float OffsetLeft { get; private set; }
        public float OffsetTop { get; private set; }
        public float OffsetRight { get; private set; }
        public float OffsetBottom { get; private set; }
        /// <summary>Matches Godot's Control.GrowHorizontal (default GROW_DIRECTION_END): how the horizontal position compensates when the anchor-resolved width is clamped up to the minimum size.</summary>
        public GrowDirection HGrowDirection { get; set; } = GrowDirection.End;
        /// <summary>Matches Godot's Control.GrowVertical (default GROW_DIRECTION_END): how the vertical position compensates when the anchor-resolved height is clamped up to the minimum size.</summary>
        public GrowDirection VGrowDirection { get; set; } = GrowDirection.End;
        public Rectangle Bounds => new Rectangle((int)MathF.Round(GlobalPosition.X), (int)MathF.Round(GlobalPosition.Y), (int)MathF.Round(Size.X), (int)MathF.Round(Size.Y));
        public Vector2 GlobalPosition => Parent == null ? Position : Parent.GlobalPosition + Position;

        public event EventHandler LayoutChanged;
        public event EventHandler MouseEntered;
        public event EventHandler MouseExited;
        public event EventHandler FocusEntered;
        public event EventHandler FocusExited;
        public event EventHandler Attached;
        public event EventHandler Detached;
        public event EventHandler EnabledChanged;
        public event EventHandler NameChanged;
        public event EventHandler<DataContextChangedEventArgs> DataContextChanged;
        public event Action<Control, Control> ChildAdded;
        public event Action<Control, Control> ChildRemoved;
        /// <summary>Raised when this control supplies drag data after the pointer passes the drag threshold.</summary>
        public event Action<Control, object> DragStarted;
        /// <summary>Raised when a drag started by this control ends; the boolean indicates whether it was accepted.</summary>
        public event Action<Control, bool> DragEnded;

        public void AddChild(Control child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (child == this) throw new InvalidOperationException("A control cannot be its own child.");
            for (var ancestor = this; ancestor != null; ancestor = ancestor.Parent)
                if (ancestor == child) throw new InvalidOperationException("A control cannot be added below one of its descendants.");
            child.RemoveFromParent();
            var previousDataContext = child.DataContext;
            _children.Add(child);
            child.Parent = this;
            child.NotifyInheritedDataContextChanged(previousDataContext, child.DataContext);
            child._treeOrder = ++_nextChildOrder;
            _childOrderDirty = true;
            child.SetContext(Context);
            ChildAdded?.Invoke(this, child);
            QueueLayout();
        }

        public bool RemoveChild(Control child)
        {
            if (child == null || !_children.Remove(child)) return false;
            var previousDataContext = child.DataContext;
            child.Parent = null;
            child.NotifyInheritedDataContextChanged(previousDataContext, child.DataContext);
            _childOrderDirty = true;
            child.SetContext(null);
            ChildRemoved?.Invoke(this, child);
            QueueLayout();
            return true;
        }

        void IAddChild.AddChild(object child) => AddChild((Control)child);

        public void ClearDataContext()
        {
            if (!_hasLocalDataContext) return;
            var previous = DataContext;
            _dataContext = null;
            _hasLocalDataContext = false;
            NotifyDataContextChanged(previous, DataContext);
        }

        public bool TryFindResource(string key, out object value)
        {
            if (Resources.TryFind(key, out value)) return true;
            if (Parent != null) return Parent.TryFindResource(key, out value);
            if (Context != null) return Context.Resources.TryFind(key, out value);
            value = null;
            return false;
        }

        public T FindName<T>(string name) where T : class => NameScope.GetNameScope(this)?.Find<T>(name);

        private void NotifyDataContextChanged(object previous, object current)
        {
            if (Equals(previous, current)) return;
            DataContextChanged?.Invoke(this, new DataContextChangedEventArgs(previous, current));
            foreach (var child in _children)
                child.NotifyInheritedDataContextChanged(previous, current);
        }

        private void NotifyInheritedDataContextChanged(object previous, object current)
        {
            if (_hasLocalDataContext || Equals(previous, current)) return;
            DataContextChanged?.Invoke(this, new DataContextChangedEventArgs(previous, current));
            foreach (var child in _children)
                child.NotifyInheritedDataContextChanged(previous, current);
        }

        /// <summary>Moves an existing child to a different sibling index without changing its parent or UI context.</summary>
        public void MoveChild(Control child, int toIndex)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            var fromIndex = _children.IndexOf(child);
            if (fromIndex < 0) throw new ArgumentException("The control is not a child of this control.", nameof(child));
            if (toIndex < 0 || toIndex >= _children.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
            if (fromIndex == toIndex) return;
            _children.RemoveAt(fromIndex);
            _children.Insert(toIndex, child);
            foreach (var sibling in _children) sibling._treeOrder = ++_nextChildOrder;
            _childOrderDirty = true;
            QueueLayout();
        }

        public void RemoveFromParent() => Parent?.RemoveChild(this);

        public Control GetNodeOrNull(string name)
        {
            foreach (var child in _children)
            {
                if (child.Name == name) return child;
                var nested = child.GetNodeOrNull(name);
                if (nested != null) return nested;
            }
            return null;
        }

        /// <summary>Sets a local StyleBox override, equivalent to Godot's add_theme_style_override.</summary>
        public void AddThemeStyleOverride(string itemName, StyleBox styleBox)
        {
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("A theme item name is required.", nameof(itemName));
            if (styleBox == null) _styleOverrides.Remove(itemName); else _styleOverrides[itemName] = styleBox;
        }
        public void RemoveThemeStyleOverride(string itemName)
        {
            if (itemName != null) _styleOverrides.Remove(itemName);
        }
        public StyleBox GetThemeStyleBox(string itemName)
        {
            if (itemName == null) return null;
            if (_styleOverrides.TryGetValue(itemName, out var local)) return local;
            var typeNames = GetThemeTypeNames();
            for (var control = this; control != null; control = control.Parent)
            {
                var style = control.ThemeOverride?.GetStyleBox(itemName, typeNames);
                if (style != null) return style;
            }
            return Context?.Theme.GetStyleBox(itemName, typeNames);
        }

        /// <summary>Adds a local decorative theme-icon override. Existing content-icon properties are unaffected.</summary>
        public void AddThemeIconOverride(string itemName, ThemeIcon icon)
        {
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("A theme item name is required.", nameof(itemName));
            _iconOverrides[itemName] = icon;
        }
        /// <summary>Removes a local icon override so theme inheritance becomes visible again.</summary>
        public void RemoveThemeIconOverride(string itemName)
        {
            if (itemName != null) _iconOverrides.Remove(itemName);
        }
        /// <summary>Suppresses a decorative theme icon on this control without affecting content icons.</summary>
        public void SuppressThemeIcon(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("A theme item name is required.", nameof(itemName));
            _iconOverrides[itemName] = null;
        }
        /// <summary>Resolves a decorative icon from local overrides, ancestor themes, and the context theme.</summary>
        public ThemeIcon? GetThemeIcon(string itemName) => GetThemeIcon(itemName, null);
        protected ThemeIcon? GetThemeIcon(string itemName, string preferredTypeName)
        {
            if (itemName == null) return null;
            if (_iconOverrides.TryGetValue(itemName, out var local)) return local;
            var typeNames = GetThemeTypeNames(preferredTypeName);
            for (var control = this; control != null; control = control.Parent)
                if (control.ThemeOverride != null && control.ThemeOverride.TryGetIcon(itemName, typeNames, out var inherited)) return inherited;
            if (Context?.Theme.TryGetIcon(itemName, typeNames, out var contextual) == true) return contextual;
            return Context?.TryGetDefaultThemeIcon(itemName, typeNames, out var fallback) == true ? fallback : null;
        }

        private IEnumerable<string> GetThemeTypeNames() => GetThemeTypeNames(null);
        private IEnumerable<string> GetThemeTypeNames(string preferredTypeName)
        {
            if (!string.IsNullOrEmpty(preferredTypeName)) yield return preferredTypeName;
            for (var type = GetType(); type != null && typeof(Control).IsAssignableFrom(type); type = type.BaseType)
            {
                if (type.Name == preferredTypeName) continue;
                yield return type.Name;
            }
        }

        private static Side Opposite(Side side) => (Side)(((int)side + 2) % 4);

        private float GetAnchorRaw(Side side)
        {
            switch (side)
            {
                case Side.Left: return AnchorLeft;
                case Side.Top: return AnchorTop;
                case Side.Right: return AnchorRight;
                default: return AnchorBottom;
            }
        }

        private void SetAnchorRaw(Side side, float value)
        {
            switch (side)
            {
                case Side.Left: AnchorLeft = value; break;
                case Side.Top: AnchorTop = value; break;
                case Side.Right: AnchorRight = value; break;
                case Side.Bottom: AnchorBottom = value; break;
            }
        }

        private float GetOffsetRaw(Side side)
        {
            switch (side)
            {
                case Side.Left: return OffsetLeft;
                case Side.Top: return OffsetTop;
                case Side.Right: return OffsetRight;
                default: return OffsetBottom;
            }
        }

        private void SetOffsetRaw(Side side, float value)
        {
            switch (side)
            {
                case Side.Left: OffsetLeft = value; break;
                case Side.Top: OffsetTop = value; break;
                case Side.Right: OffsetRight = value; break;
                case Side.Bottom: OffsetBottom = value; break;
            }
        }

        /// <summary>
        /// Matches Godot's Control::set_anchor exactly: by default (<paramref name="keepOffset"/> false) the offset is
        /// recomputed so the control's resolved position does not jump when only the anchor changes, an anchor is never
        /// allowed to cross its opposite anchor (clamped to it, or pushing the opposite anchor along when
        /// <paramref name="pushOppositeAnchor"/> is set), and geometry is refreshed via <see cref="QueueLayout"/>.
        /// </summary>
        public void SetAnchor(Side side, float anchor, bool keepOffset = false, bool pushOppositeAnchor = false)
        {
            anchor = MathHelper.Clamp(anchor, 0, 1);
            var parentRange = Parent != null ? ((side == Side.Left || side == Side.Right) ? Parent.Size.X : Parent.Size.Y) : 0f;
            var oppositeSide = Opposite(side);
            var previousPos = GetOffsetRaw(side) + GetAnchorRaw(side) * parentRange;
            var previousOppositePos = GetOffsetRaw(oppositeSide) + GetAnchorRaw(oppositeSide) * parentRange;

            SetAnchorRaw(side, anchor);

            var crossed = (side == Side.Left || side == Side.Top)
                ? GetAnchorRaw(side) > GetAnchorRaw(oppositeSide)
                : GetAnchorRaw(side) < GetAnchorRaw(oppositeSide);
            if (crossed)
            {
                if (pushOppositeAnchor) SetAnchorRaw(oppositeSide, GetAnchorRaw(side));
                else SetAnchorRaw(side, GetAnchorRaw(oppositeSide));
            }

            if (!keepOffset)
            {
                SetOffsetRaw(side, previousPos - GetAnchorRaw(side) * parentRange);
                if (pushOppositeAnchor) SetOffsetRaw(oppositeSide, previousOppositePos - GetAnchorRaw(oppositeSide) * parentRange);
            }

            QueueLayout();
        }

        public void SetOffset(Side side, float offset)
        {
            switch (side)
            {
                case Side.Left: OffsetLeft = offset; break;
                case Side.Top: OffsetTop = offset; break;
                case Side.Right: OffsetRight = offset; break;
                case Side.Bottom: OffsetBottom = offset; break;
            }
            QueueLayout();
        }

        public void SetAnchorsAndOffsets(float left, float top, float right, float bottom)
        {
            AnchorLeft = MathHelper.Clamp(left, 0, 1); AnchorTop = MathHelper.Clamp(top, 0, 1);
            AnchorRight = MathHelper.Clamp(right, 0, 1); AnchorBottom = MathHelper.Clamp(bottom, 0, 1);
            OffsetLeft = OffsetTop = 0; OffsetRight = OffsetBottom = 0;
            QueueLayout();
        }

        private bool _eventAccepted;
        /// <summary>
        /// Matches Godot's Control::accept_event(): marks the current input event as handled for just this one
        /// dispatch, stopping propagation to ancestors independent of <see cref="MouseFilter"/> (which only governs
        /// every future event). Unlike setting <see cref="MouseFilter"/> to Stop, this does not affect later events.
        /// </summary>
        protected void AcceptEvent() => _eventAccepted = true;
        /// <summary>Consumes and returns whether <see cref="AcceptEvent"/> was called since the last consume, for use by a single dispatch loop iteration.</summary>
        internal bool ConsumeEventAccepted()
        {
            if (!_eventAccepted) return false;
            _eventAccepted = false;
            return true;
        }

        public virtual Vector2 GetMinimumSize() => CustomMinimumSize;
        /// <summary>Returns this control's preferred size before minimum-size clamping, matching Godot's virtual get_desired_size().</summary>
        public virtual Vector2 GetDesiredSize() => Vector2.Zero;
        /// <summary>Returns this control's intrinsic maximum size; negative components are unbounded.</summary>
        public virtual Vector2 GetMaximumSize() => new Vector2(-1, -1);
        public Vector2 GetCombinedMaximumSize()
        {
            var maximum = GetMaximumSize();
            if (CustomMaximumSize.X >= 0) maximum.X = maximum.X >= 0 ? Math.Min(maximum.X, CustomMaximumSize.X) : CustomMaximumSize.X;
            if (CustomMaximumSize.Y >= 0) maximum.Y = maximum.Y >= 0 ? Math.Min(maximum.Y, CustomMaximumSize.Y) : CustomMaximumSize.Y;
            return maximum;
        }
        /// <summary>Returns the desired size clamped to this control's minimum and combined maximum sizes.</summary>
        public Vector2 GetBoundDesiredSize()
        {
            var desired = Vector2.Max(GetDesiredSize(), GetMinimumSize());
            var maximum = GetCombinedMaximumSize();
            if (maximum.X >= 0) desired.X = Math.Min(desired.X, maximum.X);
            if (maximum.Y >= 0) desired.Y = Math.Min(desired.Y, maximum.Y);
            return desired;
        }
        public virtual bool ContainsPoint(Point point) => Bounds.Contains(point);
        internal virtual bool HitTestBeforeChildren(Point point) => false;
        /// <summary>Returns the tooltip at a global pointer position. Override to provide dynamic help text.</summary>
        public virtual string GetTooltip(Point position) => TooltipText;
        public void GrabFocus() => Context?.SetFocus(this);
        public void ReleaseFocus() { if (Context?.FocusedControl == this) Context.SetFocus(null); }
        /// <summary>Marks this control and every ancestor dirty, matching Godot's Control::update_minimum_size walking the full parent chain so a deeply nested size change reaches the root container.</summary>
        public void QueueLayout()
        {
            for (var control = this; control != null; control = control.Parent)
                control._layoutDirty = true;
        }

        internal void MarkInheritedLayoutDirectionDirty()
        {
            _layoutDirty = true;
            foreach (var child in _children) child.MarkInheritedLayoutDirectionDirty();
        }

        internal void MarkThemeDirty()
        {
            _layoutDirty = true;
            foreach (var child in _children) child.MarkThemeDirty();
        }

        private void ThemeOverrideChanged(object sender, EventArgs args)
        {
            Context?.TextLayoutEngine.Clear();
            MarkThemeDirty();
            QueueLayout();
        }

        internal UIFont ResolveFont(UIFontSelection selection)
        {
            var themes = new List<Theme>();
            for (var control = this; control != null; control = control.Parent)
                if (control.ThemeOverride != null) themes.Add(control.ThemeOverride);
            var inherited = new List<Theme>();
            var effective = Context?.Theme;
            for (var index = themes.Count - 1; index >= 0; index--)
            {
                var theme = themes[index];
                if (theme.Parent == null && effective != null) { theme.SetInheritedParent(effective); inherited.Add(theme); }
                effective = theme;
            }
            try { return selection.Resolve(effective); }
            finally
            {
                for (var index = inherited.Count - 1; index >= 0; index--) inherited[index].SetInheritedParent(null);
            }
        }

        internal void SetContext(UIContext context)
        {
            var previous = Context;
            Context = context;
            if (previous != context)
            {
                OnContextChanged(previous, context);
                if (previous != null) Detached?.Invoke(this, EventArgs.Empty);
                XamlAttachment.ContextChanged(this, previous, context);
                if (context != null) Attached?.Invoke(this, EventArgs.Empty);
            }
            foreach (var child in _children) child.SetContext(context);
        }

        /// <summary>Called when this control enters, exits, or moves between retained UI contexts.</summary>
        protected virtual void OnContextChanged(UIContext previous, UIContext current) { }

        internal void SetTreeOrder(long order) => _treeOrder = order;
        internal long TreeOrder => _treeOrder;

        internal IReadOnlyList<Control> GetChildrenInDrawOrder()
        {
            if (!_childOrderDirty) return _childrenInDrawOrder;
            _childrenInDrawOrder.Clear();
            _childrenInDrawOrder.AddRange(_children);
            _childrenInDrawOrder.Sort((left, right) =>
            {
                var zOrder = left.ZIndex.CompareTo(right.ZIndex);
                return zOrder != 0 ? zOrder : left._treeOrder.CompareTo(right._treeOrder);
            });
            _childOrderDirty = false;
            return _childrenInDrawOrder;
        }

        internal void LayoutTree()
        {
            if (Parent != null && (AnchorLeft != 0 || AnchorTop != 0 || AnchorRight != 0 || AnchorBottom != 0 || OffsetLeft != 0 || OffsetTop != 0 || OffsetRight != 0 || OffsetBottom != 0))
            {
                var parentSize = Parent.Size;
                var newPosition = new Vector2(parentSize.X * AnchorLeft + OffsetLeft, parentSize.Y * AnchorTop + OffsetTop);
                var newSize = new Vector2(parentSize.X * AnchorRight + OffsetRight - newPosition.X, parentSize.Y * AnchorBottom + OffsetBottom - newPosition.Y);

                // Matches Godot's Control::_size_changed(): clamp each axis up to the combined minimum size,
                // compensating position per GrowDirection, then mirror horizontally under RTL — in that exact order.
                var minimumSize = GetMinimumSize();
                if (minimumSize.X > newSize.X)
                {
                    if (HGrowDirection == GrowDirection.Begin) newPosition.X += newSize.X - minimumSize.X;
                    else if (HGrowDirection == GrowDirection.Both) newPosition.X += 0.5f * (newSize.X - minimumSize.X);
                    newSize.X = minimumSize.X;
                }
                if (IsLayoutRtl()) newPosition.X = parentSize.X - newPosition.X - newSize.X;
                if (minimumSize.Y > newSize.Y)
                {
                    if (VGrowDirection == GrowDirection.Begin) newPosition.Y += newSize.Y - minimumSize.Y;
                    else if (VGrowDirection == GrowDirection.Both) newPosition.Y += 0.5f * (newSize.Y - minimumSize.Y);
                    newSize.Y = minimumSize.Y;
                }

                // Matches Godot's Container::_notification(NOTIFICATION_RESIZED) => queue_sort(): a control whose own
                // resolved size just changed (even purely from a parent resize, bypassing the Size setter's QueueLayout)
                // must re-arrange its own children this same pass, or nested containers go stale after any ancestor resize.
                if (newSize != _size) _layoutDirty = true;
                _position = newPosition;
                _size = newSize;
            }
            if (_layoutDirty)
            {
                ArrangeChildren();
                _layoutDirty = false;
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
            foreach (var child in _children) child.LayoutTree();
        }

        protected virtual void ArrangeChildren() { }
        internal virtual void Process(GameTime gameTime) { foreach (var child in _children) if (child.Visible) child.Process(gameTime); }
        internal void DrawTree(UIRenderContext context)
        {
            context.PushTheme(ThemeOverride);
            try { Draw(context); }
            finally { context.PopTheme(); }
        }
        internal virtual void Draw(UIRenderContext context)
        {
            if (ClipContents) context.PushClip(Bounds);
            try { foreach (var child in GetChildrenInDrawOrder()) if (child.Visible) child.DrawTree(context); }
            finally { if (ClipContents) context.PopClip(); }
        }
        internal virtual void PointerEntered() => MouseEntered?.Invoke(this, EventArgs.Empty);
        internal virtual void PointerExited() => MouseExited?.Invoke(this, EventArgs.Empty);
        internal virtual void FocusGained() => FocusEntered?.Invoke(this, EventArgs.Empty);
        internal virtual void FocusLost() => FocusExited?.Invoke(this, EventArgs.Empty);
        internal virtual void PointerPressed(Point position) { if (FocusMode != FocusMode.None) GrabFocus(); }
        /// <summary>Receives a physical pointer press. The primary button additionally routes through <see cref="PointerPressed"/> for compatibility with existing controls.</summary>
        internal virtual void PointerButtonPressed(Point position, PointerButton button) { if (button == PointerButton.Right) PointerRightPressed(position); }
        /// <summary>Receives a secondary/right pointer press. Controls that present a context menu can override this independently of primary activation.</summary>
        internal virtual void PointerRightPressed(Point position) { }
        /// <summary>Receives a physical pointer release at the current hit-tested position.</summary>
        internal virtual void PointerButtonReleased(Point position, PointerButton button) { }
        internal virtual void PointerMoved(Point position) { }
        internal virtual void PointerReleased(Point position, bool isInside) { }
        internal virtual bool PointerWheel(int delta) => false;
        internal virtual bool ShortcutInput(Keys key, KeyboardState keyboard) => false;
        internal virtual void KeyPressed(Keys key) { }
        /// <summary>Notifies the focused control that a previously-pressed key was released. Purely additive over <see cref="KeyPressed"/>; most controls have no need to override it.</summary>
        internal virtual void KeyReleased(Keys key) { }
        internal virtual void TextInput(char character) { }
        internal virtual void TextComposition(string text, int selectionStart, int selectionLength) { }
        /// <summary>Returns data to drag, or <see langword="null"/> to decline starting a drag.</summary>
        public virtual object GetDragData(Point position) => null;
        /// <summary>Returns whether this control accepts the supplied data at the screen position.</summary>
        public virtual bool CanDropData(Point position, object data) => false;
        /// <summary>Receives data accepted by <see cref="CanDropData"/>.</summary>
        public virtual void DropData(Point position, object data) { }
        internal void NotifyDragStarted(object data) => DragStarted?.Invoke(this, data);
        internal void NotifyDragEnded(bool succeeded) => DragEnded?.Invoke(this, succeeded);
    }
}
