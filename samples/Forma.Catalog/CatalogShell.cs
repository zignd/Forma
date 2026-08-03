// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Forma;
using Forma.Xaml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

public sealed class CatalogShell : BoxContainer
{
    private static readonly HashSet<string> ExcludedProperties = new HashSet<string>
    {
        nameof(Control.Name), nameof(Control.Position), nameof(Control.Size), nameof(Control.CustomMinimumSize),
        nameof(Control.Parent), nameof(Control.Context), nameof(Control.Children), nameof(Control.Bounds),
        nameof(Control.GlobalPosition), nameof(Control.Margins),
    };

    private readonly IReadOnlyList<ComponentStory> _stories;
    private readonly UIFont _font;
    private readonly UIFont _codeFont;
    private readonly SpriteFont _compatibilityFont;
    private LineEdit _search;
    private ItemList _navigation;
    private Label _storyTitle;
    private Label _storyCategory;
    private RichTextLabel _description;
    private CatalogPreviewContainer _preview;
    private VBoxContainer _inspector;
    private Label _count;
    private readonly Action<bool> _setDynamicTextEnabled;
    private readonly CatalogShellViewModel _viewModel;
    private CheckBox _dynamicTextToggle;
    private List<ComponentStory> _filteredStories;
    private ComponentStory _currentStory;
    private Control _currentControl;
    private IDisposable _authoredStyles;
    private IDisposable _authoredStoryboard;
    private bool _detached;

    public event Action<ComponentStory, Control> ActiveStoryChanged;
    public ComponentStory ActiveStory => _currentStory;
    public Control ActiveStoryControl => _currentControl;

    public CatalogShell(IReadOnlyList<ComponentStory> stories, SpriteFont font, SpriteFont codeFont, Action<bool> setDynamicTextEnabled = null)
        : this(stories, new SpriteFontAdapter(font), new SpriteFontAdapter(codeFont), font, setDynamicTextEnabled)
    {
    }

    public CatalogShell(IReadOnlyList<ComponentStory> stories, UIFont font, UIFont codeFont, SpriteFont compatibilityFont, Action<bool> setDynamicTextEnabled = null)
        : base(Orientation.Vertical)
    {
        _stories = stories;
        _font = font;
        _codeFont = codeFont;
        _compatibilityFont = compatibilityFont;
        _setDynamicTextEnabled = setDynamicTextEnabled;
        _viewModel = new CatalogShellViewModel { DynamicTextEnabled = true };
        DataContext = _viewModel;
        Attached += (_, _) => _detached = false;
        Detached += (_, _) => _detached = true;
        FormaXamlLoader.Load(this);
        BindVisualTree();
        RefreshNavigation();
    }

    public void ApplyHotReloadedTree(BoxContainer replacement)
    {
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        var selectedStory = _currentStory?.Name;
        var storyDataContext = _currentControl?.DataContext;
        var dynamicTextEnabled = DynamicTextEnabled;
        _authoredStyles?.Dispose();
        _authoredStoryboard?.Dispose();
        ClearChildren(this);
        while (replacement.Children.Count > 0) AddChild(replacement.Children[0]);
        Classes.Clear();
        foreach (var className in replacement.Classes) Classes.Add(className);
        Separation = replacement.Separation;
        Alignment = replacement.Alignment;
        ReverseSort = replacement.ReverseSort;
        NameScope.CreateForTree(this);
        _viewModel.DynamicTextEnabled = dynamicTextEnabled;
        BindVisualTree();
        RefreshNavigation(selectFirst: selectedStory == null);
        if (selectedStory != null)
        {
            var story = _stories.First(candidate => candidate.Name == selectedStory);
            _navigation.Select(_filteredStories.IndexOf(story));
            LoadStory(story, storyDataContext);
        }
    }

    public void ReplaceActiveStory(Control expected, Control replacement)
    {
        if (!ReferenceEquals(_currentControl, expected)) return;
        if (replacement == null) throw new ArgumentNullException(nameof(replacement));
        var index = 0;
        while (index < _preview.Children.Count && !ReferenceEquals(_preview.Children[index], expected)) index++;
        if (index == _preview.Children.Count) throw new InvalidOperationException("The active story is no longer in the preview slot.");
        _preview.RemoveChild(expected);
        _preview.AddChild(replacement);
        if (index < _preview.Children.Count - 1) _preview.MoveChild(replacement, index);
        ClearChildren(_inspector);
        _currentControl = replacement;
        FontApplicator.Apply(replacement, _font, _codeFont, _compatibilityFont);
        _currentStory.Attached?.Invoke(replacement);
        BuildInspector(replacement);
    }

