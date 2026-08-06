// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT
// Basic control APIs and behavior are adapted from Godot Engine's Label, BaseButton,
// Button, CheckBox, Range, Slider, ProgressBar, and Panel implementations under scene/gui;
// see THIRD-PARTY-NOTICES.md.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Forma
{
    public class Panel : Control
    {
        public Color? BackgroundColor { get; set; }
        public Color? BorderColor { get; set; }
        public int BorderWidth { get; set; } = 1;
        internal override void Draw(UIRenderContext context)
        {
            var rect = Bounds;
            var style = GetThemeStyleBox("panel");
            if (style != null) style.Draw(context, rect);
            else { context.Fill(rect, BackgroundColor ?? context.Theme.PanelColor); context.Border(rect, BorderColor ?? context.Theme.PanelBorderColor, BorderWidth); }
            base.Draw(context);
        }
    }

    /// <summary>Read-only downsampled visualization of one dynamic glyph-atlas page.</summary>
    public sealed class DynamicGlyphAtlasView : Control
    {
        public DynamicGlyphAtlasPageSnapshot Snapshot { get; set; }
        public int SampleColumns { get; set; } = 32;
        public DynamicGlyphAtlasView() => CustomMinimumSize = new Vector2(128, 128);
        internal override void Draw(UIRenderContext context)
        {
            context.Fill(Bounds, new Color(16, 20, 27));
            var snapshot = Snapshot;
            if (snapshot != null && SampleColumns > 0 && Size.X > 0 && Size.Y > 0)
            {
                var columns = Math.Min(SampleColumns, snapshot.Width);
                var rows = Math.Max(1, (int)MathF.Round(columns * snapshot.Height / (float)snapshot.Width));
                var pixels = snapshot.Pixels.Span;
                for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    var sourceX = Math.Min(snapshot.Width - 1, column * snapshot.Width / columns);
                    var sourceY = Math.Min(snapshot.Height - 1, row * snapshot.Height / rows);
                    var coverage = pixels[sourceY * snapshot.Width + sourceX];
                    if (coverage == 0) continue;
                    var left = Bounds.Left + column * Bounds.Width / columns;
                    var top = Bounds.Top + row * Bounds.Height / rows;
                    var right = Bounds.Left + (column + 1) * Bounds.Width / columns;
                    var bottom = Bounds.Top + (row + 1) * Bounds.Height / rows;
                    context.Fill(new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)), new Color(coverage, coverage, coverage));
                }
            }
            context.Border(Bounds, new Color(56, 66, 82));
            base.Draw(context);
        }
    }

    public enum LabelAutowrapMode { Off, Arbitrary, Word, WordSmart }
    public enum LabelTextOverrunBehavior { NoTrimming, TrimCharacters, TrimWords, Ellipsis, WordEllipsis, EllipsisForce, WordEllipsisForce }
    public enum LabelVisibleCharactersBehavior { CharactersBeforeShaping, CharactersAfterShaping, GlyphsLayoutDirection, GlyphsLeftToRight, GlyphsRightToLeft }
    [Flags]
    public enum LabelJustificationFlags
    {
        None = 0,
        Kashida = 1,
        WordBound = 2,
        AfterLastTab = 8,
        SkipLastLine = 32,
        SkipLastLineWithVisibleCharacters = 64,
        DoNotSkipSingleLine = 128,
    }

    public class Label : Control
    {
        private static readonly TextLayoutEngine DynamicLayoutEngine = new TextLayoutEngine();
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private string _text = string.Empty;
        private LabelAutowrapMode _autowrapMode;
        private TextDirection _textDirection = TextDirection.Auto;
        private float[] _tabStops = Array.Empty<float>();
        private IReadOnlyList<UIFontOpenTypeFeature> _openTypeFeatures = Array.Empty<UIFontOpenTypeFeature>();
        private IReadOnlyList<object> _structuredTextBidiOverrideOptions = Array.Empty<object>();
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value) return;
                _text = value ?? string.Empty;
                // Godot's Label::set_text resyncs visible_chars to keep an absolute visible-character
                // count proportionally consistent with the new text's length, whenever a ratio below 1
                // is active.
                if (VisibleRatio < 1) VisibleCharacters = (int)(GetTotalCharacterCount() * VisibleRatio);
                QueueLayout();
            }
        }
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection, FontFamily, FontSize, FontWeight, FontStyle, FontStretch);
        public Color? FontColor { get => Foreground; set => Foreground = value; }
        public new HorizontalAlignment HorizontalAlignment { get; set; }
        public new VerticalAlignment VerticalAlignment { get; set; }
        /// <summary>Legacy convenience switch; true maps to Godot's WordSmart mode.</summary>
        public bool Autowrap { get => AutowrapMode != LabelAutowrapMode.Off; set => AutowrapMode = value ? LabelAutowrapMode.WordSmart : LabelAutowrapMode.Off; }
        public LabelAutowrapMode AutowrapMode { get => _autowrapMode; set { _autowrapMode = value; QueueLayout(); } }
        public LabelTextOverrunBehavior TextOverrunBehavior { get; set; }
        public string EllipsisCharacter { get; set; } = "…";
        public bool Uppercase { get; set; }
        public bool ClipText { get; set; }
        public int VisibleCharacters { get; set; } = -1;
        public float VisibleRatio { get; set; } = 1;
        public LabelVisibleCharactersBehavior VisibleCharactersBehavior { get; set; }
        public int LinesSkipped { get; set; }
        public int MaxLinesVisible { get; set; } = -1;
        public string ParagraphSeparator { get; set; } = "\n";
        public float ParagraphSpacing { get; set; }
        public TextDirection TextDirection { get => _textDirection; set { if (_textDirection == value) return; _textDirection = value; QueueLayout(); } }
        public StructuredTextParser StructuredTextBidiOverride { get; set; } = StructuredTextParser.Default;
        public LabelJustificationFlags JustificationFlags { get; set; } = LabelJustificationFlags.Kashida | LabelJustificationFlags.WordBound | LabelJustificationFlags.SkipLastLine | LabelJustificationFlags.DoNotSkipSingleLine;
        public Thickness Padding { get; set; } = new Thickness(3);
        public void SetHorizontalAlignment(HorizontalAlignment alignment) { if (!Enum.IsDefined(typeof(HorizontalAlignment), alignment)) throw new ArgumentOutOfRangeException(nameof(alignment)); HorizontalAlignment = alignment; }
        public HorizontalAlignment GetHorizontalAlignment() => HorizontalAlignment;
        public void SetVerticalAlignment(VerticalAlignment alignment) { if (!Enum.IsDefined(typeof(VerticalAlignment), alignment)) throw new ArgumentOutOfRangeException(nameof(alignment)); VerticalAlignment = alignment; }
        public VerticalAlignment GetVerticalAlignment() => VerticalAlignment;
        public void SetText(string text) => Text = text;
        public string GetText() => Text;
        public void SetAutowrapMode(LabelAutowrapMode mode) { if (!Enum.IsDefined(typeof(LabelAutowrapMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode)); AutowrapMode = mode; }
        public LabelAutowrapMode GetAutowrapMode() => AutowrapMode;
        public void SetJustificationFlags(LabelJustificationFlags flags) => JustificationFlags = flags;
        public LabelJustificationFlags GetJustificationFlags() => JustificationFlags;
        public void SetClipText(bool enable) => ClipText = enable;
        public bool IsClippingText() => ClipText;
        public void SetTextOverrunBehavior(LabelTextOverrunBehavior behavior) { if (!Enum.IsDefined(typeof(LabelTextOverrunBehavior), behavior)) throw new ArgumentOutOfRangeException(nameof(behavior)); TextOverrunBehavior = behavior; }
        public LabelTextOverrunBehavior GetTextOverrunBehavior() => TextOverrunBehavior;
        public void SetEllipsisChar(string value) => EllipsisCharacter = string.IsNullOrEmpty(value) ? "…" : value;
        public string GetEllipsisChar() => EllipsisCharacter;
        public void SetUppercase(bool enable) => Uppercase = enable;
        public bool IsUppercase() => Uppercase;
        public void SetVisibleCharacters(int amount)
        {
            // Godot's set_visible_characters performs no clamping on the derived ratio at all - an
            // amount larger than the total character count legitimately produces a ratio above 1.
            VisibleCharacters = amount;
            var total = GetTotalCharacterCount();
            VisibleRatio = amount == -1 || total == 0 ? 1 : (float)amount / total;
        }
        public int GetVisibleCharacters() => VisibleCharacters;
        public void SetVisibleRatio(float ratio)
        {
            if (ratio >= 1) { VisibleCharacters = -1; VisibleRatio = 1; }
            else if (ratio < 0) { VisibleCharacters = 0; VisibleRatio = 0; }
            else { VisibleCharacters = (int)(GetTotalCharacterCount() * ratio); VisibleRatio = ratio; }
        }
        public float GetVisibleRatio() => VisibleRatio;
        public void SetVisibleCharactersBehavior(LabelVisibleCharactersBehavior behavior) { if (!Enum.IsDefined(typeof(LabelVisibleCharactersBehavior), behavior)) throw new ArgumentOutOfRangeException(nameof(behavior)); VisibleCharactersBehavior = behavior; }
        public LabelVisibleCharactersBehavior GetVisibleCharactersBehavior() => VisibleCharactersBehavior;
        public void SetLinesSkipped(int lines) { if (lines < 0) throw new ArgumentOutOfRangeException(nameof(lines)); LinesSkipped = lines; }
        public int GetLinesSkipped() => LinesSkipped;
        public void SetMaxLinesVisible(int lines) => MaxLinesVisible = lines;
        public int GetMaxLinesVisible() => MaxLinesVisible;
        public int GetLineCount() => GetDynamicLayout()?.Lines.Count ?? GetAllLineLayouts().Count;
        public int GetVisibleLineCount()
        {
            var layout = GetDynamicLayout();
            if (layout == null) return GetDisplayLines().Count;
            var start = Math.Min(LinesSkipped, layout.Lines.Count);
            var count = MaxLinesVisible < 0 ? layout.Lines.Count - start : Math.Min(MaxLinesVisible, layout.Lines.Count - start);
            var availableHeight = Math.Max(0, Size.Y - Padding.Vertical);
            var usedHeight = 0f;
            var visible = 0;
            while (visible < count && usedHeight + layout.Lines[start + visible].Size.Y <= availableHeight)
            {
                usedHeight += layout.Lines[start + visible].Size.Y;
                visible++;
            }
            return visible;
        }
        public int GetTotalCharacterCount() => GetTextForLayout().Length;
        public void SetTextDirection(TextDirection direction) { if (!Enum.IsDefined(typeof(TextDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction)); TextDirection = direction; }
        public TextDirection GetTextDirection() => TextDirection;
        public void SetLanguage(string language) => Language = language ?? string.Empty;
        public string GetLanguage() => Language;
        // Godot's set_paragraph_separator assigns the passed string directly with no empty-string
        // special-casing - an empty separator is a legitimate way to disable paragraph splitting
        // (the whole text becomes a single paragraph).
        public void SetParagraphSeparator(string paragraphSeparator) => ParagraphSeparator = paragraphSeparator ?? string.Empty;
        public string GetParagraphSeparator() => ParagraphSeparator;
        public void SetParagraphSpacing(float spacing) => ParagraphSpacing = Math.Max(0, spacing);
        public float GetParagraphSpacing() => ParagraphSpacing;
        public void SetStructuredTextBidiOverride(StructuredTextParser parser) { if (!Enum.IsDefined(typeof(StructuredTextParser), parser)) throw new ArgumentOutOfRangeException(nameof(parser)); StructuredTextBidiOverride = parser; }
        public StructuredTextParser GetStructuredTextBidiOverride() => StructuredTextBidiOverride;
        public void SetStructuredTextBidiOverrideOptions(IEnumerable<object> options) => _structuredTextBidiOverrideOptions = options == null ? Array.Empty<object>() : new List<object>(options).ToArray();
        public IReadOnlyList<object> GetStructuredTextBidiOverrideOptions() => _structuredTextBidiOverrideOptions;
        public void SetTabStops(IEnumerable<float> tabStops) => _tabStops = tabStops == null ? Array.Empty<float>() : new List<float>(tabStops).ToArray();
        public IReadOnlyList<float> GetTabStops() => _tabStops;
        public void SetOpenTypeFeatures(IEnumerable<UIFontOpenTypeFeature> features) { _openTypeFeatures = features == null ? Array.Empty<UIFontOpenTypeFeature>() : new List<UIFontOpenTypeFeature>(features).ToArray(); QueueLayout(); }
        public IReadOnlyList<UIFontOpenTypeFeature> GetOpenTypeFeatures() => _openTypeFeatures;
        public int GetLineHeight(int line = -1)
        {
            var dynamicLayout = GetDynamicLayout();
            if (dynamicLayout != null && dynamicLayout.Lines.Count > 0)
            {
                var index = line < 0 ? 0 : Math.Min(line, dynamicLayout.Lines.Count - 1);
                return Math.Max(1, (int)MathF.Ceiling(dynamicLayout.Lines[index].Size.Y));
            }
            return Font?.LineSpacing ?? 16;
        }
        public virtual Rectangle GetCharacterBounds(int position)
        {
            var dynamicLayout = GetDynamicLayout();
            if (dynamicLayout != null) return GetDynamicCharacterBounds(dynamicLayout, position);
            var text = GetVisibleText();
            if (position < 0 || position >= text.Length) return Rectangle.Empty;
            var layouts = GetDisplayLineLayouts();
            var line = -1;
            for (var index = 0; index < layouts.Count; index++)
            {
                if (position < layouts[index].SourceStart || position >= layouts[index].SourceStart + layouts[index].Text.Length) continue;
                line = index;
                break;
            }
            if (line < 0) return Rectangle.Empty;
            var layout = layouts[line];
            var lineText = layout.Text;
            var column = position - layout.SourceStart;
            var contentWidth = Math.Max(0, Size.X - Padding.Horizontal);
            var justificationStart = GetJustificationStart(lineText, layout.GlobalIndex);
            var extraSpace = GetJustificationExtra(lineText, contentWidth, justificationStart);
            var lineWidth = MeasureLineAdvance(lineText, lineText.Length, extraSpace, justificationStart);
            var x = (float)Padding.Left;
            if (HorizontalAlignment == HorizontalAlignment.Center) x += MathF.Max(0, (contentWidth - lineWidth) / 2);
            else if (HorizontalAlignment == HorizontalAlignment.Right) x += MathF.Max(0, contentWidth - lineWidth);
            x += MeasureLineAdvance(lineText, Math.Min(column, lineText.Length), extraSpace, justificationStart);
            var y = GetLineOffsets(layouts)[line];
            var nextX = MeasureLineAdvance(lineText, Math.Min(column + 1, lineText.Length), extraSpace, justificationStart);
            var currentX = MeasureLineAdvance(lineText, Math.Min(column, lineText.Length), extraSpace, justificationStart);
            var width = Math.Max(1, (int)MathF.Ceiling(nextX - currentX));
            return new Rectangle((int)MathF.Round(x), (int)MathF.Round(y), width, GetLineHeight(layout.GlobalIndex));
        }
        public override Vector2 GetMinimumSize()
        {
            var textSize = MeasureTextBlock();
            // Godot's get_minimum_size: clipping/trimming is what lets a label shrink below its natural
            // text size in a layout - autowrap always collapses width to a nominal minimum and, when
            // clipped/trimmed, collapses height to a nominal minimum too (unless MaxLinesVisible clamps
            // it to that many lines' worth of height instead); without autowrap, clipping/trimming
            // collapses width instead.
            var trimming = ClipText || TextOverrunBehavior != LabelTextOverrunBehavior.NoTrimming;
            if (AutowrapMode != LabelAutowrapMode.Off)
            {
                if (!ClipText && TextOverrunBehavior != LabelTextOverrunBehavior.NoTrimming && MaxLinesVisible > 0)
                    textSize.Y = Math.Min(textSize.Y, ((Font?.LineSpacing ?? 0) + ParagraphSpacing) * MaxLinesVisible);
                else if (trimming) textSize.Y = 1;
                textSize.X = 0;
            }
            else if (trimming) textSize.X = 1;
            return Vector2.Max(CustomMinimumSize, textSize + new Vector2(Padding.Horizontal, Padding.Vertical));
        }
        internal override void Draw(UIRenderContext context)
        {
            if (ClipText) context.PushClip(Bounds);
            try { DrawLabelText(context); }
            finally { if (ClipText) context.PopClip(); }
            DrawLabelChildren(context);
        }
        /// <summary>Draws this label's text. Rich-text derived controls can replace this while retaining child rendering.</summary>
        protected virtual void DrawLabelText(UIRenderContext context)
        {
            var dynamicLayout = GetDynamicLayout();
            if (dynamicLayout != null)
            {
                var color = Enabled ? FontColor ?? context.Theme.TextColor : context.Theme.DisabledTextColor;
                context.Text(dynamicLayout, GlobalPosition + new Vector2(Padding.Left, Padding.Top + GetDynamicVerticalOffset(dynamicLayout)), color);
                return;
            }
            if (Font != null && !string.IsNullOrEmpty(Text))
            {
                var layouts = GetDisplayLineLayouts();
                var lines = new List<string>(); foreach (var layout in layouts) lines.Add(layout.Text);
                var lineOffsets = GetLineOffsets(layouts);
                var content = Size - new Vector2(Padding.Horizontal, Padding.Vertical);
                var color = Enabled ? FontColor ?? context.Theme.TextColor : context.Theme.DisabledTextColor;
                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    var line = lines[lineIndex];
                    var linePosition = GlobalPosition + new Vector2(Padding.Left, lineOffsets[lineIndex]);
                    var justificationStart = GetJustificationStart(line, layouts[lineIndex].GlobalIndex);
                    var extraSpace = GetJustificationExtra(line, content.X, justificationStart);
                    var lineWidth = MeasureLineAdvance(line, line.Length, extraSpace, justificationStart);
                    if (HorizontalAlignment == HorizontalAlignment.Center) linePosition.X += MathF.Max(0, (content.X - lineWidth) / 2);
                    else if (HorizontalAlignment == HorizontalAlignment.Right) linePosition.X += MathF.Max(0, content.X - lineWidth);
                    DrawLine(context, line, linePosition, color, extraSpace, justificationStart);
                }
            }
        }
        /// <summary>Draws retained child controls after the label's text.</summary>
        protected void DrawLabelChildren(UIRenderContext context) => base.Draw(context);
        /// <summary>Returns the text as it will be displayed after casing, visible-character, wrapping, and line-limit rules.</summary>
        public IReadOnlyList<string> GetDisplayLines()
        {
            var result = new List<string>();
            foreach (var layout in GetDisplayLineLayouts()) result.Add(layout.Text);
            return result;
        }
        private List<string> GetAllDisplayLines()
        {
            var lines = new List<string>(); foreach (var layout in GetAllLineLayouts()) lines.Add(layout.Text); return lines;
        }
        private List<LabelLineLayout> GetAllLineLayouts()
        {
            var lines = new List<LabelLineLayout>();
            var visibleText = GetVisibleText();
            var globalIndex = 0;
            var paragraphStart = 0;
            var separator = GetNormalizedParagraphSeparator();
            foreach (var paragraph in SplitParagraphs(visibleText))
            {
                var paragraphLines = GetParagraphLines(paragraph);
                var searchStart = 0;
                for (var index = 0; index < paragraphLines.Count; index++)
                {
                    var line = paragraphLines[index];
                    var sourceStart = line.Length == 0 ? searchStart : paragraph.IndexOf(line, searchStart, StringComparison.Ordinal);
                    if (sourceStart < 0) sourceStart = searchStart;
                    lines.Add(new LabelLineLayout(line, index == paragraphLines.Count - 1, globalIndex++, paragraphStart + sourceStart));
                    searchStart = sourceStart + line.Length;
                }
                paragraphStart += paragraph.Length + separator.Length;
            }
            return lines;
        }
        private List<LabelLineLayout> GetDisplayLineLayouts()
        {
            var lines = GetAllLineLayouts();
            var start = Math.Max(0, Math.Min(LinesSkipped, lines.Count));
            var count = MaxLinesVisible < 0 ? lines.Count - start : Math.Min(MaxLinesVisible, lines.Count - start);
            var contentHeight = Size.Y > Padding.Vertical ? Size.Y - Padding.Vertical : float.MaxValue;
            var usedHeight = 0f; var heightCount = 0;
            while (heightCount < count)
            {
                var index = start + heightCount;
                var addition = (float)GetLineHeight(index);
                if (heightCount > 0 && lines[index - 1].ParagraphEnd) addition += ParagraphSpacing;
                if (usedHeight + addition > contentHeight) break;
                usedHeight += addition; heightCount++;
            }
            count = heightCount;
            var texts = new List<string>(); for (var index = 0; index < lines.Count; index++) texts.Add(lines[index].Text);
            var displayed = ApplyLineWindow(texts, start, count);
            var result = new List<LabelLineLayout>();
            for (var index = 0; index < count; index++)
            {
                var source = lines[start + index];
                result.Add(new LabelLineLayout(displayed[index], source.ParagraphEnd, source.GlobalIndex, source.SourceStart));
            }
            return result;
        }
        private float GetLayoutsHeight(IReadOnlyList<LabelLineLayout> layouts)
        {
            var height = (float)(layouts.Count * GetLineHeight());
            for (var index = 0; index + 1 < layouts.Count; index++) if (layouts[index].ParagraphEnd) height += ParagraphSpacing;
            return height;
        }
        private List<float> GetLineOffsets(IReadOnlyList<LabelLineLayout> layouts)
        {
            var offsets = new List<float>();
            var contentHeight = Math.Max(0, Size.Y - Padding.Vertical);
            var spareHeight = Math.Max(0, contentHeight - GetLayoutsHeight(layouts));
            var begin = 0f;
            var extraSpacing = 0f;
            if (VerticalAlignment == VerticalAlignment.Center) begin = MathF.Floor(spareHeight / 2);
            else if (VerticalAlignment == VerticalAlignment.Bottom) begin = MathF.Floor(spareHeight);
            else if (VerticalAlignment == VerticalAlignment.Fill && layouts.Count > 1) extraSpacing = MathF.Floor(spareHeight / (layouts.Count - 1));
            var y = Padding.Top + begin;
            for (var index = 0; index < layouts.Count; index++)
            {
                offsets.Add(y);
                y += GetLineHeight(layouts[index].GlobalIndex) + extraSpacing;
                if (layouts[index].ParagraphEnd && index + 1 < layouts.Count) y += ParagraphSpacing;
            }
            return offsets;
        }
        private readonly struct LabelLineLayout
        {
            public LabelLineLayout(string text, bool paragraphEnd, int globalIndex, int sourceStart) { Text = text; ParagraphEnd = paragraphEnd; GlobalIndex = globalIndex; SourceStart = sourceStart; }
            public string Text { get; }
            public bool ParagraphEnd { get; }
            public int GlobalIndex { get; }
            public int SourceStart { get; }
        }
        private List<string> GetParagraphLines(string paragraph)
        {
            var lines = new List<string>();
            if (AutowrapMode == LabelAutowrapMode.Off || Font == null || Size.X <= Padding.Horizontal)
            {
                lines.Add(paragraph);
                return lines;
            }
            var width = Math.Max(1, Size.X - Padding.Horizontal);
            if (AutowrapMode == LabelAutowrapMode.Arbitrary)
            {
                var currentCharacters = string.Empty;
                foreach (var character in paragraph)
                {
                    var candidate = currentCharacters + character;
                    if (currentCharacters.Length > 0 && MeasureTextWidth(candidate) > width) { lines.Add(currentCharacters); currentCharacters = character.ToString(); }
                    else currentCharacters = candidate;
                }
                lines.Add(currentCharacters);
                return lines;
            }
            var current = string.Empty;
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
                if (!string.IsNullOrEmpty(current) && MeasureTextWidth(candidate) > width)
                {
                    lines.Add(current);
                    current = word;
                }
                else current = candidate;
            }
            lines.Add(current);
            return lines;
        }
        private IEnumerable<string> SplitParagraphs(string text)
        {
            text = text.Replace("\r", string.Empty);
            var separator = GetNormalizedParagraphSeparator();
            // An empty separator disables paragraph splitting entirely (the whole text is one
            // paragraph), matching Godot allowing an empty paragraph_separator - handled explicitly
            // rather than relying on String.Split's edge-case behavior for an empty separator.
            if (separator.Length == 0) return new[] { text };
            return text.Split(new[] { separator }, StringSplitOptions.None);
        }
        private string GetNormalizedParagraphSeparator() => (ParagraphSeparator ?? "\n").Replace("\\n", "\n");
        private string GetTextForLayout() => Uppercase ? Text.ToUpperInvariant() : Text;
        private int MeasureTextWidth(string text) => (int)MathF.Ceiling(MeasureLineAdvance(text ?? string.Empty, text?.Length ?? 0, 0, int.MaxValue));
        private Vector2 MeasureTextBlock()
        {
            var dynamicLayout = GetDynamicLayout(useAvailableWidth: false);
            if (dynamicLayout != null) return dynamicLayout.Size;
            if (Font == null || string.IsNullOrEmpty(Text)) return Vector2.Zero;
            var size = Vector2.Zero;
            var lineCount = 0;
            foreach (var line in SplitParagraphs(GetTextForLayout()))
            {
                size.X = Math.Max(size.X, MeasureTextWidth(line));
                lineCount++;
            }
            size.Y = lineCount * Font.LineSpacing;
            return size;
        }
        private TextLayout GetDynamicLayout(bool useAvailableWidth = true)
        {
            var font = EffectiveUIFont;
            if (font == null) return null;
            var text = GetTextForLayout();
            var boundaries = UnicodeGraphemeSegmenter.GetUtf16Boundaries(text);
            var graphemeCount = Math.Max(0, boundaries.Length - 1);
            var visibleCount = VisibleCharacters >= 0
                ? Math.Min(VisibleCharacters, graphemeCount)
                : (int)MathF.Floor(graphemeCount * MathHelper.Clamp(VisibleRatio, 0, 1));
            if (VisibleCharacters < 0 && VisibleRatio >= 1) visibleCount = graphemeCount;
            var maxVisibleCharacters = visibleCount;
            if (VisibleCharactersBehavior == LabelVisibleCharactersBehavior.CharactersBeforeShaping)
            {
                text = text.Substring(0, boundaries[visibleCount]);
                maxVisibleCharacters = int.MaxValue;
            }
            var contentWidth = useAvailableWidth && Size.X > Padding.Horizontal ? Size.X - Padding.Horizontal : float.PositiveInfinity;
            var wrapping = AutowrapMode switch
            {
                LabelAutowrapMode.Arbitrary => TextWrapping.Character,
                LabelAutowrapMode.Word => TextWrapping.Word,
                LabelAutowrapMode.WordSmart => TextWrapping.Word,
                _ => TextWrapping.NoWrap
            };
            var trimming = TextOverrunBehavior switch
            {
                LabelTextOverrunBehavior.TrimCharacters => TextTrimming.CharacterEllipsis,
                LabelTextOverrunBehavior.Ellipsis => TextTrimming.CharacterEllipsis,
                LabelTextOverrunBehavior.EllipsisForce => TextTrimming.CharacterEllipsis,
                LabelTextOverrunBehavior.TrimWords => TextTrimming.WordEllipsis,
                LabelTextOverrunBehavior.WordEllipsis => TextTrimming.WordEllipsis,
                LabelTextOverrunBehavior.WordEllipsisForce => TextTrimming.WordEllipsis,
                _ => TextTrimming.None
            };
            var direction = TextDirection == TextDirection.Inherited ? TextDirection.Auto : TextDirection;
            var options = new TextLayoutOptions(
                contentWidth,
                wrapping,
                HorizontalAlignment,
                direction,
                GetTextLineSpacing(font),
                4,
                trimming,
                maxVisibleCharacters,
                Language,
                GetNormalizedParagraphSeparator(),
                GetValidTabStops(),
                _openTypeFeatures,
                ellipsis: EllipsisCharacter,
                paragraphSpacing: ParagraphSpacing,
                justificationFlags: MapJustificationFlags());
            return AdjustTextLayout((Context?.TextLayoutEngine ?? DynamicLayoutEngine).Layout(font, text, options));
        }
        protected virtual float GetTextLineSpacing(UIFont font) => 1;
        protected virtual TextLayout AdjustTextLayout(TextLayout layout) => layout;
        private TextJustificationFlags MapJustificationFlags()
        {
            var flags = TextJustificationFlags.None;
            if ((JustificationFlags & LabelJustificationFlags.WordBound) != 0) flags |= TextJustificationFlags.WordBound;
            if ((JustificationFlags & LabelJustificationFlags.AfterLastTab) != 0) flags |= TextJustificationFlags.AfterLastTab;
            if ((JustificationFlags & LabelJustificationFlags.SkipLastLine) != 0) flags |= TextJustificationFlags.SkipLastLine;
            if ((JustificationFlags & LabelJustificationFlags.SkipLastLineWithVisibleCharacters) != 0) flags |= TextJustificationFlags.SkipLastLineWithVisibleCharacters;
            if ((JustificationFlags & LabelJustificationFlags.DoNotSkipSingleLine) != 0) flags |= TextJustificationFlags.DoNotSkipSingleLine;
            return flags;
        }
        private IReadOnlyList<float> GetValidTabStops()
        {
            foreach (var stop in _tabStops)
                if (!float.IsFinite(stop) || stop <= 0) return Array.Empty<float>();
            return _tabStops;
        }
        private Rectangle GetDynamicCharacterBounds(TextLayout layout, int position)
        {
            if (position < 0 || position >= layout.Text.Length) return Rectangle.Empty;
            if (!layout.IsUtf16IndexVisible(position)) return Rectangle.Empty;
            TextLayoutLine line = null;
            foreach (var candidate in layout.Lines)
            {
                if (position < candidate.Start || position >= candidate.Start + candidate.Length) continue;
                line = candidate;
                break;
            }
            if (line == null) return Rectangle.Empty;
            var current = layout.GetCaretPosition(position);
            var next = layout.GetCaretPosition(position + 1);
            var left = Math.Min(current.X, next.X) + Padding.Left;
            var top = line.Origin.Y + Padding.Top + GetDynamicVerticalOffset(layout);
            return new Rectangle(
                (int)MathF.Floor(left),
                (int)MathF.Floor(top),
                Math.Max(1, (int)MathF.Ceiling(MathF.Abs(next.X - current.X))),
                Math.Max(1, (int)MathF.Ceiling(line.Size.Y)));
        }
        private float GetDynamicVerticalOffset(TextLayout layout)
        {
            var contentHeight = Math.Max(0, Size.Y - Padding.Vertical);
            var spare = Math.Max(0, contentHeight - layout.Size.Y);
            return VerticalAlignment switch
            {
                VerticalAlignment.Center => MathF.Floor(spare / 2),
                VerticalAlignment.Bottom => MathF.Floor(spare),
                _ => 0
            };
        }
        private float MeasureLineAdvance(string line, int characterCount, float extraSpace, int justificationStart)
        {
            if (string.IsNullOrEmpty(line) || characterCount <= 0) return 0;
            characterCount = Math.Min(characterCount, line.Length);
            if (Font == null)
            {
                var fallbackAdvance = 0f;
                var fallbackSegmentAdvance = 0f;
                var fallbackTabIndex = 0;
                for (var index = 0; index < characterCount; index++)
                {
                    if (line[index] == '\t')
                    {
                        var tabAdvance = GetTabAdvance(fallbackSegmentAdvance, ref fallbackTabIndex);
                        fallbackAdvance += tabAdvance;
                        fallbackSegmentAdvance = 0;
                    }
                    else
                    {
                        var glyphAdvance = 8f + (line[index] == ' ' && index >= justificationStart ? extraSpace : 0);
                        fallbackAdvance += glyphAdvance;
                        fallbackSegmentAdvance += glyphAdvance;
                    }
                }
                return fallbackAdvance;
            }
            var advance = 0f;
            var segmentAdvance = 0f;
            var runStart = 0;
            var tabIndex = 0;
            for (var index = 0; index < characterCount; index++)
            {
                var character = line[index];
                if (character != '\t' && (character != ' ' || index < justificationStart)) continue;
                var runLength = index - runStart + (character == ' ' ? 1 : 0);
                var runAdvance = runLength > 0 ? TextMetrics.Measure(Font, line.Substring(runStart, runLength)).X : 0;
                advance += runAdvance;
                segmentAdvance += runAdvance;
                if (character == '\t')
                {
                    var tabAdvance = GetTabAdvance(segmentAdvance, ref tabIndex);
                    advance += tabAdvance;
                    segmentAdvance = 0;
                }
                else
                {
                    advance += extraSpace;
                    segmentAdvance += extraSpace;
                }
                runStart = index + 1;
            }
            if (runStart < characterCount)
            {
                advance += TextMetrics.Measure(Font, line.Substring(runStart, characterCount - runStart)).X;
            }
            return advance;
        }
        private float GetTabAdvance(float segmentAdvance, ref int tabIndex)
        {
            var validStops = _tabStops.Length > 0;
            for (var index = 0; index < _tabStops.Length; index++) validStops &= _tabStops[index] > 0;
            if (!validStops)
            {
                var defaultWidth = Font == null ? 32 : Math.Max(1, TextMetrics.Measure(Font, "    ").X);
                return defaultWidth - segmentAdvance % defaultWidth;
            }
            var tabOffset = 0f;
            while (tabOffset <= segmentAdvance)
            {
                tabOffset += _tabStops[tabIndex];
                tabIndex = (tabIndex + 1) % _tabStops.Length;
            }
            return tabOffset - segmentAdvance;
        }
        private int GetJustificationStart(string line, int globalLineIndex)
        {
            if (HorizontalAlignment != HorizontalAlignment.Fill || (JustificationFlags & LabelJustificationFlags.WordBound) == 0) return int.MaxValue;
            var lineOffset = 0;
            var paragraphLineIndex = 0;
            List<string> paragraphLines = null;
            foreach (var paragraph in SplitParagraphs(GetVisibleText()))
            {
                paragraphLines = GetParagraphLines(paragraph);
                if (globalLineIndex < lineOffset + paragraphLines.Count)
                {
                    paragraphLineIndex = Math.Max(0, globalLineIndex - lineOffset);
                    break;
                }
                lineOffset += paragraphLines.Count;
                paragraphLines = null;
            }
            if (paragraphLines == null) return int.MaxValue;
            var justifyToLine = paragraphLines.Count;
            if (paragraphLines.Count != 1 || (JustificationFlags & LabelJustificationFlags.DoNotSkipSingleLine) == 0)
            {
                if ((JustificationFlags & LabelJustificationFlags.SkipLastLine) != 0) justifyToLine = paragraphLines.Count - 1;
                if ((JustificationFlags & LabelJustificationFlags.SkipLastLineWithVisibleCharacters) != 0)
                {
                    for (var index = paragraphLines.Count - 1; index >= 0; index--)
                    {
                        if (paragraphLines[index].Trim().Length == 0) continue;
                        justifyToLine = index;
                        break;
                    }
                }
            }
            if (paragraphLineIndex >= justifyToLine) return int.MaxValue;
            var afterLastTab = _tabStops.Length > 0 || (JustificationFlags & LabelJustificationFlags.AfterLastTab) != 0;
            return afterLastTab ? line.LastIndexOf('\t') + 1 : 0;
        }
        private float GetJustificationExtra(string line, float width, int justificationStart)
        {
            if (justificationStart == int.MaxValue) return 0;
            var spaces = 0;
            for (var index = Math.Max(0, justificationStart); index < line.Length; index++) if (line[index] == ' ') spaces++;
            return spaces == 0 ? 0 : Math.Max(0, width - MeasureTextWidth(line)) / spaces;
        }
        private void DrawLine(UIRenderContext context, string line, Vector2 position, Color color, float extraSpace, int justificationStart)
        {
            if (line.IndexOf('\t') < 0 && extraSpace <= 0) { context.Text(Font, line, position, color); return; }
            var advance = 0f;
            var segmentAdvance = 0f;
            var runStart = 0;
            var tabIndex = 0;
            for (var index = 0; index < line.Length; index++)
            {
                var character = line[index];
                if (character != '\t' && (character != ' ' || index < justificationStart)) continue;
                var runLength = index - runStart;
                if (runLength > 0)
                {
                    var run = line.Substring(runStart, runLength);
                    context.Text(Font, run, position + new Vector2(advance, 0), color);
                    var runAdvance = TextMetrics.Measure(Font, run).X;
                    advance += runAdvance;
                    segmentAdvance += runAdvance;
                }
                if (character == '\t')
                {
                    var tabAdvance = GetTabAdvance(segmentAdvance, ref tabIndex);
                    advance += tabAdvance;
                    segmentAdvance = 0;
                }
                else
                {
                    var spaceAdvance = TextMetrics.Measure(Font, " ").X + extraSpace;
                    advance += spaceAdvance;
                    segmentAdvance += spaceAdvance;
                }
                runStart = index + 1;
            }
            if (runStart < line.Length) context.Text(Font, line.Substring(runStart), position + new Vector2(advance, 0), color);
        }
        private string GetVisibleText()
        {
            var text = GetTextForLayout();
            // Godot only substrs the text itself for VC_CHARS_BEFORE_SHAPING; every other behavior
            // shapes/wraps the FULL text and hides characters per-glyph at draw time instead (out of
            // scope for this port, which doesn't model glyph-level rendering) - so line count, wrapping,
            // and minimum size must still be computed from the full text for those behaviors.
            if (VisibleCharactersBehavior != LabelVisibleCharactersBehavior.CharactersBeforeShaping) return text;
            var count = VisibleCharacters >= 0 ? VisibleCharacters : (int)MathF.Floor(text.Length * MathHelper.Clamp(VisibleRatio, 0, 1));
            if (VisibleCharacters < 0 && VisibleRatio >= 1) count = text.Length;
            return text.Substring(0, Math.Max(0, Math.Min(text.Length, count)));
        }
        private IReadOnlyList<string> ApplyLineWindow(List<string> lines)
        {
            var start = Math.Max(0, Math.Min(LinesSkipped, lines.Count));
            var count = MaxLinesVisible < 0 ? lines.Count - start : Math.Min(MaxLinesVisible, lines.Count - start);
            return ApplyLineWindow(lines, start, count);
        }
        private IReadOnlyList<string> ApplyLineWindow(List<string> lines, int start, int count)
        {
            if (TextOverrunBehavior != LabelTextOverrunBehavior.NoTrimming && AutowrapMode == LabelAutowrapMode.Off && Font != null && Size.X > Padding.Horizontal)
            {
                var width = Size.X - Padding.Horizontal;
                for (var i = start; i < start + count; i++) lines[i] = TrimLine(lines[i], width);
            }
            // Godot forces an ellipsis on the last visible line when autowrap produced more lines than
            // MaxLinesVisible allows, EVEN when TextOverrunBehavior is NoTrimming (the default) -
            // signaling that text continues beyond what's shown.
            if (AutowrapMode != LabelAutowrapMode.Off && count > 0 && start + count < lines.Count && Font != null)
            {
                var lastIndex = start + count - 1;
                var width = Size.X > Padding.Horizontal ? Size.X - Padding.Horizontal : float.MaxValue;
                lines[lastIndex] = TrimLine(lines[lastIndex], width, forceEllipsis: true);
            }
            return lines.GetRange(start, count);
        }
        private string TrimLine(string source, float width, bool forceEllipsis = false)
        {
            if (!forceEllipsis && MeasureTextWidth(source) <= width) return source;
            var ellipsis = !forceEllipsis && (TextOverrunBehavior == LabelTextOverrunBehavior.TrimCharacters || TextOverrunBehavior == LabelTextOverrunBehavior.TrimWords) ? string.Empty : EllipsisCharacter;
            var candidate = source;
            while (candidate.Length > 0 && MeasureTextWidth(candidate + ellipsis) > width)
            {
                var cut = candidate.Length - 1;
                if (TextOverrunBehavior == LabelTextOverrunBehavior.TrimWords || TextOverrunBehavior == LabelTextOverrunBehavior.WordEllipsis || TextOverrunBehavior == LabelTextOverrunBehavior.WordEllipsisForce) cut = candidate.LastIndexOf(' ', Math.Max(0, cut - 1));
                if (cut < 0) break;
                candidate = candidate.Substring(0, cut).TrimEnd();
            }
            return candidate + ellipsis;
        }
    }

    /// <summary>Chooses whether a button activates on pointer press or pointer release.</summary>
    public enum ButtonActionMode
    {
        Press,
        Release,
    }

    public class BaseButton : ContentControl
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.Button;
        public override string AccessibilityName => string.IsNullOrEmpty(base.AccessibilityName) ? Text ?? string.Empty : base.AccessibilityName;
        public override AccessibilityActions AccessibilityActions => base.AccessibilityActions | AccessibilityActions.Press |
            (ToggleMode ? AccessibilityActions.Toggle : AccessibilityActions.None);
        public override AccessibilityStates AccessibilityStates => base.AccessibilityStates |
            (ButtonPressed ? AccessibilityStates.Checked : AccessibilityStates.None);
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private bool _pressed;
        private bool _activationHandled;
        private bool _buttonPressed;
        private float _shortcutFeedbackRemaining;
        private PointerButton _activePointerButton;
        private Keys? _activeKey;
        private ButtonGroup _buttonGroup;
        public BaseButton()
        {
            FocusMode = FocusMode.All;
            Padding = new Thickness(8, 4, 8, 4);
        }
        public string Text { get; set; } = string.Empty;
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection);
        public Thickness Padding { get; set; }
        /// <summary>Horizontal text placement for text-bearing buttons.</summary>
        public HorizontalAlignment TextAlignment { get; set; } = HorizontalAlignment.Center;
        /// <summary>Optional Godot-style icon displayed alongside the button text.</summary>
        public Texture2D Icon { get; set; }
        public bool ExpandIcon { get; set; }
        public HorizontalAlignment IconAlignment { get; set; } = HorizontalAlignment.Left;
        public VerticalAlignment VerticalIconAlignment { get; set; } = VerticalAlignment.Center;
        public float IconSeparation { get; set; } = 4;
        public Color IconModulate { get; set; } = Color.White;
        internal Func<ThemeIcon?> DecorativeIconProvider { get; set; }
        internal bool HideTextWhenDecorativeIconAvailable { get; set; }
        /// <summary>Suppresses the default button panel while retaining focus feedback.</summary>
        public bool Flat { get; set; }
        public bool ToggleMode { get; set; }
        public ButtonActionMode ActionMode { get; set; } = ButtonActionMode.Release;
        /// <summary>Whether a release outside the button still completes an active pointer press.</summary>
        public bool KeepPressedOutside { get; set; }
        /// <summary>Physical pointer buttons that can activate this button, matching Godot's button_mask flags.</summary>
        public ButtonMouseMask ButtonMask { get; set; } = ButtonMouseMask.Left;
        /// <summary>Optional retained shortcut that activates this button without requiring focus.</summary>
        public PopupMenuShortcut Shortcut { get; private set; }
        /// <summary>Whether shortcut text should be appended to this button's tooltip, matching Godot's shortcut_in_tooltip.</summary>
        public bool ShortcutInTooltip { get; set; } = true;
        /// <summary>Whether shortcut activation should temporarily use pressed drawing feedback.</summary>
        public bool ShortcutFeedback { get; set; } = true;
        /// <summary>Seconds of retained visual feedback after shortcut activation.</summary>
        public float ShortcutFeedbackDuration { get; set; } = .2f;
        /// <summary>Gets or sets the toggled state, emitting <see cref="Toggled"/> when it changes.</summary>
        public bool ButtonPressed { get => _buttonPressed; set => SetPressed(value, true); }
        /// <summary>Gets or sets the mutual-exclusion group used by this button.</summary>
        public ButtonGroup ButtonGroup
        {
            get => _buttonGroup;
            set
            {
                if (ReferenceEquals(_buttonGroup, value)) return;
                _buttonGroup?.Unregister(this);
                _buttonGroup = value;
                _buttonGroup?.Register(this);
            }
        }
        public bool IsHovering { get; private set; }
        public bool IsPressing => _pressed;
        protected bool WasActivatedByPointer { get; private set; }
        public bool IsShortcutFeedbackActive => _shortcutFeedbackRemaining > 0;
        /// <summary>Whether the button should currently render as pressed, matching Godot's BaseButton::get_draw_mode() DRAW_PRESSED case. Unlike <see cref="IsPressing"/>/release activation, this combines with <see cref="KeepPressedOutside"/> while dragging outside the button's bounds. A keyboard-driven press is always visually pressed, matching Godot's status.pressing_inside being forced true for the accept-action path.</summary>
        public bool IsVisuallyPressed => (_pressed && (IsHovering || KeepPressedOutside || _activeKey != null)) || ButtonPressed || IsShortcutFeedbackActive;
        public override bool IsPseudoStateActive(string state) => state switch
        {
            "pressed" => IsVisuallyPressed,
            "checked" => ButtonPressed,
            _ => base.IsPseudoStateActive(state),
        };
        public event EventHandler Pressed;
        public event EventHandler ButtonDown;
        public event EventHandler ButtonUp;
        public event Action<BaseButton, bool> Toggled;
        public void SetShortcut(PopupMenuShortcut shortcut) => Shortcut = shortcut;
        public PopupMenuShortcut GetShortcut() => Shortcut;
        public void SetShortcutInTooltip(bool enabled) => ShortcutInTooltip = enabled;
        public bool IsShortcutInTooltipEnabled() => ShortcutInTooltip;
        public void SetShortcutFeedback(bool enabled) { ShortcutFeedback = enabled; if (!enabled) _shortcutFeedbackRemaining = 0; }
        public bool IsShortcutFeedback() => ShortcutFeedback;
        public void SetToggleMode(bool enabled) { ToggleMode = enabled; if (!enabled) SetPressedDirect(false, false); }
        public bool IsToggleMode() => ToggleMode;
        public void SetActionMode(ButtonActionMode mode) { if (!Enum.IsDefined(typeof(ButtonActionMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode)); ActionMode = mode; }
        public ButtonActionMode GetActionMode() => ActionMode;
        public void SetButtonMask(ButtonMouseMask mask) { if ((mask & ~(ButtonMouseMask.Left | ButtonMouseMask.Right | ButtonMouseMask.Middle)) != 0) throw new ArgumentOutOfRangeException(nameof(mask)); ButtonMask = mask; }
        public ButtonMouseMask GetButtonMask() => ButtonMask;
        public void SetKeepPressedOutside(bool enabled) => KeepPressedOutside = enabled;
        public bool IsKeepPressedOutside() => KeepPressedOutside;
        public void SetDisabled(bool disabled) => Enabled = !disabled;
        public bool IsDisabled() => !Enabled;
        /// <summary>Sets the pressed state without emitting <see cref="Toggled"/>.</summary>
        public void SetPressedNoSignal(bool pressed) => SetPressed(pressed, false);
        public void SetPressed(bool pressed) => SetPressed(pressed, true);
        /// <summary>Matches Godot's BaseButton::is_pressed: the toggled state for a toggle button, or whether the pointer/key is currently held down otherwise.</summary>
        public bool IsPressed() => ToggleMode ? ButtonPressed : IsPressing;
        public bool IsHovered() => IsHovering;
        public override Vector2 GetMinimumSize()
        {
            var text = EffectiveUIFont == null ? Vector2.Zero : TextMetrics.Measure(EffectiveUIFont, Text ?? string.Empty);
            var decorativeIcon = Icon == null ? DecorativeIconProvider?.Invoke() : null;
            if (decorativeIcon.HasValue && HideTextWhenDecorativeIconAvailable) text = Vector2.Zero;
            var icon = ExpandIcon ? Vector2.Zero : Icon != null ? new Vector2(Icon.Width, Icon.Height) : decorativeIcon.HasValue ? decorativeIcon.Value.LogicalSize.ToVector2() : Vector2.Zero;
            if (icon != Vector2.Zero)
            {
                text.Y = VerticalIconAlignment == VerticalAlignment.Center ? Math.Max(text.Y, icon.Y) : text.Y + icon.Y;
                text.X = IconAlignment == HorizontalAlignment.Center ? Math.Max(text.X, icon.X) : text.X + icon.X + (text.X > 0 ? IconSeparation : 0);
            }
            return Vector2.Max(base.GetMinimumSize(), Vector2.Max(CustomMinimumSize, text + new Vector2(Padding.Horizontal, Padding.Vertical)));
        }
        /// <summary>Calculates local text placement independent of a font renderer.</summary>
        public Vector2 GetTextPosition(Vector2 textSize)
        {
            var x = TextAlignment == HorizontalAlignment.Left ? Padding.Left
                : TextAlignment == HorizontalAlignment.Right ? Size.X - Padding.Right - textSize.X
                : (Size.X - textSize.X) / 2;
            return new Vector2(MathF.Max(Padding.Left, x), MathF.Max(Padding.Top, (Size.Y - textSize.Y) / 2));
        }
        /// <summary>Calculates the local icon rectangle for the configured icon alignment and expansion mode.</summary>
        public Rectangle GetIconRectangle(Vector2 iconSize)
        {
            if (iconSize.X <= 0 || iconSize.Y <= 0) return Rectangle.Empty;
            var size = iconSize;
            if (ExpandIcon)
            {
                var available = Vector2.Max(Vector2.Zero, Size - new Vector2(Padding.Horizontal, Padding.Vertical));
                var scale = Math.Min(available.X / iconSize.X, available.Y / iconSize.Y);
                size *= Math.Max(0, scale);
            }
            var alignment = IconAlignment;
            if (IsLayoutRtl()) alignment = alignment == HorizontalAlignment.Left ? HorizontalAlignment.Right : alignment == HorizontalAlignment.Right ? HorizontalAlignment.Left : alignment;
            var x = alignment == HorizontalAlignment.Right ? Size.X - Padding.Right - size.X : alignment == HorizontalAlignment.Center ? (Size.X - size.X) / 2 : Padding.Left;
            var y = VerticalIconAlignment == VerticalAlignment.Bottom ? Size.Y - Padding.Bottom - size.Y : VerticalIconAlignment == VerticalAlignment.Center ? (Size.Y - size.Y) / 2 : Padding.Top;
            return new Rectangle((int)MathF.Round(MathF.Max(Padding.Left, x)), (int)MathF.Round(MathF.Max(Padding.Top, y)), Math.Max(0, (int)MathF.Round(size.X)), Math.Max(0, (int)MathF.Round(size.Y)));
        }
        internal override bool HitTestBeforeChildren(Point point) => ContainsPoint(point);
        internal override void PointerEntered() { IsHovering = true; base.PointerEntered(); NotifyPseudoStateChanged("pressed"); }
        internal override void PointerExited() { IsHovering = false; base.PointerExited(); NotifyPseudoStateChanged("pressed"); }
        internal override void PointerPressed(Point position)
        {
            if (IsPointerButtonMasked(PointerButton.Left)) BeginPointerActivation(position, PointerButton.Left);
        }
        internal override void PointerButtonPressed(Point position, PointerButton button)
        {
            if (button == PointerButton.Left) return;
            if (!IsPointerButtonMasked(button)) { base.PointerButtonPressed(position, button); return; }
            BeginPointerActivation(position, button);
        }
        internal override void PointerReleased(Point position, bool isInside)
        {
            if (_activePointerButton == PointerButton.Left) EndPointerActivation(isInside);
        }
        internal override void PointerButtonReleased(Point position, PointerButton button)
        {
            if (button == PointerButton.Left) return;
            if (_activePointerButton == button) EndPointerActivation(ContainsPoint(position));
        }
        internal override void KeyPressed(Keys key)
        {
            // Godot's BaseButton::gui_input returns immediately for every input kind, including
            // the ui_accept action, whenever the button is disabled.
            if (!Enabled) return;
            if (key != Keys.Enter && key != Keys.Space) return;
            if (_pressed) return;
            _activeKey = key;
            _pressed = true;
            ButtonDown?.Invoke(this, EventArgs.Empty);
            NotifyPseudoStateChanged("pressed");
            // Godot's on_action_event treats the accept action like every other input kind: it honors
            // action_mode (activating immediately on press, or waiting for the matching release below).
            _activationHandled = ActionMode == ButtonActionMode.Press;
            if (_activationHandled) Activate();
        }
        /// <summary>Completes a keyboard-driven press, matching Godot's on_action_event release branch: button_up always fires, and release-mode activation happens here.</summary>
        internal override void KeyReleased(Keys key)
        {
            if (_activeKey != key) return;
            _activeKey = null;
            var activate = _pressed && !_activationHandled;
            _pressed = false;
            _activationHandled = false;
            ButtonUp?.Invoke(this, EventArgs.Empty);
            NotifyPseudoStateChanged("pressed");
            if (activate) Activate();
        }
        internal override bool ShortcutInput(Keys key, KeyboardState keyboard)
        {
            if (!Enabled || !Visible || Shortcut == null || !Shortcut.Matches(key, keyboard)) return false;
            Activate();
            if (ShortcutFeedback)
            {
                _shortcutFeedbackRemaining = Math.Max(0, ShortcutFeedbackDuration);
                NotifyPseudoStateChanged("pressed");
            }
            return true;
        }
        internal override void Process(GameTime gameTime)
        {
            var wasActive = IsShortcutFeedbackActive;
            if (_shortcutFeedbackRemaining > 0)
                _shortcutFeedbackRemaining = Math.Max(0, _shortcutFeedbackRemaining - (float)gameTime.ElapsedGameTime.TotalSeconds);
            if (wasActive != IsShortcutFeedbackActive) NotifyPseudoStateChanged("pressed");
            base.Process(gameTime);
        }
        public override string GetTooltip(Point position)
        {
            var tooltip = base.GetTooltip(position) ?? string.Empty;
            if (!ShortcutInTooltip || Shortcut == null) return tooltip;
            var shortcutText = Shortcut.DisplayText;
            if (string.IsNullOrEmpty(shortcutText)) return tooltip;
            var shortcutName = Shortcut.Name ?? string.Empty;
            if (string.IsNullOrEmpty(tooltip))
                return string.IsNullOrEmpty(shortcutName) ? shortcutText : $"{shortcutName} ({shortcutText})";
            if (!string.IsNullOrEmpty(shortcutName) && string.Equals(tooltip, shortcutName, StringComparison.OrdinalIgnoreCase))
                return $"{tooltip} ({shortcutText})";
            return $"{tooltip}\n{(string.IsNullOrEmpty(shortcutName) ? shortcutText : $"{shortcutName} ({shortcutText})")}";
        }
        private bool IsPointerButtonMasked(PointerButton button)
        {
            switch (button)
            {
                case PointerButton.Left: return (ButtonMask & ButtonMouseMask.Left) != 0;
                case PointerButton.Right: return (ButtonMask & ButtonMouseMask.Right) != 0;
                case PointerButton.Middle: return (ButtonMask & ButtonMouseMask.Middle) != 0;
                default: return false;
            }
        }
        private void BeginPointerActivation(Point position, PointerButton button)
        {
            base.PointerPressed(position);
            if (_pressed) return;
            _activePointerButton = button;
            _pressed = true;
            ButtonDown?.Invoke(this, EventArgs.Empty);
            NotifyPseudoStateChanged("pressed");
            _activationHandled = ActionMode == ButtonActionMode.Press;
            if (_activationHandled) Activate(true);
        }
        private void EndPointerActivation(bool isInside)
        {
            // Godot's on_action_event gates real activation on status.pressing_inside alone;
            // keep_pressed_outside only widens get_draw_mode's visual "pressed" state (see Draw()).
            var wasPressing = _pressed;
            var activate = wasPressing && isInside && !_activationHandled;
            _pressed = false;
            _activePointerButton = PointerButton.None;
            _activationHandled = false;
            if (wasPressing) ButtonUp?.Invoke(this, EventArgs.Empty);
            if (wasPressing) NotifyPseudoStateChanged("pressed");
            if (activate) Activate(true);
        }
        private void Activate(bool fromPointer = false)
        {
            WasActivatedByPointer = fromPointer;
            try
            {
                if (ToggleMode)
                {
                    if (ButtonPressed && _buttonGroup != null && !_buttonGroup.AllowUnpress)
                    {
                        // Godot's on_action_event unconditionally re-fires the group's pressed signal here even
                        // though _unpress_group() reasserts pressed=true and nothing actually changes.
                        _buttonGroup.NotifyPressed(this);
                        Toggled?.Invoke(this, true);
                    }
                    else
                        SetPressed(!ButtonPressed, true);
                }
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            finally { WasActivatedByPointer = false; }
        }
        private bool SetPressed(bool pressed, bool emitSignal)
        {
            if (!ToggleMode) return false;
            if (_buttonGroup != null) return _buttonGroup.SetPressed(this, pressed, emitSignal);
            return SetPressedDirect(pressed, emitSignal);
        }
        internal bool SetPressedDirect(bool pressed, bool emitSignal)
        {
            if (_buttonPressed == pressed) return false;
            _buttonPressed = pressed;
            if (emitSignal) Toggled?.Invoke(this, pressed);
            NotifyPseudoStateChanged("checked");
            NotifyPseudoStateChanged("pressed");
            return true;
        }
    }

    /// <summary>Maintains Godot-style mutually exclusive toggle buttons.</summary>
    public sealed class ButtonGroup
    {
        private readonly List<BaseButton> _buttons = new List<BaseButton>();
        /// <summary>Whether the selected button may be unpressed, leaving no selection.</summary>
        public bool AllowUnpress { get; set; }
        public IReadOnlyList<BaseButton> Buttons => _buttons;
        public BaseButton PressedButton
        {
            get
            {
                foreach (var button in _buttons) if (button.ButtonPressed) return button;
                return null;
            }
        }
        internal void Register(BaseButton button)
        {
            if (!_buttons.Contains(button)) _buttons.Add(button);
            if (button.ButtonPressed) SetPressed(button, true, false);
        }
        internal void Unregister(BaseButton button) => _buttons.Remove(button);
        internal bool SetPressed(BaseButton button, bool pressed, bool emitSignal)
        {
            if (!_buttons.Contains(button)) return button.SetPressedDirect(pressed, emitSignal);
            if (pressed)
            {
                foreach (var other in _buttons)
                    if (!ReferenceEquals(other, button)) other.SetPressedDirect(false, emitSignal);
                var changed = button.SetPressedDirect(true, emitSignal);
                if (changed) Pressed?.Invoke(this, button);
                return changed;
            }
            return button.SetPressedDirect(false, emitSignal);
        }
        /// <summary>Fires <see cref="Pressed"/> without changing any button's state, matching Godot re-firing button_group's pressed signal when re-activating the already-pressed member of a non-allow-unpress group.</summary>
        internal void NotifyPressed(BaseButton button) => Pressed?.Invoke(this, button);
        /// <summary>Raised when a member becomes the active button.</summary>
        public event Action<ButtonGroup, BaseButton> Pressed;
    }

    public sealed class Button : BaseButton { }

    public class CheckBox : BaseButton
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.CheckBox;
        public bool Checked { get => ButtonPressed; set => ButtonPressed = value; }
        public CheckBox() { ToggleMode = true; Padding = new Thickness(8, 4, 8, 4); }
        public override Vector2 GetMinimumSize()
        {
            var icon = GetStateIcon();
            var result = base.GetMinimumSize();
            if (icon.HasValue)
            {
                result.X += icon.Value.LogicalSize.X + IconSeparation;
                result.Y = Math.Max(result.Y, icon.Value.LogicalSize.Y + Padding.Vertical);
            }
            return Vector2.Max(CustomMinimumSize, result);
        }
        internal ThemeIcon? GetStateIcon() => GetThemeIcon(GetStateIconName());
        private string GetStateIconName()
        {
            var name = ButtonGroup != null ? (Checked ? "radio_checked" : "radio_unchecked") : Checked ? "checked" : "unchecked";
            if (!Enabled) name += "_disabled";
            if (this is CheckButton && IsLayoutRtl()) name += "_mirrored";
            return name;
        }
    }

    /// <summary>Toggle button variant that shares check-box semantics without a box glyph.</summary>
    public sealed class CheckButton : CheckBox { }

    public abstract class Range : TemplatedControl
    {
        public override string AccessibilityValue => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public override AccessibilityActions AccessibilityActions => base.AccessibilityActions |
            AccessibilityActions.Increment | AccessibilityActions.Decrement | AccessibilityActions.SetValue;
        private sealed class SharedState
        {
            public float Value, MinValue, MaxValue = 100, Step = 1, Page;
            public bool ExpRatio, AllowGreater, AllowLesser;
            public readonly List<Range> Owners = new List<Range>();
        }
        private SharedState _shared;
        protected Range() { _shared = new SharedState(); _shared.Owners.Add(this); }
        public float MinValue { get => _shared.MinValue; set { _shared.MinValue = value; _shared.MaxValue = Math.Max(_shared.MaxValue, value); _shared.Page = MathHelper.Clamp(_shared.Page, 0, _shared.MaxValue - _shared.MinValue); Value = _shared.Value; } }
        public float MaxValue { get => _shared.MaxValue; set { _shared.MaxValue = Math.Max(value, _shared.MinValue); _shared.Page = MathHelper.Clamp(_shared.Page, 0, _shared.MaxValue - _shared.MinValue); Value = _shared.Value; } }
        public float Step { get => _shared.Step; set => _shared.Step = value; }
        public float Page { get => _shared.Page; set { _shared.Page = MathHelper.Clamp(value, 0, MaxValue - MinValue); Value = _shared.Value; } }
        public bool AllowGreater { get => _shared.AllowGreater; set => _shared.AllowGreater = value; }
        public bool AllowLesser { get => _shared.AllowLesser; set => _shared.AllowLesser = value; }
        public bool ExpRatio { get => _shared.ExpRatio; set => _shared.ExpRatio = value; }
        public bool UseRoundedValues { get; set; }
        public float Value
        {
            get => _shared.Value;
            set
            {
                var clamped = CalculateValue(value); if (_shared.Value == clamped) return;
                _shared.Value = clamped; foreach (var owner in _shared.Owners) owner.ValueChanged?.Invoke(owner, clamped);
            }
        }
        public float Ratio { get => GetAsRatio(); set => SetAsRatio(value); }
        public event Action<Range, float> ValueChanged;
        public void SetValueNoSignal(float value) => _shared.Value = CalculateValue(value);
        public void SetAsRatio(float ratio)
        {
            ratio = MathHelper.Clamp(ratio, 0, 1);
            if (ExpRatio && MinValue >= 0 && MaxValue > 0)
            {
                var minExponent = MinValue == 0 ? 0 : Log2(MinValue); Value = MathF.Pow(2, minExponent + (Log2(MaxValue) - minExponent) * ratio);
            }
            else Value = MinValue + (MaxValue - MinValue) * ratio;
        }
        public float GetAsRatio()
        {
            if (MaxValue == MinValue) return 1;
            if (ExpRatio && MinValue >= 0 && MaxValue > 0 && Value > 0) { var minExponent = MinValue == 0 ? 0 : Log2(MinValue); return MathHelper.Clamp((Log2(Value) - minExponent) / (Log2(MaxValue) - minExponent), 0, 1); }
            return MathHelper.Clamp((Value - MinValue) / (MaxValue - MinValue), 0, 1);
        }
        private static float Log2(float value) => (float)(Math.Log(value) / Math.Log(2));
        public void Share(Range range)
        {
            if (range == null) throw new ArgumentNullException(nameof(range)); if (ReferenceEquals(_shared, range._shared)) return;
            _shared.Owners.Remove(this); _shared = range._shared; _shared.Owners.Add(this);
        }
        public void Unshare()
        {
            if (_shared.Owners.Count == 1) return;
            var copy = new SharedState { Value = _shared.Value, MinValue = _shared.MinValue, MaxValue = _shared.MaxValue, Step = _shared.Step, Page = _shared.Page, ExpRatio = _shared.ExpRatio, AllowGreater = _shared.AllowGreater, AllowLesser = _shared.AllowLesser };
            _shared.Owners.Remove(this); _shared = copy; _shared.Owners.Add(this);
        }
        /// <summary>Snaps a value to the nearest multiple of step anchored at MinValue, matching Godot's
        /// Range::_calc_value / _snapped_r128 formula (floor(x/step + 0.5) * step) - round-half-up, not
        /// .NET's default round-half-to-even.</summary>
        protected float SnapToStep(float value, float step) => step > 0 ? MathF.Floor((value - MinValue) / step + 0.5f) * step + MinValue : value;
        /// <summary>Snaps a value to the nearest multiple of step with no anchor, matching Godot's
        /// anchor-free Math::snapped (used e.g. to pre-snap SpinBox's arrow_step itself).</summary>
        protected static float SnapToMultiple(float value, float step) => step > 0 ? MathF.Floor(value / step + 0.5f) * step : value;
        private float CalculateValue(float value)
        {
            if (Step > 0) value = SnapToStep(value, Step);
            // Godot's Math::round is round-half-away-from-zero; .NET's default MathF.Round is round-half-to-even.
            if (UseRoundedValues) value = MathF.Round(value, MidpointRounding.AwayFromZero);
            if (!AllowGreater) value = Math.Min(MaxValue - Page, value);
            if (!AllowLesser) value = Math.Max(MinValue, value);
            return value;
        }
    }

    public enum SliderTickPosition { BottomRight, TopLeft, Both, Center }

    public class Slider : Range
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.Slider;
        private bool _dragging;
        private bool _hovering;
        private float _dragStartMain;
        private float _dragStartRatio;
        private float _ratioBeforeDragging;
        public Slider(Orientation orientation = Orientation.Horizontal) { Orientation = orientation; FocusMode = FocusMode.All; }
        public Orientation Orientation { get; }
        public bool Editable { get; set; } = true;
        /// <summary>Raised when a pointer drag begins, matching Godot's Slider.drag_started signal.</summary>
        public new event EventHandler DragStarted;
        /// <summary>Raised when a pointer drag ends; the argument reports whether the ratio actually changed, matching Godot's Slider.drag_ended(value_changed) signal.</summary>
        public new event Action<Slider, bool> DragEnded;
        /// <summary>Enables mouse-wheel adjustment, matching Godot's Slider.scrollable property.</summary>
        public bool Scrollable { get; set; } = true;
        /// <summary>Number of evenly-spaced visual ticks; values below two draw no ticks.</summary>
        public int TickCount { get; set; }
        public bool TicksOnBorders { get; set; }
        public SliderTickPosition TicksPosition { get; set; } = SliderTickPosition.BottomRight;
        /// <summary>Godot-style keyboard/gamepad increment override; negative values fall back to <see cref="Range.Step"/>.</summary>
        public float CustomStep { get; set; } = -1;
        public void SetCustomStep(float customStep) => CustomStep = customStep;
        public float GetCustomStep() => CustomStep;
        internal override void PointerEntered() { _hovering = true; base.PointerEntered(); }
        internal override void PointerExited() { _hovering = false; base.PointerExited(); }
        public override Vector2 GetMinimumSize() => Vector2.Max(CustomMinimumSize, Orientation == Orientation.Horizontal ? new Vector2(48, 20) : new Vector2(20, 48));
        /// <summary>Returns local tick rectangles for deterministic theme-independent validation.</summary>
        public IReadOnlyList<Rectangle> GetTickRectangles()
        {
            var result = new List<Rectangle>();
            if (TickCount <= 1) return result;
            var mainLength = Math.Max(0, (int)MathF.Round((Orientation == Orientation.Horizontal ? Size.X : Size.Y) - 12));
            var crossLength = Math.Max(0, (int)MathF.Round(Orientation == Orientation.Horizontal ? Size.Y : Size.X));
            for (var i = 0; i < TickCount; i++)
            {
                if (!TicksOnBorders && (i == 0 || i == TickCount - 1)) continue;
                var main = 6 + (int)MathF.Round(mainLength * i / (float)(TickCount - 1));
                AddTick(result, main, crossLength, TicksPosition);
            }
            return result;
        }
        internal override void PointerPressed(Point point)
        {
            base.PointerPressed(point);
            if (!Editable) return;
            _ratioBeforeDragging = Ratio;
            DragStarted?.Invoke(this, EventArgs.Empty);
            SetFromPoint(point);
            _dragging = true;
            _dragStartMain = Orientation == Orientation.Horizontal ? point.X : point.Y;
            _dragStartRatio = Ratio;
        }
        /// <summary>Tracks the pointer while held, matching Godot's Slider::gui_input relative-motion grab (grab.pos/grab.uvalue).</summary>
        internal override void PointerMoved(Point point)
        {
            if (!_dragging || !Editable) return;
            var main = Orientation == Orientation.Horizontal ? point.X : point.Y;
            var motion = main - _dragStartMain;
            if (Orientation == Orientation.Vertical) motion = -motion;
            else if (IsLayoutRtl()) motion = -motion;
            var areaSize = Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
            if (areaSize <= 0) return;
            Ratio = _dragStartRatio + motion / areaSize;
        }
        internal override void PointerReleased(Point point, bool isInside)
        {
            if (!_dragging) return;
            _dragging = false;
            DragEnded?.Invoke(this, Math.Abs(_ratioBeforeDragging - Ratio) > 0.0001f);
        }
        private void SetFromPoint(Point point)
        {
            if (!Editable) return;
            var ratio = Orientation == Orientation.Horizontal ? (point.X - Bounds.Left) / Math.Max(1f, Bounds.Width) : (point.Y - Bounds.Top) / Math.Max(1f, Bounds.Height);
            ratio = MathHelper.Clamp(ratio, 0, 1);
            // Godot's Slider::gui_input inverts the ratio for a vertical slider (the top of the track is
            // max, the standard "fader" convention) and mirrors it for a horizontal slider under RTL;
            // this click-jump path had been computing the un-inverted ratio directly, while the drag-
            // continuation path in PointerMoved already applied both inversions correctly.
            if (Orientation == Orientation.Vertical) ratio = 1 - ratio;
            else if (IsLayoutRtl()) ratio = 1 - ratio;
            Value = MinValue + (MaxValue - MinValue) * ratio;
        }
        internal override void KeyPressed(Keys key)
        {
            if (!Editable) return;
            var step = CustomStep >= 0 ? CustomStep : Step;
            if (key == Keys.Home) Value = MinValue;
            else if (key == Keys.End) Value = MaxValue;
            else if (Orientation == Orientation.Horizontal && key == Keys.Left) Value += IsLayoutRtl() ? step : -step;
            else if (Orientation == Orientation.Horizontal && key == Keys.Right) Value += IsLayoutRtl() ? -step : step;
            else if (Orientation == Orientation.Vertical && key == Keys.Up) Value += step;
            else if (Orientation == Orientation.Vertical && key == Keys.Down) Value -= step;
        }
        /// <summary>Mouse-wheel adjustment gated on <see cref="Editable"/> and <see cref="Scrollable"/>, matching Godot's Slider::gui_input WHEEL_UP/WHEEL_DOWN handling.</summary>
        internal override bool PointerWheel(int delta)
        {
            if (!Editable || !Scrollable || delta == 0) return false;
            if (FocusMode != FocusMode.None) GrabFocus();
            Value += delta > 0 ? Step : -Step;
            return true;
        }
        internal override bool HitTestBeforeChildren(Point point) => ContainsPoint(point);
        internal bool IsGrabberHighlighted => _hovering || _dragging;

        internal ThemeIcon? GetSliderThemeIcon(string itemName) => GetThemeIcon(itemName, Orientation == Orientation.Horizontal ? nameof(HSlider) : nameof(VSlider));

        private void AddTick(List<Rectangle> ticks, int main, int crossLength, SliderTickPosition position)
        {
            if (Orientation == Orientation.Horizontal)
            {
                if (position == SliderTickPosition.BottomRight || position == SliderTickPosition.Both) ticks.Add(new Rectangle(main - 1, Math.Max(0, crossLength - 4), 2, 4));
                if (position == SliderTickPosition.TopLeft || position == SliderTickPosition.Both) ticks.Add(new Rectangle(main - 1, 0, 2, 4));
                if (position == SliderTickPosition.Center) ticks.Add(new Rectangle(main - 1, Math.Max(0, crossLength / 2 - 2), 2, 4));
            }
            else
            {
                if (position == SliderTickPosition.BottomRight || position == SliderTickPosition.Both) ticks.Add(new Rectangle(Math.Max(0, crossLength - 4), main - 1, 4, 2));
                if (position == SliderTickPosition.TopLeft || position == SliderTickPosition.Both) ticks.Add(new Rectangle(0, main - 1, 4, 2));
                if (position == SliderTickPosition.Center) ticks.Add(new Rectangle(Math.Max(0, crossLength / 2 - 2), main - 1, 4, 2));
            }
        }
    }
    public sealed class HSlider : Slider { public HSlider() : base(Orientation.Horizontal) { } }
    public sealed class VSlider : Slider { public VSlider() : base(Orientation.Vertical) { } }

    public enum ProgressBarFillMode { BeginToEnd, EndToBegin, TopToBottom, BottomToTop }

    public sealed class ProgressBar : Range
    {
        public override AccessibilityRole AccessibilityRole => AccessibilityRole.ProgressBar;
        public override AccessibilityActions AccessibilityActions => base.AccessibilityActions &
            ~(AccessibilityActions.Increment | AccessibilityActions.Decrement | AccessibilityActions.SetValue);
        private readonly UIFontSelection _fontSelection = new UIFontSelection();
        private ProgressBarFillMode _fillMode;
        private bool _indeterminate;
        // Godot's ProgressBar constructor calls set_step(0.01) - near-continuous, not Range's own
        // integer-snapping default of 1 - so setting a fractional Value like 0.37 isn't silently rounded.
        public ProgressBar() { Step = 0.01f; }
        public bool ShowPercentage { get; set; } = true;
        public ProgressBarFillMode FillMode { get => _fillMode; set => SetFillMode(value); }
        public bool Indeterminate { get => _indeterminate; set => SetIndeterminate(value); }
        public bool EditorPreviewIndeterminate { get; set; }
        /// <summary>Pixels per second used by the indeterminate segment.</summary>
        public float IndeterminateSpeed { get; set; } = 200;
        public float IndeterminateOffset { get; private set; }
        public SpriteFont Font { get => _fontSelection.SpriteFont; set { _fontSelection.SetSpriteFont(value); QueueLayout(); } }
        public UIFont UIFont { get => _fontSelection.UIFont; set { _fontSelection.SetUIFont(value); QueueLayout(); } }
        internal UIFont EffectiveUIFont => ResolveFont(_fontSelection);
        public void SetFillMode(ProgressBarFillMode mode)
        {
            if (!Enum.IsDefined(typeof(ProgressBarFillMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            _fillMode = mode;
            IndeterminateOffset = 0;
        }
        public ProgressBarFillMode GetFillMode() => FillMode;
        public void SetShowPercentage(bool visible) => ShowPercentage = visible;
        public bool IsPercentageShown() => ShowPercentage;
        public void SetIndeterminate(bool indeterminate)
        {
            if (_indeterminate == indeterminate) return;
            _indeterminate = indeterminate;
            IndeterminateOffset = 0;
            QueueLayout();
        }
        public bool IsIndeterminate() => Indeterminate;
        public void SetEditorPreviewIndeterminate(bool previewIndeterminate)
        {
            if (EditorPreviewIndeterminate == previewIndeterminate) return;
            EditorPreviewIndeterminate = previewIndeterminate;
            IndeterminateOffset = 0;
        }
        public bool IsEditorPreviewIndeterminateEnabled() => EditorPreviewIndeterminate;
        public override Vector2 GetMinimumSize() => Vector2.Max(CustomMinimumSize, new Vector2(48, 20));
        /// <summary>Returns the current local fill rectangle for determinate state.</summary>
        public Rectangle GetFillRectangle(float ratio)
        {
            ratio = MathHelper.Clamp(ratio, 0, 1);
            var width = Math.Max(0, (int)MathF.Round(Size.X - 2));
            var height = Math.Max(0, (int)MathF.Round(Size.Y - 2));
            var filledWidth = (int)MathF.Round(width * ratio);
            var filledHeight = (int)MathF.Round(height * ratio);
            switch (FillMode)
            {
                case ProgressBarFillMode.EndToBegin: return new Rectangle(1 + width - filledWidth, 1, filledWidth, height);
                case ProgressBarFillMode.TopToBottom: return new Rectangle(1, 1, width, filledHeight);
                case ProgressBarFillMode.BottomToTop: return new Rectangle(1, 1 + height - filledHeight, width, filledHeight);
                default: return new Rectangle(1, 1, filledWidth, height);
            }
        }
        internal override void Process(GameTime gameTime)
        {
            if (Indeterminate)
            {
                var extent = FillMode == ProgressBarFillMode.TopToBottom || FillMode == ProgressBarFillMode.BottomToTop ? Size.Y : Size.X;
                var segment = Math.Min(Size.X, Size.Y) * 2;
                IndeterminateOffset += Math.Max(1, IndeterminateSpeed) * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (IndeterminateOffset > extent + segment) IndeterminateOffset = 0;
            }
            base.Process(gameTime);
        }
    }
}
