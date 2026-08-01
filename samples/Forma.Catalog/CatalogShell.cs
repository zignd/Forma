// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Forma;
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
    private readonly SpriteFont _font;
    private readonly LineEdit _search;
    private readonly ItemList _navigation;
    private readonly Label _storyTitle;
    private readonly Label _storyCategory;
    private readonly RichTextLabel _description;
    private readonly CenterContainer _preview;
    private readonly VBoxContainer _inspector;
    private readonly Label _count;
    private List<ComponentStory> _filteredStories;
    private ComponentStory _currentStory;
    private Control _currentControl;

    public CatalogShell(IReadOnlyList<ComponentStory> stories, SpriteFont font)
        : base(Orientation.Vertical)
    {
        _stories = stories;
        _font = font;
        Separation = 0;

        AddChild(BuildHeader());

        var body = new HBoxContainer { Separation = 0, VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand };
        var navigationPanel = CreatePanel(new Color(24, 29, 38), new Color(24, 29, 38));
        navigationPanel.CustomMinimumSize = new Vector2(340, 0);
        var navigationContent = new VBoxContainer { Separation = 8, Margins = new Thickness(14) };
        _search = new LineEdit { PlaceholderText = "Search components", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(0, 36) };
        _search.TextChanged += (_, _) => RefreshNavigation();
        var navigationScroll = new ScrollContainer { VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand, HorizontalScrollMode = ScrollBarVisibility.Never };
        _navigation = new ItemList { AllowSearch = true, AutoWidth = true, AutoHeight = true, HorizontalSizeFlags = SizeFlags.Fill | SizeFlags.Expand, VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand };
        _navigation.ItemSelected += (_, index) => SelectStory(index);
        _count = new Label { Font = font, FontColor = new Color(143, 153, 170), CustomMinimumSize = new Vector2(0, 22) };
        navigationScroll.AddChild(_navigation);
        navigationContent.AddChild(_search);
        navigationContent.AddChild(navigationScroll);
        navigationContent.AddChild(_count);
        navigationPanel.AddChild(navigationContent);

        var center = new VBoxContainer { Separation = 0, HorizontalSizeFlags = SizeFlags.Fill | SizeFlags.Expand };
        var storyHeader = new VBoxContainer { Separation = 4, Margins = new Thickness(24, 18, 24, 12), CustomMinimumSize = new Vector2(0, 106) };
        _storyCategory = new Label { Font = font, Uppercase = true, FontColor = new Color(48, 185, 164), CustomMinimumSize = new Vector2(0, 20) };
        _storyTitle = new Label { Font = font, CustomMinimumSize = new Vector2(0, 30) };
        _description = new RichTextLabel { Font = font, FitContent = false, AutowrapMode = LabelAutowrapMode.Word, ScrollActive = false, FontColor = new Color(174, 184, 200), CustomMinimumSize = new Vector2(0, 42) };
        storyHeader.AddChild(_storyCategory);
        storyHeader.AddChild(_storyTitle);
        storyHeader.AddChild(_description);
        center.AddChild(storyHeader);
        center.AddChild(new HSeparator());

        var previewMargin = new MarginContainer { ThemeOverrides = new Thickness(24), VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand };
        var previewPanel = CreatePanel(new Color(25, 30, 39), new Color(56, 66, 82));
        _preview = new CenterContainer();
        previewPanel.AddChild(_preview);
        previewMargin.AddChild(previewPanel);
        center.AddChild(previewMargin);

        var inspectorPanel = CreatePanel(new Color(24, 29, 38), new Color(24, 29, 38));
        inspectorPanel.CustomMinimumSize = new Vector2(320, 0);
        var inspectorColumn = new VBoxContainer { Separation = 8, Margins = new Thickness(16) };
        var inspectorTitle = new Label { Text = "PROPERTIES", Font = font, FontColor = new Color(246, 185, 73), CustomMinimumSize = new Vector2(0, 24) };
        var reset = new Button { Text = "Reset story", Font = font, CustomMinimumSize = new Vector2(0, 34) };
        reset.Pressed += (_, _) => LoadStory(_currentStory);
        var inspectorScroll = new ScrollContainer { VerticalSizeFlags = SizeFlags.Fill | SizeFlags.Expand, HorizontalScrollMode = ScrollBarVisibility.Never };
        _inspector = new VBoxContainer { Separation = 10, CustomMinimumSize = new Vector2(278, 0) };
        inspectorScroll.AddChild(_inspector);
        inspectorColumn.AddChild(inspectorTitle);
        inspectorColumn.AddChild(reset);
        inspectorColumn.AddChild(new HSeparator());
        inspectorColumn.AddChild(inspectorScroll);
        inspectorPanel.AddChild(inspectorColumn);

        body.AddChild(navigationPanel);
        body.AddChild(new VSeparator());
        body.AddChild(center);
        body.AddChild(new VSeparator());
        body.AddChild(inspectorPanel);
        AddChild(body);

        FontApplicator.Apply(this, font);
        RefreshNavigation();
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer { Separation = 12, CustomMinimumSize = new Vector2(0, 62), Margins = new Thickness(18, 12, 18, 10) };
        header.AddChild(new ColorRect { Color = new Color(48, 185, 164), CustomMinimumSize = new Vector2(6, 36), VerticalSizeFlags = SizeFlags.ShrinkCenter });
        header.AddChild(new Label { Text = "FORMA", Font = _font, CustomMinimumSize = new Vector2(178, 36), VerticalAlignment = VerticalAlignment.Center });
        header.AddChild(new Label { Text = "Component Catalog", Font = _font, FontColor = new Color(143, 153, 170), HorizontalSizeFlags = SizeFlags.Fill | SizeFlags.Expand, VerticalAlignment = VerticalAlignment.Center });
        header.AddChild(new Label { Text = $"LIVE {CatalogBackend.Name.ToUpperInvariant()}", Font = _font, FontColor = new Color(246, 185, 73), CustomMinimumSize = new Vector2(132, 36), VerticalAlignment = VerticalAlignment.Center });
        return header;
    }

    private void RefreshNavigation()
    {
        var query = _search?.Text?.Trim() ?? string.Empty;
        _filteredStories = _stories.Where(story =>
                string.IsNullOrEmpty(query) ||
                story.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                story.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _navigation.Clear();
        foreach (var story in _filteredStories) _navigation.AddItem($"{story.Category}  /  {story.Name}");
        _count.Text = $"{_filteredStories.Count} of {_stories.Count} controls";
        if (_filteredStories.Count > 0)
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

    private void LoadStory(ComponentStory story)
    {
        if (story == null) return;
        _currentStory = story;
        ClearChildren(_preview);
        ClearChildren(_inspector);
        _currentControl = story.Factory();
        FontApplicator.Apply(_currentControl, _font);
        _preview.AddChild(_currentControl);
        _storyCategory.Text = story.Category;
        _storyTitle.Text = story.Name;
        _description.ParseBbcode(story.Description);
        BuildInspector(_currentControl);
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
            _inspector.AddChild(new Label { Text = "No safe writable properties exposed.", Font = _font, AutowrapMode = LabelAutowrapMode.Word, CustomMinimumSize = new Vector2(270, 42) });
            return;
        }

        foreach (var property in properties)
        {
            var editor = CreatePropertyEditor(target, property);
            if (editor == null) continue;
            var section = new VBoxContainer { Separation = 4 };
            section.AddChild(new Label { Text = SplitName(property.Name), Font = _font, FontColor = new Color(174, 184, 200), CustomMinimumSize = new Vector2(0, 20) });
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
            var toggle = new CheckBox { Text = "Enabled", Font = _font, ButtonPressed = (bool)(ReadValue() ?? false), CustomMinimumSize = new Vector2(0, 30) };
            toggle.Toggled += (_, value) => SetValue(value);
            return toggle;
        }
        if (property.PropertyType == typeof(string))
        {
            var field = new LineEdit { Font = _font, Text = (string)ReadValue() ?? string.Empty, CustomMinimumSize = new Vector2(0, 32) };
            field.TextChanged += (_, value) => SetValue(value);
            return field;
        }
        if (property.PropertyType.IsEnum)
        {
            var option = new OptionButton { Font = _font, CustomMinimumSize = new Vector2(0, 32) };
            var values = Enum.GetValues(property.PropertyType);
            for (var index = 0; index < values.Length; index++) option.AddItem(values.GetValue(index).ToString(), index);
            var current = ReadValue();
            for (var index = 0; index < values.Length; index++) if (Equals(values.GetValue(index), current)) option.Select(index);
            option.ItemSelected += (_, index) => SetValue(values.GetValue(index));
            return option;
        }
        if (property.PropertyType == typeof(Color))
        {
            var picker = new ColorPickerButton { Font = _font, Color = (Color)(ReadValue() ?? Color.White), CustomMinimumSize = new Vector2(0, 34) };
            picker.ColorChanged += (_, value) => SetValue(value);
            return picker;
        }
        if (IsNumeric(property.PropertyType))
        {
            var value = Convert.ToSingle(ReadValue() ?? 0, CultureInfo.InvariantCulture);
            var maximum = Math.Max(100, MathF.Ceiling(Math.Abs(value) * 2 + 10));
            var row = new HBoxContainer { Separation = 8, CustomMinimumSize = new Vector2(0, 30) };
            var valueLabel = new Label { Text = value.ToString("0.##", CultureInfo.InvariantCulture), Font = _font, CustomMinimumSize = new Vector2(54, 28), VerticalAlignment = VerticalAlignment.Center };
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

    private static PanelContainer CreatePanel(Color background, Color border)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleOverride("panel", new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderWidth = 1,
            ContentMargin = new Thickness(0),
        });
        return panel;
    }

    private static void ClearChildren(Control control)
    {
        while (control.Children.Count > 0) control.RemoveChild(control.Children[control.Children.Count - 1]);
    }
}