    private void BindVisualTree()
    {
        var scope = NameScope.GetNameScope(this) ?? throw new InvalidOperationException("CatalogShell XAML did not create a namescope.");
        _search = scope.Find<LineEdit>("Search") ?? throw MissingName("Search");
        _navigation = scope.Find<ItemList>("Navigation") ?? throw MissingName("Navigation");
        _storyTitle = scope.Find<Label>("StoryTitle") ?? throw MissingName("StoryTitle");
        _storyCategory = scope.Find<Label>("StoryCategory") ?? throw MissingName("StoryCategory");
        _description = scope.Find<RichTextLabel>("Description") ?? throw MissingName("Description");
        _preview = scope.Find<CatalogPreviewContainer>("Preview") ?? throw MissingName("Preview");
        _inspector = scope.Find<VBoxContainer>("Inspector") ?? throw MissingName("Inspector");
        _count = scope.Find<Label>("Count") ?? throw MissingName("Count");
        _dynamicTextToggle = scope.Find<CheckBox>("dynamicTextMode") ?? throw MissingName("dynamicTextMode");
        _dynamicTextToggle.ButtonPressed = _viewModel.DynamicTextEnabled;
        _count.Text = _viewModel.CountText;
        _storyCategory.Text = _viewModel.StoryCategory;
        _storyTitle.Text = _viewModel.StoryTitle;
        _description.Text = _viewModel.Description;
        _search.TextChanged += (_, _) => RefreshNavigation();
        _navigation.ItemSelected += (_, index) => SelectStory(index);
        var reset = scope.Find<Button>("Reset") ?? throw MissingName("Reset");
        reset.Pressed += (_, _) => LoadStory(_currentStory);
        _dynamicTextToggle.Toggled += (_, enabled) =>
        {
            if (_detached) return;
            _setDynamicTextEnabled?.Invoke(enabled);
            if (_currentStory != null) LoadStory(_currentStory);
        };

        var visuals = scope.Find<CatalogVisualResources>("VisualResources") ?? throw MissingName("VisualResources");
        ApplyPanel(scope.Find<PanelContainer>("NavigationPanel"), visuals.PanelBackground, visuals.PanelBackground);
        ApplyPanel(scope.Find<PanelContainer>("PreviewPanel"), visuals.PreviewBackground, visuals.PreviewBorder);
        ApplyPanel(scope.Find<PanelContainer>("InspectorPanel"), visuals.PanelBackground, visuals.PanelBackground);
        scope.Find<ColorRect>("HeaderAccent").Color = visuals.AccentColor;
        scope.Find<Label>("CatalogSubtitle").FontColor = visuals.MutedTextColor;
        scope.Find<Label>("BackendStatus").FontColor = visuals.WarningColor;
        AttachAuthoredStyles(visuals);
        BeginAuthoredStoryboard(visuals);
        RemoveChild(visuals);

        FontApplicator.Apply(this, _font, _codeFont, _compatibilityFont);
    }

    public bool DynamicTextEnabled => _dynamicTextToggle.ButtonPressed;

