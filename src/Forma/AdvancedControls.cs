// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// Advanced control APIs and behavior are adapted from Godot Engine's SyntaxHighlighter,
// CodeHighlighter, LineEdit, TextEdit, SpinBox, OptionButton, TabContainer,
// ScrollContainer, Popup, and PopupPanel implementations; see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Forma
{
    [Flags]
    public enum TextSearchFlags { None = 0, MatchCase = 1, WholeWords = 2, Backwards = 4 }
    public enum TextEditGutterType { String, Icon, Custom }
    /// <summary>Godot-compatible source-line wrapping policy for <see cref="TextEdit"/>.</summary>
    public enum TextEditLineWrappingMode { None, Boundary }
    /// <summary>Godot-compatible grouping categories for retained TextEdit history operations.</summary>
    public enum TextEditEditAction { None, Typing, Backspace, Delete }
    /// <summary>Core retained TextEdit context-menu commands, corresponding to Godot's TextEdit menu IDs.</summary>
    public enum TextEditMenuOption
    {
        Cut, Copy, Paste, Clear, SelectAll, Undo, Redo,
        SubmenuTextDirection, DirectionInherited, DirectionAuto, DirectionLeftToRight, DirectionRightToLeft,
        DisplayControlCharacters, SubmenuInsertControlCharacter,
        InsertLeftToRightMark, InsertRightToLeftMark, InsertLeftToRightEmbedding, InsertRightToLeftEmbedding,
        InsertLeftToRightOverride, InsertRightToLeftOverride, InsertPopDirectionFormatting, InsertArabicLetterMark,
        InsertLeftToRightIsolate, InsertRightToLeftIsolate, InsertFirstStrongIsolate, InsertPopDirectionIsolate,
        InsertZeroWidthJoiner, InsertZeroWidthNonJoiner, InsertWordJoiner, InsertSoftHyphen
    }
    /// <summary>Core retained LineEdit context-menu commands, corresponding to Godot's LineEdit menu IDs.</summary>
    public enum LineEditMenuOption
    {
        Cut, Copy, Paste, Clear, SelectAll, Undo, Redo,
        SubmenuTextDirection, DirectionInherited, DirectionAuto, DirectionLeftToRight, DirectionRightToLeft,
        DisplayControlCharacters, SubmenuInsertControlCharacter,
        InsertLeftToRightMark, InsertRightToLeftMark, InsertLeftToRightEmbedding, InsertRightToLeftEmbedding,
        InsertLeftToRightOverride, InsertRightToLeftOverride, InsertPopDirectionFormatting, InsertArabicLetterMark,
        InsertLeftToRightIsolate, InsertRightToLeftIsolate, InsertFirstStrongIsolate, InsertPopDirectionIsolate,
        InsertZeroWidthJoiner, InsertZeroWidthNonJoiner, InsertWordJoiner, InsertSoftHyphen
    }
    public enum StructuredTextParser { Default, Uri, File, Email, List, None, Custom }

    /// <summary>Snapshot of one TextEdit caret, using Godot's source-line and source-column coordinates.</summary>
    public readonly struct TextEditCaret
    {
        public TextEditCaret(int line, int column, int selectionOriginLine, int selectionOriginColumn)
        {
            Line = line; Column = column; SelectionOriginLine = selectionOriginLine; SelectionOriginColumn = selectionOriginColumn;
        }
        public int Line { get; }
        public int Column { get; }
        public int SelectionOriginLine { get; }
        public int SelectionOriginColumn { get; }
    }

    /// <summary>A colored source range returned by a <see cref="SyntaxHighlighter"/> for one document line.</summary>
    public readonly struct SyntaxHighlightSpan
    {
        public SyntaxHighlightSpan(int startColumn, int length, Color color)
        {
            StartColumn = Math.Max(0, startColumn);
            Length = Math.Max(0, length);
            Color = color;
        }
        public int StartColumn { get; }
        public int Length { get; }
        public Color Color { get; }
    }

    /// <summary>One configurable <see cref="CodeHighlighter"/> color region, analogous to Godot's color_regions dictionary entry.</summary>
    public readonly struct CodeHighlightColorRegion
    {
        public CodeHighlightColorRegion(string startKey, string endKey, Color color, bool lineOnly = false)
        {
            StartKey = startKey ?? string.Empty;
            EndKey = endKey ?? string.Empty;
            Color = color;
            LineOnly = lineOnly || string.IsNullOrEmpty(EndKey);
        }
        public string StartKey { get; }
        public string EndKey { get; }
        public Color Color { get; }
        public bool LineOnly { get; }
    }

    /// <summary>
    /// Document-bound syntax-color provider analogous to Godot's SyntaxHighlighter resource.
    /// Override <see cref="GetLineSyntaxHighlightingCore"/> to return colored source ranges for a line.
    /// </summary>
    public abstract class SyntaxHighlighter
    {
        private readonly Dictionary<int, IReadOnlyList<SyntaxHighlightSpan>> _highlightingCache = new Dictionary<int, IReadOnlyList<SyntaxHighlightSpan>>();
        private TextEdit _textEdit;
        /// <summary>Raised when cached highlighting was invalidated and the owning editor should redraw.</summary>
        public event EventHandler Changed;
        public TextEdit TextEdit => _textEdit;
        public IReadOnlyList<SyntaxHighlightSpan> GetLineSyntaxHighlighting(int line)
        {
            if (_textEdit == null || line < 0 || line >= _textEdit.LineCount) return Array.Empty<SyntaxHighlightSpan>();
            if (_highlightingCache.TryGetValue(line, out var cached)) return cached;
            var highlighting = GetLineSyntaxHighlightingCore(line) ?? Array.Empty<SyntaxHighlightSpan>();
            _highlightingCache[line] = highlighting;
            return highlighting;
        }
        public void ClearHighlightingCache()
        {
            _highlightingCache.Clear();
            OnClearHighlightingCache();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        public void UpdateCache()
        {
            _highlightingCache.Clear();
            OnClearHighlightingCache();
            OnUpdateCache();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        internal void InvalidateFromLine(int line)
        {
            foreach (var cachedLine in new List<int>(_highlightingCache.Keys))
                if (cachedLine >= line) _highlightingCache.Remove(cachedLine);
            OnInvalidateFromLine(line);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        protected abstract IReadOnlyList<SyntaxHighlightSpan> GetLineSyntaxHighlightingCore(int line);
        protected virtual void OnClearHighlightingCache() { }
        protected virtual void OnUpdateCache() { }
        protected virtual void OnInvalidateFromLine(int line) { }
        internal void SetTextEdit(TextEdit textEdit)
        {
            if (_textEdit == textEdit) return;
            _textEdit = textEdit;
            UpdateCache();
        }
    }

    /// <summary>
    /// Basic configurable code highlighter corresponding to Godot's CodeHighlighter.
    /// It supports word/member colors and one-line or paired multiline color regions.
    /// </summary>
    public sealed class CodeHighlighter : SyntaxHighlighter
    {
        private sealed class ColorRegion
        {
            public string StartKey;
            public string EndKey;
            public Color Color;
            public bool LineOnly;
        }
        private readonly List<ColorRegion> _colorRegions = new List<ColorRegion>();
        private readonly Dictionary<string, Color> _keywordColors = new Dictionary<string, Color>(StringComparer.Ordinal);
        private readonly Dictionary<string, Color> _memberKeywordColors = new Dictionary<string, Color>(StringComparer.Ordinal);
        private readonly Dictionary<int, int> _colorRegionCache = new Dictionary<int, int>();
        private Color _numberColor = Color.Transparent;
        private Color _symbolColor = Color.Transparent;
        private Color _functionColor = Color.Transparent;
        private Color _memberVariableColor = Color.Transparent;
        private bool _uintSuffixEnabled;
        public Color NumberColor { get => _numberColor; set { if (_numberColor == value) return; _numberColor = value; UpdateCache(); } }
        public Color SymbolColor { get => _symbolColor; set { if (_symbolColor == value) return; _symbolColor = value; UpdateCache(); } }
        public Color FunctionColor { get => _functionColor; set { if (_functionColor == value) return; _functionColor = value; UpdateCache(); } }
        public Color MemberVariableColor { get => _memberVariableColor; set { if (_memberVariableColor == value) return; _memberVariableColor = value; UpdateCache(); } }
        public bool UIntSuffixEnabled { get => _uintSuffixEnabled; set { if (_uintSuffixEnabled == value) return; _uintSuffixEnabled = value; UpdateCache(); } }
        public void AddKeywordColor(string keyword, Color color) { if (string.IsNullOrEmpty(keyword)) throw new ArgumentException("A keyword is required.", nameof(keyword)); _keywordColors[keyword] = color; UpdateCache(); }
        public void RemoveKeywordColor(string keyword) { if (_keywordColors.Remove(keyword ?? string.Empty)) UpdateCache(); }
        public bool HasKeywordColor(string keyword) => !string.IsNullOrEmpty(keyword) && _keywordColors.ContainsKey(keyword);
        public Color GetKeywordColor(string keyword) => _keywordColors.TryGetValue(keyword ?? string.Empty, out var color) ? color : Color.Transparent;
        public IReadOnlyDictionary<string, Color> GetKeywordColors() => new Dictionary<string, Color>(_keywordColors);
        public void SetKeywordColors(IDictionary<string, Color> colors) { _keywordColors.Clear(); if (colors != null) foreach (var pair in colors) if (!string.IsNullOrEmpty(pair.Key)) _keywordColors[pair.Key] = pair.Value; UpdateCache(); }
        public void ClearKeywordColors() { if (_keywordColors.Count == 0) return; _keywordColors.Clear(); UpdateCache(); }
        public void AddMemberKeywordColor(string keyword, Color color) { if (string.IsNullOrEmpty(keyword)) throw new ArgumentException("A member keyword is required.", nameof(keyword)); _memberKeywordColors[keyword] = color; UpdateCache(); }
        public void RemoveMemberKeywordColor(string keyword) { if (_memberKeywordColors.Remove(keyword ?? string.Empty)) UpdateCache(); }
        public bool HasMemberKeywordColor(string keyword) => !string.IsNullOrEmpty(keyword) && _memberKeywordColors.ContainsKey(keyword);
        public Color GetMemberKeywordColor(string keyword) => _memberKeywordColors.TryGetValue(keyword ?? string.Empty, out var color) ? color : Color.Transparent;
        public IReadOnlyDictionary<string, Color> GetMemberKeywordColors() => new Dictionary<string, Color>(_memberKeywordColors);
        public void SetMemberKeywordColors(IDictionary<string, Color> colors) { _memberKeywordColors.Clear(); if (colors != null) foreach (var pair in colors) if (!string.IsNullOrEmpty(pair.Key)) _memberKeywordColors[pair.Key] = pair.Value; UpdateCache(); }
        public void ClearMemberKeywordColors() { if (_memberKeywordColors.Count == 0) return; _memberKeywordColors.Clear(); UpdateCache(); }
        public void AddColorRegion(string startKey, string endKey, Color color, bool lineOnly = false)
        {
            AddColorRegionInternal(startKey, endKey, color, lineOnly);
            UpdateCache();
        }
        public void RemoveColorRegion(string startKey) { if (RemoveColorRegionInternal(startKey)) UpdateCache(); }
        public bool HasColorRegion(string startKey) { foreach (var region in _colorRegions) if (region.StartKey == startKey) return true; return false; }
        public IReadOnlyList<CodeHighlightColorRegion> GetColorRegions()
        {
            var regions = new List<CodeHighlightColorRegion>(_colorRegions.Count);
            foreach (var region in _colorRegions) regions.Add(new CodeHighlightColorRegion(region.StartKey, region.EndKey, region.Color, region.LineOnly));
            return regions;
        }
        public void SetColorRegions(IEnumerable<CodeHighlightColorRegion> regions)
        {
            _colorRegions.Clear();
            if (regions != null) foreach (var region in regions) AddColorRegionInternal(region.StartKey, region.EndKey, region.Color, region.LineOnly);
            UpdateCache();
        }
        public void ClearColorRegions() { if (_colorRegions.Count == 0) return; _colorRegions.Clear(); UpdateCache(); }
        public void SetNumberColor(Color color) => NumberColor = color;
        public Color GetNumberColor() => NumberColor;
        public void SetSymbolColor(Color color) => SymbolColor = color;
        public Color GetSymbolColor() => SymbolColor;
        public void SetFunctionColor(Color color) => FunctionColor = color;
        public Color GetFunctionColor() => FunctionColor;
        public void SetMemberVariableColor(Color color) => MemberVariableColor = color;
        public Color GetMemberVariableColor() => MemberVariableColor;
        public void SetUIntSuffixEnabled(bool enabled) => UIntSuffixEnabled = enabled;
        public bool IsUIntSuffixEnabled() => UIntSuffixEnabled;
        protected override void OnClearHighlightingCache() => _colorRegionCache.Clear();
        protected override void OnInvalidateFromLine(int line)
        {
            foreach (var cachedLine in new List<int>(_colorRegionCache.Keys))
                if (cachedLine >= line) _colorRegionCache.Remove(cachedLine);
        }
        protected override IReadOnlyList<SyntaxHighlightSpan> GetLineSyntaxHighlightingCore(int line)
        {
            var text = TextEdit.GetLine(line);
            var spans = new List<SyntaxHighlightSpan>();
            var activeRegion = GetActiveRegionAtLineStart(line);
            var column = 0;
            while (column < text.Length)
            {
                if (activeRegion >= 0)
                {
                    var region = _colorRegions[activeRegion];
                    var end = string.IsNullOrEmpty(region.EndKey) ? -1 : text.IndexOf(region.EndKey, column, StringComparison.Ordinal);
                    var last = end < 0 ? text.Length : end + region.EndKey.Length;
                    AddSpan(spans, column, last - column, region.Color);
                    if (end < 0) { _colorRegionCache[line] = region.LineOnly ? -1 : activeRegion; return spans; }
                    activeRegion = -1; column = last; continue;
                }
                var enteringRegion = FindRegionAt(text, column);
                if (enteringRegion >= 0)
                {
                    var region = _colorRegions[enteringRegion];
                    var afterStart = column + region.StartKey.Length;
                    var end = string.IsNullOrEmpty(region.EndKey) ? -1 : text.IndexOf(region.EndKey, afterStart, StringComparison.Ordinal);
                    var last = end < 0 ? text.Length : end + region.EndKey.Length;
                    AddSpan(spans, column, last - column, region.Color);
                    if (end < 0) { _colorRegionCache[line] = region.LineOnly ? -1 : enteringRegion; return spans; }
                    column = last; continue;
                }
                if (IsWordCharacter(text[column]))
                {
                    var start = column; while (column < text.Length && IsWordCharacter(text[column])) column++;
                    var word = text.Substring(start, column - start);
                    var memberAccess = start > 0 && text[start - 1] == '.';
                    if (_keywordColors.TryGetValue(word, out var keywordColor)) AddSpan(spans, start, word.Length, keywordColor);
                    else if (!memberAccess && _memberKeywordColors.TryGetValue(word, out var memberKeywordColor)) AddSpan(spans, start, word.Length, memberKeywordColor);
                    else if (memberAccess && MemberVariableColor != Color.Transparent) AddSpan(spans, start, word.Length, MemberVariableColor);
                    else if (IsNumericToken(word) && NumberColor != Color.Transparent) AddSpan(spans, start, word.Length, NumberColor);
                    else if (NextNonWhitespaceIsOpenParenthesis(text, column) && FunctionColor != Color.Transparent) AddSpan(spans, start, word.Length, FunctionColor);
                    continue;
                }
                if (!char.IsWhiteSpace(text[column]) && SymbolColor != Color.Transparent) AddSpan(spans, column, 1, SymbolColor);
                column++;
            }
            _colorRegionCache[line] = -1;
            return spans;
        }
        private int GetActiveRegionAtLineStart(int line)
        {
            if (line <= 0) return -1;
            GetLineSyntaxHighlighting(line - 1);
            return _colorRegionCache.TryGetValue(line - 1, out var region) ? region : -1;
        }
        private int FindRegionAt(string text, int column)
        {
            for (var index = 0; index < _colorRegions.Count; index++)
            {
                var start = _colorRegions[index].StartKey;
                if (column + start.Length <= text.Length && string.CompareOrdinal(text, column, start, 0, start.Length) == 0) return index;
            }
            return -1;
        }
        private bool RemoveColorRegionInternal(string startKey)
        {
            for (var index = _colorRegions.Count - 1; index >= 0; index--) if (_colorRegions[index].StartKey == startKey) { _colorRegions.RemoveAt(index); return true; }
            return false;
        }
        private void AddColorRegionInternal(string startKey, string endKey, Color color, bool lineOnly)
        {
            if (string.IsNullOrEmpty(startKey)) throw new ArgumentException("A region start key is required.", nameof(startKey));
            if (!IsSymbolSequence(startKey)) throw new ArgumentException("Color region keys must contain only symbol characters.", nameof(startKey));
            endKey ??= string.Empty;
            if (!string.IsNullOrEmpty(endKey) && !IsSymbolSequence(endKey)) throw new ArgumentException("Color region keys must contain only symbol characters.", nameof(endKey));
            if (HasColorRegion(startKey)) throw new ArgumentException("A color region with the same start key already exists.", nameof(startKey));
            var index = 0; while (index < _colorRegions.Count && _colorRegions[index].StartKey.Length >= startKey.Length) index++;
            _colorRegions.Insert(index, new ColorRegion { StartKey = startKey, EndKey = endKey, Color = color, LineOnly = lineOnly || string.IsNullOrEmpty(endKey) });
        }
        private static void AddSpan(List<SyntaxHighlightSpan> spans, int start, int length, Color color) { if (length > 0 && color != Color.Transparent) spans.Add(new SyntaxHighlightSpan(start, length, color)); }
        private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';
        private static bool IsSymbolSequence(string text) { foreach (var character in text) if (char.IsLetterOrDigit(character) || character == '_' || char.IsWhiteSpace(character)) return false; return true; }
        private static bool NextNonWhitespaceIsOpenParenthesis(string text, int column) { while (column < text.Length && char.IsWhiteSpace(text[column])) column++; return column < text.Length && text[column] == '('; }
        private bool IsNumericToken(string word)
        {
            if (string.IsNullOrEmpty(word) || !char.IsDigit(word[0])) return false;
            var end = word.Length;
            if (word[end - 1] == 'u') { if (!UIntSuffixEnabled) return false; end--; }
            for (var index = 0; index < end; index++)
            {
                var character = word[index];
                if (char.IsDigit(character) || character == '_' || character == '.' || character == 'x' || character == 'X' || character == 'e' || character == 'E' || character == 'f' || (index > 1 && (character >= 'a' && character <= 'f' || character >= 'A' && character <= 'F'))) continue;
                return false;
            }
            return end > 0;
        }
    }

    /// <summary>Configuration for one of a <see cref="TextEdit"/>'s left-side gutter columns.</summary>
    public sealed class TextEditGutter
    {
        internal TextEditGutter() { }
        public string Name { get; internal set; } = string.Empty;
        public TextEditGutterType Type { get; internal set; }
        public int Width { get; internal set; } = 24;
        public bool Draw { get; internal set; } = true;
        public bool Clickable { get; internal set; }
        public bool Overwritable { get; internal set; }
        public Action<UIRenderContext, TextEdit, int, Rectangle> CustomDraw { get; internal set; }
    }

    /// <summary>Single-line editable text. Call <see cref="InsertText"/> from the host window's text-input callback for IME-safe text entry.</summary>
    [TemplatePart(EditorPresenterPartName, typeof(LineEditPresenter))]
    public class LineEdit : TemplatedControl
    {
        public const string EditorPresenterPartName = "PART_EditorPresenter";
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.TextBox;
        public override string AccessibilityName => string.IsNullOrEmpty(base.AccessibilityName) ? Text ?? string.Empty : base.AccessibilityName;
        public override string AccessibilityValue => Text ?? string.Empty;
        public override AccessibilityActions AccessibilityActions => base.AccessibilityActions |
            (Editable ? AccessibilityActions.SetValue : AccessibilityActions.None);
        public override AccessibilityStates AccessibilityStates => base.AccessibilityStates |
            (!Editable ? AccessibilityStates.ReadOnly : AccessibilityStates.None);
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private string _text = string.Empty;
        private int _selectionAnchor = -1;
        private readonly List<string> _undoStack = new List<string>();
        private readonly List<string> _redoStack = new List<string>();
        private bool _restoringHistory;
        private readonly PopupMenu _contextMenu;
        private readonly PopupMenu _directionMenu;
        private readonly PopupMenu _controlCharacterMenu;
        private object[] _structuredTextBidiOverrideOptions = Array.Empty<object>();
        private bool _selectingText;
        private bool _selectingByWord;
        private int _pointerSelectionAnchor;
        private int _textClickCount;
        private TimeSpan _lastTextClickTime = TimeSpan.MinValue;
        private Point _lastTextClickPosition;
        private float _scrollOffset;
        private string _imeComposition = string.Empty;
        private int _imeSelectionStart;
        private int _imeSelectionLength;
        private int _imeReplaceStart;
        private int _imeReplaceLength;
        private VerticalAlignment _textVerticalAlignment = VerticalAlignment.Center;
        private static readonly TimeSpan MultiClickTimeout = TimeSpan.FromMilliseconds(600);
        private const int MultiClickTolerance = 5;
        public LineEdit()
        {
            FocusMode = FocusMode.All;
            Padding = new Thickness(6, 4, 6, 4);
            _contextMenu = new PopupMenu { Visible = false };
            _contextMenu.IdPressed += (_, id) => MenuOption((LineEditMenuOption)id);
            _directionMenu = new PopupMenu { Visible = false };
            _directionMenu.IdPressed += (_, id) => MenuOption((LineEditMenuOption)id);
            _controlCharacterMenu = new PopupMenu { Visible = false };
            _controlCharacterMenu.IdPressed += (_, id) => MenuOption((LineEditMenuOption)id);
        }
        public string Text
        {
            get => _text;
            set
            {
                value ??= string.Empty;
                if (_text == value) return;
                if (!_restoringHistory && UndoEnabled)
                {
                    _undoStack.Add(_text);
                    if (_undoStack.Count > UndoStackMaxSize) _undoStack.RemoveAt(0);
                    _redoStack.Clear();
                }
                _text = value;
                CaretColumn = Math.Min(CaretColumn, _text.Length);
                if (_selectionAnchor >= 0) _selectionAnchor = Math.Min(_selectionAnchor, _text.Length);
                TextChanged?.Invoke(this, _text);
            }
        }
        protected override void OnTemplateApplied()
        {
            if (GetTemplateChild(EditorPresenterPartName) is LineEditPresenter presenter) presenter.Owner = this;
            base.OnTemplateApplied();
        }
        public string PlaceholderText { get; set; } = string.Empty;
        public string SecretCharacter { get; set; } = string.Empty;
        public bool Editable { get; set; } = true;
        private int _maxLength;
        public int MaxLength
        {
            get => _maxLength;
            set
            {
                _maxLength = Math.Max(0, value);
                // Godot's set_max_length re-applies set_text(text), re-inserting the current text through
                // the same max_length-truncating insertion pipeline used for ordinary typed/pasted input -
                // so shrinking the limit below the current length clips it immediately, from the tail end.
                if (_maxLength > 0 && Text.Length > _maxLength)
                {
                    var rejected = Text.Substring(_maxLength);
                    Text = Text.Substring(0, _maxLength);
                    TextChangeRejected?.Invoke(this, rejected);
                }
            }
        }
        public bool ClearButtonEnabled { get; set; }
        public bool SubmitOnFocusExit { get; set; }
        public bool DeselectOnFocusLoss { get; set; } = true;
        public bool ContextMenuEnabled { get; set; } = true;
        public bool ShortcutKeysEnabled { get; set; } = true;
        public bool SelectAllOnFocus { get; set; }
        public bool SelectingEnabled
        {
            get => _selectingEnabled;
            set { _selectingEnabled = value; if (!value) Deselect(); }
        }
        private bool _selectingEnabled = true;
        public bool UndoEnabled { get; set; } = true;
        public int UndoStackMaxSize { get; set; } = 50;
        public TextDirection TextDirection { get; private set; } = TextDirection.Auto;
        public bool DrawControlCharacters { get; private set; }
        public StructuredTextParser StructuredTextBidiOverride { get; private set; } = StructuredTextParser.Default;
        /// <summary>Zero-based caret index in the underlying string.</summary>
        public int CaretColumn { get; protected set; }
        public bool HasImeComposition => _imeComposition.Length > 0;
        public string ImeCompositionText => _imeComposition;
        public Point ImeCompositionSelection => new Point(_imeSelectionStart, _imeSelectionLength);
        public float GetScrollOffset()
        {
            if (EffectiveUIFont != null) EnsureCaretVisible(GetEditingLayout(GetComposedDisplayText()), GetDisplayCaretColumn());
            return _scrollOffset;
        }
        public bool HasSelection => _selectionAnchor >= 0 && _selectionAnchor != CaretColumn;
        public int SelectionFrom => HasSelection ? Math.Min(_selectionAnchor, CaretColumn) : CaretColumn;
        public int SelectionTo => HasSelection ? Math.Max(_selectionAnchor, CaretColumn) : CaretColumn;
        public string SelectedText => HasSelection ? Text.Substring(SelectionFrom, SelectionTo - SelectionFrom) : string.Empty;
        public bool HasUndo => _undoStack.Count > 0;
        public bool HasRedo => _redoStack.Count > 0;
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection);
        public Thickness Padding { get; set; }
        public VerticalAlignment TextVerticalAlignment
        {
            get => _textVerticalAlignment;
            set
            {
                if (!Enum.IsDefined(typeof(VerticalAlignment), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (_textVerticalAlignment == value) return;
                _textVerticalAlignment = value;
                QueueLayout();
            }
        }
        /// <summary>Optional per-control override used by <see cref="Paste()"/> before <see cref="UIContext.Clipboard"/>.</summary>
        public Func<LineEdit, string> ClipboardTextProvider { get; set; }
        public event Action<LineEdit, string> TextChanged;
        public event Action<LineEdit, string> TextChangeRejected;
        public event EventHandler TextSubmitted;
        /// <summary>Raised after Copy or Cut submits text to <see cref="UIContext.Clipboard"/>.</summary>
        public event Action<LineEdit, string> CopyRequested;
        /// <summary>Returns the retained context popup, equivalent to Godot's <c>get_menu()</c>.</summary>
        public virtual PopupMenu GetMenu() => _contextMenu;
        public PopupMenu GetTextDirectionMenu() => _directionMenu;
        public PopupMenu GetControlCharacterMenu() => _controlCharacterMenu;
        public virtual bool IsMenuVisible() => _contextMenu.Visible;
        public void SetContextMenuEnabled(bool enabled) => ContextMenuEnabled = enabled;
        public bool IsContextMenuEnabled() => ContextMenuEnabled;
        public void SetShortcutKeysEnabled(bool enabled) => ShortcutKeysEnabled = enabled;
        public bool IsShortcutKeysEnabled() => ShortcutKeysEnabled;
        public void SetSelectAllOnFocus(bool enabled) => SelectAllOnFocus = enabled;
        public bool IsSelectAllOnFocus() => SelectAllOnFocus;
        public void SetSelectingEnabled(bool enabled) => SelectingEnabled = enabled;
        public bool IsSelectingEnabled() => SelectingEnabled;
        public void SetTextDirection(TextDirection direction)
        {
            if (!Enum.IsDefined(typeof(TextDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            if (TextDirection == direction) return;
            TextDirection = direction;
            QueueLayout();
        }
        public TextDirection GetTextDirection() => TextDirection;
        public void SetLanguage(string language)
        {
            language ??= string.Empty;
            if (Language == language) return;
            Language = language;
            QueueLayout();
        }
        public string GetLanguage() => Language;
        public void SetDrawControlChars(bool enabled) => DrawControlCharacters = enabled;
        public bool GetDrawControlChars() => DrawControlCharacters;
        public void SetStructuredTextBidiOverride(StructuredTextParser parser)
        {
            if (!Enum.IsDefined(typeof(StructuredTextParser), parser)) throw new ArgumentOutOfRangeException(nameof(parser));
            StructuredTextBidiOverride = parser;
        }
        public StructuredTextParser GetStructuredTextBidiOverride() => StructuredTextBidiOverride;
        public void SetStructuredTextBidiOverrideOptions(IEnumerable<object> options) => _structuredTextBidiOverrideOptions = options == null ? Array.Empty<object>() : new List<object>(options).ToArray();
        public IReadOnlyList<object> GetStructuredTextBidiOverrideOptions() => _structuredTextBidiOverrideOptions;
        /// <summary>Local clear-button hit region, or an empty rectangle when the affordance is unavailable.</summary>
        public Rectangle GetClearButtonRectangle()
        {
            if (!ClearButtonEnabled || !Editable || string.IsNullOrEmpty(Text)) return Rectangle.Empty;
            var side = Math.Max(12, Math.Min(16, (int)MathF.Round(Size.Y - Padding.Vertical)));
            return new Rectangle(Math.Max(0, (int)MathF.Round(Size.X - Padding.Right - side)), Math.Max(0, (int)MathF.Round((Size.Y - side) / 2)), side, side);
        }
        public virtual void InsertText(string text)
        {
            if (!Editable || string.IsNullOrEmpty(text)) return;
            if (HasSelection) DeleteSelection();
            if (MaxLength > 0)
            {
                var available = Math.Max(0, MaxLength - Text.Length);
                if (text.Length > available)
                {
                    var rejected = text.Substring(available);
                    text = text.Substring(0, available);
                    if (!string.IsNullOrEmpty(rejected)) TextChangeRejected?.Invoke(this, rejected);
                }
            }
            if (string.IsNullOrEmpty(text)) return;
            Text = Text.Insert(CaretColumn, text); CaretColumn += text.Length; Deselect();
        }
        public void SetImeComposition(string text, int selectionStart = 0, int selectionLength = 0)
        {
            text ??= string.Empty;
            if (!HasImeComposition)
            {
                _imeReplaceStart = SelectionFrom;
                _imeReplaceLength = SelectionTo - SelectionFrom;
            }
            _imeComposition = text;
            _imeSelectionStart = MathHelper.Clamp(selectionStart, 0, text.Length);
            _imeSelectionLength = MathHelper.Clamp(selectionLength, 0, text.Length - _imeSelectionStart);
            if (text.Length == 0) CancelImeComposition();
            QueueLayout();
        }
        public void CommitImeComposition(string text = null)
        {
            if (!HasImeComposition && string.IsNullOrEmpty(text)) return;
            var committed = text ?? _imeComposition;
            var start = HasImeComposition ? _imeReplaceStart : CaretColumn;
            var length = HasImeComposition ? _imeReplaceLength : 0;
            CancelImeComposition();
            if (!Editable || string.IsNullOrEmpty(committed)) return;
            Text = Text.Remove(start, length).Insert(start, committed);
            CaretColumn = start + committed.Length;
            Deselect();
        }
        public void CancelImeComposition()
        {
            _imeComposition = string.Empty;
            _imeSelectionStart = 0;
            _imeSelectionLength = 0;
            _imeReplaceStart = CaretColumn;
            _imeReplaceLength = 0;
            QueueLayout();
        }
        public void Select(int from, int to)
        {
            _selectionAnchor = MathHelper.Clamp(from, 0, Text.Length);
            CaretColumn = MathHelper.Clamp(to, 0, Text.Length);
        }
        public void SelectAll() => Select(0, Text.Length);
        public void Deselect() => _selectionAnchor = -1;
        public void DeleteSelection()
        {
            if (!HasSelection) return;
            var start = SelectionFrom;
            Text = Text.Remove(start, SelectionTo - start);
            CaretColumn = start;
            Deselect();
        }
        public void DeleteText(int fromColumn, int toColumn)
        {
            var start = Math.Max(0, Math.Min(fromColumn, toColumn)); var end = Math.Min(Text.Length, Math.Max(fromColumn, toColumn));
            if (end <= start) return;
            Text = Text.Remove(start, end - start); CaretColumn = Math.Min(start, Text.Length); Deselect();
        }
        public void Clear()
        {
            if (!Editable) return;
            Text = string.Empty;
            CaretColumn = 0;
            Deselect();
        }
        public void ClearUndoHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
        public void Undo()
        {
            if (!Editable || !UndoEnabled || _undoStack.Count == 0) return;
            var previous = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(Text);
            RestoreHistoryText(previous);
        }
        public void Redo()
        {
            if (!Editable || !UndoEnabled || _redoStack.Count == 0) return;
            var next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(Text);
            RestoreHistoryText(next);
        }
        public void Copy()
        {
            if (!HasSelection || !string.IsNullOrEmpty(SecretCharacter)) return;
            var copied = SelectedText;
            if (!string.IsNullOrEmpty(copied)) WriteClipboard(copied);
        }
        public void Cut()
        {
            // Godot's ui_cut handler falls back to a plain copy when the field isn't editable, rather
            // than no-oping entirely: a read-only field with a selection still puts it on the clipboard.
            if (!Editable) { Copy(); return; }
            if (!HasSelection || !string.IsNullOrEmpty(SecretCharacter)) return;
            var copied = SelectedText;
            if (string.IsNullOrEmpty(copied)) return;
            WriteClipboard(copied);
            DeleteSelection();
        }
        public void Paste() => Paste(ClipboardTextProvider?.Invoke(this) ?? Context?.Clipboard?.GetText());
        public void Paste(string clipboard)
        {
            if (!Editable || string.IsNullOrEmpty(clipboard)) return;
            var stripped = new System.Text.StringBuilder(clipboard.Length);
            foreach (var character in clipboard)
                if (!char.IsControl(character)) stripped.Append(character);
            InsertText(stripped.ToString());
        }
        public void MenuOption(LineEditMenuOption option)
        {
            switch (option)
            {
                case LineEditMenuOption.Cut: Cut(); break;
                case LineEditMenuOption.Copy: Copy(); break;
                case LineEditMenuOption.Paste: Paste(); break;
                case LineEditMenuOption.Clear: Clear(); break;
                case LineEditMenuOption.SelectAll: SelectAll(); break;
                case LineEditMenuOption.Undo: Undo(); break;
                case LineEditMenuOption.Redo: Redo(); break;
                case LineEditMenuOption.DirectionInherited: SetTextDirection(TextDirection.Inherited); break;
                case LineEditMenuOption.DirectionAuto: SetTextDirection(TextDirection.Auto); break;
                case LineEditMenuOption.DirectionLeftToRight: SetTextDirection(TextDirection.LeftToRight); break;
                case LineEditMenuOption.DirectionRightToLeft: SetTextDirection(TextDirection.RightToLeft); break;
                case LineEditMenuOption.DisplayControlCharacters: SetDrawControlChars(!GetDrawControlChars()); break;
                case LineEditMenuOption.InsertLeftToRightMark: InsertControlCharacter('\u200E'); break;
                case LineEditMenuOption.InsertRightToLeftMark: InsertControlCharacter('\u200F'); break;
                case LineEditMenuOption.InsertLeftToRightEmbedding: InsertControlCharacter('\u202A'); break;
                case LineEditMenuOption.InsertRightToLeftEmbedding: InsertControlCharacter('\u202B'); break;
                case LineEditMenuOption.InsertLeftToRightOverride: InsertControlCharacter('\u202D'); break;
                case LineEditMenuOption.InsertRightToLeftOverride: InsertControlCharacter('\u202E'); break;
                case LineEditMenuOption.InsertPopDirectionFormatting: InsertControlCharacter('\u202C'); break;
                case LineEditMenuOption.InsertArabicLetterMark: InsertControlCharacter('\u061C'); break;
                case LineEditMenuOption.InsertLeftToRightIsolate: InsertControlCharacter('\u2066'); break;
                case LineEditMenuOption.InsertRightToLeftIsolate: InsertControlCharacter('\u2067'); break;
                case LineEditMenuOption.InsertFirstStrongIsolate: InsertControlCharacter('\u2068'); break;
                case LineEditMenuOption.InsertPopDirectionIsolate: InsertControlCharacter('\u2069'); break;
                case LineEditMenuOption.InsertZeroWidthJoiner: InsertControlCharacter('\u200D'); break;
                case LineEditMenuOption.InsertZeroWidthNonJoiner: InsertControlCharacter('\u200C'); break;
                case LineEditMenuOption.InsertWordJoiner: InsertControlCharacter('\u2060'); break;
                case LineEditMenuOption.InsertSoftHyphen: InsertControlCharacter('\u00AD'); break;
            }
        }
        private void WriteClipboard(string text)
        {
            Context?.Clipboard?.SetText(text);
            CopyRequested?.Invoke(this, text);
        }
        public override Vector2 GetMinimumSize() => Vector2.Max(CustomMinimumSize, new Vector2(80, EffectiveUIFont == null ? 24 : TextMetrics.LineHeight(EffectiveUIFont) + Padding.Vertical));
        internal override void PointerPressed(Point position)
        {
            CancelImeComposition();
            var hadSelectionBeforeFocus = HasSelection;
            base.PointerPressed(position);
            var clear = GetClearButtonRectangle();
            if (!clear.IsEmpty && clear.Contains((int)(position.X - GlobalPosition.X), (int)(position.Y - GlobalPosition.Y)))
            {
                Text = string.Empty; CaretColumn = 0; Deselect(); return;
            }
            // Godot's SelectAllOnFocus defers select_all() to mouse release when focus was gained by a
            // mouse press, so the click's caret placement never overwrites the just-created selection;
            // this port has no PointerReleased hook for LineEdit, so the equivalent is simply: if this
            // exact press just caused a fresh focus-driven SelectAll (no selection before, one now),
            // don't let the click below immediately collapse it back down to a caret position.
            if (SelectAllOnFocus && !hadSelectionBeforeFocus && HasSelection) return;
            var clickedColumn = GetCaretColumnAtPosition(position);
            if (HasShiftModifier() && SelectingEnabled)
            {
                if (!HasSelection) _selectionAnchor = CaretColumn;
                CaretColumn = clickedColumn;
                _pointerSelectionAnchor = _selectionAnchor;
                _selectingText = true;
                _selectingByWord = false;
                return;
            }
            CaretColumn = clickedColumn;
            if (!SelectingEnabled || string.IsNullOrEmpty(Text)) { Deselect(); return; }
            var clickTime = Context?.CurrentTime ?? TimeSpan.Zero;
            var withinTimeout = _lastTextClickTime != TimeSpan.MinValue && clickTime - _lastTextClickTime <= MultiClickTimeout;
            var withinTolerance = Vector2.DistanceSquared(position.ToVector2(), _lastTextClickPosition.ToVector2()) <= MultiClickTolerance * MultiClickTolerance;
            _textClickCount = withinTimeout && withinTolerance ? Math.Min(3, _textClickCount + 1) : 1;
            _lastTextClickTime = clickTime;
            _lastTextClickPosition = position;
            _pointerSelectionAnchor = clickedColumn;
            _selectingText = true;
            _selectingByWord = _textClickCount == 2;
            if (_textClickCount == 3)
            {
                SelectAll();
                _selectingText = false;
                _selectingByWord = false;
                _textClickCount = 0;
                _lastTextClickTime = TimeSpan.MinValue;
            }
            else if (_selectingByWord) SelectPointerRange(clickedColumn);
            else Deselect();
        }
        internal override void PointerMoved(Point position)
        {
            if (_selectingText) SelectPointerRange(GetCaretColumnAtPosition(position));
        }
        internal override void PointerReleased(Point position, bool isInside)
        {
            if (_selectingText) SelectPointerRange(GetCaretColumnAtPosition(position));
            _selectingText = false;
            _selectingByWord = false;
            base.PointerReleased(position, isInside);
        }
        internal override void PointerRightPressed(Point position) => OpenContextMenu(position);
        internal override void KeyPressed(Keys key)
        {
            if (HasImeComposition)
            {
                if (key == Keys.Escape) { CancelImeComposition(); return; }
                if (key == Keys.Enter) { CommitImeComposition(); return; }
                CancelImeComposition();
            }
            if (key == Keys.Apps || key == Keys.F10 && HasShiftModifier()) { OpenContextMenu(new Point((int)GlobalPosition.X, (int)GlobalPosition.Y)); return; }
            if (ShortcutKeysEnabled && HasCommandModifier())
            {
                if (key == Keys.A) { SelectAll(); return; }
                if (key == Keys.C) { Copy(); return; }
                if (key == Keys.X) { Cut(); return; }
                if (key == Keys.V) { Paste(); return; }
                if (key == Keys.Z) { Undo(); return; }
                if (key == Keys.Y) { Redo(); return; }
            }
            if (!Editable) return;
            var shift = HasShiftModifier();
            var ctrl = HasCommandModifier();
            if (key == Keys.Left)
            {
                if (HasSelection && !shift) { CaretColumn = SelectionFrom; Deselect(); }
                else { ShiftSelectionCheckPre(shift); CaretColumn = ctrl ? FindWordBoundaryLeft(CaretColumn) : FindGraphemeBoundaryLeft(CaretColumn); }
            }
            else if (key == Keys.Right)
            {
                if (HasSelection && !shift) { CaretColumn = SelectionTo; Deselect(); }
                else { ShiftSelectionCheckPre(shift); CaretColumn = ctrl ? FindWordBoundaryRight(CaretColumn) : FindGraphemeBoundaryRight(CaretColumn); }
            }
            else if (key == Keys.Home) { ShiftSelectionCheckPre(shift); CaretColumn = 0; }
            else if (key == Keys.End) { ShiftSelectionCheckPre(shift); CaretColumn = Text.Length; }
            else if (key == Keys.Back)
            {
                if (HasSelection) DeleteSelection();
                else if (ctrl) { var start = FindWordBoundaryLeft(CaretColumn); if (start < CaretColumn) DeleteText(start, CaretColumn); }
                else if (CaretColumn > 0) DeleteText(FindGraphemeBoundaryLeft(CaretColumn), CaretColumn);
            }
            else if (key == Keys.Delete)
            {
                if (HasSelection) DeleteSelection();
                else if (ctrl) { var end = FindWordBoundaryRight(CaretColumn); if (end > CaretColumn) DeleteText(CaretColumn, end); }
                else if (CaretColumn < Text.Length) DeleteText(CaretColumn, FindGraphemeBoundaryRight(CaretColumn));
            }
            else if (key == Keys.Enter) TextSubmitted?.Invoke(this, EventArgs.Empty);
        }
        private int FindGraphemeBoundaryLeft(int from) => EffectiveUIFont == null ? Math.Max(0, from - 1) : GetEditingLayout().GetPreviousGraphemeBoundary(from);
        private int FindGraphemeBoundaryRight(int from) => EffectiveUIFont == null ? Math.Min(Text.Length, from + 1) : GetEditingLayout().GetNextGraphemeBoundary(from);
        private int FindWordBoundaryLeft(int from)
        {
            if (EffectiveUIFont != null) return GetEditingLayout().GetPreviousWordBoundary(from);
            if (from > 0) from--;
            while (from > 0 && !IsWordChar(Text[from])) from--;
            while (from > 0 && IsWordChar(Text[from - 1])) from--;
            return from;
        }
        private int FindWordBoundaryRight(int from)
        {
            if (EffectiveUIFont != null) return GetEditingLayout().GetNextWordBoundary(from);
            while (from < Text.Length && !IsWordChar(Text[from])) from++;
            while (from < Text.Length && IsWordChar(Text[from])) from++;
            return from;
        }
        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
        protected virtual int GetCaretColumnAtPosition(Point position)
        {
            if (EffectiveUIFont == null) return Text.Length;
            var layout = GetEditingLayout();
            EnsureCaretVisible(layout);
            var localX = position.X - GlobalPosition.X - Padding.Left + _scrollOffset;
            return layout.HitTest(new Vector2(localX, 0));
        }
        private void SelectPointerRange(int column)
        {
            if (!_selectingByWord)
            {
                Select(_pointerSelectionAnchor, column);
                return;
            }
            var anchorRange = GetWordRange(_pointerSelectionAnchor);
            var currentRange = GetWordRange(column);
            if (anchorRange.X == anchorRange.Y || currentRange.X == currentRange.Y) return;
            if (column < _pointerSelectionAnchor) Select(anchorRange.Y, currentRange.X);
            else Select(anchorRange.X, currentRange.Y);
        }
        private Point GetWordRange(int column)
        {
            if (string.IsNullOrEmpty(Text)) return Point.Zero;
            var range = GetEditingLayout().GetWordBoundary(Math.Min(Math.Max(0, column), Text.Length));
            return new Point(range.Start, range.End);
        }
        /// <summary>Marks the selection anchor at the current caret before a caret-movement key, matching
        /// Godot's LineEdit::shift_selection_check_pre - the resulting selection range is then implicit
        /// in SelectionFrom/SelectionTo once the caret actually moves, matching shift_selection_check_post.</summary>
        private void ShiftSelectionCheckPre(bool shiftHeld)
        {
            if (!HasSelection && shiftHeld) _selectionAnchor = CaretColumn;
            if (!shiftHeld) Deselect();
        }
        internal override void TextInput(char character)
        {
            if (HasImeComposition) CommitImeComposition(character.ToString());
            else InsertText(character.ToString());
        }
        internal override void TextComposition(string text, int selectionStart, int selectionLength) => SetImeComposition(text, selectionStart, selectionLength);
        internal override void FocusGained()
        {
            base.FocusGained();
            if (SelectAllOnFocus) SelectAll();
        }
        internal override void FocusLost()
        {
            CancelImeComposition();
            if (DeselectOnFocusLoss && !IsMenuVisible()) Deselect();
            if (SubmitOnFocusExit) TextSubmitted?.Invoke(this, EventArgs.Empty);
            base.FocusLost();
        }
        private bool HasCommandModifier()
        {
            var keyboard = Context?.CurrentKeyboardState ?? default;
            return keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl) || keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows);
        }
        private bool HasShiftModifier()
        {
            var keyboard = Context?.CurrentKeyboardState ?? default;
            return keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        }
        private void RestoreHistoryText(string text)
        {
            _restoringHistory = true;
            try { Text = text; }
            finally { _restoringHistory = false; }
            CaretColumn = Text.Length;
            Deselect();
        }
        private void InsertControlCharacter(char character)
        {
            if (!Editable) return;
            InsertText(character.ToString());
        }
        private void OpenContextMenu(Point position)
        {
            if (!ContextMenuEnabled || Context == null) return;
            _contextMenu.Clear(); _contextMenu.Font = Font; _contextMenu.UIFont = UIFont;
            _directionMenu.Clear(); _directionMenu.Font = Font; _directionMenu.UIFont = UIFont;
            _directionMenu.AddRadioCheckItem("Same as Layout Direction", (int)LineEditMenuOption.DirectionInherited).Checked = TextDirection == TextDirection.Inherited;
            _directionMenu.AddRadioCheckItem("Auto-Detect Direction", (int)LineEditMenuOption.DirectionAuto).Checked = TextDirection == TextDirection.Auto;
            _directionMenu.AddRadioCheckItem("Left-to-Right", (int)LineEditMenuOption.DirectionLeftToRight).Checked = TextDirection == TextDirection.LeftToRight;
            _directionMenu.AddRadioCheckItem("Right-to-Left", (int)LineEditMenuOption.DirectionRightToLeft).Checked = TextDirection == TextDirection.RightToLeft;
            _controlCharacterMenu.Clear(); _controlCharacterMenu.Font = Font; _controlCharacterMenu.UIFont = UIFont;
            _controlCharacterMenu.AddItem("Left-to-Right Mark (LRM)", (int)LineEditMenuOption.InsertLeftToRightMark);
            _controlCharacterMenu.AddItem("Right-to-Left Mark (RLM)", (int)LineEditMenuOption.InsertRightToLeftMark);
            _controlCharacterMenu.AddItem("Start of Left-to-Right Embedding (LRE)", (int)LineEditMenuOption.InsertLeftToRightEmbedding);
            _controlCharacterMenu.AddItem("Start of Right-to-Left Embedding (RLE)", (int)LineEditMenuOption.InsertRightToLeftEmbedding);
            _controlCharacterMenu.AddItem("Start of Left-to-Right Override (LRO)", (int)LineEditMenuOption.InsertLeftToRightOverride);
            _controlCharacterMenu.AddItem("Start of Right-to-Left Override (RLO)", (int)LineEditMenuOption.InsertRightToLeftOverride);
            _controlCharacterMenu.AddItem("Pop Direction Formatting (PDF)", (int)LineEditMenuOption.InsertPopDirectionFormatting);
            _controlCharacterMenu.AddSeparator();
            _controlCharacterMenu.AddItem("Arabic Letter Mark (ALM)", (int)LineEditMenuOption.InsertArabicLetterMark);
            _controlCharacterMenu.AddItem("Left-to-Right Isolate (LRI)", (int)LineEditMenuOption.InsertLeftToRightIsolate);
            _controlCharacterMenu.AddItem("Right-to-Left Isolate (RLI)", (int)LineEditMenuOption.InsertRightToLeftIsolate);
            _controlCharacterMenu.AddItem("First Strong Isolate (FSI)", (int)LineEditMenuOption.InsertFirstStrongIsolate);
            _controlCharacterMenu.AddItem("Pop Direction Isolate (PDI)", (int)LineEditMenuOption.InsertPopDirectionIsolate);
            _controlCharacterMenu.AddSeparator();
            _controlCharacterMenu.AddItem("Zero-Width Joiner (ZWJ)", (int)LineEditMenuOption.InsertZeroWidthJoiner);
            _controlCharacterMenu.AddItem("Zero-Width Non-Joiner (ZWNJ)", (int)LineEditMenuOption.InsertZeroWidthNonJoiner);
            _controlCharacterMenu.AddItem("Word Joiner (WJ)", (int)LineEditMenuOption.InsertWordJoiner);
            _controlCharacterMenu.AddItem("Soft Hyphen (SHY)", (int)LineEditMenuOption.InsertSoftHyphen);
            _contextMenu.AddItem("Cut", (int)LineEditMenuOption.Cut).Disabled = !Editable;
            _contextMenu.AddItem("Copy", (int)LineEditMenuOption.Copy);
            _contextMenu.AddItem("Paste", (int)LineEditMenuOption.Paste).Disabled = !Editable;
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Select All", (int)LineEditMenuOption.SelectAll);
            _contextMenu.AddItem("Clear", (int)LineEditMenuOption.Clear).Disabled = !Editable;
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Undo", (int)LineEditMenuOption.Undo).Disabled = !Editable || !UndoEnabled || !HasUndo;
            _contextMenu.AddItem("Redo", (int)LineEditMenuOption.Redo).Disabled = !Editable || !UndoEnabled || !HasRedo;
            _contextMenu.AddSeparator();
            _contextMenu.AddSubmenuNodeItem("Text Writing Direction", _directionMenu, (int)LineEditMenuOption.SubmenuTextDirection);
            _contextMenu.AddSeparator();
            _contextMenu.AddCheckItem("Display Control Characters", (int)LineEditMenuOption.DisplayControlCharacters).Checked = DrawControlCharacters;
            _contextMenu.AddSubmenuNodeItem("Insert Control Character", _controlCharacterMenu, (int)LineEditMenuOption.SubmenuInsertControlCharacter).Disabled = !Editable;
            if (_contextMenu.Context != Context) Context.Add(_contextMenu);
            _contextMenu.PopupAt(new Vector2(position.X, position.Y), null);
        }
        internal virtual void DrawEditor(UIRenderContext context)
        {
            context.Fill(Bounds, context.Theme.BackgroundColor); context.Border(Bounds, Context?.FocusedControl == this ? context.Theme.FocusColor : context.Theme.PanelBorderColor);
            if (EffectiveUIFont != null)
            {
                var shown = string.IsNullOrEmpty(Text) && !HasImeComposition ? PlaceholderText : string.IsNullOrEmpty(SecretCharacter) ? GetComposedDisplayText() : new string(SecretCharacter[0], Text.Length);
                var layout = GetEditingLayout(shown);
                if (string.IsNullOrEmpty(Text) && !HasImeComposition) _scrollOffset = 0;
                else EnsureCaretVisible(layout, GetDisplayCaretColumn());
                var viewport = GetTextViewport();
                var origin = GlobalPosition + new Vector2(Padding.Left - _scrollOffset, Padding.Top + GetTextVerticalOffset(layout));
                context.PushClip(viewport);
                try
                {
                    if (HasSelection && !HasImeComposition && string.IsNullOrEmpty(SecretCharacter) && !string.IsNullOrEmpty(Text))
                        foreach (var rectangle in layout.GetSelectionRectangles(SelectionFrom, SelectionTo - SelectionFrom))
                            context.Fill(new Rectangle(
                                (int)MathF.Floor(origin.X + rectangle.X),
                                (int)MathF.Floor(origin.Y + rectangle.Y),
                                Math.Max(1, (int)MathF.Ceiling(rectangle.Width)),
                                Math.Max(1, (int)MathF.Ceiling(rectangle.Height))), context.Theme.AccentColor.WithAlpha(96));
                    if (HasImeComposition && string.IsNullOrEmpty(SecretCharacter))
                        foreach (var rectangle in layout.GetSelectionRectangles(_imeReplaceStart, _imeComposition.Length))
                            context.Fill(new Rectangle(
                                (int)MathF.Floor(origin.X + rectangle.X),
                                (int)MathF.Floor(origin.Y + rectangle.Bottom - 1),
                                Math.Max(1, (int)MathF.Ceiling(rectangle.Width)),
                                1), context.Theme.FocusColor);
                    context.Text(layout, origin, string.IsNullOrEmpty(Text) ? context.Theme.DisabledTextColor : context.Theme.TextColor);
                    if (Context?.FocusedControl == this)
                    {
                        var caret = layout.GetCaretPosition(Math.Min(GetDisplayCaretColumn(), layout.Text.Length));
                        context.Fill(new Rectangle((int)MathF.Round(origin.X + caret.X), (int)MathF.Round(origin.Y + caret.Y), 1, Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont))), context.Theme.FocusColor);
                    }
                }
                finally { context.PopClip(); }
            }
            var clear = GetClearButtonRectangle();
            var clearIcon = GetThemeIcon("clear");
            if (!clear.IsEmpty && clearIcon.HasValue)
            {
                var x = Bounds.X + clear.Center.X - clearIcon.Value.LogicalSize.X / 2;
                var y = Bounds.Y + clear.Center.Y - clearIcon.Value.LogicalSize.Y / 2;
                context.Icon(clearIcon.Value, new Vector2(x, y), Enabled ? Color.White : context.Theme.DisabledTextColor);
            }
        }
        internal Vector2 GetEditorMinimumSize()
        {
            var lineHeight = EffectiveUIFont == null ? 16 : TextMetrics.LineHeight(EffectiveUIFont);
            return Vector2.Max(CustomMinimumSize, new Vector2(64, Math.Max(24, lineHeight + 8)));
        }
        internal TextLayout GetEditingLayout(string text = null)
        {
            text ??= Text;
            if (EffectiveUIFont == null) throw new InvalidOperationException("A font is required for text layout.");
            var direction = TextDirection == TextDirection.Inherited ? TextDirection.Auto : TextDirection;
            return TextMetrics.Layout(EffectiveUIFont, text, new TextLayoutOptions(direction: direction, locale: Language));
        }
        internal float GetTextVerticalOffset(TextLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var spareHeight = MathF.Max(0, Size.Y - Padding.Vertical - layout.Size.Y);
            return TextVerticalAlignment == VerticalAlignment.Center ? spareHeight * .5f
                : TextVerticalAlignment == VerticalAlignment.Bottom ? spareHeight : 0;
        }
        private Rectangle GetTextViewport()
        {
            var clear = GetClearButtonRectangle();
            var right = clear.IsEmpty ? Size.X - Padding.Right : clear.X - 2;
            return new Rectangle((int)MathF.Round(GlobalPosition.X + Padding.Left), (int)MathF.Round(GlobalPosition.Y + Padding.Top), Math.Max(0, (int)MathF.Floor(right - Padding.Left)), Math.Max(0, (int)MathF.Floor(Size.Y - Padding.Vertical)));
        }
        private string GetComposedDisplayText() => HasImeComposition
            ? Text.Remove(_imeReplaceStart, _imeReplaceLength).Insert(_imeReplaceStart, _imeComposition)
            : Text;
        private int GetDisplayCaretColumn() => HasImeComposition
            ? _imeReplaceStart + _imeSelectionStart + _imeSelectionLength
            : CaretColumn;
        private void EnsureCaretVisible(TextLayout layout, int? displayCaretColumn = null)
        {
            if (string.IsNullOrEmpty(Text) && !HasImeComposition) { _scrollOffset = 0; return; }
            var viewportWidth = GetTextViewport().Width;
            if (viewportWidth <= 0) { _scrollOffset = 0; return; }
            var caretX = layout.GetCaretPosition(Math.Min(displayCaretColumn ?? CaretColumn, layout.Text.Length)).X;
            if (caretX < _scrollOffset) _scrollOffset = caretX;
            else if (caretX > _scrollOffset + viewportWidth - 1) _scrollOffset = caretX - viewportWidth + 1;
            _scrollOffset = MathHelper.Clamp(_scrollOffset, 0, Math.Max(0, layout.Size.X - viewportWidth + 1));
        }
    }

    /// <summary>Edits multiline text with wrapping, multiple carets, gutters, syntax highlighting, and undo history.</summary>
    public class TextEdit : LineEdit
    {
        private readonly List<string> _undoStack = new List<string>();
        private readonly List<string> _redoStack = new List<string>();
        private readonly List<uint> _undoVersions = new List<uint>();
        private readonly List<uint> _redoVersions = new List<uint>();
        private readonly Dictionary<int, Color> _lineBackgroundColors = new Dictionary<int, Color>();
        private readonly List<TextEditGutter> _gutters = new List<TextEditGutter>();
        private readonly Dictionary<(int Line, int Gutter), TextEditGutterItem> _lineGutterItems = new Dictionary<(int Line, int Gutter), TextEditGutterItem>();
        private string _historyText = string.Empty;
        private bool _restoringHistory;
        private SyntaxHighlighter _syntaxHighlighter;
        private TextEditLineWrappingMode _lineWrappingMode;
        private int _wrapAtColumn;
        private string _wrapLayoutText = null;
        private float _wrapLayoutWidth = -1;
        private UIFontIdentity _wrapLayoutFontIdentity;
        private float _wrapLayoutFontSize;
        private string _wrapLayoutLanguage = string.Empty;
        private TextDirection _wrapLayoutDirection;
        private readonly Dictionary<int, List<TextEditWrapSegment>> _wrapLayoutCache = new Dictionary<int, List<TextEditWrapSegment>>();
        internal int WrapLayoutBuildCount { get; private set; }
        private readonly List<SecondaryCaret> _secondaryCarets = new List<SecondaryCaret>();
        private readonly PopupMenu _contextMenu;
        private bool _multipleCaretsEnabled = true;
        private bool _updatingMultipleCarets;
        private int _caretMergeSuspension;
        private string _cutCopyLine = string.Empty;
        private TextEditEditAction _currentAction;
        private string _actionStartText = string.Empty;
        private uint _actionStartVersion;
        private bool _actionChanged;
        private uint _version;
        private uint _savedVersion;
        /// <summary>Extra horizontal space reserved by specialized text editors before their editable content.</summary>
        protected virtual float TextContentLeftInset => GetTotalGutterWidth();
        public TextEdit()
        {
            TextChanged += TrackTextChange;
            TextChanged += (_, _) => { if (!_updatingMultipleCarets) _secondaryCarets.Clear(); };
            _contextMenu = new PopupMenu { Visible = false };
            _contextMenu.IdPressed += (_, id) => MenuOption((TextEditMenuOption)id);
            _historyText = Text;
        }
        /// <summary>Compatibility alias for enabling or disabling <see cref="LineWrappingMode"/>.</summary>
        public bool WrapMode { get => LineWrappingMode != TextEditLineWrappingMode.None; set => SetLineWrappingMode(value ? TextEditLineWrappingMode.Boundary : TextEditLineWrappingMode.None); }
        public TextEditLineWrappingMode LineWrappingMode { get => _lineWrappingMode; set => SetLineWrappingMode(value); }
        /// <summary>Optional deterministic wrap width in source columns; zero derives a width from the editor viewport.</summary>
        public int WrapAtColumn { get => _wrapAtColumn; set { var column = Math.Max(0, value); if (_wrapAtColumn == column) return; _wrapAtColumn = column; InvalidateWrapLayout(); QueueLayout(); } }
        public new bool UndoEnabled { get; set; } = true;
        public new int UndoStackMaxSize { get; set; } = 50;
        /// <summary>Enables Control/Command copy, cut, paste, and select-all dispatch, matching Godot's <c>shortcut_keys_enabled</c>.</summary>
        public new bool ShortcutKeysEnabled { get; set; } = true;
        /// <summary>Allows copying/cutting whole caret line ranges when no caret has a selection, matching Godot's <c>empty_selection_clipboard_enabled</c>.</summary>
        public bool EmptySelectionClipboardEnabled { get; set; } = true;
        /// <summary>Optional per-control override used by <see cref="Paste(int)"/> before <see cref="UIContext.Clipboard"/>.</summary>
        public new Func<TextEdit, string> ClipboardTextProvider { get; set; }
        /// <summary>First visible source line, corresponding to Godot's get_first_visible_line().</summary>
        public int FirstVisibleLine { get; private set; }
        /// <summary>Visual wrap-row offset within <see cref="FirstVisibleLine"/>.</summary>
        public int FirstVisibleLineWrapIndex { get; private set; }
        public string SearchText { get; private set; } = string.Empty;
        public TextSearchFlags SearchFlags { get; private set; }
        public new bool HasUndo => _undoStack.Count > 0;
        public new bool HasRedo => _redoStack.Count > 0;
        public TextEditEditAction CurrentAction => _currentAction;
        public int GutterCount => _gutters.Count;
        /// <summary>Enables Godot-style secondary caret creation and multi-caret text input.</summary>
        public bool MultipleCaretsEnabled { get => _multipleCaretsEnabled; set { _multipleCaretsEnabled = value; if (!value) RemoveSecondaryCarets(); } }
        public int CaretCount => 1 + _secondaryCarets.Count;
        public event Action<TextEdit, int, int> GutterClicked;
        /// <summary>Raised after Copy or Cut submits text to <see cref="UIContext.Clipboard"/>.</summary>
        public new event Action<TextEdit, string> CopyRequested;
        /// <summary>Returns the retained context popup, equivalent to Godot's <c>get_menu()</c>.</summary>
        public override PopupMenu GetMenu() => _contextMenu;
        public override bool IsMenuVisible() => _contextMenu.Visible;
        /// <summary>Sets the document-bound syntax provider, analogous to Godot's set_syntax_highlighter().</summary>
        public void SetSyntaxHighlighter(SyntaxHighlighter highlighter)
        {
            if (_syntaxHighlighter == highlighter) return;
            if (_syntaxHighlighter != null)
            {
                _syntaxHighlighter.Changed -= SyntaxHighlighterChanged;
                _syntaxHighlighter.SetTextEdit(null);
            }
            _syntaxHighlighter = highlighter;
            if (_syntaxHighlighter != null)
            {
                _syntaxHighlighter.SetTextEdit(this);
                _syntaxHighlighter.Changed += SyntaxHighlighterChanged;
            }
            QueueLayout();
        }
        public SyntaxHighlighter GetSyntaxHighlighter() => _syntaxHighlighter;
        /// <summary>Gets the retained highlighter output for a line. Empty when no provider is assigned.</summary>
        public IReadOnlyList<SyntaxHighlightSpan> GetLineSyntaxHighlighting(int line)
        {
            ValidateLine(line);
            return _syntaxHighlighter?.GetLineSyntaxHighlighting(line) ?? Array.Empty<SyntaxHighlightSpan>();
        }
        public int CaretLine
        {
            get => GetLineForIndex(CaretColumn);
            set => SetCaret(value, CaretColumnInLine);
        }
        public int CaretColumnInLine => CaretColumn - GetLineStart(CaretLine);
        public int LineCount => string.IsNullOrEmpty(Text) ? 1 : Text.Split('\n').Length;
        public string GetLine(int line)
        {
            if (line < 0 || line >= LineCount) throw new ArgumentOutOfRangeException(nameof(line));
            var start = GetLineStart(line);
            var end = Text.IndexOf('\n', start);
            return end < 0 ? Text.Substring(start) : Text.Substring(start, end - start);
        }
        public void SetCaret(int line, int column) => SetCaret(line, column, 0);
        public void SetCaret(int line, int column, int caret)
        {
            line = MathHelper.Clamp(line, 0, LineCount - 1);
            var index = GetLineStart(line) + MathHelper.Clamp(column, 0, GetLine(line).Length);
            if (caret == 0) { CaretColumn = index; Deselect(); RequestCaretMerge(); return; }
            var secondary = GetSecondaryCaret(caret); secondary.Index = index; secondary.Anchor = -1; RequestCaretMerge();
        }
        public void SetMultipleCaretsEnabled(bool enabled) => MultipleCaretsEnabled = enabled;
        public bool IsMultipleCaretsEnabled() => MultipleCaretsEnabled;
        /// <summary>Adds a non-overlapping secondary caret and returns its Godot-compatible index, or -1 when unavailable.</summary>
        public int AddCaret(int line, int column)
        {
            if (!MultipleCaretsEnabled) return -1;
            line = MathHelper.Clamp(line, 0, LineCount - 1); var index = GetLineStart(line) + MathHelper.Clamp(column, 0, GetLine(line).Length);
            if (CaretContainsIndex(0, index) || _secondaryCarets.Exists(caret => CaretContainsIndex(caret, index))) return -1;
            _secondaryCarets.Add(new SecondaryCaret { Index = index, Anchor = -1 }); return CaretCount - 1;
        }
        public void RemoveCaret(int caret)
        {
            if (caret <= 0 || caret >= CaretCount) throw new ArgumentOutOfRangeException(nameof(caret), "The primary caret cannot be removed.");
            _secondaryCarets.RemoveAt(caret - 1);
        }
        public void RemoveSecondaryCarets() => _secondaryCarets.Clear();
        /// <summary>Merges caret points or selections that overlap, following Godot's inclusive overlap rule.</summary>
        public void MergeOverlappingCarets()
        {
            if (_secondaryCarets.Count == 0) return;
            var entries = new List<CaretMerge>(CaretCount);
            for (var caret = 0; caret < CaretCount; caret++) entries.Add(new CaretMerge { Caret = caret, From = GetSelectionFromIndex(caret), To = GetSelectionToIndex(caret) });
            entries.Sort((left, right) => { var comparison = left.From.CompareTo(right.From); return comparison != 0 ? comparison : left.To.CompareTo(right.To); });
            var mergedGroups = new List<CaretMerge>(); var index = 0;
            while (index < entries.Count)
            {
                var group = entries[index]; var containsPrimary = group.Caret == 0; index++;
                while (index < entries.Count && entries[index].From <= group.To)
                {
                    group.To = Math.Max(group.To, entries[index].To); containsPrimary |= entries[index].Caret == 0; index++;
                }
                group.ContainsPrimary = containsPrimary; mergedGroups.Add(group);
            }
            CaretMerge primary = null;
            foreach (var group in mergedGroups) if (group.ContainsPrimary) { primary = group; break; }
            if (primary == null) return;
            if (primary.From == primary.To) { CaretColumn = primary.From; Deselect(); }
            else base.Select(primary.From, primary.To);
            _secondaryCarets.Clear();
            foreach (var group in mergedGroups)
            {
                if (group.ContainsPrimary) continue;
                _secondaryCarets.Add(new SecondaryCaret { Index = group.To, Anchor = group.From == group.To ? -1 : group.From });
            }
        }
        public int GetCaretCount() => CaretCount;
        public int GetCaretLine(int caret = 0) => GetLineForIndex(GetCaretIndex(caret));
        public int GetCaretColumn(int caret = 0) => GetCaretIndex(caret) - GetLineStart(GetCaretLine(caret));
        public void SetCaretLine(int line, int caret = 0) => SetCaret(line, GetCaretColumn(caret), caret);
        public void SetCaretColumn(int column, int caret = 0) => SetCaret(GetCaretLine(caret), column, caret);
        /// <summary>Returns selection state for a specific caret without shadowing LineEdit's primary <see cref="LineEdit.HasSelection"/> property.</summary>
        public bool HasCaretSelection(int caret) => caret == 0 ? base.HasSelection : GetSecondaryCaret(caret).Anchor >= 0 && GetSecondaryCaret(caret).Anchor != GetSecondaryCaret(caret).Index;
        public int GetSelectionOriginLine(int caret = 0) => GetLineForIndex(GetSelectionOriginIndex(caret));
        public int GetSelectionOriginColumn(int caret = 0) { var origin = GetSelectionOriginIndex(caret); return origin - GetLineStart(GetLineForIndex(origin)); }
        public int GetSelectionFrom(int caret) => GetSelectionFromIndex(caret);
        public int GetSelectionTo(int caret) => GetSelectionToIndex(caret);
        public void Select(int fromLine, int fromColumn, int toLine, int toColumn, int caret = 0)
        {
            fromLine = MathHelper.Clamp(fromLine, 0, LineCount - 1); toLine = MathHelper.Clamp(toLine, 0, LineCount - 1);
            var from = GetLineStart(fromLine) + MathHelper.Clamp(fromColumn, 0, GetLine(fromLine).Length);
            var to = GetLineStart(toLine) + MathHelper.Clamp(toColumn, 0, GetLine(toLine).Length);
            if (caret == 0) base.Select(from, to);
            else { var secondary = GetSecondaryCaret(caret); secondary.Anchor = from; secondary.Index = to; }
            RequestCaretMerge();
        }
        /// <summary>Selects the word at one caret, or toggles the existing selection off, matching Godot's <c>select_word_under_caret</c>.</summary>
        public void SelectWordUnderCaret(int caret = -1)
        {
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            var first = caret < 0 ? 0 : caret; var last = caret < 0 ? CaretCount - 1 : caret;
            _caretMergeSuspension++;
            try
            {
                for (var index = first; index <= last; index++)
                {
                    if (HasCaretSelection(index)) { Deselect(index); continue; }
                    if (!TryGetWordRange(index, out var line, out var from, out var to)) continue;
                    Select(line, from, line, to, index);
                }
            }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        /// <summary>Adds a selected secondary caret at the next case-sensitive occurrence of the final caret's selected word.</summary>
        public void AddSelectionForNextOccurrence()
        {
            if (!MultipleCaretsEnabled || string.IsNullOrEmpty(Text)) return;
            var caret = CaretCount - 1;
            if (!HasCaretSelection(caret)) { SelectWordUnderCaret(caret); return; }
            var selected = GetSelectedText(caret); if (string.IsNullOrEmpty(selected)) return;
            var from = GetSelectionFromIndex(caret); var line = GetLineForIndex(from); var column = from - GetLineStart(line) + 1;
            var occurrence = Search(selected, TextSearchFlags.MatchCase, line, column);
            if (occurrence.X < 0 || occurrence.Y < 0) return;
            var added = AddCaret(occurrence.Y, occurrence.X + selected.Length);
            if (added >= 0) Select(occurrence.Y, occurrence.X, occurrence.Y, occurrence.X + selected.Length, added);
        }
        /// <summary>Moves the final occurrence selection to its next match, matching Godot's <c>skip_selection_for_next_occurrence</c>.</summary>
        public void SkipSelectionForNextOccurrence()
        {
            if (string.IsNullOrEmpty(Text)) return;
            var caret = CaretCount - 1; var selected = HasCaretSelection(caret) ? GetSelectedText(caret) : GetWordAtCaret(caret);
            if (string.IsNullOrEmpty(selected)) return;
            var from = HasCaretSelection(caret) ? GetSelectionFromIndex(caret) : GetCaretIndex(caret); var line = GetLineForIndex(from); var column = from - GetLineStart(line) + 1;
            var occurrence = Search(selected, TextSearchFlags.MatchCase, line, column);
            if (occurrence.X < 0 || occurrence.Y < 0) return;
            if (caret == 0)
            {
                Deselect(0); Select(occurrence.Y, occurrence.X, occurrence.Y, occurrence.X + selected.Length); return;
            }
            var added = AddCaret(occurrence.Y, occurrence.X + selected.Length);
            if (added < 0) return;
            Select(occurrence.Y, occurrence.X, occurrence.Y, occurrence.X + selected.Length, added); RemoveCaret(caret);
        }
        public IReadOnlyList<TextEditCaret> GetCarets()
        {
            var carets = new List<TextEditCaret>(CaretCount); for (var caret = 0; caret < CaretCount; caret++) carets.Add(new TextEditCaret(GetCaretLine(caret), GetCaretColumn(caret), GetSelectionOriginLine(caret), GetSelectionOriginColumn(caret))); return carets;
        }
        /// <summary>Returns screen-space visual-row rectangles for a caret selection, including wrapped source rows.</summary>
        public IReadOnlyList<Rectangle> GetSelectionRectangles(int caret = 0)
        {
            if (caret < 0 || caret >= CaretCount) throw new ArgumentOutOfRangeException(nameof(caret));
            var rectangles = new List<Rectangle>(); if (!HasCaretSelection(caret)) return rectangles;
            var from = GetSelectionFromIndex(caret); var to = GetSelectionToIndex(caret); var fromLine = GetLineForIndex(from); var toLine = GetLineForIndex(to); var lineHeight = EffectiveUIFont == null ? 16 : Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont));
            for (var line = fromLine; line <= toLine; line++)
            {
                if (IsLineHiddenForDisplay(line)) continue;
                var sourceStart = GetLineStart(line); var source = GetLine(line);
                for (var wrap = 0; wrap <= GetLineWrapCount(line); wrap++)
                {
                    var localSegmentStart = GetLineWrapStartColumn(line, wrap); var segmentStart = sourceStart + localSegmentStart; var segmentLength = GetLineWrapLength(line, wrap); var segmentEnd = segmentStart + segmentLength;
                    var start = Math.Max(from, segmentStart); var end = Math.Min(to, segmentEnd); if (end <= start) continue;
                    var x = GlobalPosition.X + Padding.Left + TextContentLeftInset; var y = GlobalPosition.Y + Padding.Top + GetVisibleRow(line, wrap) * lineHeight;
                    if (EffectiveUIFont == null)
                    {
                        var prefix = source.Substring(localSegmentStart, start - segmentStart);
                        var selected = source.Substring(start - sourceStart, end - start);
                        rectangles.Add(new Rectangle((int)(x + MeasureTextWidth(prefix)), (int)y, Math.Max(1, (int)MathF.Ceiling(MeasureTextWidth(selected))), lineHeight));
                        continue;
                    }
                    var layout = GetSegmentLayout(source, localSegmentStart, segmentLength);
                    foreach (var rectangle in layout.GetSelectionRectangles(start - segmentStart, end - start))
                        rectangles.Add(new Rectangle((int)MathF.Floor(x + rectangle.X), (int)MathF.Floor(y + rectangle.Y), Math.Max(1, (int)MathF.Ceiling(rectangle.Width)), Math.Max(1, (int)MathF.Ceiling(rectangle.Height))));
                }
            }
            return rectangles;
        }
        /// <summary>Adds one caret above or below every existing caret where the adjacent visible source line exists.</summary>
        public void AddCaretAtCarets(bool below)
        {
            if (!MultipleCaretsEnabled) return;
            var existing = GetCarets();
            foreach (var caret in existing)
            {
                var line = caret.Line + (below ? 1 : -1); if (line < 0 || line >= LineCount || IsLineHiddenForDisplay(line)) continue;
                AddCaret(line, caret.Column);
            }
            MergeOverlappingCarets();
        }
        public override void InsertText(string text)
        {
            if (!Editable || string.IsNullOrEmpty(text) || _secondaryCarets.Count == 0) { base.InsertText(text); return; }
            var edits = new List<CaretEdit>();
            for (var caret = 0; caret < CaretCount; caret++) edits.Add(new CaretEdit { Caret = caret, Start = GetSelectionFromIndex(caret), End = GetSelectionToIndex(caret) });
            ApplyMultiCaretEdits(edits, text);
        }
        public void InsertNewline() => InsertText("\n");
        /// <summary>Inserts text at one caret, or all carets when <paramref name="caret"/> is -1, matching Godot's <c>insert_text_at_caret</c>.</summary>
        public void InsertTextAtCaret(string text, int caret = -1)
        {
            if (!Editable || string.IsNullOrEmpty(text)) return;
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            if (caret < 0) { InsertText(text); return; }
            var edits = new List<CaretEdit>();
            for (var index = 0; index < CaretCount; index++)
            {
                var target = index == caret; var start = target ? GetSelectionFromIndex(index) : GetCaretIndex(index); var end = target ? GetSelectionToIndex(index) : start;
                edits.Add(new CaretEdit { Caret = index, Start = start, End = end });
            }
            ApplyMultiCaretEdits(edits, edit => edit.Caret == caret ? text : string.Empty);
        }
        /// <summary>Inserts text at a source line and column, retaining all caret and selection positions relative to the inserted range.</summary>
        public void InsertText(string text, int line, int column, bool beforeSelectionBegin = true, bool beforeSelectionEnd = false)
        {
            if (!Editable || string.IsNullOrEmpty(text)) return;
            line = MathHelper.Clamp(line, 0, LineCount - 1); var index = GetLineStart(line) + MathHelper.Clamp(column, 0, GetLine(line).Length);
            var states = CaptureCaretStates();
            foreach (var state in states)
            {
                if (state.Index > index || state.Index == index && beforeSelectionEnd) state.Index += text.Length;
                if (state.Anchor >= 0 && (state.Anchor > index || state.Anchor == index && beforeSelectionBegin)) state.Anchor += text.Length;
            }
            _updatingMultipleCarets = true;
            try { Text = Text.Insert(index, text); RestoreCaretStates(states); }
            finally { _updatingMultipleCarets = false; }
        }
        /// <summary>Removes a source range, retaining all carets and selections at the collapsed range start where necessary.</summary>
        public void RemoveText(int fromLine, int fromColumn, int toLine, int toColumn)
        {
            fromLine = MathHelper.Clamp(fromLine, 0, LineCount - 1); toLine = MathHelper.Clamp(toLine, 0, LineCount - 1);
            var from = GetLineStart(fromLine) + MathHelper.Clamp(fromColumn, 0, GetLine(fromLine).Length); var to = GetLineStart(toLine) + MathHelper.Clamp(toColumn, 0, GetLine(toLine).Length);
            if (to < from) { var swap = from; from = to; to = swap; }
            if (to == from) return;
            var states = CaptureCaretStates();
            foreach (var state in states) { state.Index = CollapseTextIndex(state.Index, from, to); if (state.Anchor >= 0) state.Anchor = CollapseTextIndex(state.Anchor, from, to); }
            _updatingMultipleCarets = true;
            try { Text = Text.Remove(from, to - from); RestoreCaretStates(states); }
            finally { _updatingMultipleCarets = false; }
        }
        /// <summary>Deletes selections at one caret, or every selected caret when <paramref name="caret"/> is -1.</summary>
        public void DeleteSelection(int caret)
        {
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            if (caret >= 0 ? HasCaretSelection(caret) : HasAnyCaretSelection()) DeleteCaretSelections(caret);
        }
        public void DeleteAllSelections() => DeleteSelection(-1);
        /// <summary>Clears selection state at one caret, or all carets when <paramref name="caret"/> is -1.</summary>
        public void Deselect(int caret)
        {
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            if (caret <= 0) base.Deselect();
            if (caret < 0) for (var index = 1; index < CaretCount; index++) GetSecondaryCaret(index).Anchor = -1;
            else if (caret > 0) GetSecondaryCaret(caret).Anchor = -1;
        }
        public void DeselectAll() => Deselect(-1);
        /// <summary>Returns leading indentation measured with Godot-compatible tab width of four columns.</summary>
        public int GetIndentLevel(int line)
        {
            var indent = 0; foreach (var character in GetLine(line)) { if (character == ' ') indent++; else if (character == '\t') indent += 4; else break; } return indent;
        }
        /// <summary>Returns the first non-whitespace source column, or the line length when a line is blank.</summary>
        public int GetFirstNonWhitespaceColumn(int line)
        {
            var text = GetLine(line); var column = 0; while (column < text.Length && char.IsWhiteSpace(text[column])) column++; return column;
        }
        /// <summary>Swaps two source lines while retaining carets, selections, line backgrounds, and gutter items with their original line content.</summary>
        public void SwapLines(int fromLine, int toLine)
        {
            ValidateLine(fromLine); ValidateLine(toLine); if (fromLine == toLine) return;
            var caretStates = new List<CaretLineState>();
            for (var caret = 0; caret < CaretCount; caret++) caretStates.Add(new CaretLineState { Line = GetCaretLine(caret), Column = GetCaretColumn(caret), OriginLine = GetSelectionOriginLine(caret), OriginColumn = GetSelectionOriginColumn(caret), Selected = HasCaretSelection(caret) });
            var lines = GetLines(); var swap = lines[fromLine]; lines[fromLine] = lines[toLine]; lines[toLine] = swap;
            SwapLineMetadata(fromLine, toLine);
            _updatingMultipleCarets = true;
            try { Text = string.Join("\n", lines); RestoreCaretLineStates(caretStates, fromLine, toLine); }
            finally { _updatingMultipleCarets = false; }
        }
        public new void SetShortcutKeysEnabled(bool enabled) => ShortcutKeysEnabled = enabled;
        public new bool IsShortcutKeysEnabled() => ShortcutKeysEnabled;
        public void SetEmptySelectionClipboardEnabled(bool enabled) => EmptySelectionClipboardEnabled = enabled;
        public bool IsEmptySelectionClipboardEnabled() => EmptySelectionClipboardEnabled;
        /// <summary>Returns Godot-style selected text, or newline-joined selected fragments from all carets when <paramref name="caret"/> is -1.</summary>
        public string GetSelectedText(int caret = -1)
        {
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            if (caret >= 0) return HasCaretSelection(caret) ? Text.Substring(GetSelectionFromIndex(caret), GetSelectionToIndex(caret) - GetSelectionFromIndex(caret)) : string.Empty;
            var caretIndexes = GetSortedCaretIndexes(); var fragments = new List<string>();
            foreach (var index in caretIndexes) if (HasCaretSelection(index)) fragments.Add(Text.Substring(GetSelectionFromIndex(index), GetSelectionToIndex(index) - GetSelectionFromIndex(index)));
            return string.Join("\n", fragments);
        }
        /// <summary>Requests a host clipboard write using Godot's selection-or-whole-line copy policy.</summary>
        public void Copy(int caret = -1)
        {
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            var copied = GetSelectedText(caret);
            if (!string.IsNullOrEmpty(copied))
            {
                _cutCopyLine = string.Empty; WriteClipboard(copied); return;
            }
            if (!EmptySelectionClipboardEnabled) return;
            var ranges = GetCaretLineRanges(caret); var lines = new System.Text.StringBuilder();
            foreach (var range in ranges) for (var line = range.First; line <= range.Last; line++) { lines.Append(GetLine(line)); lines.Append('\n'); }
            copied = lines.ToString(); _cutCopyLine = CaretCount == 1 ? copied : string.Empty;
            if (!string.IsNullOrEmpty(copied)) WriteClipboard(copied);
        }
        /// <summary>Copies text using <see cref="Copy"/> then removes selections or whole caret line ranges when editable.</summary>
        public void Cut(int caret = -1)
        {
            Copy(caret); if (!Editable) return;
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            if (caret >= 0 ? HasCaretSelection(caret) : HasAnyCaretSelection()) { DeleteCaretSelections(caret); return; }
            if (!EmptySelectionClipboardEnabled) return;
            DeleteCaretLines(caret);
        }
        /// <summary>Obtains host clipboard content and applies Godot's multi-caret paste distribution policy.</summary>
        public void Paste(int caret = -1) => Paste(ClipboardTextProvider?.Invoke(this) ?? Context?.Clipboard?.GetText(), caret);
        /// <summary>Pastes supplied clipboard content at one caret or all carets. A line per caret is used when the counts match.</summary>
        public void Paste(string clipboard, int caret = -1)
        {
            if (!Editable || string.IsNullOrEmpty(clipboard)) return;
            if (caret >= CaretCount || caret < -1) throw new ArgumentOutOfRangeException(nameof(caret));
            clipboard = clipboard.Replace("\r", string.Empty);
            if (CaretCount == 1 && (caret < 0 || caret == 0) && !HasCaretSelection(0) && !string.IsNullOrEmpty(_cutCopyLine) && string.Equals(_cutCopyLine, clipboard, StringComparison.Ordinal))
            {
                var originalLine = CaretLine; var originalColumn = CaretColumnInLine; Text = Text.Insert(GetLineStart(originalLine), clipboard); SetCaret(originalLine + clipboard.Split('\n').Length - 1, originalColumn); return;
            }
            var carets = caret < 0 ? GetSortedCaretIndexes() : new List<int> { caret };
            var clipboardLines = clipboard.Split('\n'); var distributeLines = caret < 0 && CaretCount > 1 && clipboardLines.Length == CaretCount;
            var replacements = new Dictionary<int, string>();
            for (var index = 0; index < CaretCount; index++) replacements[index] = string.Empty;
            for (var index = 0; index < carets.Count; index++) replacements[carets[index]] = distributeLines ? clipboardLines[index] : clipboard;
            var edits = new List<CaretEdit>();
            for (var index = 0; index < CaretCount; index++)
            {
                var target = carets.Contains(index); var start = target ? GetSelectionFromIndex(index) : GetCaretIndex(index); var end = target ? GetSelectionToIndex(index) : start;
                edits.Add(new CaretEdit { Caret = index, Start = start, End = end });
            }
            foreach (var index in carets) replacements[index] = distributeLines ? replacements[index] : clipboard;
            ApplyMultiCaretEdits(edits, edit => replacements[edit.Caret]);
        }
        private void WriteClipboard(string text)
        {
            Context?.Clipboard?.SetText(text);
            CopyRequested?.Invoke(this, text);
        }
        /// <summary>Clears the retained document and secondary carets, matching Godot's <c>clear</c>.</summary>
        public new void Clear()
        {
            if (!Editable) return;
            Text = string.Empty; RemoveSecondaryCarets(); SetCaret(0, 0);
        }
        /// <summary>Selects the entire document with the primary caret, matching Godot's <c>select_all</c>.</summary>
        public new void SelectAll()
        {
            RemoveSecondaryCarets(); base.SelectAll();
        }
        /// <summary>Executes one retained context-menu command.</summary>
        public void MenuOption(TextEditMenuOption option)
        {
            switch (option)
            {
                case TextEditMenuOption.Cut: Cut(); break;
                case TextEditMenuOption.Copy: Copy(); break;
                case TextEditMenuOption.Paste: Paste(); break;
                case TextEditMenuOption.Clear: Clear(); break;
                case TextEditMenuOption.SelectAll: SelectAll(); break;
                case TextEditMenuOption.Undo: Undo(); break;
                case TextEditMenuOption.Redo: Redo(); break;
                case TextEditMenuOption.DirectionInherited: SetTextDirection(TextDirection.Inherited); break;
                case TextEditMenuOption.DirectionAuto: SetTextDirection(TextDirection.Auto); break;
                case TextEditMenuOption.DirectionLeftToRight: SetTextDirection(TextDirection.LeftToRight); break;
                case TextEditMenuOption.DirectionRightToLeft: SetTextDirection(TextDirection.RightToLeft); break;
                case TextEditMenuOption.DisplayControlCharacters: SetDrawControlChars(!GetDrawControlChars()); break;
                case TextEditMenuOption.InsertLeftToRightMark: InsertText("\u200E"); break;
                case TextEditMenuOption.InsertRightToLeftMark: InsertText("\u200F"); break;
                case TextEditMenuOption.InsertLeftToRightEmbedding: InsertText("\u202A"); break;
                case TextEditMenuOption.InsertRightToLeftEmbedding: InsertText("\u202B"); break;
                case TextEditMenuOption.InsertLeftToRightOverride: InsertText("\u202D"); break;
                case TextEditMenuOption.InsertRightToLeftOverride: InsertText("\u202E"); break;
                case TextEditMenuOption.InsertPopDirectionFormatting: InsertText("\u202C"); break;
                case TextEditMenuOption.InsertArabicLetterMark: InsertText("\u061C"); break;
                case TextEditMenuOption.InsertLeftToRightIsolate: InsertText("\u2066"); break;
                case TextEditMenuOption.InsertRightToLeftIsolate: InsertText("\u2067"); break;
                case TextEditMenuOption.InsertFirstStrongIsolate: InsertText("\u2068"); break;
                case TextEditMenuOption.InsertPopDirectionIsolate: InsertText("\u2069"); break;
                case TextEditMenuOption.InsertZeroWidthJoiner: InsertText("\u200D"); break;
                case TextEditMenuOption.InsertZeroWidthNonJoiner: InsertText("\u200C"); break;
                case TextEditMenuOption.InsertWordJoiner: InsertText("\u2060"); break;
                case TextEditMenuOption.InsertSoftHyphen: InsertText("\u00AD"); break;
            }
        }
        public void SetLine(int line, string text)
        {
            var lines = GetLines(); ValidateLine(line); lines[line] = text ?? string.Empty; Text = string.Join("\n", lines); SetCaret(line, Math.Min(CaretColumnInLine, lines[line].Length));
        }
        public void InsertLineAt(int line, string text)
        {
            var lines = GetLines(); if (line < 0 || line > lines.Count) throw new ArgumentOutOfRangeException(nameof(line)); ShiftLineBackgrounds(line, 1); lines.Insert(line, text ?? string.Empty); Text = string.Join("\n", lines); SetCaret(line, 0);
        }
        public void RemoveLineAt(int line)
        {
            var lines = GetLines(); ValidateLine(line); if (lines.Count == 1) lines[0] = string.Empty; else { lines.RemoveAt(line); _lineBackgroundColors.Remove(line); RemoveLineGutterItems(line); ShiftLineBackgrounds(line + 1, -1); } Text = string.Join("\n", lines); SetCaret(Math.Min(line, lines.Count - 1), 0);
        }
        public void SetLineBackgroundColor(int line, Color color) { ValidateLine(line); _lineBackgroundColors[line] = color; }
        public void ClearLineBackgroundColor(int line) { ValidateLine(line); _lineBackgroundColors.Remove(line); }
        public Color? GetLineBackgroundColor(int line) { ValidateLine(line); return _lineBackgroundColors.TryGetValue(line, out var color) ? color : (Color?)null; }
        public void SetSearchText(string text) => SearchText = text ?? string.Empty;
        public void SetSearchFlags(TextSearchFlags flags) => SearchFlags = flags;
        public void SetLineWrappingMode(TextEditLineWrappingMode mode)
        {
            if (mode != TextEditLineWrappingMode.None && mode != TextEditLineWrappingMode.Boundary) throw new ArgumentOutOfRangeException(nameof(mode));
            if (_lineWrappingMode == mode) return;
            _lineWrappingMode = mode; InvalidateWrapLayout(); QueueLayout();
        }
        public TextEditLineWrappingMode GetLineWrappingMode() => _lineWrappingMode;
        public bool IsLineWrapped(int line) { ValidateLine(line); return GetWrapSegments(line).Count > 1; }
        /// <summary>Returns the number of continuation rows after the first visual row for a source line.</summary>
        public int GetLineWrapCount(int line) { ValidateLine(line); return Math.Max(0, GetWrapSegments(line).Count - 1); }
        public int GetLineWrapIndexAtColumn(int line, int column)
        {
            ValidateLine(line); column = MathHelper.Clamp(column, 0, GetLine(line).Length);
            var segments = GetWrapSegments(line);
            for (var index = 0; index < segments.Count; index++) if (column < segments[index].Start + segments[index].Length || index == segments.Count - 1) return index;
            return 0;
        }
        public IReadOnlyList<string> GetLineWrappedText(int line)
        {
            ValidateLine(line); var source = GetLine(line); var result = new List<string>();
            foreach (var segment in GetWrapSegments(line)) result.Add(source.Substring(segment.Start, segment.Length));
            return result;
        }
        /// <summary>Returns the width of a source line or one of its visual wrap rows.</summary>
        public float GetLineWidth(int line, int wrapIndex = -1)
        {
            ValidateLine(line); var source = GetLine(line);
            if (wrapIndex < 0) return EffectiveUIFont == null ? source.Length * 8 : TextMetrics.Measure(EffectiveUIFont, source).X;
            var segments = GetWrapSegments(line); if (wrapIndex >= segments.Count) throw new ArgumentOutOfRangeException(nameof(wrapIndex));
            var segment = segments[wrapIndex]; var text = source.Substring(segment.Start, segment.Length);
            return EffectiveUIFont == null ? text.Length * 8 : TextMetrics.Measure(EffectiveUIFont, text).X;
        }
        public void AddGutter(int at = -1)
        {
            if (at < 0) at = _gutters.Count;
            if (at < 0 || at > _gutters.Count) throw new ArgumentOutOfRangeException(nameof(at));
            _gutters.Insert(at, new TextEditGutter()); ShiftGutterItems(at, 1); QueueLayout();
        }
        public void RemoveGutter(int gutter)
        {
            ValidateGutter(gutter); _gutters.RemoveAt(gutter); ShiftGutterItems(gutter + 1, -1, gutter); QueueLayout();
        }
        public string GetGutterName(int gutter) => GetGutter(gutter).Name;
        public void SetGutterName(int gutter, string name) => GetGutter(gutter).Name = name ?? string.Empty;
        public TextEditGutterType GetGutterType(int gutter) => GetGutter(gutter).Type;
        public void SetGutterType(int gutter, TextEditGutterType type) => GetGutter(gutter).Type = type;
        public int GetGutterWidth(int gutter) => GetGutter(gutter).Width;
        public void SetGutterWidth(int gutter, int width) { GetGutter(gutter).Width = Math.Max(0, width); QueueLayout(); }
        public bool IsGutterDrawn(int gutter) => GetGutter(gutter).Draw;
        public void SetGutterDraw(int gutter, bool draw) { GetGutter(gutter).Draw = draw; QueueLayout(); }
        public bool IsGutterClickable(int gutter) => GetGutter(gutter).Clickable;
        public void SetGutterClickable(int gutter, bool clickable) => GetGutter(gutter).Clickable = clickable;
        public bool IsGutterOverwritable(int gutter) => GetGutter(gutter).Overwritable;
        public void SetGutterOverwritable(int gutter, bool overwritable) => GetGutter(gutter).Overwritable = overwritable;
        public void SetGutterCustomDraw(int gutter, Action<UIRenderContext, TextEdit, int, Rectangle> draw) { var state = GetGutter(gutter); state.Type = TextEditGutterType.Custom; state.CustomDraw = draw; }
        public int GetTotalGutterWidth() { var width = 0; foreach (var gutter in _gutters) if (gutter.Draw) width += gutter.Width; return width; }
        public void SetLineGutterMetadata(int line, int gutter, object metadata) => GetLineGutterItem(line, gutter, true).Metadata = metadata;
        public object GetLineGutterMetadata(int line, int gutter) => GetLineGutterItem(line, gutter, false)?.Metadata;
        public void SetLineGutterText(int line, int gutter, string text) => GetLineGutterItem(line, gutter, true).Text = text ?? string.Empty;
        public string GetLineGutterText(int line, int gutter) => GetLineGutterItem(line, gutter, false)?.Text ?? string.Empty;
        public void SetLineGutterIcon(int line, int gutter, Texture2D icon) => GetLineGutterItem(line, gutter, true).Icon = icon;
        public Texture2D GetLineGutterIcon(int line, int gutter) => GetLineGutterItem(line, gutter, false)?.Icon;
        public void SetLineGutterItemColor(int line, int gutter, Color color) => GetLineGutterItem(line, gutter, true).Color = color;
        public Color GetLineGutterItemColor(int line, int gutter) => GetLineGutterItem(line, gutter, false)?.Color ?? Color.White;
        public void SetLineGutterClickable(int line, int gutter, bool clickable) => GetLineGutterItem(line, gutter, true).Clickable = clickable;
        public bool IsLineGutterClickable(int line, int gutter) => GetLineGutterItem(line, gutter, false)?.Clickable ?? false;
        /// <summary>Returns a Godot-compatible (column, line) point, or (-1, -1) when not found.</summary>
        public Point Search(string key, TextSearchFlags flags, int fromLine = 0, int fromColumn = 0)
        {
            if (string.IsNullOrEmpty(key)) return new Point(-1, -1);
            var comparison = flags.HasFlag(TextSearchFlags.MatchCase) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var lines = GetLines(); fromLine = MathHelper.Clamp(fromLine, 0, lines.Count - 1);
            if (flags.HasFlag(TextSearchFlags.Backwards))
            {
                for (var line = fromLine; line >= 0; line--)
                {
                    var start = (line == fromLine ? Math.Min(fromColumn, lines[line].Length) : lines[line].Length) - 1;
                    while (start >= 0)
                    {
                        var column = lines[line].LastIndexOf(key, start, comparison);
                        if (column < 0) break;
                        if (!flags.HasFlag(TextSearchFlags.WholeWords) || IsWholeWord(lines[line], column, key.Length)) return new Point(column, line);
                        start = column - 1;
                    }
                }
            }
            else
            {
                for (var line = fromLine; line < lines.Count; line++)
                {
                    var start = line == fromLine ? MathHelper.Clamp(fromColumn, 0, lines[line].Length) : 0;
                    for (var column = lines[line].IndexOf(key, start, comparison); column >= 0; column = lines[line].IndexOf(key, column + 1, comparison)) if (!flags.HasFlag(TextSearchFlags.WholeWords) || IsWholeWord(lines[line], column, key.Length)) return new Point(column, line);
                }
            }
            return new Point(-1, -1);
        }
        public new void Undo()
        {
            if (!UndoEnabled || _undoStack.Count == 0) return;
            _redoStack.Add(Text); _redoVersions.Add(_version); var last = _undoStack.Count - 1; RestoreHistory(_undoStack[last], _undoVersions[last]); _undoStack.RemoveAt(last); _undoVersions.RemoveAt(last);
        }
        public new void Redo()
        {
            if (!UndoEnabled || _redoStack.Count == 0) return;
            _undoStack.Add(Text); _undoVersions.Add(_version); var last = _redoStack.Count - 1; RestoreHistory(_redoStack[last], _redoVersions[last]); _redoStack.RemoveAt(last); _redoVersions.RemoveAt(last);
        }
        public new void ClearUndoHistory() { _undoStack.Clear(); _redoStack.Clear(); _undoVersions.Clear(); _redoVersions.Clear(); _historyText = Text; }
        /// <summary>Begins a Godot-style edit action; all edits until <see cref="EndAction"/> become one undo step.</summary>
        public void StartAction(TextEditEditAction action)
        {
            if (action == TextEditEditAction.None) { EndAction(); return; }
            if (_currentAction == action) return;
            EndAction(); _currentAction = action; _actionStartText = Text; _actionStartVersion = _version; _actionChanged = false;
        }
        /// <summary>Finishes the active action and commits its grouped undo/version entry.</summary>
        public void EndAction()
        {
            if (_currentAction == TextEditEditAction.None) return;
            if (_actionChanged)
            {
                if (UndoEnabled) PushUndoState(_actionStartText, _actionStartVersion);
                _version = _actionStartVersion + 1;
            }
            _currentAction = TextEditEditAction.None; _actionChanged = false;
        }
        public TextEditEditAction GetCurrentAction() => _currentAction;
        public void TagSavedVersion() => _savedVersion = _version;
        public uint GetVersion() => _version;
        public uint GetSavedVersion() => _savedVersion;
        /// <summary>Returns the visual-row scroll position for a source line and wrap row.</summary>
        public int GetScrollPosForLine(int line, int wrapIndex = 0) => GetVisualRowOffset(line, wrapIndex);
        public int GetFirstVisibleLine() => FirstVisibleLine;
        public void SetLineAsFirstVisible(int line, int wrapIndex = 0) => SetFirstVisibleRow(line, wrapIndex);
        public void SetLineAsCenterVisible(int line, int wrapIndex = 0) => SetFirstVisibleVisualRow(GetVisualRowOffset(line, wrapIndex) - GetVisibleLineCount() / 2);
        public void SetLineAsLastVisible(int line, int wrapIndex = 0) => SetFirstVisibleVisualRow(GetVisualRowOffset(line, wrapIndex) - GetVisibleLineCount() + 1);
        public int GetLastFullVisibleLine() { GetLineAndWrapAtVisibleRow(Math.Max(0, GetVisibleLineCount() - 1), out var line, out _); return line; }
        public int GetLastFullVisibleLineWrapIndex() { GetLineAndWrapAtVisibleRow(Math.Max(0, GetVisibleLineCount() - 1), out _, out var wrapIndex); return wrapIndex; }
        /// <summary>Returns the visible line capacity of the current text viewport.</summary>
        public int GetVisibleLineCount() => EffectiveUIFont == null ? Math.Max(1, (int)(Size.Y / 16)) : Math.Max(1, (int)((Size.Y - Padding.Vertical) / TextMetrics.LineHeight(EffectiveUIFont)));
        public int GetVisibleLineCountInRange(int fromLine, int toLine)
        {
            ValidateLine(fromLine); ValidateLine(toLine);
            var from = Math.Min(fromLine, toLine); var to = Math.Max(fromLine, toLine); var visible = 0;
            for (var line = from; line <= to; line++) if (!IsLineHiddenForDisplay(line)) visible += GetWrapSegments(line).Count;
            return visible;
        }
        public int GetTotalVisibleLineCount()
        {
            var visible = 0;
            for (var line = 0; line < LineCount; line++) if (!IsLineHiddenForDisplay(line)) visible += GetWrapSegments(line).Count;
            return visible;
        }
        public bool IsLineInViewport(int line)
        {
            ValidateLine(line);
            if (IsLineHiddenForDisplay(line)) return false;
            var first = GetVisualRowOffset(FirstVisibleLine, FirstVisibleLineWrapIndex);
            var lineFirst = GetVisualRowOffset(line, 0); var lineLast = lineFirst + GetWrapSegments(line).Count - 1;
            var last = first + GetVisibleLineCount() - 1;
            return lineLast >= first && lineFirst <= last;
        }
        /// <summary>Moves the text viewport only when the caret would otherwise be outside it.</summary>
        public void AdjustViewportToCaret()
        {
            if (IsLineHiddenForDisplay(CaretLine)) return;
            var caretWrap = GetLineWrapIndexAtColumn(CaretLine, CaretColumnInLine);
            var first = GetVisualRowOffset(FirstVisibleLine, FirstVisibleLineWrapIndex);
            var caretRow = GetVisualRowOffset(CaretLine, caretWrap); var last = first + GetVisibleLineCount() - 1;
            if (caretRow < first) SetLineAsFirstVisible(CaretLine, caretWrap);
            else if (caretRow > last) SetLineAsLastVisible(CaretLine, caretWrap);
        }
        /// <summary>Centers the active caret line in the text viewport where possible.</summary>
        public void CenterViewportToCaret() => SetLineAsCenterVisible(CaretLine, GetLineWrapIndexAtColumn(CaretLine, CaretColumnInLine));
        internal override void PointerPressed(Point position)
        {
            base.PointerPressed(position);
            if (EffectiveUIFont == null) return;
            if (TryGetGutterAt(position, out var gutter))
            {
                var gutterLine = GetLineAtVisibleRow((int)((position.Y - GlobalPosition.Y - Padding.Top) / Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont))));
                if (_gutters[gutter].Clickable && IsLineGutterClickable(gutterLine, gutter)) GutterClicked?.Invoke(this, gutterLine, gutter);
            }
        }
        protected override int GetCaretColumnAtPosition(Point position)
        {
            if (EffectiveUIFont == null) return Text.Length;
            GetLineAndWrapAtVisibleRow((int)((position.Y - GlobalPosition.Y - Padding.Top) / Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont))), out var line, out var wrapIndex);
            var localX = position.X - GlobalPosition.X - Padding.Left - TextContentLeftInset;
            var source = GetLine(line); var segment = GetWrapSegments(line)[wrapIndex];
            var column = segment.Start + GetSegmentLayout(source, segment.Start, segment.Length).HitTest(new Vector2(localX, 0));
            return GetLineStart(line) + column;
        }
        internal override void PointerRightPressed(Point position) => OpenContextMenu(position);
        internal override void KeyPressed(Keys key)
        {
            if (key == Keys.Apps || key == Keys.F10 && HasShiftModifier()) { OpenContextMenu(new Point((int)GlobalPosition.X, (int)GlobalPosition.Y)); return; }
            if (ShortcutKeysEnabled && HasCommandModifier())
            {
                if (key == Keys.A) { SelectAll(); return; }
                if (key == Keys.C) { Copy(); return; }
                if (key == Keys.X) { Cut(); return; }
                if (key == Keys.V) { Paste(); return; }
            }
            if (_secondaryCarets.Count > 0)
            {
                if (key == Keys.Back) { DeleteAtCarets(true); return; }
                if (key == Keys.Delete) { DeleteAtCarets(false); return; }
                if (key == Keys.Left) { MoveAllCaretsHorizontal(-1); return; }
                if (key == Keys.Right) { MoveAllCaretsHorizontal(1); return; }
                if (key == Keys.Home) { MoveAllCaretsToLineBoundary(false); return; }
                if (key == Keys.End) { MoveAllCaretsToLineBoundary(true); return; }
                if (key == Keys.Up) { MoveAllCaretsVertical(-1); return; }
                if (key == Keys.Down) { MoveAllCaretsVertical(1); return; }
            }
            if (key == Keys.Enter) InsertNewline();
            else if (key == Keys.Home) SetCaret(CaretLine, 0);
            else if (key == Keys.End) SetCaret(CaretLine, GetLine(CaretLine).Length);
            else if (key == Keys.Up) MoveCaretVisualRow(-1);
            else if (key == Keys.Down) MoveCaretVisualRow(1);
            else base.KeyPressed(key);
        }
        internal override void TextInput(char character) => InsertText(character.ToString());
        internal override void DrawEditor(UIRenderContext context)
        {
            context.Fill(Bounds, context.Theme.BackgroundColor);
            context.Border(Bounds, Context?.FocusedControl == this ? context.Theme.FocusColor : context.Theme.PanelBorderColor);
            if (EffectiveUIFont != null)
            {
                var position = GlobalPosition + new Vector2(Padding.Left + TextContentLeftInset, Padding.Top);
                DrawGutters(context);
                for (var line = FirstVisibleLine; line >= 0 && line < LineCount && position.Y + TextMetrics.LineHeight(EffectiveUIFont) <= Bounds.Bottom; line++)
                {
                    if (IsLineHiddenForDisplay(line)) continue;
                    var source = GetLine(line);
                    var segments = GetWrapSegments(line);
                    var firstWrap = line == FirstVisibleLine ? FirstVisibleLineWrapIndex : 0;
                    for (var wrapIndex = firstWrap; wrapIndex < segments.Count; wrapIndex++)
                    {
                        if (position.Y + TextMetrics.LineHeight(EffectiveUIFont) > Bounds.Bottom) break;
                        var segment = segments[wrapIndex];
                        if (_lineBackgroundColors.TryGetValue(line, out var background)) context.Fill(new Rectangle(Bounds.X, (int)position.Y, Bounds.Width, TextMetrics.LineHeight(EffectiveUIFont)), background);
                        var text = source.Substring(segment.Start, segment.Length);
                        DrawSelectionHighlights(context, line, segment.Start, segment.Length, position);
                        context.Text(EffectiveUIFont, text, position, context.Theme.TextColor);
                        DrawSyntaxHighlighting(context, line, source, segment.Start, segment.Length, position);
                        DrawControlCharacterIcons(context, source, segment.Start, segment.Length, position);
                        position.Y += TextMetrics.LineHeight(EffectiveUIFont);
                    }
                }
                if (Context?.FocusedControl == this)
                {
                    var wrapIndex = GetLineWrapIndexAtColumn(CaretLine, CaretColumnInLine); var segment = GetWrapSegments(CaretLine)[wrapIndex];
                    var segmentLayout = GetSegmentLayout(GetLine(CaretLine), segment.Start, segment.Length);
                    var x = GlobalPosition.X + Padding.Left + TextContentLeftInset + segmentLayout.GetCaretPosition(CaretColumnInLine - segment.Start).X;
                    var y = GlobalPosition.Y + Padding.Top + GetVisibleRow(CaretLine, wrapIndex) * TextMetrics.LineHeight(EffectiveUIFont);
                    if (y >= Bounds.Top && y < Bounds.Bottom) context.Fill(new Rectangle((int)x, (int)y, 1, Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont))), context.Theme.FocusColor);
                    for (var caret = 1; caret < CaretCount; caret++)
                    {
                        var line = GetCaretLine(caret); var column = GetCaretColumn(caret); var secondaryWrap = GetLineWrapIndexAtColumn(line, column); var secondarySegment = GetWrapSegments(line)[secondaryWrap];
                        var secondaryLayout = GetSegmentLayout(GetLine(line), secondarySegment.Start, secondarySegment.Length);
                        var secondaryX = GlobalPosition.X + Padding.Left + TextContentLeftInset + secondaryLayout.GetCaretPosition(column - secondarySegment.Start).X;
                        var secondaryY = GlobalPosition.Y + Padding.Top + GetVisibleRow(line, secondaryWrap) * TextMetrics.LineHeight(EffectiveUIFont);
                        if (secondaryY >= Bounds.Top && secondaryY < Bounds.Bottom) context.Fill(new Rectangle((int)secondaryX, (int)secondaryY, 1, Math.Max(1, TextMetrics.LineHeight(EffectiveUIFont))), context.Theme.FocusColor);
                    }
                }
            }
        }
        private void DrawControlCharacterIcons(UIRenderContext context, string source, int start, int length, Vector2 position)
        {
            if (!DrawControlCharacters) return;
            var layout = GetSegmentLayout(source, start, length);
            for (var offset = 0; offset < length; offset++)
            {
                var name = source[start + offset] == '\t' ? "tab" : source[start + offset] == ' ' ? "space" : null;
                if (name == null) continue;
                var icon = GetThemeIcon(name);
                if (!icon.HasValue) continue;
                var x = position.X + layout.GetCaretPosition(offset).X;
                var advance = layout.GetCaretPosition(offset + 1).X - layout.GetCaretPosition(offset).X;
                context.Icon(icon.Value, new Vector2(x + (advance - icon.Value.LogicalSize.X) / 2, position.Y + (TextMetrics.LineHeight(EffectiveUIFont) - icon.Value.LogicalSize.Y) / 2), context.Theme.DisabledTextColor);
            }
        }
        private int GetLineForIndex(int index)
        {
            var line = 0;
            for (var i = 0; i < Math.Min(index, Text.Length); i++) if (Text[i] == '\n') line++;
            return line;
        }
        private int GetLineStart(int line)
        {
            var start = 0;
            while (line-- > 0)
            {
                var next = Text.IndexOf('\n', start);
                if (next < 0) return Text.Length;
                start = next + 1;
            }
            return start;
        }
        private List<string> GetLines() => new List<string>(Text.Split('\n'));
        private void ValidateLine(int line) { if (line < 0 || line >= LineCount) throw new ArgumentOutOfRangeException(nameof(line)); }
        private void ValidateGutter(int gutter) { if (gutter < 0 || gutter >= _gutters.Count) throw new ArgumentOutOfRangeException(nameof(gutter)); }
        private TextEditGutter GetGutter(int gutter) { ValidateGutter(gutter); return _gutters[gutter]; }
        private TextEditGutterItem GetLineGutterItem(int line, int gutter, bool create)
        {
            ValidateLine(line); ValidateGutter(gutter); var key = (line, gutter);
            if (!_lineGutterItems.TryGetValue(key, out var item) && create) { item = new TextEditGutterItem(); _lineGutterItems[key] = item; }
            return item;
        }
        private void DrawGutters(UIRenderContext context)
        {
            if (_gutters.Count == 0 || EffectiveUIFont == null) return;
            var x = Bounds.X;
            for (var index = 0; index < _gutters.Count; index++)
            {
                var gutter = _gutters[index]; if (!gutter.Draw) continue;
                var rect = new Rectangle(x, Bounds.Y, gutter.Width, Bounds.Height); context.Fill(rect, context.Theme.PanelColor); x += gutter.Width;
            }
            var y = Bounds.Y + Padding.Top;
            for (var line = FirstVisibleLine; line >= 0 && line < LineCount && y + TextMetrics.LineHeight(EffectiveUIFont) <= Bounds.Bottom; line++)
            {
                if (IsLineHiddenForDisplay(line)) continue;
                var firstWrap = line == FirstVisibleLine ? FirstVisibleLineWrapIndex : 0;
                if (firstWrap == 0)
                {
                x = Bounds.X;
                for (var index = 0; index < _gutters.Count; index++)
                {
                    var gutter = _gutters[index]; if (!gutter.Draw) continue;
                    var rect = new Rectangle(x, (int)y, gutter.Width, TextMetrics.LineHeight(EffectiveUIFont)); var item = GetLineGutterItem(line, index, false);
                    if (gutter.Type == TextEditGutterType.Custom) gutter.CustomDraw?.Invoke(context, this, line, rect);
                    else if (gutter.Type == TextEditGutterType.Icon && item?.Icon != null) context.SpriteBatch.Draw(item.Icon, new Rectangle(rect.X + 2, rect.Y + 2, Math.Max(1, Math.Min(rect.Width - 4, TextMetrics.LineHeight(EffectiveUIFont) - 4)), Math.Max(1, Math.Min(rect.Height - 4, TextMetrics.LineHeight(EffectiveUIFont) - 4))), item.Color);
                    else if (gutter.Type == TextEditGutterType.String && item != null) context.Text(EffectiveUIFont, item.Text, new Vector2(rect.X + 2, rect.Y), item.Color);
                    x += gutter.Width;
                }
                }
                y += TextMetrics.LineHeight(EffectiveUIFont) * (GetWrapSegments(line).Count - firstWrap);
            }
        }
        private bool TryGetGutterAt(Point point, out int gutter)
        {
            var x = Bounds.X;
            for (var index = 0; index < _gutters.Count; index++)
            {
                var state = _gutters[index]; if (!state.Draw) continue;
                if (new Rectangle(x, Bounds.Y, state.Width, Bounds.Height).Contains(point)) { gutter = index; return true; }
                x += state.Width;
            }
            gutter = -1; return false;
        }
        private void TrackTextChange(LineEdit _, string text)
        {
            var previousText = _historyText;
            if (!_restoringHistory && _historyText != text)
            {
                if (_currentAction != TextEditEditAction.None) _actionChanged = true;
                else
                {
                    if (UndoEnabled) PushUndoState(_historyText, _version);
                    _version++;
                }
            }
            _historyText = text;
            var firstChangedLine = GetFirstChangedLine(previousText, text);
            _syntaxHighlighter?.InvalidateFromLine(firstChangedLine);
            InvalidateWrapLayoutAfterEdit(previousText, text, firstChangedLine);
            if (FirstVisibleLine >= LineCount || IsLineHiddenForDisplay(FirstVisibleLine)) SetFirstVisibleRow(Math.Min(FirstVisibleLine, LineCount - 1), 0);
            else FirstVisibleLineWrapIndex = Math.Min(FirstVisibleLineWrapIndex, Math.Max(0, GetWrapSegments(FirstVisibleLine).Count - 1));
        }
        private void SyntaxHighlighterChanged(object _, EventArgs __) => QueueLayout();
        private void DrawSyntaxHighlighting(UIRenderContext context, int line, string source, int segmentStart, int segmentLength, Vector2 position)
        {
            if (_syntaxHighlighter == null || string.IsNullOrEmpty(source)) return;
            var layout = GetSegmentLayout(source, segmentStart, segmentLength);
            foreach (var span in _syntaxHighlighter.GetLineSyntaxHighlighting(line))
            {
                var start = Math.Max(segmentStart, span.StartColumn); var end = Math.Min(segmentStart + segmentLength, span.StartColumn + span.Length);
                var length = end - start;
                if (length <= 0) continue;
                foreach (var rectangle in layout.GetSelectionRectangles(start - segmentStart, length))
                {
                    context.PushClip(new Rectangle((int)MathF.Floor(position.X + rectangle.X), (int)MathF.Floor(position.Y + rectangle.Y), Math.Max(1, (int)MathF.Ceiling(rectangle.Width)), Math.Max(1, (int)MathF.Ceiling(rectangle.Height))));
                    try { context.Text(layout, position, span.Color); }
                    finally { context.PopClip(); }
                }
            }
        }
        private void DrawSelectionHighlights(UIRenderContext context, int line, int segmentStart, int segmentLength, Vector2 position)
        {
            var sourceStart = GetLineStart(line); var absoluteStart = sourceStart + segmentStart; var absoluteEnd = absoluteStart + segmentLength; var source = GetLine(line);
            for (var caret = 0; caret < CaretCount; caret++)
            {
                if (!HasCaretSelection(caret)) continue;
                var start = Math.Max(GetSelectionFromIndex(caret), absoluteStart); var end = Math.Min(GetSelectionToIndex(caret), absoluteEnd);
                if (end <= start) continue;
                var layout = GetSegmentLayout(source, segmentStart, segmentLength);
                foreach (var rectangle in layout.GetSelectionRectangles(start - absoluteStart, end - start))
                    context.Fill(new Rectangle((int)(position.X + rectangle.X), (int)(position.Y + rectangle.Y), Math.Max(1, (int)MathF.Ceiling(rectangle.Width)), Math.Max(1, (int)MathF.Ceiling(rectangle.Height))), context.Theme.HoverColor);
            }
        }
        private TextLayout GetSegmentLayout(string source, int start, int length) => GetEditingLayout(source.Substring(start, length));
        private float MeasureTextWidth(string text) => EffectiveUIFont == null ? text.Length * 8 : TextMetrics.Measure(EffectiveUIFont, text).X;
        private void MoveAllCaretsHorizontal(int direction)
        {
            _caretMergeSuspension++;
            try
            {
                for (var caret = 0; caret < CaretCount; caret++)
                {
                    var index = direction < 0 ? GetSelectionFromIndex(caret) : GetSelectionToIndex(caret);
                    if (!HasCaretSelection(caret)) index = GetAdjacentGraphemeIndex(index, direction);
                    SetCaretAtTextIndex(index, caret);
                }
            }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        private void MoveAllCaretsToLineBoundary(bool end)
        {
            _caretMergeSuspension++;
            try { for (var caret = 0; caret < CaretCount; caret++) { var line = GetCaretLine(caret); SetCaret(line, end ? GetLine(line).Length : 0, caret); } }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        private void MoveAllCaretsVertical(int direction)
        {
            _caretMergeSuspension++;
            try { for (var caret = 0; caret < CaretCount; caret++) MoveCaretVisualRow(direction, caret); }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        private void DeleteAtCarets(bool backward)
        {
            var edits = new List<CaretEdit>();
            for (var caret = 0; caret < CaretCount; caret++)
            {
                var start = GetSelectionFromIndex(caret); var end = GetSelectionToIndex(caret);
                if (start == end)
                {
                    if (backward) start = GetAdjacentGraphemeIndex(start, -1);
                    else end = GetAdjacentGraphemeIndex(end, 1);
                }
                edits.Add(new CaretEdit { Caret = caret, Start = start, End = end });
            }
            ApplyMultiCaretEdits(edits, string.Empty);
        }
        private bool HasAnyCaretSelection()
        {
            for (var caret = 0; caret < CaretCount; caret++) if (HasCaretSelection(caret)) return true;
            return false;
        }
        private string GetWordAtCaret(int caret)
        {
            return TryGetWordRange(caret, out var line, out var from, out var to) ? GetLine(line).Substring(from, to - from) : string.Empty;
        }
        private bool TryGetWordRange(int caret, out int line, out int from, out int to)
        {
            line = GetCaretLine(caret); var text = GetLine(line); var column = GetCaretColumn(caret); from = to = 0;
            if (string.IsNullOrEmpty(text)) return false;
            var index = Math.Min(column, text.Length - 1);
            if (!IsWordCharacter(text[index]) && index > 0 && IsWordCharacter(text[index - 1])) index--;
            if (!IsWordCharacter(text[index])) return false;
            from = index; to = index + 1;
            while (from > 0 && IsWordCharacter(text[from - 1])) from--;
            while (to < text.Length && IsWordCharacter(text[to])) to++;
            return true;
        }
        private List<CaretState> CaptureCaretStates()
        {
            var states = new List<CaretState>();
            for (var caret = 0; caret < CaretCount; caret++) states.Add(new CaretState { Index = GetCaretIndex(caret), Anchor = HasCaretSelection(caret) ? GetSelectionOriginIndex(caret) : -1 });
            return states;
        }
        private void RestoreCaretStates(List<CaretState> states)
        {
            _caretMergeSuspension++;
            try
            {
                var primary = states[0];
                if (primary.Anchor >= 0) base.Select(MathHelper.Clamp(primary.Anchor, 0, Text.Length), MathHelper.Clamp(primary.Index, 0, Text.Length));
                else { CaretColumn = MathHelper.Clamp(primary.Index, 0, Text.Length); base.Deselect(); }
                for (var caret = 1; caret < states.Count; caret++)
                {
                    var state = states[caret]; var secondary = GetSecondaryCaret(caret); secondary.Index = MathHelper.Clamp(state.Index, 0, Text.Length); secondary.Anchor = state.Anchor < 0 ? -1 : MathHelper.Clamp(state.Anchor, 0, Text.Length);
                }
            }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        private void RestoreCaretLineStates(List<CaretLineState> states, int fromLine, int toLine)
        {
            _caretMergeSuspension++;
            try
            {
                for (var caret = 0; caret < states.Count; caret++)
                {
                    var state = states[caret]; var line = SwapLineNumber(state.Line, fromLine, toLine); var originLine = SwapLineNumber(state.OriginLine, fromLine, toLine);
                    var index = GetLineStart(line) + MathHelper.Clamp(state.Column, 0, GetLine(line).Length);
                    var origin = GetLineStart(originLine) + MathHelper.Clamp(state.OriginColumn, 0, GetLine(originLine).Length);
                    if (caret == 0)
                    {
                        if (state.Selected) base.Select(origin, index); else { CaretColumn = index; base.Deselect(); }
                    }
                    else { var secondary = GetSecondaryCaret(caret); secondary.Index = index; secondary.Anchor = state.Selected ? origin : -1; }
                }
            }
            finally { _caretMergeSuspension--; }
            MergeOverlappingCarets();
        }
        private void SwapLineMetadata(int fromLine, int toLine)
        {
            var hasFromBackground = _lineBackgroundColors.TryGetValue(fromLine, out var fromBackground); var hasToBackground = _lineBackgroundColors.TryGetValue(toLine, out var toBackground);
            _lineBackgroundColors.Remove(fromLine); _lineBackgroundColors.Remove(toLine);
            if (hasFromBackground) _lineBackgroundColors[toLine] = fromBackground;
            if (hasToBackground) _lineBackgroundColors[fromLine] = toBackground;
            var moved = new List<KeyValuePair<(int Line, int Gutter), TextEditGutterItem>>();
            foreach (var item in _lineGutterItems) if (item.Key.Line == fromLine || item.Key.Line == toLine) moved.Add(item);
            foreach (var item in moved) _lineGutterItems.Remove(item.Key);
            foreach (var item in moved) _lineGutterItems[(SwapLineNumber(item.Key.Line, fromLine, toLine), item.Key.Gutter)] = item.Value;
        }
        private static int SwapLineNumber(int line, int fromLine, int toLine) => line == fromLine ? toLine : line == toLine ? fromLine : line;
        private static int CollapseTextIndex(int index, int from, int to) => index <= from ? index : index >= to ? index - (to - from) : from;
        private bool HasCommandModifier()
        {
            var keyboard = Context?.CurrentKeyboardState ?? default;
            return keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl) || keyboard.IsKeyDown(Keys.LeftWindows) || keyboard.IsKeyDown(Keys.RightWindows);
        }
        private bool HasShiftModifier()
        {
            var keyboard = Context?.CurrentKeyboardState ?? default;
            return keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        }
        private void OpenContextMenu(Point position)
        {
            if (!ContextMenuEnabled || Context == null) return;
            _contextMenu.Clear(); _contextMenu.Font = Font;
            var directionMenu = GetTextDirectionMenu();
            directionMenu.Clear(); directionMenu.Font = Font;
            directionMenu.AddRadioCheckItem("Same as Layout Direction", (int)TextEditMenuOption.DirectionInherited).Checked = TextDirection == TextDirection.Inherited;
            directionMenu.AddRadioCheckItem("Auto-Detect Direction", (int)TextEditMenuOption.DirectionAuto).Checked = TextDirection == TextDirection.Auto;
            directionMenu.AddRadioCheckItem("Left-to-Right", (int)TextEditMenuOption.DirectionLeftToRight).Checked = TextDirection == TextDirection.LeftToRight;
            directionMenu.AddRadioCheckItem("Right-to-Left", (int)TextEditMenuOption.DirectionRightToLeft).Checked = TextDirection == TextDirection.RightToLeft;
            var controlCharacterMenu = GetControlCharacterMenu();
            controlCharacterMenu.Clear(); controlCharacterMenu.Font = Font;
            controlCharacterMenu.AddItem("Left-to-Right Mark (LRM)", (int)TextEditMenuOption.InsertLeftToRightMark);
            controlCharacterMenu.AddItem("Right-to-Left Mark (RLM)", (int)TextEditMenuOption.InsertRightToLeftMark);
            controlCharacterMenu.AddItem("Start of Left-to-Right Embedding (LRE)", (int)TextEditMenuOption.InsertLeftToRightEmbedding);
            controlCharacterMenu.AddItem("Start of Right-to-Left Embedding (RLE)", (int)TextEditMenuOption.InsertRightToLeftEmbedding);
            controlCharacterMenu.AddItem("Start of Left-to-Right Override (LRO)", (int)TextEditMenuOption.InsertLeftToRightOverride);
            controlCharacterMenu.AddItem("Start of Right-to-Left Override (RLO)", (int)TextEditMenuOption.InsertRightToLeftOverride);
            controlCharacterMenu.AddItem("Pop Direction Formatting (PDF)", (int)TextEditMenuOption.InsertPopDirectionFormatting);
            controlCharacterMenu.AddSeparator();
            controlCharacterMenu.AddItem("Arabic Letter Mark (ALM)", (int)TextEditMenuOption.InsertArabicLetterMark);
            controlCharacterMenu.AddItem("Left-to-Right Isolate (LRI)", (int)TextEditMenuOption.InsertLeftToRightIsolate);
            controlCharacterMenu.AddItem("Right-to-Left Isolate (RLI)", (int)TextEditMenuOption.InsertRightToLeftIsolate);
            controlCharacterMenu.AddItem("First Strong Isolate (FSI)", (int)TextEditMenuOption.InsertFirstStrongIsolate);
            controlCharacterMenu.AddItem("Pop Direction Isolate (PDI)", (int)TextEditMenuOption.InsertPopDirectionIsolate);
            controlCharacterMenu.AddSeparator();
            controlCharacterMenu.AddItem("Zero-Width Joiner (ZWJ)", (int)TextEditMenuOption.InsertZeroWidthJoiner);
            controlCharacterMenu.AddItem("Zero-Width Non-Joiner (ZWNJ)", (int)TextEditMenuOption.InsertZeroWidthNonJoiner);
            controlCharacterMenu.AddItem("Word Joiner (WJ)", (int)TextEditMenuOption.InsertWordJoiner);
            controlCharacterMenu.AddItem("Soft Hyphen (SHY)", (int)TextEditMenuOption.InsertSoftHyphen);
            _contextMenu.AddItem("Cut", (int)TextEditMenuOption.Cut).Disabled = !Editable;
            _contextMenu.AddItem("Copy", (int)TextEditMenuOption.Copy);
            _contextMenu.AddItem("Paste", (int)TextEditMenuOption.Paste).Disabled = !Editable;
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Select All", (int)TextEditMenuOption.SelectAll);
            _contextMenu.AddItem("Clear", (int)TextEditMenuOption.Clear).Disabled = !Editable;
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Undo", (int)TextEditMenuOption.Undo).Disabled = !Editable || !HasUndo;
            _contextMenu.AddItem("Redo", (int)TextEditMenuOption.Redo).Disabled = !Editable || !HasRedo;
            _contextMenu.AddSeparator();
            _contextMenu.AddSubmenuNodeItem("Text Writing Direction", directionMenu, (int)TextEditMenuOption.SubmenuTextDirection);
            _contextMenu.AddSeparator();
            _contextMenu.AddCheckItem("Display Control Characters", (int)TextEditMenuOption.DisplayControlCharacters).Checked = DrawControlCharacters;
            _contextMenu.AddSubmenuNodeItem("Insert Control Character", controlCharacterMenu, (int)TextEditMenuOption.SubmenuInsertControlCharacter).Disabled = !Editable;
            if (_contextMenu.Context != Context) Context.Add(_contextMenu);
            _contextMenu.PopupAt(new Vector2(position.X, position.Y), null);
        }
        private List<int> GetSortedCaretIndexes()
        {
            var indexes = new List<int>(); for (var caret = 0; caret < CaretCount; caret++) indexes.Add(caret);
            indexes.Sort((left, right) => GetSelectionFromIndex(left).CompareTo(GetSelectionFromIndex(right))); return indexes;
        }
        private List<CaretLineRange> GetCaretLineRanges(int caret)
        {
            var indexes = caret < 0 ? GetSortedCaretIndexes() : new List<int> { caret }; var ranges = new List<CaretLineRange>(); var last = int.MinValue;
            foreach (var index in indexes)
            {
                var first = GetCaretLine(index); var line = first;
                if (HasCaretSelection(index))
                {
                    line = GetLineForIndex(GetSelectionToIndex(index));
                    if (GetSelectionToIndex(index) == GetLineStart(line) && line > first) line--;
                }
                if (ranges.Count > 0 && first <= last + 1) ranges[ranges.Count - 1].Last = Math.Max(ranges[ranges.Count - 1].Last, line);
                else ranges.Add(new CaretLineRange(first, line));
                last = Math.Max(last, line);
            }
            return ranges;
        }
        private void DeleteCaretSelections(int caret)
        {
            var edits = new List<CaretEdit>();
            for (var index = 0; index < CaretCount; index++)
            {
                var target = caret < 0 || index == caret; var start = GetSelectionFromIndex(index); var end = target && HasCaretSelection(index) ? GetSelectionToIndex(index) : start;
                edits.Add(new CaretEdit { Caret = index, Start = start, End = end });
            }
            ApplyMultiCaretEdits(edits, string.Empty);
        }
        private void DeleteCaretLines(int caret)
        {
            var lines = GetLines(); var ranges = GetCaretLineRanges(caret);
            for (var index = ranges.Count - 1; index >= 0; index--) lines.RemoveRange(ranges[index].First, ranges[index].Last - ranges[index].First + 1);
            if (lines.Count == 0) lines.Add(string.Empty);
            Text = string.Join("\n", lines); RemoveSecondaryCarets();
            for (var index = 0; index < ranges.Count; index++)
            {
                var line = Math.Min(ranges[index].First, lines.Count - 1); if (index == 0) SetCaret(line, 0); else AddCaret(line, 0);
            }
        }
        private void ApplyMultiCaretEdits(List<CaretEdit> edits, string replacement)
            => ApplyMultiCaretEdits(edits, _ => replacement);
        private void ApplyMultiCaretEdits(List<CaretEdit> edits, Func<CaretEdit, string> replacement)
        {
            edits.Sort((left, right) => left.Start.CompareTo(right.Start));
            for (var index = 1; index < edits.Count; index++) if (edits[index].Start < edits[index - 1].End || edits[index].Start == edits[index - 1].Start) return;
            var builder = new System.Text.StringBuilder(Text.Length); var cursor = 0; var finalIndexes = new int[CaretCount];
            foreach (var edit in edits)
            {
                var inserted = replacement(edit) ?? string.Empty;
                builder.Append(Text, cursor, edit.Start - cursor); builder.Append(inserted); finalIndexes[edit.Caret] = builder.Length; cursor = edit.End;
            }
            builder.Append(Text, cursor, Text.Length - cursor);
            _updatingMultipleCarets = true;
            try { Text = builder.ToString(); CaretColumn = finalIndexes[0]; Deselect(); for (var caret = 1; caret < CaretCount; caret++) { var secondary = _secondaryCarets[caret - 1]; secondary.Index = finalIndexes[caret]; secondary.Anchor = -1; } }
            finally { _updatingMultipleCarets = false; }
            MergeOverlappingCarets();
        }
        private void SetCaretAtTextIndex(int index, int caret)
        {
            index = MathHelper.Clamp(index, 0, Text.Length); var line = GetLineForIndex(index); SetCaret(line, index - GetLineStart(line), caret);
        }
        private void MoveCaretVisualRow(int direction, int caret = 0)
        {
            var line = GetCaretLine(caret); var sourceColumn = GetCaretColumn(caret); var segments = GetWrapSegments(line); var wrapIndex = GetLineWrapIndexAtColumn(line, sourceColumn);
            var targetLine = line; var targetWrap = wrapIndex + direction;
            if (targetWrap < 0)
            {
                targetLine = line - 1; while (targetLine >= 0 && IsLineHiddenForDisplay(targetLine)) targetLine--;
                if (targetLine < 0) return;
                targetWrap = Math.Max(0, GetWrapSegments(targetLine).Count - 1);
            }
            else if (targetWrap >= segments.Count)
            {
                targetLine = line + 1; while (targetLine < LineCount && IsLineHiddenForDisplay(targetLine)) targetLine++;
                if (targetLine >= LineCount) return;
                targetWrap = 0;
            }
            var current = segments[wrapIndex]; var targetSource = GetLine(targetLine); var target = GetWrapSegments(targetLine)[targetWrap];
            if (EffectiveUIFont == null)
            {
                SetCaret(targetLine, target.Start + Math.Min(Math.Max(0, sourceColumn - current.Start), target.Length), caret);
                return;
            }
            var currentLayout = GetSegmentLayout(GetLine(line), current.Start, current.Length);
            var targetLayout = GetSegmentLayout(targetSource, target.Start, target.Length);
            var preferredX = currentLayout.GetCaretPosition(MathHelper.Clamp(sourceColumn - current.Start, 0, current.Length)).X;
            var targetColumn = target.Start + targetLayout.HitTest(new Vector2(preferredX, 0));
            SetCaret(targetLine, targetColumn, caret);
        }
        private int GetAdjacentGraphemeIndex(int index, int direction)
        {
            index = MathHelper.Clamp(index, 0, Text.Length);
            if (EffectiveUIFont == null) return MathHelper.Clamp(index + Math.Sign(direction), 0, Text.Length);
            var line = GetLineForIndex(index);
            var lineStart = GetLineStart(line);
            var column = index - lineStart;
            var source = GetLine(line);
            if (direction < 0)
            {
                if (column == 0) return Math.Max(0, index - 1);
                return lineStart + GetEditingLayout(source).GetPreviousGraphemeBoundary(column);
            }
            if (column == source.Length) return Math.Min(Text.Length, index + 1);
            return lineStart + GetEditingLayout(source).GetNextGraphemeBoundary(column);
        }
        private List<TextEditWrapSegment> GetWrapSegments(int line)
        {
            EnsureWrapLayout();
            if (!_wrapLayoutCache.TryGetValue(line, out var segments))
            {
                segments = BuildWrapSegments(GetLine(line), GetEffectiveWrapWidth());
                _wrapLayoutCache[line] = segments;
                WrapLayoutBuildCount++;
            }
            return segments;
        }
        private void EnsureWrapLayout()
        {
            var width = GetEffectiveWrapWidth();
            var font = EffectiveUIFont;
            var identity = font?.Identity ?? default;
            var size = font?.Size ?? 0;
            if (_wrapLayoutText == Text && _wrapLayoutWidth == width && _wrapLayoutFontIdentity == identity && _wrapLayoutFontSize == size && _wrapLayoutLanguage == Language && _wrapLayoutDirection == TextDirection) return;
            _wrapLayoutText = Text; _wrapLayoutWidth = width; _wrapLayoutFontIdentity = identity; _wrapLayoutFontSize = size; _wrapLayoutLanguage = Language; _wrapLayoutDirection = TextDirection; _wrapLayoutCache.Clear();
        }
        private void InvalidateWrapLayout() { _wrapLayoutText = null; _wrapLayoutWidth = -1; _wrapLayoutCache.Clear(); }
        private void InvalidateWrapLayoutAfterEdit(string previousText, string text, int firstChangedLine)
        {
            var previousLineCount = CountLines(previousText);
            var currentLineCount = CountLines(text);
            if (previousLineCount != currentLineCount)
            {
                foreach (var line in new List<int>(_wrapLayoutCache.Keys)) if (line >= firstChangedLine) _wrapLayoutCache.Remove(line);
            }
            else
            {
                var lastChangedLine = GetLastChangedLine(previousText, text);
                foreach (var line in new List<int>(_wrapLayoutCache.Keys)) if (line >= firstChangedLine && line <= lastChangedLine) _wrapLayoutCache.Remove(line);
            }
            _wrapLayoutText = text;
        }
        private float GetEffectiveWrapWidth()
        {
            if (LineWrappingMode == TextEditLineWrappingMode.None) return float.PositiveInfinity;
            var glyphWidth = EffectiveUIFont == null ? 8 : TextMetrics.Measure(EffectiveUIFont, "0").X;
            if (WrapAtColumn > 0) return Math.Max(1, glyphWidth * WrapAtColumn);
            return Math.Max(1, Size.X - Padding.Horizontal - TextContentLeftInset);
        }
        private List<TextEditWrapSegment> BuildWrapSegments(string text, float wrapWidth)
        {
            var segments = new List<TextEditWrapSegment>();
            if (LineWrappingMode != TextEditLineWrappingMode.None && EffectiveUIFont == null && WrapAtColumn > 0) return BuildColumnWrapSegments(text, WrapAtColumn);
            if (!float.IsFinite(wrapWidth) || EffectiveUIFont == null || string.IsNullOrEmpty(text)) { segments.Add(new TextEditWrapSegment(0, text.Length)); return segments; }
            var layout = GetEditingLayout(text);
            var start = 0;
            while (start < text.Length)
            {
                var end = start;
                var lastBreak = -1;
                var width = 0f;
                foreach (var cluster in layout.Clusters)
                {
                    if (cluster.Start < start) continue;
                    var clusterWidth = Math.Max(cluster.Bounds.Width, MathF.Abs(layout.GetCaretPosition(cluster.Start + cluster.Length).X - layout.GetCaretPosition(cluster.Start).X));
                    if (end > start && width + clusterWidth > wrapWidth) break;
                    width += clusterWidth;
                    end = cluster.Start + cluster.Length;
                    if (char.IsWhiteSpace(text[cluster.Start])) lastBreak = end;
                }
                if (end <= start) end = layout.GetNextGraphemeBoundary(start);
                if (end < text.Length && lastBreak > start) end = lastBreak;
                segments.Add(new TextEditWrapSegment(start, end - start));
                start = end;
            }
            return segments;
        }
        private static List<TextEditWrapSegment> BuildColumnWrapSegments(string text, int wrapColumn)
        {
            var segments = new List<TextEditWrapSegment>();
            if (wrapColumn <= 0 || text.Length <= wrapColumn) { segments.Add(new TextEditWrapSegment(0, text.Length)); return segments; }
            var start = 0;
            while (start < text.Length)
            {
                var end = Math.Min(text.Length, start + wrapColumn);
                if (end < text.Length)
                    for (var candidate = end - 1; candidate > start; candidate--)
                        if (char.IsWhiteSpace(text[candidate])) { end = candidate + 1; break; }
                if (end <= start) end = Math.Min(text.Length, start + wrapColumn);
                segments.Add(new TextEditWrapSegment(start, end - start));
                start = end;
            }
            return segments;
        }
        private static int GetFirstChangedLine(string previousText, string text)
        {
            var common = 0;
            while (common < previousText.Length && common < text.Length && previousText[common] == text[common]) common++;
            var line = 0;
            for (var index = 0; index < common; index++) if (text[index] == '\n') line++;
            return line;
        }
        private static int GetLastChangedLine(string previousText, string text)
        {
            var suffix = 0;
            while (suffix < previousText.Length && suffix < text.Length && previousText[previousText.Length - suffix - 1] == text[text.Length - suffix - 1]) suffix++;
            var changedEnd = text.Length - suffix;
            var line = 0;
            for (var index = 0; index < changedEnd; index++) if (text[index] == '\n') line++;
            return line;
        }
        private static int CountLines(string text)
        {
            var count = 1;
            foreach (var character in text) if (character == '\n') count++;
            return count;
        }
        private readonly struct TextEditWrapSegment
        {
            public TextEditWrapSegment(int start, int length) { Start = start; Length = length; }
            public int Start { get; }
            public int Length { get; }
        }
        /// <summary>Allows specialized editors to hide document lines without mutating the underlying text.</summary>
        protected virtual bool IsLineHiddenForDisplay(int line) => false;
        protected int GetLineAtVisibleRow(int row)
        {
            GetLineAndWrapAtVisibleRow(row, out var line, out _); return line;
        }
        /// <summary>Returns the source-line wrap row represented by a viewport-relative visual row.</summary>
        protected int GetLineWrapIndexAtVisibleRow(int row)
        {
            GetLineAndWrapAtVisibleRow(row, out _, out var wrapIndex); return wrapIndex;
        }
        protected int GetLineWrapStartColumn(int line, int wrapIndex)
        {
            ValidateLine(line); var segments = GetWrapSegments(line); if (wrapIndex < 0 || wrapIndex >= segments.Count) throw new ArgumentOutOfRangeException(nameof(wrapIndex));
            return segments[wrapIndex].Start;
        }
        protected int GetLineWrapLength(int line, int wrapIndex)
        {
            ValidateLine(line); var segments = GetWrapSegments(line); if (wrapIndex < 0 || wrapIndex >= segments.Count) throw new ArgumentOutOfRangeException(nameof(wrapIndex));
            return segments[wrapIndex].Length;
        }
        private void GetLineAndWrapAtVisibleRow(int row, out int line, out int wrapIndex)
        {
            row = Math.Max(0, row);
            for (line = FirstVisibleLine; line >= 0 && line < LineCount; line++)
            {
                if (IsLineHiddenForDisplay(line)) continue;
                var wrapCount = GetWrapSegments(line).Count;
                var firstWrap = line == FirstVisibleLine ? FirstVisibleLineWrapIndex : 0;
                wrapCount -= firstWrap;
                if (row < wrapCount) { wrapIndex = firstWrap + row; return; }
                row -= wrapCount;
            }
            line = Math.Max(0, LineCount - 1); wrapIndex = Math.Max(0, GetWrapSegments(line).Count - 1);
        }
        protected int GetVisibleRow(int line, int wrapIndex = 0)
        {
            if (line < FirstVisibleLine || IsLineHiddenForDisplay(line)) return -1;
            var row = -FirstVisibleLineWrapIndex;
            for (var current = FirstVisibleLine; current < line; current++) if (!IsLineHiddenForDisplay(current)) row += GetWrapSegments(current).Count;
            return row + MathHelper.Clamp(wrapIndex, 0, Math.Max(0, GetWrapSegments(line).Count - 1));
        }
        private int GetVisualRowOffset(int line, int wrapIndex)
        {
            ValidateLine(line);
            var row = 0;
            for (var current = 0; current < line; current++) if (!IsLineHiddenForDisplay(current)) row += GetWrapSegments(current).Count;
            if (IsLineHiddenForDisplay(line)) return row;
            return row + MathHelper.Clamp(wrapIndex, 0, Math.Max(0, GetWrapSegments(line).Count - 1));
        }
        private void SetFirstVisibleVisualRow(int row)
        {
            var total = Math.Max(1, GetTotalVisibleLineCount()); row = MathHelper.Clamp(row, 0, total - 1);
            var line = 0;
            for (; line < LineCount; line++)
            {
                if (IsLineHiddenForDisplay(line)) continue;
                var wraps = GetWrapSegments(line).Count;
                if (row < wraps) { SetFirstVisibleRow(line, row); return; }
                row -= wraps;
            }
            SetFirstVisibleRow(Math.Max(0, LineCount - 1), Math.Max(0, GetWrapSegments(Math.Max(0, LineCount - 1)).Count - 1));
        }
        private void SetFirstVisibleRow(int line, int wrapIndex)
        {
            line = FindVisibleLineAtOrAfter(MathHelper.Clamp(line, 0, LineCount - 1));
            FirstVisibleLine = line;
            FirstVisibleLineWrapIndex = MathHelper.Clamp(wrapIndex, 0, Math.Max(0, GetWrapSegments(line).Count - 1));
        }
        protected int FindVisibleLineAtOrAfter(int line)
        {
            for (var current = Math.Max(0, line); current < LineCount; current++) if (!IsLineHiddenForDisplay(current)) return current;
            for (var current = Math.Min(LineCount - 1, line - 1); current >= 0; current--) if (!IsLineHiddenForDisplay(current)) return current;
            return 0;
        }
        protected int FindVisibleLineAtOrBefore(int line, int fallback)
        {
            for (var current = Math.Min(LineCount - 1, line); current >= 0; current--) if (!IsLineHiddenForDisplay(current)) return current;
            return FindVisibleLineAtOrAfter(fallback);
        }
        private void PushUndoState(string text, uint version)
        {
            _undoStack.Add(text); _undoVersions.Add(version);
            var maximum = Math.Max(1, UndoStackMaxSize);
            while (_undoStack.Count > maximum) { _undoStack.RemoveAt(0); _undoVersions.RemoveAt(0); }
            _redoStack.Clear(); _redoVersions.Clear();
        }
        private void RestoreHistory(string text, uint version) { _restoringHistory = true; Text = text; _restoringHistory = false; _historyText = Text; _version = version; CaretColumn = Math.Min(CaretColumn, Text.Length); Deselect(); }
        private void ShiftLineBackgrounds(int firstLine, int delta)
        {
            if (delta == 0) return;
            var moved = new List<KeyValuePair<int, Color>>(); foreach (var pair in _lineBackgroundColors) if (pair.Key >= firstLine) moved.Add(pair);
            foreach (var pair in moved) _lineBackgroundColors.Remove(pair.Key);
            foreach (var pair in moved) _lineBackgroundColors[pair.Key + delta] = pair.Value;
            ShiftLineGutterItems(firstLine, delta);
        }
        private void ShiftLineGutterItems(int firstLine, int delta)
        {
            var moved = new List<KeyValuePair<(int Line, int Gutter), TextEditGutterItem>>(); foreach (var pair in _lineGutterItems) if (pair.Key.Line >= firstLine) moved.Add(pair);
            foreach (var pair in moved) _lineGutterItems.Remove(pair.Key);
            foreach (var pair in moved) _lineGutterItems[(pair.Key.Line + delta, pair.Key.Gutter)] = pair.Value;
        }
        private void RemoveLineGutterItems(int line)
        {
            var removed = new List<(int Line, int Gutter)>(); foreach (var pair in _lineGutterItems) if (pair.Key.Line == line) removed.Add(pair.Key);
            foreach (var key in removed) _lineGutterItems.Remove(key);
        }
        private void ShiftGutterItems(int firstGutter, int delta, int removedGutter = -1)
        {
            var moved = new List<KeyValuePair<(int Line, int Gutter), TextEditGutterItem>>(_lineGutterItems); _lineGutterItems.Clear();
            foreach (var pair in moved)
            {
                if (pair.Key.Gutter == removedGutter) continue;
                var gutter = pair.Key.Gutter >= firstGutter ? pair.Key.Gutter + delta : pair.Key.Gutter;
                _lineGutterItems[(pair.Key.Line, gutter)] = pair.Value;
            }
        }
        private SecondaryCaret GetSecondaryCaret(int caret)
        {
            if (caret <= 0 || caret >= CaretCount) throw new ArgumentOutOfRangeException(nameof(caret));
            return _secondaryCarets[caret - 1];
        }
        private void RequestCaretMerge() { if (_caretMergeSuspension == 0) MergeOverlappingCarets(); }
        private int GetCaretIndex(int caret) => caret == 0 ? CaretColumn : GetSecondaryCaret(caret).Index;
        private int GetSelectionOriginIndex(int caret)
        {
            if (caret == 0)
            {
                if (!base.HasSelection) return CaretColumn;
                return SelectionFrom == CaretColumn ? SelectionTo : SelectionFrom;
            }
            var secondary = GetSecondaryCaret(caret); return secondary.Anchor < 0 ? secondary.Index : secondary.Anchor;
        }
        private int GetSelectionFromIndex(int caret)
        {
            if (caret == 0) return base.HasSelection ? SelectionFrom : CaretColumn;
            var secondary = GetSecondaryCaret(caret); return secondary.Anchor < 0 ? secondary.Index : Math.Min(secondary.Anchor, secondary.Index);
        }
        private int GetSelectionToIndex(int caret)
        {
            if (caret == 0) return base.HasSelection ? SelectionTo : CaretColumn;
            var secondary = GetSecondaryCaret(caret); return secondary.Anchor < 0 ? secondary.Index : Math.Max(secondary.Anchor, secondary.Index);
        }
        private bool CaretContainsIndex(int caret, int index) => index >= GetSelectionFromIndex(caret) && index <= GetSelectionToIndex(caret);
        private static bool CaretContainsIndex(SecondaryCaret caret, int index)
        {
            var from = caret.Anchor < 0 ? caret.Index : Math.Min(caret.Anchor, caret.Index); var to = caret.Anchor < 0 ? caret.Index : Math.Max(caret.Anchor, caret.Index);
            return index >= from && index <= to;
        }
        private sealed class SecondaryCaret { public int Index; public int Anchor; }
        private sealed class CaretEdit { public int Caret; public int Start; public int End; }
        private sealed class CaretLineRange { public CaretLineRange(int first, int last) { First = first; Last = last; } public int First; public int Last; }
        private sealed class CaretState { public int Index; public int Anchor; }
        private sealed class CaretLineState { public int Line; public int Column; public int OriginLine; public int OriginColumn; public bool Selected; }
        private sealed class CaretMerge { public int Caret; public int From; public int To; public bool ContainsPrimary; }
        private sealed class TextEditGutterItem
        {
            public object Metadata;
            public string Text = string.Empty;
            public Texture2D Icon;
            public Color Color = Color.White;
            public bool Clickable;
        }
        private static bool IsWholeWord(string text, int start, int length)
        {
            return (start == 0 || !IsWordCharacter(text[start - 1])) && (start + length >= text.Length || !IsWordCharacter(text[start + length]));
        }
        private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character == '_';
    }

    /// <summary>Editable numeric field owned by a <see cref="SpinBox"/>.</summary>
    public sealed class SpinBoxLineEdit : LineEdit
    {
        internal SpinBoxLineEdit(SpinBox owner) { Owner = owner; }
        public SpinBox Owner { get; }
        internal override void KeyPressed(Keys key)
        {
            if (key == Keys.Up) Owner.StepArrow(true);
            else if (key == Keys.Down) Owner.StepArrow(false);
            else base.KeyPressed(key);
        }
    }

    /// <summary>Edits a numeric range through text entry, arrow stepping, keyboard input, or vertical dragging.</summary>
    [TemplatePart(EditorPartName, typeof(ContentPresenter))]
    public sealed class SpinBox : Range
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.SpinButton;
        public const string EditorPartName = "PART_Editor";
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private HorizontalAlignment _horizontalAlignment;
        private bool _updateOnTextChanged;
        private bool _syncingText;
        private bool _dragAllowed;
        private bool _dragging;
        private Point _dragStart;
        private int _lastDragY;
        private float _dragBaseValue;
        private float _dragDiffY;
        private Point _heldArrowPoint;
        private double _heldArrowElapsed;
        private bool _heldArrowRepeating;
        private bool _heldArrowActive;
        private const double ArrowRepeatDelaySeconds = 0.6;
        private const double ArrowRepeatIntervalSeconds = 0.075;
        public SpinBox()
        {
            FocusMode = FocusMode.All;
            LineEdit = new SpinBoxLineEdit(this) { MouseFilter = MouseFilter.Stop };
            LineEdit.TextSubmitted += (_, _) => CommitText();
            LineEdit.TextChanged += (_, _) => { if (UpdateOnTextChanged && !_syncingText) CommitText(onlyIfValid: true); };
            ValueChanged += (_, _) => SyncText();
            AddChild(LineEdit);
            SyncText();
        }
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public bool UpdateOnTextChanged { get => _updateOnTextChanged; set => _updateOnTextChanged = value; }
        public float CustomArrowStep { get; set; }
        public bool CustomArrowRound { get; set; }
        public bool IsDraggingValue => _dragging;
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); LineEdit.Font = value; QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); LineEdit.UIFont = value; QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection);
        public SpinBoxLineEdit LineEdit { get; }
        public void SetHorizontalAlignment(HorizontalAlignment alignment) { if (!Enum.IsDefined(typeof(HorizontalAlignment), alignment)) throw new ArgumentOutOfRangeException(nameof(alignment)); _horizontalAlignment = alignment; }
        public HorizontalAlignment GetHorizontalAlignment() => _horizontalAlignment;
        public void SetPrefix(string prefix) { Prefix = prefix ?? string.Empty; SyncText(); }
        public string GetPrefix() => Prefix;
        public void SetSuffix(string suffix) { Suffix = suffix ?? string.Empty; SyncText(); }
        public string GetSuffix() => Suffix;
        public void SetUpdateOnTextChanged(bool enabled) => UpdateOnTextChanged = enabled;
        public bool GetUpdateOnTextChanged() => UpdateOnTextChanged;
        public void SetSelectAllOnFocus(bool enabled) => LineEdit.SetSelectAllOnFocus(enabled);
        public bool IsSelectAllOnFocus() => LineEdit.IsSelectAllOnFocus();
        public void SetEditable(bool enabled) => LineEdit.Editable = enabled;
        public bool IsEditable() => LineEdit.Editable;
        public void SetCustomArrowStep(float customArrowStep) => CustomArrowStep = Math.Max(0, customArrowStep);
        public float GetCustomArrowStep() => CustomArrowStep;
        public void SetCustomArrowRound(bool round) => CustomArrowRound = round;
        public bool IsCustomArrowRounding() => CustomArrowRound;
        public void Apply() => CommitText();
        public LineEdit GetLineEdit() => LineEdit;
        public override Vector2 GetMinimumSize() => Vector2.Max(CustomMinimumSize, new Vector2(72, 24));
        internal override void PointerPressed(Point point)
        {
            base.PointerPressed(point);
            _dragAllowed = IsEditable();
            _dragging = false;
            _dragStart = point;
            _lastDragY = point.Y;
            _dragBaseValue = Value;
            _dragDiffY = 0;
            if (!IsEditable()) return;
            StepArrow(point.Y < Bounds.Center.Y);
            _heldArrowPoint = point;
            _heldArrowElapsed = 0;
            _heldArrowRepeating = false;
            _heldArrowActive = IsPointOnArrowButton(point);
        }
        internal override void PointerMoved(Point point)
        {
            _heldArrowPoint = point;
            if (!_dragAllowed) return;
            if (!_dragging)
            {
                var dx = point.X - _dragStart.X;
                var dy = point.Y - _dragStart.Y;
                if (dx * dx + dy * dy <= 4) return;
                _dragging = true;
                _heldArrowActive = false;
                _dragBaseValue = Value;
                _dragDiffY = 0;
                _lastDragY = point.Y;
                return;
            }
            _dragDiffY += point.Y - _lastDragY;
            _lastDragY = point.Y;
            var step = Step == 0 ? 1 : Step;
            var diff = -0.01f * MathF.Pow(MathF.Abs(_dragDiffY), 1.8f) * MathF.Sign(_dragDiffY);
            Value = _dragBaseValue + step * diff;
        }
        internal override void PointerReleased(Point point, bool isInside)
        {
            _dragAllowed = false;
            _dragging = false;
            _heldArrowActive = false;
            _heldArrowRepeating = false;
            _heldArrowElapsed = 0;
        }
        internal override void KeyPressed(Keys key) { if (key == Keys.Up) StepArrow(true); else if (key == Keys.Down) StepArrow(false); }
        internal override void Process(GameTime gameTime)
        {
            ProcessHeldArrowRepeat(gameTime);
            base.Process(gameTime);
        }
        internal void DrawSpinBoxChrome(UIRenderContext context)
        {
            context.Fill(Bounds, context.Theme.BackgroundColor); context.Border(Bounds, context.Theme.PanelBorderColor);
            context.Fill(new Rectangle(Bounds.Right - 16, Bounds.Top, 16, Math.Max(1, Bounds.Height / 2)), context.Theme.HoverColor);
            context.Fill(new Rectangle(Bounds.Right - 16, Bounds.Center.Y, 16, Math.Max(1, Bounds.Height - Bounds.Height / 2)), context.Theme.HoverColor);
            DrawArrow(context, true);
            DrawArrow(context, false);
        }
        private void DrawArrow(UIRenderContext context, bool up)
        {
            var suffix = !Enabled || !IsEditable() ? "_disabled" : _heldArrowActive && TryGetArrowButton(_heldArrowPoint, out var heldUp) && heldUp == up ? "_pressed" : string.Empty;
            var icon = GetThemeIcon((up ? "up" : "down") + suffix);
            if (!icon.HasValue) return;
            var half = up ? new Rectangle(Bounds.Right - 16, Bounds.Top, 16, Math.Max(1, Bounds.Height / 2)) : new Rectangle(Bounds.Right - 16, Bounds.Center.Y, 16, Math.Max(1, Bounds.Height - Bounds.Height / 2));
            context.Icon(icon.Value, new Vector2(half.Center.X - icon.Value.LogicalSize.X / 2, half.Center.Y - icon.Value.LogicalSize.Y / 2), Color.White);
        }
        internal void StepArrow(bool up)
        {
            if (!IsEditable()) return;
            var arrowStep = CustomArrowStep != 0 ? CustomArrowStep : Step;
            if (CustomArrowRound)
            {
                // Godot's SpinBox::_arrow_clicked: pre-snap arrow_step itself to a multiple of Step, snap
                // the CURRENT value to the nearest multiple of that arrow_step, and only step-and-resnap
                // if that snap didn't actually move in the requested direction (e.g. already exactly on
                // a multiple, or the nearest multiple undershoots).
                arrowStep = SnapToMultiple(arrowStep, Step);
                var newValue = SnapToStep(Value, arrowStep);
                if ((up && newValue <= Value) || (!up && newValue >= Value)) newValue = SnapToStep(Value + (up ? arrowStep : -arrowStep), arrowStep);
                Value = newValue;
            }
            else Value += up ? arrowStep : -arrowStep;
        }
        private void ProcessHeldArrowRepeat(GameTime gameTime)
        {
            if (!_heldArrowActive || _dragging || !IsEditable()) return;
            _heldArrowElapsed += gameTime.ElapsedGameTime.TotalSeconds;
            var delay = _heldArrowRepeating ? ArrowRepeatIntervalSeconds : ArrowRepeatDelaySeconds;
            while (_heldArrowElapsed >= delay)
            {
                _heldArrowElapsed -= delay;
                _heldArrowRepeating = true;
                delay = ArrowRepeatIntervalSeconds;
                if (TryGetArrowButton(_heldArrowPoint, out var up))
                    StepArrow(up);
            }
        }
        private bool IsPointOnArrowButton(Point point) => TryGetArrowButton(point, out _);
        private bool TryGetArrowButton(Point point, out bool up)
        {
            up = point.Y < Bounds.Center.Y;
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return false;
            if (point.X < Bounds.Right - 16 || point.X >= Bounds.Right) return false;
            if (point.Y < Bounds.Top || point.Y >= Bounds.Bottom) return false;
            return true;
        }
        private void CommitText(bool onlyIfValid = false)
        {
            var text = StripAffixes(LineEdit.Text);
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) Value = value;
            else if (!onlyIfValid) SyncText();
        }
        private string StripAffixes(string text)
        {
            text = text ?? string.Empty;
            if (!string.IsNullOrEmpty(Prefix) && text.StartsWith(Prefix + " ", StringComparison.Ordinal)) text = text.Substring(Prefix.Length + 1);
            if (!string.IsNullOrEmpty(Suffix) && text.EndsWith(" " + Suffix, StringComparison.Ordinal)) text = text.Substring(0, text.Length - Suffix.Length - 1);
            return text;
        }
        private void SyncText()
        {
            _syncingText = true;
            var value = Value.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(Prefix)) value = Prefix + " " + value;
            if (!string.IsNullOrEmpty(Suffix)) value += " " + Suffix;
            LineEdit.Text = value;
            _syncingText = false;
        }
    }

    /// <summary>Godot-style OptionButton item state.</summary>
    public sealed class OptionButtonItem
    {
        internal OptionButtonItem(string text, int id, bool separator) { Text = text ?? string.Empty; Id = id; Separator = separator; }
        public string Text { get; internal set; }
        public Texture2D Icon { get; internal set; }
        public int Id { get; internal set; }
        public object Metadata { get; internal set; }
        public string Tooltip { get; internal set; } = string.Empty;
        public AutoTranslateMode AutoTranslateMode { get; internal set; } = AutoTranslateMode.Inherit;
        public bool Disabled { get; internal set; }
        public bool Separator { get; }
    }

    /// <summary>Opens a popup menu of choices and reflects the selected item's text and icon.</summary>
    public sealed class OptionButton : BaseButton
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.ComboBox;
        private readonly List<OptionButtonItem> _items = new List<OptionButtonItem>();
        public OptionButton()
        {
            // Godot's constructor sets toggle_mode(true), text_alignment(LEFT), and
            // action_mode(BUTTON_PRESS), and resets the pressed look when the popup closes.
            ToggleMode = true;
            TextAlignment = HorizontalAlignment.Left;
            ActionMode = ButtonActionMode.Press;
            Popup = new PopupMenu { Visible = false };
            // Godot's OptionButton::pressed() closes an already-open popup instead of reopening it.
            Pressed += (_, _) => { if (Popup.Visible) Popup.Hide(); else ShowPopup(); };
            Popup.IndexPressed += (_, index) => Select(index, true);
            Popup.IndexFocused += (_, index) => ItemFocused?.Invoke(this, index);
            Popup.PopupHidden += (_, _) => SetPressedNoSignal(false);
        }
        public PopupMenu Popup { get; }
        public IReadOnlyList<OptionButtonItem> ItemItems => _items;
        public IReadOnlyList<string> Items
        {
            get { var values = new List<string>(_items.Count); foreach (var item in _items) values.Add(item.Text); return values; }
        }
        public int Selected { get; private set; } = -1;
        // Godot's remove_item doesn't shift `current` down for later indices (see RemoveItem), so a
        // stale `current`/Selected can end up pointing past the shrunk item list; Godot's own
        // get_item_id/get_item_metadata gracefully fall back via the popup's own bounds check rather
        // than crash, so these guard the upper bound too, not just Selected < 0.
        public int SelectedId => Selected < 0 || Selected >= _items.Count ? -1 : _items[Selected].Id;
        public object SelectedMetadata => Selected < 0 || Selected >= _items.Count ? null : _items[Selected].Metadata;
        public bool FitToLongestItem { get; set; } = true;
        public bool AllowReselect { get; set; }
        /// <summary>Gates OptionButton's own accelerator/shortcut item activation, matching Godot's disable_shortcuts property.</summary>
        public bool DisableShortcuts { get; set; }
        public event Action<OptionButton, int> ItemSelected;
        /// <summary>Raised whenever the popup's focused item changes (keyboard navigation, incremental
        /// search, hover), matching Godot's item_focused signal wired from the popup's id_focused.</summary>
        public event Action<OptionButton, int> ItemFocused;
        /// <summary>Activates a matching item's accelerator/shortcut directly, without opening the popup, mirroring Godot's OptionButton::shortcut_input.</summary>
        internal override bool ShortcutInput(Keys key, KeyboardState keyboard)
        {
            if (!DisableShortcuts && Enabled && Visible && Popup.ActivateItemByShortcut(key, keyboard)) return true;
            return base.ShortcutInput(key, keyboard);
        }
        public int AddItem(string text, int id = -1) => AddItemCore(text, null, id, false);
        public int AddIconItem(Texture2D icon, string text, int id = -1) => AddItemCore(text, icon, id, false);
        public int AddSeparator(string text = "") => AddItemCore(text, null, -1, true);
        public void Clear() { _items.Clear(); Popup.Clear(); Selected = -1; Text = string.Empty; Icon = null; }
        public void RemoveItem(int index)
        {
            // Godot's OptionButton::remove_item only resets the selection when the removed index is
            // exactly the current one - it does NOT shift `current` down for later indices, so selecting
            // a later item then removing an earlier one leaves `current` pointing at the wrong item. A
            // real Godot quirk, matched here for behavioral parity rather than "fixed".
            ValidateIndex(index); _items.RemoveAt(index); Popup.Clear(); RebuildPopup();
            if (Selected == index) { Selected = -1; Text = string.Empty; }
        }
        public void SetItemCount(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            while (_items.Count < count) AddItem(string.Empty);
            while (_items.Count > count) RemoveItem(_items.Count - 1);
        }
        public void SetItemText(int index, string text) { ValidateIndex(index); _items[index].Text = text ?? string.Empty; Popup.SetItemText(index, _items[index].Text); if (Selected == index) Text = _items[index].Text; }
        public string GetItemText(int index) { ValidateIndex(index); return _items[index].Text; }
        public void SetItemIcon(int index, Texture2D icon) { ValidateIndex(index); _items[index].Icon = icon; Popup.SetItemIcon(index, icon); if (Selected == index) Icon = icon; }
        public Texture2D GetItemIcon(int index) { ValidateIndex(index); return _items[index].Icon; }
        public void SetItemId(int index, int id) { ValidateIndex(index); _items[index].Id = id; Popup.SetItemId(index, id); }
        public int GetItemId(int index) { ValidateIndex(index); return _items[index].Id; }
        public int GetItemIndex(int id) { for (var i = 0; i < _items.Count; i++) if (_items[i].Id == id) return i; return -1; }
        public void SetItemMetadata(int index, object metadata) { ValidateIndex(index); _items[index].Metadata = metadata; Popup.SetItemMetadata(index, metadata); }
        public object GetItemMetadata(int index) { ValidateIndex(index); return _items[index].Metadata; }
        public void SetItemTooltip(int index, string tooltip) { ValidateIndex(index); _items[index].Tooltip = tooltip ?? string.Empty; Popup.SetItemTooltip(index, _items[index].Tooltip); }
        public string GetItemTooltip(int index) { ValidateIndex(index); return _items[index].Tooltip; }
        public void SetItemAutoTranslateMode(int index, AutoTranslateMode mode)
        {
            ValidateIndex(index);
            if (!Enum.IsDefined(typeof(AutoTranslateMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            _items[index].AutoTranslateMode = mode;
            Popup.SetItemAutoTranslateMode(index, mode);
        }
        public AutoTranslateMode GetItemAutoTranslateMode(int index) { ValidateIndex(index); return _items[index].AutoTranslateMode; }
        public void SetItemDisabled(int index, bool disabled) { ValidateIndex(index); _items[index].Disabled = disabled; Popup.GetItem(index).Disabled = disabled; }
        public bool IsItemDisabled(int index) { ValidateIndex(index); return _items[index].Disabled; }
        public bool IsItemSeparator(int index) { ValidateIndex(index); return _items[index].Separator; }
        public int GetItemCount() => _items.Count;
        public void SetFitToLongestItem(bool fit) { FitToLongestItem = fit; QueueLayout(); }
        public bool IsFitToLongestItem() => FitToLongestItem;
        public void SetAllowReselect(bool allow) => AllowReselect = allow;
        public bool GetAllowReselect() => AllowReselect;
        public void SetSearchBarEnabled(bool enabled) => Popup.SetSearchBarEnabled(enabled);
        public bool IsSearchBarEnabled() => Popup.IsSearchBarEnabled();
        public void SetSearchBarMinItemCount(int count) => Popup.SetSearchBarMinItemCount(count);
        public int GetSearchBarMinItemCount() => Popup.GetSearchBarMinItemCount();
        public void SetSearchBarFuzzySearchEnabled(bool enabled) => Popup.SetSearchBarFuzzySearchEnabled(enabled);
        public bool IsSearchBarFuzzySearchEnabled() => Popup.IsSearchBarFuzzySearchEnabled();
        public void SetSearchBarFuzzySearchMaxMisses(int maxMisses) => Popup.SetSearchBarFuzzySearchMaxMisses(maxMisses);
        public int GetSearchBarFuzzySearchMaxMisses() => Popup.GetSearchBarFuzzySearchMaxMisses();
        public bool HasSelectableItems() => GetSelectableItem() >= 0;
        public int GetSelectableItem(bool fromLast = false)
        {
            if (fromLast)
            {
                for (var reverseIndex = _items.Count - 1; reverseIndex >= 0; reverseIndex--)
                    if (IsSelectable(reverseIndex)) return reverseIndex;
            }
            else
            {
                for (var forwardIndex = 0; forwardIndex < _items.Count; forwardIndex++)
                    if (IsSelectable(forwardIndex)) return forwardIndex;
            }
            return -1;
        }
        public void Select(int index) => Select(index, false);
        public void Select(int index, bool emitSignal)
        {
            if (Selected == index && !AllowReselect) return;
            if (index < 0)
            {
                for (var itemIndex = 0; itemIndex < Popup.GetItemCount(); itemIndex++) Popup.SetItemChecked(itemIndex, false);
                Selected = -1; Text = string.Empty; Icon = null; return;
            }
            // Godot's OptionButton::_select never checks selectability - select() can target a disabled
            // or separator item directly; only the popup's own UI prevents clicking one.
            ValidateIndex(index);
            for (var itemIndex = 0; itemIndex < Popup.GetItemCount(); itemIndex++) Popup.SetItemChecked(itemIndex, itemIndex == index);
            Selected = index; Text = _items[index].Text; Icon = _items[index].Icon;
            if (emitSignal) ItemSelected?.Invoke(this, index);
        }
        public void ShowPopup()
        {
            if (Context == null) return;
            if (Popup.Context != Context) Context.Add(Popup);
            Popup.LayoutDirection = LayoutDirection;
            Popup.PopupAt(new Vector2(Bounds.Left, Bounds.Bottom), new Vector2(Math.Max(Bounds.Width, Popup.CustomMinimumSize.X), 0), false);
            var focusIndex = Selected >= 0 && Selected < _items.Count && !_items[Selected].Disabled ? Selected : -1;
            if (focusIndex < 0)
            {
                for (var index = 0; index < _items.Count; index++)
                    if (!_items[index].Disabled) { focusIndex = index; break; }
            }
            if (focusIndex >= 0)
            {
                if (WasActivatedByPointer) Popup.ScrollToItem(focusIndex);
                else Popup.SetFocusedItem(focusIndex);
            }
            if (Context.ViewportSize.X > 0 || Context.ViewportSize.Y > 0)
            {
                var maxX = Context.ViewportSize.X > 0 ? Math.Max(0, Context.ViewportSize.X - Popup.Size.X) : Popup.Position.X;
                var maxY = Context.ViewportSize.Y > 0 ? Math.Max(0, Context.ViewportSize.Y - Popup.Size.Y) : Popup.Position.Y;
                Popup.Position = new Vector2(MathHelper.Clamp(Popup.Position.X, 0, maxX), MathHelper.Clamp(Popup.Position.Y, 0, maxY));
            }
        }
        public override Vector2 GetMinimumSize()
        {
            var result = base.GetMinimumSize();
            var arrow = GetThemeIcon("arrow");
            if (arrow.HasValue)
            {
                result.X += arrow.Value.LogicalSize.X + IconSeparation;
                result.Y = Math.Max(result.Y, arrow.Value.LogicalSize.Y + Padding.Vertical);
            }
            if (!FitToLongestItem || EffectiveUIFont == null) return result;
            foreach (var item in _items)
            {
                if (item.Separator) continue;
                // Matches Godot's _refresh_size_cache measuring get_minimum_size_for_text_and_icon per
                // item, folding in each item's own icon width, not just the currently-selected item's.
                var iconWidth = item.Icon != null ? item.Icon.Width + IconSeparation : 0;
                result.X = Math.Max(result.X, TextMetrics.Measure(EffectiveUIFont, item.Text).X + iconWidth + Padding.Horizontal);
            }
            return result;
        }
        private int AddItemCore(string text, Texture2D icon, int id, bool separator)
        {
            // Godot's add_item/add_icon_item check !has_selectable_items() BEFORE adding, not whether
            // Selected is currently -1 - these diverge once a previously-selected item has since become
            // disabled (Selected stays >= 0 pointing at it, but no item is actually selectable).
            var firstSelectable = !separator && !HasSelectableItems();
            var index = _items.Count;
            _items.Add(new OptionButtonItem(text, id < 0 ? index : id, separator) { Icon = icon });
            if (separator) Popup.AddSeparator(text, id < 0 ? index : id);
            else if (icon != null) Popup.AddIconRadioCheckItem(icon, text, id < 0 ? index : id);
            else Popup.AddRadioCheckItem(text, id < 0 ? index : id);
            if (firstSelectable) Select(index);
            return index;
        }
        private void RebuildPopup()
        {
            Popup.Clear();
            for (var index = 0; index < _items.Count; index++)
            {
                var item = _items[index];
                var popupItem = item.Separator ? Popup.AddSeparator(item.Text, item.Id) :
                    item.Icon != null ? Popup.AddIconRadioCheckItem(item.Icon, item.Text, item.Id) : Popup.AddRadioCheckItem(item.Text, item.Id);
                popupItem.Disabled = item.Disabled;
                popupItem.Metadata = item.Metadata;
                popupItem.Tooltip = item.Tooltip;
                popupItem.AutoTranslateMode = item.AutoTranslateMode;
                popupItem.Checked = index == Selected;
            }
        }
        private bool IsSelectable(int index) => !_items[index].Separator && !_items[index].Disabled;
        private void ValidateIndex(int index) { if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index)); }
    }

    /// <summary>Displays one child page at a time and provides tabs for switching the selected page.</summary>
    public sealed class TabContainer : TemplatedControl
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.TabPanel;
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private sealed class TabPageState
        {
            public string Title;
            public Texture2D Icon;
            public Texture2D ButtonIcon;
            public string Tooltip = string.Empty;
            public object Metadata;
            public int IconMaxWidth;
            public bool Disabled;
            public bool Hidden;
        }

        private readonly Dictionary<Control, TabPageState> _tabStates = new Dictionary<Control, TabPageState>();
        private int _currentTab;
        private int _previousTab = -1;
        private int _draggedTab = -1;
        private int _hoveredTab = -1;
        private Popup _popup;
        private bool _deselectEnabled;
        private const int PopupButtonWidth = 20;
        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (Children.Count == 0) { _currentTab = 0; return; }
                if (value == -1)
                {
                    if (!DeselectEnabled) throw new InvalidOperationException("Cannot deselect tabs when deselection is disabled.");
                    _previousTab = _currentTab;
                    TabSelected?.Invoke(this, -1);
                    if (_currentTab == -1) return;
                    _currentTab = -1; UpdateVisibility();
                    TabChanged?.Invoke(this, -1);
                    return;
                }
                var next = Math.Max(0, Math.Min(value, Children.Count - 1));
                // Godot's TabContainer::set_current_tab forwards straight to the internal TabBar's
                // set_current_tab, which has no disabled/hidden guard - only clicking a tab blocks that.
                _previousTab = _currentTab;
                TabSelected?.Invoke(this, next);
                if (_currentTab == next) return;
                _currentTab = next; UpdateVisibility(); TabChanged?.Invoke(this, next);
            }
        }
        /// <summary>The tab selected immediately before the current one, matching Godot's get_previous_tab.</summary>
        public int GetPreviousTab() => _previousTab;
        /// <summary>Allows CurrentTab to become -1, matching Godot's deselect_enabled property.</summary>
        public bool DeselectEnabled { get => _deselectEnabled; set => _deselectEnabled = value; }
        public void SetDeselectEnabled(bool enabled) => DeselectEnabled = enabled;
        public bool GetDeselectEnabled() => DeselectEnabled;
        public float TabHeight { get; set; } = 28;
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection);
        /// <summary>Enables pointer drag reordering of tab pages, like Godot's TabContainer.</summary>
        public bool DragToRearrangeEnabled { get; set; }
        /// <summary>Optional group identifier reserved for compatibility with Godot tab-container rearrangement groups.</summary>
        public int TabsRearrangeGroup { get; set; } = -1;
        public event Action<TabContainer, int> TabChanged;
        /// <summary>Raised on every tab selection attempt, even reselecting the current tab, matching Godot's tab_selected signal (TabChanged only fires when the selection actually changes).</summary>
        public event Action<TabContainer, int> TabSelected;
        /// <summary>Raised when a (non-disabled) tab is clicked, matching Godot's tab_clicked signal.</summary>
        public event Action<TabContainer, int> TabClicked;
        /// <summary>Raised when the pointer enters a tab header, matching Godot's tab_hovered signal.</summary>
        public event Action<TabContainer, int> TabHovered;
        /// <summary>Raised when a tab's configured button icon is pressed.</summary>
        public event Action<TabContainer, int> TabButtonPressed;
        /// <summary>Raised while the selected tab page is moved through the header strip.</summary>
        public event Action<TabContainer, int> ActiveTabRearranged;
        /// <summary>Raised immediately before the attached popup is shown, matching Godot's pre_popup_pressed signal.</summary>
        public event EventHandler PrePopupPressed;
        /// <summary>Returns the page Control at the given tab index, matching Godot's get_tab_control.</summary>
        public Control GetTabControl(int tab) { if (tab < 0 || tab >= Children.Count) throw new ArgumentOutOfRangeException(nameof(tab)); return Children[tab]; }
        /// <summary>Returns the currently selected page Control, or null when deselected, matching Godot's get_current_tab_control.</summary>
        public Control GetCurrentTabControl() => CurrentTab >= 0 && CurrentTab < Children.Count ? Children[CurrentTab] : null;
        /// <summary>Selects the nearest available tab before the current one, wrapping around; returns whether one was found, matching Godot's select_previous_available.</summary>
        public bool SelectPreviousAvailable() => SelectAvailableTab(-1);
        /// <summary>Selects the nearest available tab after the current one, wrapping around; returns whether one was found, matching Godot's select_next_available.</summary>
        public bool SelectNextAvailable() => SelectAvailableTab(1);
        public void NextTab() => SelectAvailableTab(1);
        public void PreviousTab() => SelectAvailableTab(-1);
        /// <summary>Attaches an arbitrary popup opened from a header button, matching Godot's TabContainer::set_popup.</summary>
        public void SetPopup(Popup popup)
        {
            if (_popup == popup) return;
            var hadPopup = _popup != null;
            _popup = popup;
            if (hadPopup != (popup != null)) QueueLayout();
        }
        /// <summary>Returns the popup attached with <see cref="SetPopup"/>, matching Godot's TabContainer::get_popup.</summary>
        public Popup GetPopup() => _popup;
        /// <summary>Returns the header rectangle of the popup button, empty when no popup is attached.</summary>
        public Rectangle GetPopupButtonRectangle()
        {
            if (_popup == null) return Rectangle.Empty;
            return IsLayoutRtl()
                ? new Rectangle(Bounds.X, Bounds.Y, PopupButtonWidth, (int)TabHeight)
                : new Rectangle(Bounds.Right - PopupButtonWidth, Bounds.Y, PopupButtonWidth, (int)TabHeight);
        }
        private void ShowPopupAtButton()
        {
            if (_popup == null) return;
            PrePopupPressed?.Invoke(this, EventArgs.Empty);
            if (Context != null && _popup.Context != Context) Context.Add(_popup);
            var button = GetPopupButtonRectangle();
            var x = IsLayoutRtl() ? button.X : button.X + button.Width - (int)_popup.Size.X;
            var y = button.Bottom;
            _popup.PopupAt(new Vector2(x, y));
        }
        public string GetTabTitle(int tab) => GetState(tab).Title ?? Children[tab].Name ?? string.Empty;
        public void SetTabTitle(int tab, string title) { GetState(tab).Title = title ?? string.Empty; QueueLayout(); }
        public Texture2D GetTabIcon(int tab) => GetState(tab).Icon;
        public void SetTabIcon(int tab, Texture2D icon) { GetState(tab).Icon = icon; QueueLayout(); }
        public Texture2D GetTabButtonIcon(int tab) => GetState(tab).ButtonIcon;
        public void SetTabButtonIcon(int tab, Texture2D icon) => GetState(tab).ButtonIcon = icon;
        public string GetTabTooltip(int tab) => GetState(tab).Tooltip;
        public void SetTabTooltip(int tab, string tooltip) => GetState(tab).Tooltip = tooltip ?? string.Empty;
        public object GetTabMetadata(int tab) => GetState(tab).Metadata;
        public void SetTabMetadata(int tab, object metadata) => GetState(tab).Metadata = metadata;
        public int GetTabIconMaxWidth(int tab) => GetState(tab).IconMaxWidth;
        public void SetTabIconMaxWidth(int tab, int width) { GetState(tab).IconMaxWidth = Math.Max(0, width); QueueLayout(); }
        public bool IsTabDisabled(int tab) => GetState(tab).Disabled;
        public void SetTabDisabled(int tab, bool disabled) { GetState(tab).Disabled = disabled; EnsureCurrentTab(); QueueLayout(); }
        public bool IsTabHidden(int tab) => GetState(tab).Hidden;
        public void SetTabHidden(int tab, bool hidden) { GetState(tab).Hidden = hidden; EnsureCurrentTab(); QueueLayout(); }
        /// <summary>Moves a tab page while preserving the selected page.</summary>
        public void MoveTab(int from, int to)
        {
            if (from < 0 || from >= Children.Count) throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= Children.Count) throw new ArgumentOutOfRangeException(nameof(to));
            if (from == to) return;
            MoveChild(Children[from], to);
            if (_currentTab == from) _currentTab = to;
            else if (_currentTab > from && _currentTab <= to) _currentTab--;
            else if (_currentTab < from && _currentTab >= to) _currentTab++;
            UpdateVisibility();
        }
        public override string GetTooltip(Point position)
        {
            var tab = GetTabAt(position);
            return tab >= 0 && !string.IsNullOrEmpty(GetState(tab).Tooltip) ? GetState(tab).Tooltip : base.GetTooltip(position);
        }
        /// <summary>Matches Godot's TabContainer::_get_minimum_size: the tab-bar height plus (by
        /// default) only the CURRENT page's minimum size, not the max over every page - Godot only
        /// folds in every page when use_hidden_tabs_for_min_size is enabled, which this port doesn't
        /// model. The popup button, when attached, adds its width like Godot's popup_button branch.</summary>
        public override Vector2 GetMinimumSize()
        {
            var current = CurrentTab >= 0 && CurrentTab < Children.Count ? Children[CurrentTab] : null;
            var contentMin = current?.GetMinimumSize() ?? Vector2.Zero;
            var width = contentMin.X + (_popup != null ? PopupButtonWidth : 0);
            return Vector2.Max(CustomMinimumSize, new Vector2(width, TabHeight + contentMin.Y));
        }
        protected override void ArrangeChildren()
        {
            base.ArrangeChildren();
            UpdateVisibility();
            foreach (var child in Children) { child.Position = new Vector2(0, TabHeight); child.Size = new Vector2(Size.X, Math.Max(0, Size.Y - TabHeight)); }
        }
        private void UpdateVisibility() { for (var i = 0; i < Children.Count; i++) Children[i].Visible = i == CurrentTab && !GetState(i).Hidden; }
        internal override void PointerPressed(Point point)
        {
            if (_popup != null && GetPopupButtonRectangle().Contains(point)) { ShowPopupAtButton(); return; }
            if (point.Y >= Bounds.Top && point.Y < Bounds.Top + TabHeight && Children.Count > 0)
            {
                var index = GetTabAt(point);
                UpdateHoveredTab(point);
                if (index >= 0 && GetState(index).ButtonIcon != null && GetTabButtonRectangle(index).Contains(point))
                {
                    TabButtonPressed?.Invoke(this, index);
                    GrabFocus();
                    return;
                }
                // Godot's gui_input only selects/emits tab_clicked for a hit, non-disabled tab.
                if (index >= 0 && !GetState(index).Disabled)
                {
                    CurrentTab = index;
                    TabClicked?.Invoke(this, index);
                }
                _draggedTab = DragToRearrangeEnabled ? index : -1;
                GrabFocus();
            }
            else base.PointerPressed(point);
        }
        internal override void PointerMoved(Point point)
        {
            UpdateHoveredTab(point);
            if (_draggedTab < 0) return;
            var target = GetTabAt(point);
            if (target == _draggedTab) return;
            MoveTab(_draggedTab, target);
            _draggedTab = target;
            ActiveTabRearranged?.Invoke(this, target);
        }
        internal override void PointerEntered() { UpdateHoveredTab(Context?.PointerPosition ?? Point.Zero); base.PointerEntered(); }
        internal override void PointerExited() { _hoveredTab = -1; base.PointerExited(); }
        internal override void PointerReleased(Point point, bool isInside) { _draggedTab = -1; }
        internal void DrawTabContainerChrome(UIRenderContext context)
        {
            context.Fill(new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, (int)TabHeight), context.Theme.BackgroundColor);
            var strip = GetTabStripRectangle();
            var visible = GetVisibleTabs();
            var width = visible.Count == 0 ? strip.Width : Math.Max(1, strip.Width / visible.Count);
            for (var order = 0; order < visible.Count; order++)
            {
                var i = visible[order]; var state = GetState(i);
                var rect = new Rectangle(strip.X + width * order, Bounds.Y, order == visible.Count - 1 ? strip.Right - (strip.X + width * order) : width, (int)TabHeight);
                context.Fill(rect, i == CurrentTab ? context.Theme.PanelColor : context.Theme.BackgroundColor); context.Border(rect, context.Theme.PanelBorderColor);
                var textX = rect.X + 6;
                if (state.Icon != null)
                {
                    var iconHeight = Math.Max(1, Math.Min(16, rect.Height - 4));
                    var iconWidth = Math.Max(1, (int)MathF.Round(iconHeight * state.Icon.Width / (float)Math.Max(1, state.Icon.Height)));
                    if (state.IconMaxWidth > 0) iconWidth = Math.Min(iconWidth, state.IconMaxWidth);
                    var icon = new Rectangle(textX, rect.Y + (rect.Height - iconHeight) / 2, iconWidth, iconHeight);
                    context.SpriteBatch.Draw(state.Icon, icon, Color.White); textX = icon.Right + 4;
                }
                if (EffectiveUIFont != null)
                {
                    var title = GetTabTitle(i);
                    context.Text(EffectiveUIFont, title, new Vector2(textX, rect.Y + Math.Max(2, (rect.Height - TextMetrics.LineHeight(EffectiveUIFont)) / 2)), state.Disabled ? context.Theme.DisabledTextColor : context.Theme.TextColor);
                }
                if (state.ButtonIcon != null) context.SpriteBatch.Draw(state.ButtonIcon, GetTabButtonRectangle(i), Color.White);
            }
            if (_popup != null)
            {
                var button = GetPopupButtonRectangle();
                var hovered = button.Contains(Context?.PointerPosition ?? Point.Zero);
                var menu = GetThemeIcon(hovered ? "menu_highlight" : "menu");
                if (menu.HasValue) context.Icon(menu.Value, new Vector2(button.Center.X - menu.Value.LogicalSize.X / 2, button.Center.Y - menu.Value.LogicalSize.Y / 2), Color.White);
            }
        }
        private TabPageState GetState(int tab)
        {
            if (tab < 0 || tab >= Children.Count) throw new ArgumentOutOfRangeException(nameof(tab));
            var child = Children[tab];
            if (!_tabStates.TryGetValue(child, out var state)) { state = new TabPageState(); _tabStates.Add(child, state); }
            return state;
        }
        private bool IsTabAvailable(int tab) { var state = GetState(tab); return !state.Disabled && !state.Hidden; }
        private void EnsureCurrentTab()
        {
            if (Children.Count == 0 || IsTabAvailable(_currentTab)) { UpdateVisibility(); return; }
            for (var i = 0; i < Children.Count; i++)
                if (IsTabAvailable(i)) { _currentTab = i; UpdateVisibility(); return; }
            UpdateVisibility();
        }
        private bool SelectAvailableTab(int direction)
        {
            if (Children.Count == 0) return false;
            // Godot's get_next_available/get_previous_available only ever visit the OTHER tabs once
            // each (loop bound Children.Count - 1 steps) - they never wrap back around to re-consider
            // the current tab itself as a valid "next available" result.
            for (var i = 1; i < Children.Count; i++)
            {
                var tab = (_currentTab + direction * i + Children.Count) % Children.Count;
                if (IsTabAvailable(tab)) { CurrentTab = tab; return true; }
            }
            return false;
        }
        private List<int> GetVisibleTabs()
        {
            var tabs = new List<int>();
            for (var i = 0; i < Children.Count; i++) if (!GetState(i).Hidden) tabs.Add(i);
            return tabs;
        }
        private int GetTabAt(Point point)
        {
            var visible = GetVisibleTabs();
            if (visible.Count == 0 || point.Y < Bounds.Top || point.Y >= Bounds.Top + TabHeight) return -1;
            var strip = GetTabStripRectangle();
            var width = Math.Max(1, strip.Width / visible.Count);
            var order = MathHelper.Clamp((point.X - strip.Left) / width, 0, visible.Count - 1);
            return point.X < strip.Left || point.X >= strip.Right ? -1 : visible[order];
        }
        private Rectangle GetTabRectangle(int tab)
        {
            var visible = GetVisibleTabs();
            var order = visible.IndexOf(tab);
            if (order < 0) return Rectangle.Empty;
            var strip = GetTabStripRectangle();
            var width = Math.Max(1, strip.Width / visible.Count);
            var x = strip.X + width * order;
            return new Rectangle(x, Bounds.Y, order == visible.Count - 1 ? strip.Right - x : width, (int)TabHeight);
        }
        private Rectangle GetTabButtonRectangle(int tab)
        {
            var rect = GetTabRectangle(tab);
            return rect == Rectangle.Empty ? Rectangle.Empty : new Rectangle(rect.Right - 14, rect.Y + Math.Max(3, (rect.Height - 10) / 2), 10, 10);
        }
        private void UpdateHoveredTab(Point point)
        {
            var hovered = GetTabAt(point);
            if (hovered == _hoveredTab) return;
            _hoveredTab = hovered;
            if (hovered >= 0) TabHovered?.Invoke(this, hovered);
        }
        /// <summary>Returns the header rectangle available for tabs, excluding the popup button's reserved space.</summary>
        private Rectangle GetTabStripRectangle()
        {
            if (_popup == null) return new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, (int)TabHeight);
            var width = Math.Max(0, Bounds.Width - PopupButtonWidth);
            var x = IsLayoutRtl() ? Bounds.X + PopupButtonWidth : Bounds.X;
            return new Rectangle(x, Bounds.Y, width, (int)TabHeight);
        }
    }

    /// <summary>Visual overflow hint placement, corresponding to Godot's <c>ScrollContainer.ScrollHintMode</c>.</summary>
    public enum ScrollContainerScrollHintMode { Disabled, All, TopAndLeft, BottomAndRight }

    /// <summary>Clips content to a viewport and provides horizontal and vertical scrolling when it overflows.</summary>
    [TemplatePart(ScrollPresenterPartName, typeof(ScrollPresenter))]
    public sealed class ScrollContainer : TemplatedControl, IScrollViewportOwner
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.ScrollView;
        public override AccessibilityActions AccessibilityActions => base.AccessibilityActions | AccessibilityActions.Scroll;
        public const string ScrollPresenterPartName = "PART_ScrollPresenter";
        private readonly ScrollViewportController _viewportController = new ScrollViewportController();
        private Vector2 _viewportSize;
        private readonly HScrollBar _horizontalScrollBar;
        private readonly VScrollBar _verticalScrollBar;
        private Control _content;
        private ScrollPresenter _scrollPresenter;
        private ScrollBarVisibility _horizontalScrollMode = ScrollBarVisibility.Auto;
        private ScrollBarVisibility _verticalScrollMode = ScrollBarVisibility.Auto;
        private Control _followedFocus;
        private Vector2 _focusScrollDiff;

        public ScrollContainer()
        {
            ClipContents = true;
            _horizontalScrollBar = new HScrollBar { ZIndex = 1, Visible = false };
            _verticalScrollBar = new VScrollBar { ZIndex = 1, Visible = false };
            _horizontalScrollBar.ValueChanged += (_, value) => ScrollOffset = new Vector2(value, ScrollOffset.Y);
            _verticalScrollBar.ValueChanged += (_, value) => ScrollOffset = new Vector2(ScrollOffset.X, value);
            _viewportController.ScrollStarted += (_, _) => ScrollStarted?.Invoke(this, EventArgs.Empty);
            _viewportController.ScrollEnded += (_, _) => ScrollEnded?.Invoke(this, EventArgs.Empty);
            _viewportController.MetricsChanged += OnViewportMetricsChanged;
            base.AddChild(_horizontalScrollBar);
            base.AddChild(_verticalScrollBar);
            ApplyTemplate();
        }

        public Control Content
        {
            get => _content;
            set
            {
                if (ReferenceEquals(_content, value)) return;
                if (value == _horizontalScrollBar || value == _verticalScrollBar)
                    throw new InvalidOperationException("ScrollContainer scrollbar chrome cannot be assigned as content.");
                if (value != null && value.Parent != null && value.Parent != this)
                    throw new InvalidOperationException("Scroll content is already owned by another control.");
                var previous = _content;
                if (value != null && value.Parent == null) base.AddChild(value);
                try
                {
                    if (_scrollPresenter != null) _scrollPresenter.Content = value;
                    _content = value;
                    if (previous?.Parent == this) base.RemoveChild(previous);
                    OnPropertyChanged(nameof(Content));
                    QueueLayout();
                }
                catch
                {
                    if (value?.Parent == this) base.RemoveChild(value);
                    if (previous != null && previous.Parent == null) base.AddChild(previous);
                    if (_scrollPresenter != null) _scrollPresenter.Content = previous;
                    throw;
                }
            }
        }

        public override void AddChild(Control child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (child == _horizontalScrollBar || child == _verticalScrollBar)
            {
                base.AddChild(child);
                return;
            }
            if (_content != null && !ReferenceEquals(_content, child))
                throw new InvalidOperationException("ScrollContainer accepts one content control.");
            Content = child;
        }
        public Vector2 ScrollOffset
        {
            get => _viewportController.Offset;
            set
            {
                UpdateControllerMetrics();
                _viewportController.Offset = value;
                _horizontalScrollBar.SetValueNoSignal(ScrollOffset.X);
                _verticalScrollBar.SetValueNoSignal(ScrollOffset.Y);
                _scrollPresenter?.QueueLayout();
                QueueLayout();
            }
        }
        /// <summary>Horizontal scroll position, corresponding to Godot's h_scroll property.</summary>
        public int HorizontalScroll { get => (int)ScrollOffset.X; set { ScrollOffset = new Vector2(value, ScrollOffset.Y); CancelTouchDragScroll(); } }
        /// <summary>Vertical scroll position, corresponding to Godot's v_scroll property.</summary>
        public int VerticalScroll { get => (int)ScrollOffset.Y; set { ScrollOffset = new Vector2(ScrollOffset.X, value); CancelTouchDragScroll(); } }
        /// <summary>Forwards to the underlying scrollbar's own CustomStep, matching Godot's
        /// set_horizontal_custom_step/set_vertical_custom_step, which themselves just forward to
        /// h_scroll/v_scroll's set_custom_step - this has no effect on mouse-wheel scroll amount,
        /// which Godot always computes from the scrollbar's page instead (see PointerWheel).</summary>
        public float HorizontalCustomStep { get => _horizontalScrollBar.CustomStep; set => _horizontalScrollBar.CustomStep = value; }
        public float VerticalCustomStep { get => _verticalScrollBar.CustomStep; set => _verticalScrollBar.CustomStep = value; }
        /// <summary>Scrolls the focused descendant into view as focus changes, matching Godot's follow_focus property.</summary>
        public bool FollowFocus { get; set; }
        /// <summary>Scrolls while a retained drag hovers near an edge, matching Godot's scroll_on_drag_hover property.</summary>
        public bool ScrollOnDragHover { get; set; }
        public int DragHoverScrollBorder { get; set; } = 20;
        public float DragHoverScrollSpeed { get; set; } = 12;
        /// <summary>Retained touch-drag deadzone state, corresponding to Godot's scroll_deadzone property.</summary>
        public int ScrollDeadzone { get => _viewportController.ScrollDeadzone; set => _viewportController.ScrollDeadzone = value; }
        /// <summary>Retained overflow hint mode.</summary>
        public ScrollContainerScrollHintMode ScrollHintMode { get; private set; }
        /// <summary>Retains Godot's texture tiling policy for themed scroll hints.</summary>
        public bool TileScrollHint { get; private set; }
        /// <summary>Retained draw-focus-border policy.</summary>
        public bool DrawFocusBorder { get; set; }
        /// <summary>Retained default wheel-axis policy. Touch/trackpad gesture parity remains platform-dependent.</summary>
        public bool ScrollHorizontalByDefault { get; set; }
        public ScrollBarVisibility HorizontalScrollMode { get => _horizontalScrollMode; set { _horizontalScrollMode = value; UpdateControllerAxes(); QueueLayout(); } }
        public ScrollBarVisibility VerticalScrollMode { get => _verticalScrollMode; set { _verticalScrollMode = value; UpdateControllerAxes(); QueueLayout(); } }
        public HScrollBar HorizontalScrollBar => _horizontalScrollBar;
        public VScrollBar VerticalScrollBar => _verticalScrollBar;
        public Vector2 MaxScrollOffset
        {
            get
            {
                UpdateControllerMetrics();
                return _viewportController.MaxOffset;
            }
        }
        public Vector2 Viewport => _viewportController.Viewport;
        public Vector2 Extent => _viewportController.Extent;
        internal Vector2 ScrollPresenterPosition => IsLayoutRtl() && _verticalScrollBar.Visible ? new Vector2(_verticalScrollBar.Size.X, 0) : Vector2.Zero;
        internal Vector2 ScrollPresenterSize => _viewportSize;
        public ScrollAnchor? ScrollAnchor => _viewportController.Anchor;
        // Matches Godot's set_h_scroll/set_v_scroll, which both call _cancel_drag() after applying the
        // value - unlike the shared internal ScrollOffset mutator, which the wheel/touch-drag machinery
        // itself routes through and must NOT self-cancel on every frame.
        public void ScrollTo(Vector2 offset) { ScrollOffset = offset; CancelTouchDragScroll(); }
        public void SetHScroll(int value) => HorizontalScroll = value;
        public int GetHScroll() => HorizontalScroll;
        public void SetVScroll(int value) => VerticalScroll = value;
        public int GetVScroll() => VerticalScroll;
        public void SetHorizontalCustomStep(float value) => HorizontalCustomStep = value;
        public float GetHorizontalCustomStep() => HorizontalCustomStep;
        public void SetVerticalCustomStep(float value) => VerticalCustomStep = value;
        public float GetVerticalCustomStep() => VerticalCustomStep;
        public void SetHorizontalScrollMode(ScrollBarVisibility mode) { HorizontalScrollMode = mode; }
        public ScrollBarVisibility GetHorizontalScrollMode() => HorizontalScrollMode;
        public void SetVerticalScrollMode(ScrollBarVisibility mode) { VerticalScrollMode = mode; }
        public ScrollBarVisibility GetVerticalScrollMode() => VerticalScrollMode;
        public void SetScrollHorizontalByDefault(bool enable) => ScrollHorizontalByDefault = enable;
        public bool IsScrollHorizontalByDefault() => ScrollHorizontalByDefault;
        public void SetDeadzone(int deadzone) => ScrollDeadzone = deadzone;
        public int GetDeadzone() => ScrollDeadzone;
        public void SetScrollHintMode(ScrollContainerScrollHintMode mode) { if (!Enum.IsDefined(typeof(ScrollContainerScrollHintMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode)); ScrollHintMode = mode; QueueLayout(); }
        public ScrollContainerScrollHintMode GetScrollHintMode() => ScrollHintMode;
        public void SetTileScrollHint(bool enable) { TileScrollHint = enable; QueueLayout(); }
        public bool IsScrollHintTiled() => TileScrollHint;
        public void SetFollowFocus(bool enable) => FollowFocus = enable;
        public bool IsFollowingFocus() => FollowFocus;
        public void SetScrollOnDragHover(bool enable) => ScrollOnDragHover = enable;
        public bool IsScrollOnDragHoverEnabled() => ScrollOnDragHover;
        public HScrollBar GetHScrollBar() => _horizontalScrollBar;
        public VScrollBar GetVScrollBar() => _verticalScrollBar;
        public void SetDrawFocusBorder(bool draw) => DrawFocusBorder = draw;
        public bool GetDrawFocusBorder() => DrawFocusBorder;
        /// <summary>Raised when a touch drag first crosses <see cref="ScrollDeadzone"/>, matching Godot's scroll_started signal.</summary>
        public event EventHandler ScrollStarted;
        /// <summary>Raised when a touch drag that crossed the deadzone ends, matching Godot's scroll_ended signal.</summary>
        public event EventHandler ScrollEnded;
        public event EventHandler ScrollOffsetChanged;
        public event EventHandler ViewportChanged;
        public event EventHandler ScrollExtentChanged;
        public event EventHandler<ScrollViewportMetricsChangedEventArgs> ScrollMetricsChanged;
        public bool IsTouchDragging => _viewportController.IsTouchDragging;
        public bool IsTouchDragDecelerating => _viewportController.IsTouchDragDecelerating;
        public bool IsBeyondScrollDeadzone => _viewportController.IsBeyondScrollDeadzone;
        public Vector2 TouchDragSpeed => _viewportController.TouchDragSpeed;
        /// <summary>Begins a retained touch drag, mirroring Godot's touchscreen mouse-press handling in ScrollContainer::gui_input.</summary>
        public void BeginTouchDragScroll()
        {
            UpdateControllerMetrics();
            _viewportController.BeginTouchDrag();
        }
        /// <summary>Applies relative touch motion, gating on <see cref="ScrollDeadzone"/> exactly like Godot's gui_input drag accumulation.</summary>
        public void TouchDragScrollBy(Vector2 relativeMotion)
        {
            UpdateControllerMetrics();
            _viewportController.TouchDragBy(relativeMotion);
            SynchronizeControllerOffset();
        }
        /// <summary>Ends a retained touch drag, entering inertial deceleration when speed is nonzero like Godot's release handling.</summary>
        public void EndTouchDragScroll()
        {
            _viewportController.EndTouchDrag();
        }
        /// <summary>Cancels an in-progress touch drag immediately, mirroring Godot's ScrollContainer::_cancel_drag.</summary>
        public void CancelTouchDragScroll()
        {
            _viewportController.CancelTouchDrag();
        }
        protected override void OnContextChanged(UIContext previous, UIContext current)
        {
            if (previous != null)
            {
                previous.RetainedPointerPressed -= OnTouchPointerPressed;
                previous.RetainedPointerMoved -= OnTouchPointerMoved;
                previous.RetainedPointerReleased -= OnTouchPointerReleased;
            }
            if (current != null)
            {
                current.RetainedPointerPressed += OnTouchPointerPressed;
                current.RetainedPointerMoved += OnTouchPointerMoved;
                current.RetainedPointerReleased += OnTouchPointerReleased;
            }
            base.OnContextChanged(previous, current);
        }
        private void OnTouchPointerPressed(Control target, Point point)
        {
            if (Context?.TouchscreenAvailable == true && IsTouchScrollTarget(target)) BeginTouchDragScroll();
        }
        private void OnTouchPointerMoved(Control target, Point point, Vector2 relativeMotion)
        {
            if (IsTouchScrollTarget(target)) TouchDragScrollBy(relativeMotion);
        }
        private void OnTouchPointerReleased(Control target, Point point)
        {
            if (IsTouchScrollTarget(target)) EndTouchDragScroll();
        }
        private bool IsTouchScrollTarget(Control target)
        {
            for (var control = target; control != null; control = control.VisualParent)
            {
                if (control == _horizontalScrollBar || control == _verticalScrollBar) return false;
                if (control == this) return true;
            }
            return false;
        }
        /// <summary>Scrolls as little as necessary to reveal a descendant control inside the viewport.</summary>
        public void EnsureControlVisible(Control control)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (control != this && !ContainsDescendant(control)) throw new ArgumentException("The control must be a descendant of this ScrollContainer.", nameof(control));
            _focusScrollDiff = Vector2.Zero;
            if (control == this) return;
            var viewport = GetVisibleViewportRectangle();
            var target = control.Bounds;
            for (var ancestor = control.VisualParent; ancestor != null && ancestor != this; ancestor = ancestor.VisualParent)
            {
                if (!(ancestor is ScrollContainer inner)) continue;
                target.Offset(-(int)MathF.Round(inner._focusScrollDiff.X), -(int)MathF.Round(inner._focusScrollDiff.Y));
                target = Rectangle.Intersect(target, inner.GetVisibleViewportRectangle());
                if (target.Width <= 0 || target.Height <= 0) return;
            }
            var before = ScrollOffset;
            _viewportController.BringIntoView(viewport, target);
            SynchronizeControllerOffset();
            _focusScrollDiff = ScrollOffset - before;
        }

        public bool BringIndexIntoView(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (_content is not IScrollIndexProvider provider || !provider.TryGetIndexBounds(index, out var bounds)) return false;
            var viewport = new Rectangle((int)MathF.Round(ScrollOffset.X), (int)MathF.Round(ScrollOffset.Y), (int)_viewportSize.X, (int)_viewportSize.Y);
            _viewportController.BringIntoView(viewport, bounds);
            SynchronizeControllerOffset();
            return true;
        }

        public ScrollAnchor CaptureScrollAnchor(object token, Vector2 contentPosition) =>
            _viewportController.CaptureAnchor(token, contentPosition);

        public bool RestoreScrollAnchor(object token, Vector2 contentPosition)
        {
            var restored = _viewportController.RestoreAnchor(token, contentPosition);
            if (restored) SynchronizeControllerOffset();
            return restored;
        }

        public void ClearScrollAnchor() => _viewportController.ClearAnchor();

        void IScrollViewportOwner.OnScrollMetricsChanged(ScrollPresenter presenter, ScrollMetrics metrics)
        {
            if (!ReferenceEquals(presenter, _scrollPresenter)) return;
            _viewportSize = metrics.Viewport;
            _viewportController.UpdateMetrics(metrics.Viewport, metrics.Extent);
            SynchronizeControllerOffset();
        }

        void IScrollViewportOwner.BringIntoView(ScrollPresenter presenter, Control target, Rectangle targetBounds)
        {
            if (ReferenceEquals(presenter, _scrollPresenter) && target != null) EnsureControlVisible(target);
        }
        private Rectangle GetVisibleViewportRectangle()
        {
            var x = Bounds.X + (IsLayoutRtl() && _verticalScrollBar.Visible ? _verticalScrollBar.Bounds.Width : 0);
            return new Rectangle(x, Bounds.Y, (int)_viewportSize.X, (int)_viewportSize.Y);
        }
        public override Vector2 GetMinimumSize()
        {
            var content = ContentSize;
            var minimum = CustomMinimumSize;
            // Godot's _get_minimum_size also budgets for the OTHER axis's scrollbar when that axis is
            // actually showing/reserved, so a size-aware parent doesn't under-allocate space for it.
            var verticalShows = ShouldShow(VerticalScrollMode, content.Y, Size.Y);
            var horizontalShows = ShouldShow(HorizontalScrollMode, content.X, Size.X);
            if (HorizontalScrollMode == ScrollBarVisibility.Disabled || HorizontalScrollMode == ScrollBarVisibility.MaximizeFirst)
            {
                minimum.X = Math.Max(minimum.X, content.X);
                if (verticalShows) minimum.X += _verticalScrollBar.GetMinimumSize().X;
            }
            if (VerticalScrollMode == ScrollBarVisibility.Disabled || VerticalScrollMode == ScrollBarVisibility.MaximizeFirst)
            {
                minimum.Y = Math.Max(minimum.Y, content.Y);
                if (horizontalShows) minimum.Y += _horizontalScrollBar.GetMinimumSize().Y;
            }
            return minimum;
        }
        internal IReadOnlyList<Rectangle> GetVisibleScrollHintRectangles()
        {
            var result = new List<Rectangle>();
            if (ScrollHintMode == ScrollContainerScrollHintMode.Disabled) return result;
            var max = MaxScrollOffset;
            var showTop = ScrollOffset.Y > 1;
            var showBottom = ScrollOffset.Y < max.Y - 1;
            var showLeft = ScrollOffset.X > 1;
            var showRight = ScrollOffset.X < max.X - 1;
            var showVerticalHints = showTop || showBottom;
            var showHorizontalHints = showLeft || showRight;
            const int hintThickness = 4;
            if (showVerticalHints)
            {
                if (showHorizontalHints) return result;
                if (showTop && (ScrollHintMode == ScrollContainerScrollHintMode.All || ScrollHintMode == ScrollContainerScrollHintMode.TopAndLeft))
                    result.Add(new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, Math.Min(hintThickness, Bounds.Height)));
                if (showBottom && (ScrollHintMode == ScrollContainerScrollHintMode.All || ScrollHintMode == ScrollContainerScrollHintMode.BottomAndRight))
                    result.Add(new Rectangle(Bounds.X, Math.Max(Bounds.Y, Bounds.Bottom - hintThickness), Bounds.Width, Math.Min(hintThickness, Bounds.Height)));
                return result;
            }
            if (!showHorizontalHints) return result;
            var startMode = IsLayoutRtl() ? ScrollContainerScrollHintMode.BottomAndRight : ScrollContainerScrollHintMode.TopAndLeft;
            var endMode = IsLayoutRtl() ? ScrollContainerScrollHintMode.TopAndLeft : ScrollContainerScrollHintMode.BottomAndRight;
            if (showLeft && (ScrollHintMode == ScrollContainerScrollHintMode.All || ScrollHintMode == startMode))
            {
                var x = IsLayoutRtl() ? Math.Max(Bounds.X, Bounds.Right - hintThickness) : Bounds.X;
                result.Add(new Rectangle(x, Bounds.Y, Math.Min(hintThickness, Bounds.Width), Bounds.Height));
            }
            if (showRight && (ScrollHintMode == ScrollContainerScrollHintMode.All || ScrollHintMode == endMode))
            {
                var x = IsLayoutRtl() ? Bounds.X : Math.Max(Bounds.X, Bounds.Right - hintThickness);
                result.Add(new Rectangle(x, Bounds.Y, Math.Min(hintThickness, Bounds.Width), Bounds.Height));
            }
            return result;
        }
        internal bool IsFocusBorderVisible => DrawFocusBorder && (Context?.FocusedControl == this || ContainsDescendant(Context?.FocusedControl));
        /// <summary>Matches Godot's ScrollContainer::gui_input WHEEL_UP/WHEEL_DOWN handling: the scroll
        /// amount is always the relevant scrollbar's page / ScrollBar::PAGE_DIVISOR (8), Shift held swaps
        /// to the horizontal axis (scroll_horizontal_by_default flips which axis is the "default" one),
        /// and an auto-hidden vertical scrollbar falls back to scrolling horizontally either way. This
        /// port has no separate horizontal-wheel input channel (the XNA-compatible MouseState only reports a
        /// single vertical wheel value), so Godot's WHEEL_LEFT/WHEEL_RIGHT hardware path is out of scope.</summary>
        internal override bool PointerWheel(int delta)
        {
            if (delta == 0) return false;
            var swapAxes = ScrollHorizontalByDefault != HasShiftModifier();
            var horizontalEnabled = HorizontalScrollMode != ScrollBarVisibility.Disabled;
            var verticalEnabled = VerticalScrollMode != ScrollBarVisibility.Disabled;
            var verticalHidden = !_verticalScrollBar.Visible && VerticalScrollMode != ScrollBarVisibility.Never;
            UpdateControllerMetrics();
            if ((horizontalEnabled && swapAxes) || verticalHidden)
            {
                if (horizontalEnabled) _viewportController.ScrollWheel(delta, true, _horizontalScrollBar.Page, _verticalScrollBar.Page);
            }
            else if (verticalEnabled)
            {
                _viewportController.ScrollWheel(delta, false, _horizontalScrollBar.Page, _verticalScrollBar.Page);
            }
            return SynchronizeControllerOffset();
        }
        private bool HasShiftModifier()
        {
            var keyboard = Context?.CurrentKeyboardState ?? default;
            return keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        }
        internal override void Process(GameTime gameTime)
        {
            if (FollowFocus)
            {
                var focused = Context?.FocusedControl;
                if (focused != _followedFocus)
                {
                    _followedFocus = focused;
                    if (focused != null && focused != this && ContainsDescendant(focused))
                    {
                        EnsureNestedFocusFollowers(focused);
                        EnsureControlVisible(focused);
                    }
                }
            }
            ProcessDragHoverScroll(gameTime);
            ProcessTouchDrag(gameTime);
            base.Process(gameTime);
        }
        private void EnsureNestedFocusFollowers(Control focused)
        {
            for (var ancestor = focused.VisualParent; ancestor != null && ancestor != this; ancestor = ancestor.VisualParent)
            {
                if (!(ancestor is ScrollContainer nested) || !nested.FollowFocus) continue;
                nested._followedFocus = focused;
                nested.EnsureControlVisible(focused);
            }
        }
        private void ProcessDragHoverScroll(GameTime gameTime)
        {
            if (!ScrollOnDragHover || Context?.IsDragging != true || IsTabDrag(Context.DragData)) return;
            var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var border = Math.Max(0, DragHoverScrollBorder);
            if (delta <= 0 || border == 0) return;
            var pointer = new Vector2(Context.PointerPosition.X, Context.PointerPosition.Y) - GlobalPosition;
            if (pointer.X < -border || pointer.X > Size.X + border || pointer.Y < -border || pointer.Y > Size.Y + border) return;
            var offset = Vector2.Zero;
            if (Math.Abs(pointer.X) < Math.Abs(pointer.X - Size.X) && Math.Abs(pointer.X) < border) offset.X = pointer.X - border;
            else if (Math.Abs(pointer.X - Size.X) < border) offset.X = pointer.X - (Size.X - border);
            if (Math.Abs(pointer.Y) < Math.Abs(pointer.Y - Size.Y) && Math.Abs(pointer.Y) < border) offset.Y = pointer.Y - border;
            else if (Math.Abs(pointer.Y - Size.Y) < border) offset.Y = pointer.Y - (Size.Y - border);
            ScrollOffset += offset * DragHoverScrollSpeed * delta;
        }
        private static bool IsTabDrag(object data)
        {
            if (data is IDictionary<string, object> dictionary && dictionary.TryGetValue("type", out var type)) return string.Equals(type as string, "tab", StringComparison.Ordinal);
            return false;
        }
        private void ProcessTouchDrag(GameTime gameTime)
        {
            var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateControllerMetrics();
            _viewportController.Process(delta);
            SynchronizeControllerOffset();
        }
        protected override void ArrangeChildren()
        {
            base.ArrangeChildren();
            var content = ContentSize;
            var barWidth = _verticalScrollBar.GetMinimumSize().X;
            var barHeight = _horizontalScrollBar.GetMinimumSize().Y;
            var reserveHorizontal = HorizontalScrollMode == ScrollBarVisibility.Reserve;
            var reserveVertical = VerticalScrollMode == ScrollBarVisibility.Reserve;
            var showHorizontal = false; var showVertical = false;
            for (var i = 0; i < 2; i++)
            {
                var availableWidth = Size.X - (reserveVertical ? barWidth : 0);
                var availableHeight = Size.Y - (reserveHorizontal ? barHeight : 0);
                showHorizontal = ShouldShow(HorizontalScrollMode, content.X, availableWidth);
                showVertical = ShouldShow(VerticalScrollMode, content.Y, availableHeight);
                reserveHorizontal = HorizontalScrollMode == ScrollBarVisibility.Reserve || showHorizontal;
                reserveVertical = VerticalScrollMode == ScrollBarVisibility.Reserve || showVertical;
            }
            _horizontalScrollBar.Visible = showHorizontal;
            _verticalScrollBar.Visible = showVertical;
            _viewportSize = Vector2.Max(Vector2.Zero, Size - new Vector2(reserveVertical ? barWidth : 0, reserveHorizontal ? barHeight : 0));
            UpdateControllerMetrics();
            var contentOrigin = IsLayoutRtl() && reserveVertical ? new Vector2(barWidth, 0) : Vector2.Zero;
            if (TemplateRoot != null)
            {
                TemplateRoot.Position = TemplateRoot == _scrollPresenter ? contentOrigin : Vector2.Zero;
                TemplateRoot.Size = TemplateRoot == _scrollPresenter ? _viewportSize : Size;
            }
            var max = MaxScrollOffset;
            _horizontalScrollBar.Position = new Vector2(0, _viewportSize.Y);
            _horizontalScrollBar.Size = new Vector2(_viewportSize.X, barHeight);
            // Range's maximum includes its visible page; retain MaxScrollOffset as the public
            // content-relative maximum while configuring the scrollbar with the full content span.
            _horizontalScrollBar.MinValue = 0; _horizontalScrollBar.MaxValue = Math.Max(0, max.X + _viewportSize.X); _horizontalScrollBar.Page = _viewportSize.X; _horizontalScrollBar.Value = ScrollOffset.X;
            _verticalScrollBar.Position = new Vector2(IsLayoutRtl() ? 0 : _viewportSize.X, 0);
            _verticalScrollBar.Size = new Vector2(barWidth, _viewportSize.Y);
            _verticalScrollBar.MinValue = 0; _verticalScrollBar.MaxValue = Math.Max(0, max.Y + _viewportSize.Y); _verticalScrollBar.Page = _viewportSize.Y; _verticalScrollBar.Value = ScrollOffset.Y;
            _focusScrollDiff = Vector2.Zero;
            TemplateRoot?.QueueLayout();
        }
        private Vector2 ContentSize
        {
            get
            {
                return _content == null ? Vector2.Zero : Vector2.Max(_content.GetMinimumSize(), _content.Size);
            }
        }
        private bool IsScrollBar(Control control) => control == _horizontalScrollBar || control == _verticalScrollBar;

        protected override void OnTemplateApplied()
        {
            var presenter = GetTemplateChild(ScrollPresenterPartName) as ScrollPresenter;
            if (presenter == null)
                throw new InvalidOperationException($"ScrollContainer templates must provide a {nameof(ScrollPresenter)} named '{ScrollPresenterPartName}'.");
            if (_scrollPresenter != null && !ReferenceEquals(_scrollPresenter, presenter))
            {
                _scrollPresenter.Content = null;
                _scrollPresenter.Owner = null;
            }
            _scrollPresenter = presenter;
            presenter.Owner = this;
            presenter.Content = _content;
            base.OnTemplateApplied();
        }
        private bool ContainsDescendant(Control control)
        {
            for (var current = control.VisualParent; current != null; current = current.VisualParent) if (current == this) return true;
            return false;
        }
        private void UpdateControllerAxes()
        {
            _viewportController.HorizontalEnabled = HorizontalScrollMode != ScrollBarVisibility.Disabled;
            _viewportController.VerticalEnabled = VerticalScrollMode != ScrollBarVisibility.Disabled;
        }
        private void UpdateControllerMetrics()
        {
            UpdateControllerAxes();
            _viewportController.UpdateMetrics(_viewportSize, ContentSize);
        }
        private bool SynchronizeControllerOffset()
        {
            var before = new Vector2(_horizontalScrollBar.Value, _verticalScrollBar.Value);
            _horizontalScrollBar.SetValueNoSignal(ScrollOffset.X);
            _verticalScrollBar.SetValueNoSignal(ScrollOffset.Y);
            _scrollPresenter?.QueueLayout();
            QueueLayout();
            return before != ScrollOffset;
        }

        private void OnViewportMetricsChanged(object sender, ScrollViewportMetricsChangedEventArgs args)
        {
            if (args.OffsetChanged)
            {
                OnPropertyChanged(nameof(ScrollOffset));
                OnPropertyChanged(nameof(HorizontalScroll));
                OnPropertyChanged(nameof(VerticalScroll));
                ScrollOffsetChanged?.Invoke(this, EventArgs.Empty);
            }
            if (args.ViewportChanged)
            {
                OnPropertyChanged(nameof(Viewport));
                ViewportChanged?.Invoke(this, EventArgs.Empty);
            }
            if (args.ExtentChanged)
            {
                OnPropertyChanged(nameof(Extent));
                ScrollExtentChanged?.Invoke(this, EventArgs.Empty);
            }
            ScrollMetricsChanged?.Invoke(this, args);
        }
        private static bool ShouldShow(ScrollBarVisibility mode, float content, float available)
        {
            if (mode == ScrollBarVisibility.Disabled || mode == ScrollBarVisibility.Never) return false;
            if (mode == ScrollBarVisibility.Always || mode == ScrollBarVisibility.Reserve) return true;
            return content > available;
        }
    }

    public enum PopupHideReason { Programmatic, OutsideClick, Cancelled }

    /// <summary>
    /// A top-level transient panel. Modal popups gate pointer/focus input to their subtree and can
    /// optionally dismiss themselves when the user presses outside their bounds.
    /// </summary>
    public class Popup : TemplatedControl
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.Window;
        public override AccessibilityStates AccessibilityStates => base.AccessibilityStates | AccessibilityStates.Modal;
        private Control _focusBeforePopup;
        public Popup() { FocusMode = FocusMode.All; Modal = true; HideOnOutsideClick = true; }
        public Color? BackgroundColor { get; set; }
        public Color? BorderColor { get; set; }
        public int BorderWidth { get; set; } = 1;
        public bool Modal { get; set; }
        /// <summary>Prevents an outside pointer press from dismissing this modal popup.</summary>
        public bool Exclusive { get; set; }
        public bool HideOnOutsideClick { get; set; }
        public event EventHandler PopupShown;
        public event Action<Popup, PopupHideReason> PopupHidden;
        public void PopupAt(Vector2 position)
        {
            _focusBeforePopup = Context?.FocusedControl;
            Position = position;
            var wasVisible = Visible;
            Visible = true;
            GrabFocus();
            if (!wasVisible) PopupShown?.Invoke(this, EventArgs.Empty);
        }
        public void Hide(PopupHideReason reason = PopupHideReason.Programmatic)
        {
            if (!Visible) return;
            Visible = false;
            var priorFocus = _focusBeforePopup;
            _focusBeforePopup = null;
            if (Context?.FocusedControl != null && IsAncestorOf(Context.FocusedControl)) Context.SetFocus(null);
            if (priorFocus != null && priorFocus.Context == Context && priorFocus.Visible && priorFocus.Enabled && priorFocus.FocusMode != FocusMode.None)
                priorFocus.GrabFocus();
            PopupHidden?.Invoke(this, reason);
        }
        internal void OutsidePointerPressed(Point point)
        {
            if (HideOnOutsideClick && !Exclusive) Hide(PopupHideReason.OutsideClick);
        }
        internal bool IsAncestorOf(Control descendant)
        {
            for (var control = descendant; control != null; control = control.VisualParent)
                if (ReferenceEquals(control, this)) return true;
            return false;
        }
        internal override void KeyPressed(Keys key)
        {
            if (key == Keys.Escape) Hide(PopupHideReason.Cancelled);
            else base.KeyPressed(key);
        }
    }
    /// <summary>Presents popup content on a panel-styled surface with standard popup dismissal behavior.</summary>
    public class PopupPanel : Popup { }
}
