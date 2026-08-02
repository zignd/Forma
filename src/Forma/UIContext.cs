// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// Tooltip ancestor traversal is adapted from Godot Engine's scene/main/viewport.cpp;
// see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Forma
{
    /// <summary>
    /// Owns a UI tree's input state, focus and rendering. Add it to a Game's update/draw loop, or use
    /// <see cref="UIComponent"/> for the usual game component integration.
    /// </summary>
    public sealed class UIContext : IDisposable
    {
        private readonly List<Control> _roots = new List<Control>();
        private readonly List<Control> _rootsInDrawOrder = new List<Control>();
        private readonly UIFontSelection _tooltipFontSelection = new UIFontSelection();
        private MouseState _previousMouse;
        private KeyboardState _previousKeyboard;
        private Control _hovered;
        private Control _captured;
        private Control _dragSource;
        private object _dragData;
        private Point _dragStartPosition;
        private Control _tooltipOwner;
        private string _tooltipText = string.Empty;
        private TimeSpan _tooltipElapsed;
        private Point _tooltipPointerPosition;
        private UIRenderContext _renderer;
        private DefaultThemeIconResources _iconResources;
        private long _nextRootOrder;
        private bool _rootOrderDirty = true;
        private CultureInfo _applicationCulture = CultureInfo.CurrentUICulture;
        private CultureInfo _systemCulture = CultureInfo.CurrentCulture;
        private LayoutDirection _rootLayoutDirection = LayoutDirection.ApplicationLocale;
        private float _displayScale = 1f;
        private Theme _theme;
        /// <summary>Most recently dispatched pointer position, available to retained controls that coordinate transient surfaces.</summary>
        public Point PointerPosition { get; private set; }
        /// <summary>Keyboard state for the input frame currently being dispatched, including modifier keys used by controls such as Tree.</summary>
        public KeyboardState CurrentKeyboardState { get; private set; }
        /// <summary>Game time for the input frame currently being dispatched, used by retained multi-click gestures.</summary>
        public TimeSpan CurrentTime { get; private set; }

        public UIContext()
        {
            Theme = new Theme { FontFamily = UIFontDefaultRegistry.FontFamily };
        }
        public IReadOnlyList<Control> Roots => _roots;
        public Theme Theme
        {
            get => _theme;
            set
            {
                value ??= new Theme();
                if (ReferenceEquals(_theme, value)) return;
                if (_theme != null) _theme.Changed -= ThemeChanged;
                _theme = value;
                _theme.Changed += ThemeChanged;
                ThemeChanged(_theme, EventArgs.Empty);
            }
        }
        public TextLayoutEngine TextLayoutEngine { get; } = new TextLayoutEngine();
        public Control FocusedControl { get; private set; }
        /// <summary>Whether retained touch-style interactions should be enabled for pointer input.</summary>
        public bool TouchscreenAvailable { get; set; }
        /// <summary>Whether a retained drag-and-drop operation is currently active.</summary>
        public bool IsDragging => _dragSource != null;
        /// <summary>Payload supplied by the active retained drag source, or null when not dragging.</summary>
        public object DragData => _dragData;
        public Vector2 ViewportSize { get; set; }
        /// <summary>Physical display pixels per logical UI coordinate. Input is mapped back to logical coordinates and drawing is scaled to physical pixels.</summary>
        public float DisplayScale
        {
            get => _displayScale;
            set
            {
                if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (_displayScale == value) return;
                _displayScale = value;
                ClearDynamicGlyphCache();
            }
        }
        /// <summary>Optionally resolves a denser font atlas for the supplied logical font and display scale.</summary>
        public Func<SpriteFont, float, SpriteFont> DisplayFontResolver { get; set; }
        /// <summary>Read-only diagnostics for this context's device-scoped dynamic glyph cache.</summary>
        public DynamicGlyphCacheDiagnostics DynamicGlyphDiagnostics => _renderer?.DynamicGlyphDiagnostics ?? default;
        /// <summary>Returns immutable grayscale snapshots of currently allocated glyph-atlas pages.</summary>
        public IReadOnlyList<DynamicGlyphAtlasPageSnapshot> GetDynamicGlyphAtlasPages() => _renderer?.DynamicGlyphPages ?? Array.Empty<DynamicGlyphAtlasPageSnapshot>();
        /// <summary>Clears device-scoped dynamic glyph pages between draw calls. Cumulative counters remain available.</summary>
        public void ClearDynamicGlyphCache() => _renderer?.ClearDynamicGlyphCache();
        /// <summary>Application locale used by controls with <see cref="LayoutDirection.ApplicationLocale"/>.</summary>
        public CultureInfo ApplicationCulture
        {
            get => _applicationCulture;
            set { _applicationCulture = value ?? throw new ArgumentNullException(nameof(value)); MarkLayoutDirectionsDirty(); }
        }
        /// <summary>System locale used by controls with <see cref="LayoutDirection.SystemLocale"/>.</summary>
        public CultureInfo SystemCulture
        {
            get => _systemCulture;
            set { _systemCulture = value ?? throw new ArgumentNullException(nameof(value)); MarkLayoutDirectionsDirty(); }
        }
        /// <summary>Fallback layout direction for root controls whose direction is inherited.</summary>
        public LayoutDirection RootLayoutDirection
        {
            get => _rootLayoutDirection;
            set { _rootLayoutDirection = value; MarkLayoutDirectionsDirty(); }
        }
        /// <summary>Delay before hover help becomes visible. Defaults to Godot-like delayed presentation.</summary>
        public TimeSpan TooltipDelay { get; set; } = TimeSpan.FromMilliseconds(700);
        /// <summary>Font used when drawing tooltip text. Assign the same font used by the application UI.</summary>
        public SpriteFont TooltipFont { get => _tooltipFontSelection.SpriteFont; set => _tooltipFontSelection.SetSpriteFont(value); }
        public UIFont TooltipUIFont { get => _tooltipFontSelection.UIFont; set => _tooltipFontSelection.SetUIFont(value); }
        internal UIFont EffectiveTooltipUIFont => _tooltipFontSelection.Resolve(Theme);
        /// <summary>Current default icon-atlas resource and lookup counters.</summary>
        public ThemeIconDiagnostics ThemeIconDiagnostics => _iconResources?.Diagnostics ?? default;
        public Thickness TooltipPadding { get; set; } = new Thickness(7, 4, 7, 4);
        public Vector2 TooltipOffset { get; set; } = new Vector2(14, 18);
        public bool IsTooltipVisible { get; private set; }
        public Control TooltipOwner => _tooltipOwner;
        public string TooltipText => _tooltipText;
        internal event Action<Control, Point> RetainedPointerPressed;
        internal event Action<Control, Point, Vector2> RetainedPointerMoved;
        internal event Action<Control, Point> RetainedPointerReleased;
        private void ThemeChanged(object sender, EventArgs args)
        {
            TextLayoutEngine.Clear();
            foreach (var root in _roots) root.MarkThemeDirty();
        }

        public void Add(Control control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            control.RemoveFromParent();
            if (_roots.Contains(control)) return;
            _roots.Add(control);
            control.SetTreeOrder(++_nextRootOrder);
            _rootOrderDirty = true;
            control.SetContext(this);
        }

        public bool Remove(Control control)
        {
            if (control == null || !_roots.Remove(control)) return false;
            if (FocusedControl == control) SetFocus(null);
            _rootOrderDirty = true;
            control.SetContext(null);
            return true;
        }

        public void Update(GameTime gameTime) => Update(gameTime, Mouse.GetState(), Keyboard.GetState());

        /// <summary>Updates the tree from supplied states; this overload makes UI input deterministic in tests.</summary>
        public void Update(GameTime gameTime, MouseState mouse, KeyboardState keyboard)
        {
            if (Math.Abs(DisplayScale - 1f) > .0001f)
            {
                mouse = new MouseState(
                    (int)MathF.Round(mouse.X / DisplayScale),
                    (int)MathF.Round(mouse.Y / DisplayScale),
                    mouse.ScrollWheelValue,
                    mouse.LeftButton,
                    mouse.MiddleButton,
                    mouse.RightButton,
                    mouse.XButton1,
                    mouse.XButton2);
            }
            Layout();
            CurrentKeyboardState = keyboard;
            CurrentTime = gameTime?.TotalGameTime ?? TimeSpan.Zero;
            var point = mouse.GetPosition();
            PointerPosition = point;
            var modalPopup = GetActiveModalPopup();
            var target = modalPopup == null ? HitTest(point) : HitTest(modalPopup, point);
            if (target != _hovered)
            {
                _hovered?.PointerExited();
                _hovered = target;
                _hovered?.PointerEntered();
            }
            UpdateTooltip(target, point, gameTime.ElapsedGameTime);

            var pressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
            var released = mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
            if (pressed && modalPopup != null && target == null)
            {
                // Dismissal deliberately consumes the press, preventing click-through to an underlying control.
                modalPopup.OutsidePointerPressed(point);
            }
            else if (pressed && target != null)
            {
                _captured = target;
                _dragStartPosition = point;
                DispatchPointerPressed(target, point);
                RetainedPointerPressed?.Invoke(target, point);
            }
            DispatchPointerButtonTransitions(target, point, mouse, _previousMouse);
            if (mouse.GetPosition() != _previousMouse.GetPosition())
            {
                var motionTarget = _captured ?? target;
                if (motionTarget != null)
                {
                    DispatchPointerMoved(motionTarget, point);
                    RetainedPointerMoved?.Invoke(motionTarget, point, new Vector2(point.X - _previousMouse.X, point.Y - _previousMouse.Y));
                }
                if (_captured != null && mouse.LeftButton == ButtonState.Pressed) UpdateDrag(point);
            }
            if (released && _captured != null)
            {
                var capture = _captured;
                _captured = null;
                var dropped = false;
                if (_dragSource != null)
                {
                    var dropTarget = GetDropTarget(point);
                    if (dropTarget != null)
                    {
                        dropTarget.DropData(point, _dragData);
                        dropped = true;
                    }
                    _dragSource.NotifyDragEnded(dropped);
                    _dragSource = null;
                    _dragData = null;
                }
                DispatchPointerReleased(capture, point);
                RetainedPointerReleased?.Invoke(capture, point);
            }
            var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
            if (wheelDelta != 0)
                for (var control = target; control != null; control = control.Parent)
                    if (control.PointerWheel(wheelDelta)) break;

            foreach (var key in keyboard.GetPressedKeys())
                if (!_previousKeyboard.IsKeyDown(key))
                    DispatchKey(key, keyboard);
            foreach (var key in _previousKeyboard.GetPressedKeys())
                if (!keyboard.IsKeyDown(key))
                    FocusedControl?.KeyReleased(key);

            // Input handlers may add transient roots (for example a MenuButton opening its PopupMenu).
            // Process a stable snapshot so the current frame remains valid; the new root participates next frame.
            foreach (var root in new List<Control>(_roots)) if (root.Visible) root.Process(gameTime);
            _previousMouse = mouse;
            _previousKeyboard = keyboard;
        }

        public void Layout()
        {
            foreach (var root in _roots)
            {
                if (root.Size == Vector2.Zero && ViewportSize != Vector2.Zero) root.Size = ViewportSize;
                root.LayoutTree();
            }
        }

        public void Draw(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
            if (_iconResources == null || !ReferenceEquals(_iconResources.GraphicsDevice, graphicsDevice))
            {
                _iconResources?.Dispose();
                _iconResources = new DefaultThemeIconResources(graphicsDevice);
            }
            _iconResources.Ensure(DisplayScale);
            Layout();
            if (_renderer == null || _renderer.GraphicsDevice != graphicsDevice)
            {
                _renderer?.Dispose();
                _renderer = new UIRenderContext(graphicsDevice, Theme);
            }
            _renderer.Theme = Theme;
            _renderer.DisplayScale = DisplayScale;
            _renderer.DisplayFontResolver = DisplayFontResolver;
            _renderer.TextLayoutEngine = TextLayoutEngine;
            _renderer.Begin();
            try
            {
                foreach (var root in GetRootsInDrawOrder()) if (root.Visible) root.DrawTree(_renderer);
                DrawTooltip(_renderer);
            }
            finally { _renderer.End(); }
        }

        public void SetFocus(Control control)
        {
            var modalPopup = GetActiveModalPopup();
            if (control != null && modalPopup != null && !modalPopup.IsAncestorOf(control)) return;
            if (control != null && (control.Context != this || !control.Visible || !control.Enabled || control.FocusMode == FocusMode.None)) return;
            if (FocusedControl == control) return;
            FocusedControl?.FocusLost();
            FocusedControl = control;
            FocusedControl?.FocusGained();
        }

        public Control HitTest(Point point)
        {
            var roots = GetRootsInDrawOrder();
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                var hit = HitTest(roots[i], point);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Control HitTest(Control control, Point point)
        {
            if (!control.Visible || !control.Enabled) return null;
            if (control.ClipContents && !control.ContainsPoint(point)) return null;
            if (control.MouseFilter != MouseFilter.Ignore && control.ContainsPoint(point) && control.HitTestBeforeChildren(point)) return control;
            var children = control.GetChildrenInDrawOrder();
            for (var i = children.Count - 1; i >= 0; i--)
            {
                var hit = HitTest(children[i], point);
                if (hit != null) return hit;
            }
            return control.MouseFilter != MouseFilter.Ignore && control.ContainsPoint(point) ? control : null;
        }

        private static void DispatchPointerPressed(Control target, Point point)
        {
            for (var control = target; control != null; control = control.Parent)
            {
                control.PointerPressed(point);
                if (control.ConsumeEventAccepted() || control.MouseFilter != MouseFilter.Pass) break;
            }
        }

        private static void DispatchPointerMoved(Control target, Point point)
        {
            for (var control = target; control != null; control = control.Parent)
            {
                control.PointerMoved(point);
                if (control.ConsumeEventAccepted() || control.MouseFilter != MouseFilter.Pass) break;
            }
        }

        private static void DispatchPointerButtonTransitions(Control target, Point point, MouseState mouse, MouseState previousMouse)
        {
            DispatchPointerButtonTransition(target, point, PointerButton.Left, mouse.LeftButton, previousMouse.LeftButton);
            DispatchPointerButtonTransition(target, point, PointerButton.Right, mouse.RightButton, previousMouse.RightButton);
            DispatchPointerButtonTransition(target, point, PointerButton.Middle, mouse.MiddleButton, previousMouse.MiddleButton);
            DispatchPointerButtonTransition(target, point, PointerButton.XButton1, mouse.XButton1, previousMouse.XButton1);
            DispatchPointerButtonTransition(target, point, PointerButton.XButton2, mouse.XButton2, previousMouse.XButton2);
        }

        private static void DispatchPointerButtonTransition(Control target, Point point, PointerButton button, ButtonState current, ButtonState previous)
        {
            if (target == null || current == previous) return;
            for (var control = target; control != null; control = control.Parent)
            {
                if (current == ButtonState.Pressed) control.PointerButtonPressed(point, button);
                else control.PointerButtonReleased(point, button);
                if (control.ConsumeEventAccepted() || control.MouseFilter != MouseFilter.Pass) break;
            }
        }

        private static void DispatchPointerReleased(Control target, Point point)
        {
            for (var control = target; control != null; control = control.Parent)
            {
                control.PointerReleased(point, control.ContainsPoint(point));
                if (control.ConsumeEventAccepted() || control.MouseFilter != MouseFilter.Pass) break;
            }
        }

        private void DispatchKey(Keys key, KeyboardState keyboard)
        {
            var modalPopup = GetActiveModalPopup();
            if (modalPopup != null && (FocusedControl == null || !modalPopup.IsAncestorOf(FocusedControl))) SetFocus(modalPopup);
            if (DispatchShortcutInput(key, keyboard, modalPopup)) return;
            if (key == Keys.Tab)
            {
                var backwards = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
                var explicitTarget = backwards ? FocusedControl?.FocusPrevious : FocusedControl?.FocusNext;
                if (CanFocus(explicitTarget))
                {
                    SetFocus(explicitTarget);
                    return;
                }
                var focusable = new List<Control>();
                if (modalPopup != null) CollectFocusable(modalPopup, focusable);
                else foreach (var root in _roots) CollectFocusable(root, focusable);
                if (focusable.Count > 0)
                {
                    var current = focusable.IndexOf(FocusedControl);
                    SetFocus(focusable[(current + (backwards ? focusable.Count - 1 : 1)) % focusable.Count]);
                }
                return;
            }
            var neighbor = key == Keys.Left ? FocusedControl?.FocusNeighborLeft :
                key == Keys.Up ? FocusedControl?.FocusNeighborTop :
                key == Keys.Right ? FocusedControl?.FocusNeighborRight :
                key == Keys.Down ? FocusedControl?.FocusNeighborBottom : null;
            if (CanFocus(neighbor))
            {
                SetFocus(neighbor);
                return;
            }
            FocusedControl?.KeyPressed(key);
        }

        private bool DispatchShortcutInput(Keys key, KeyboardState keyboard, Control modalRoot)
        {
            if (modalRoot != null) return DispatchShortcutInput(modalRoot, key, keyboard);
            var roots = GetRootsInDrawOrder();
            for (var i = roots.Count - 1; i >= 0; i--)
                if (DispatchShortcutInput(roots[i], key, keyboard)) return true;
            return false;
        }

        private static bool DispatchShortcutInput(Control control, Keys key, KeyboardState keyboard)
        {
            if (control == null || !control.Visible || !control.Enabled) return false;
            var children = control.GetChildrenInDrawOrder();
            for (var i = children.Count - 1; i >= 0; i--)
                if (DispatchShortcutInput(children[i], key, keyboard)) return true;
            return control.ShortcutInput(key, keyboard);
        }

        private bool CanFocus(Control control) => control != null && control.Context == this && control.Visible && control.Enabled && control.FocusMode != FocusMode.None;

        /// <summary>Forwards one platform text-input character to the focused retained control.</summary>
        public void TextInput(char character)
        {
            if (!char.IsControl(character)) FocusedControl?.TextInput(character);
        }

        /// <summary>Forwards platform IME preedit text and its selected range to the focused control.</summary>
        public void TextComposition(string text, int selectionStart = 0, int selectionLength = 0) =>
            FocusedControl?.TextComposition(text ?? string.Empty, selectionStart, selectionLength);

        private void UpdateDrag(Point point)
        {
            if (_dragSource == null)
            {
                var deltaX = point.X - _dragStartPosition.X;
                var deltaY = point.Y - _dragStartPosition.Y;
                if (deltaX * deltaX + deltaY * deltaY < 16) return;
                var data = _captured.GetDragData(_dragStartPosition);
                if (data == null) return;
                _dragSource = _captured;
                _dragData = data;
                _dragSource.NotifyDragStarted(data);
            }
        }

        private Control GetDropTarget(Point point)
        {
            for (var control = HitTest(point); control != null; control = control.Parent)
                if (control.CanDropData(point, _dragData)) return control;
            return null;
        }

        private Popup GetActiveModalPopup()
        {
            var roots = GetRootsInDrawOrder();
            for (var index = roots.Count - 1; index >= 0; index--)
            {
                var popup = GetTopmostModalPopup(roots[index]);
                if (popup != null) return popup;
            }
            return null;
        }

        private static Popup GetTopmostModalPopup(Control control)
        {
            if (!control.Visible) return null;
            var children = control.GetChildrenInDrawOrder();
            for (var index = children.Count - 1; index >= 0; index--)
            {
                var popup = GetTopmostModalPopup(children[index]);
                if (popup != null) return popup;
            }
            return control is Popup popupControl && popupControl.Modal ? popupControl : null;
        }

        private void UpdateTooltip(Control target, Point position, TimeSpan elapsed)
        {
            var owner = GetTooltipOwner(target, position, out var text);
            _tooltipPointerPosition = position;
            if (owner != _tooltipOwner || !string.Equals(text, _tooltipText, StringComparison.Ordinal))
            {
                _tooltipOwner = owner;
                _tooltipText = text ?? string.Empty;
                _tooltipElapsed = TimeSpan.Zero;
                IsTooltipVisible = false;
            }
            if (_tooltipOwner == null || string.IsNullOrEmpty(_tooltipText)) return;
            _tooltipElapsed += elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
            if (_tooltipElapsed >= TooltipDelay) IsTooltipVisible = true;
        }

        /// <summary>
        /// Matches Godot's Viewport::_gui_get_tooltip: bubbles up to ancestors when a control's own tooltip is empty,
        /// but only while ancestors don't stop propagation — a control with MouseFilter.Stop is where the search ends.
        /// </summary>
        private static Control GetTooltipOwner(Control target, Point position, out string text)
        {
            for (var control = target; control != null; control = control.Parent)
            {
                text = control.GetTooltip(position);
                if (!string.IsNullOrEmpty(text)) return control;
                if (control.MouseFilter == MouseFilter.Stop) break;
            }
            text = string.Empty;
            return null;
        }

        private void DrawTooltip(UIRenderContext context)
        {
            var font = EffectiveTooltipUIFont;
            if (!IsTooltipVisible || string.IsNullOrEmpty(_tooltipText) || font == null) return;
            var textSize = TextMetrics.Measure(font, _tooltipText);
            var width = (int)MathF.Ceiling(textSize.X + TooltipPadding.Horizontal);
            var height = (int)MathF.Ceiling(Math.Max(TextMetrics.LineHeight(font), textSize.Y) + TooltipPadding.Vertical);
            var position = _tooltipPointerPosition.ToVector2() + TooltipOffset;
            var viewport = context.GraphicsDevice.Viewport.Bounds;
            position.X = MathHelper.Clamp(position.X, viewport.Left, Math.Max(viewport.Left, viewport.Right - width));
            position.Y = MathHelper.Clamp(position.Y, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - height));
            var bounds = new Rectangle((int)position.X, (int)position.Y, width, height);
            var style = Theme.GetStyleBox("panel", "TooltipPanel");
            if (style != null) style.Draw(context, bounds);
            else { context.FillRounded(bounds, Theme.PanelColor, 3); context.Border(bounds, Theme.PanelBorderColor); }
            context.Text(font, _tooltipText, position + new Vector2(TooltipPadding.Left, TooltipPadding.Top), Theme.TextColor);
        }

        private static void CollectFocusable(Control control, List<Control> result)
        {
            if (control.Visible && control.Enabled && control.FocusMode == FocusMode.All) result.Add(control);
            foreach (var child in control.Children) CollectFocusable(child, result);
        }

        internal void MarkRootOrderDirty() => _rootOrderDirty = true;
        internal bool TryGetDefaultThemeIcon(string itemName, IEnumerable<string> typeNames, out ThemeIcon? icon)
        {
            if (_iconResources != null && _iconResources.Theme.TryGetIcon(itemName, typeNames, out icon)) return true;
            _iconResources?.RecordMissing(itemName);
            icon = null;
            return false;
        }
        internal bool ResolveLayoutDirection(LayoutDirection direction)
        {
            if (direction == LayoutDirection.RightToLeft) return true;
            if (direction == LayoutDirection.LeftToRight) return false;
            if (direction == LayoutDirection.SystemLocale) return SystemCulture.TextInfo.IsRightToLeft;
            if (direction == LayoutDirection.ApplicationLocale) return ApplicationCulture.TextInfo.IsRightToLeft;
            return RootLayoutDirection == LayoutDirection.Inherited
                ? ApplicationCulture.TextInfo.IsRightToLeft
                : ResolveLayoutDirection(RootLayoutDirection);
        }
        private void MarkLayoutDirectionsDirty()
        {
            foreach (var root in _roots) root.MarkInheritedLayoutDirectionDirty();
        }

        private IReadOnlyList<Control> GetRootsInDrawOrder()
        {
            if (!_rootOrderDirty) return _rootsInDrawOrder;
            _rootsInDrawOrder.Clear();
            _rootsInDrawOrder.AddRange(_roots);
            _rootsInDrawOrder.Sort((left, right) =>
            {
                var zOrder = left.ZIndex.CompareTo(right.ZIndex);
                return zOrder != 0 ? zOrder : left.TreeOrder.CompareTo(right.TreeOrder);
            });
            _rootOrderDirty = false;
            return _rootsInDrawOrder;
        }

        public void Dispose()
        {
            _renderer?.Dispose();
            _renderer = null;
            _iconResources?.Dispose();
            _iconResources = null;
            TextLayoutEngine.Clear();
        }
    }

    /// <summary>Connects a <see cref="UIContext"/> to an XNA-compatible <see cref="Game"/>.</summary>
    public sealed class UIComponent : DrawableGameComponent
    {
        private readonly RuntimeTextInputAdapter _textInput;

        public UIComponent(Game game, UIContext context = null) : base(game)
        {
            Context = context ?? new UIContext();
            _textInput = new RuntimeTextInputAdapter(game, Context.TextInput);
        }
        public UIContext Context { get; }
        public override void Update(GameTime gameTime)
        {
            Context.ViewportSize = new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            Context.Update(gameTime);
        }
        public override void Draw(GameTime gameTime) => Context.Draw(GraphicsDevice);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _textInput.Dispose(); Context.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