    private void RefreshNavigation(bool selectFirst = true)
    {
        var query = _search?.Text?.Trim() ?? string.Empty;
        _filteredStories = _stories.Where(story =>
                string.IsNullOrEmpty(query) ||
                story.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                story.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _navigation.Clear();
        foreach (var story in _filteredStories) _navigation.AddItem($"{story.Category}  /  {story.Name}");
        _viewModel.CountText = $"{_filteredStories.Count} of {_stories.Count} controls";
        _count.Text = _viewModel.CountText;
        if (selectFirst && _filteredStories.Count > 0)
        {
            _navigation.Select(0);
            SelectStory(0);
        }
    }

    private void SelectStory(int filteredIndex)
    {
        if (filteredIndex < 0 || filteredIndex >= _filteredStories.Count) return;
        LoadStory(_filteredStories[filteredIndex]);
    }

    public bool SelectStory(string storyName)
    {
        var story = _stories.FirstOrDefault(candidate => string.Equals(candidate.Name, storyName, StringComparison.Ordinal));
        if (story == null) return false;
        LoadStory(story);
        return true;
    }

    private void LoadStory(ComponentStory story, object retainedDataContext = null)
    {
        if (story == null) return;
        _currentStory = story;
        ClearChildren(_preview);
        ClearChildren(_inspector);
        _currentControl = story.Factory();
        if (retainedDataContext != null) _currentControl.DataContext = retainedDataContext;
        _preview.AddChild(_currentControl);
        FontApplicator.Apply(_currentControl, _font, _codeFont, _compatibilityFont);
        story.Attached?.Invoke(_currentControl);
        _viewModel.StoryCategory = story.Category;
        _viewModel.StoryTitle = story.Name;
        _viewModel.Description = story.Description;
        _storyCategory.Text = _viewModel.StoryCategory;
        _storyTitle.Text = _viewModel.StoryTitle;
        _description.Text = _viewModel.Description;
        _description.ParseBbcode(story.Description);
        BuildInspector(_currentControl);
        ActiveStoryChanged?.Invoke(story, _currentControl);
    }

    private void BuildInspector(Control target)
    {
        var properties = target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            .Where(property => !ExcludedProperties.Contains(property.Name) && IsInspectable(property.PropertyType))
            .OrderBy(property => property.DeclaringType == target.GetType() ? 0 : 1)
            .ThenBy(property => property.Name)
            .Take(18)
            .ToList();

        if (properties.Count == 0)
        {
            _inspector.AddChild(new Label { Text = "No safe writable properties exposed.", UIFont = _font, AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(270, 42) });
            return;
        }

        foreach (var property in properties)
        {
            var editor = CreatePropertyEditor(target, property);
            if (editor == null) continue;
            var section = new VBoxContainer { Separation = 4 };
            section.AddChild(new Label { Text = SplitName(property.Name), UIFont = _font, FontColor = new Color(174, 184, 200), CustomMinimumSize = new Vector2(0, 20) });
            section.AddChild(editor);
            _inspector.AddChild(section);
        }
    }

    private Control CreatePropertyEditor(Control target, PropertyInfo property)
    {
        object ReadValue()
        {
            try { return property.GetValue(target); }
            catch { return null; }
        }

        void SetValue(object value)
        {
            try { property.SetValue(target, value); }
            catch { }
        }

        if (property.PropertyType == typeof(bool))
        {
            var toggle = new CheckBox { Text = "Enabled", UIFont = _font, ButtonPressed = (bool)(ReadValue() ?? false), CustomMinimumSize = new Vector2(0, 30) };
            toggle.Toggled += (_, value) => SetValue(value);
            return toggle;
        }
        if (property.PropertyType == typeof(string))
        {
            var field = new LineEdit { UIFont = _font, Text = (string)ReadValue() ?? string.Empty, CustomMinimumSize = new Vector2(0, 32) };
            field.TextChanged += (_, value) => SetValue(value);
            return field;
        }
        if (property.PropertyType.IsEnum)
        {
            var option = new OptionButton { UIFont = _font, CustomMinimumSize = new Vector2(0, 32) };
            var values = Enum.GetValues(property.PropertyType);
            for (var index = 0; index < values.Length; index++) option.AddItem(values.GetValue(index).ToString(), index);
            var current = ReadValue();
            for (var index = 0; index < values.Length; index++) if (Equals(values.GetValue(index), current)) option.Select(index);
            option.ItemSelected += (_, index) => SetValue(values.GetValue(index));
            return option;
        }
        if (property.PropertyType == typeof(Color))
        {
            var picker = new ColorPickerButton { UIFont = _font, Color = (Color)(ReadValue() ?? Color.White), CustomMinimumSize = new Vector2(0, 34) };
            picker.ColorChanged += (_, value) => SetValue(value);
            return picker;
        }
        if (IsNumeric(property.PropertyType))
        {
            var value = Convert.ToSingle(ReadValue() ?? 0, CultureInfo.InvariantCulture);
            var maximum = Math.Max(100, MathF.Ceiling(Math.Abs(value) * 2 + 10));
            var row = new HBoxContainer { Separation = 8, CustomMinimumSize = new Vector2(0, 30) };
            var valueLabel = new Label { Text = value.ToString("0.##", CultureInfo.InvariantCulture), UIFont = _font, CustomMinimumSize = new Vector2(54, 28), VerticalAlignment = VerticalAlignment.Center };
            var slider = new HSlider { MinValue = 0, MaxValue = maximum, Step = property.PropertyType == typeof(float) || property.PropertyType == typeof(double) ? .1f : 1, Value = value, HorizontalSizeFlags = SizeFlags.Fill | SizeFlags.Expand };
            slider.ValueChanged += (_, changed) =>
            {
                valueLabel.Text = changed.ToString("0.##", CultureInfo.InvariantCulture);
                SetValue(ConvertNumeric(changed, property.PropertyType));
            };
            row.AddChild(slider);
            row.AddChild(valueLabel);
            return row;
        }
        return null;
    }

    private static bool IsInspectable(Type type) => type == typeof(bool) || type == typeof(string) || type == typeof(Color) || type.IsEnum || IsNumeric(type);
    private static bool IsNumeric(Type type) => type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double);
    private static object ConvertNumeric(float value, Type type)
    {
        if (type == typeof(byte)) return (byte)MathHelper.Clamp((int)MathF.Round(value), byte.MinValue, byte.MaxValue);
        if (type == typeof(short)) return (short)MathHelper.Clamp((int)MathF.Round(value), short.MinValue, short.MaxValue);
        if (type == typeof(int)) return (int)MathF.Round(value);
        if (type == typeof(long)) return (long)MathF.Round(value);
        if (type == typeof(double)) return (double)value;
        return value;
    }

    private static string SplitName(string name)
    {
        var result = string.Empty;
        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]) && !char.IsUpper(name[index - 1])) result += " ";
            result += name[index];
        }
        return result;
    }

    private static void ApplyPanel(PanelContainer panel, Color background, Color border)
    {
        if (panel == null) return;
        panel.AddThemeStyleOverride("panel", new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderWidth = 1,
            ContentMargin = new Thickness(0),
        });
    }

    private static InvalidOperationException MissingName(string name) => new InvalidOperationException($"CatalogShell XAML name '{name}' was not found.");

    private void AttachAuthoredStyles(CatalogVisualResources resources)
    {
        var margins = new XamlProperty<Thickness>(nameof(Control.Margins), target => ((Control)target).Margins, (target, value) => ((Control)target).Margins = value);
        var normal = new Style(resources.ActionSelector);
        normal.Setters.Add(new StyleSetter<Thickness>(margins, resources.ActionMargins));
        var hover = new Style(resources.ActionHoverSelector);
        hover.Setters.Add(new StyleSetter<Thickness>(margins, resources.HoverMargins));
        var checkedStyle = new Style(resources.ToggleCheckedSelector);
        checkedStyle.Setters.Add(new StyleSetter<Thickness>(margins, resources.CheckedMargins));
        _authoredStyles = StyleEngine.Attach(this, new[] { normal, hover, checkedStyle });
    }

    private void BeginAuthoredStoryboard(CatalogVisualResources resources)
    {
        var property = new XamlProperty<Vector2>(nameof(Control.CustomMinimumSize), target => ((Control)target).CustomMinimumSize, (target, value) => ((Control)target).CustomMinimumSize = value);
        var timeline = new Vector2Timeline(resources.PulseTargetName, property);
        timeline.KeyFrames.Add(new KeyFrame<Vector2>(TimeSpan.Zero, resources.PulseFrom));
        timeline.KeyFrames.Add(new KeyFrame<Vector2>(resources.PulseDuration, resources.PulseTo, Easing.CubicInOut));
        var storyboard = new Storyboard { AutoReverse = true, RepeatBehavior = RepeatBehavior.ForeverValue };
        storyboard.Timelines.Add(timeline);
        _authoredStoryboard = storyboard.Begin(this);
    }

    private static void ClearChildren(Control control)
    {
        while (control.Children.Count > 0) control.RemoveChild(control.Children[control.Children.Count - 1]);
    }

}
