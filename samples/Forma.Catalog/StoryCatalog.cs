// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Forma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Forma.Catalog;

public sealed class ComponentStory
{
    public ComponentStory(string category, string name, string description, Func<Control> factory)
    {
        Category = category;
        Name = name;
        Description = description;
        Factory = factory;
    }

    public string Category { get; }
    public string Name { get; }
    public string Description { get; }
    public Func<Control> Factory { get; }
}

public static class StoryCatalog
{
    public static IReadOnlyList<ComponentStory> Create(Texture2D texture)
    {
        var stories = new List<ComponentStory>();
        var controlType = typeof(Control);
        var explicitFactories = new Dictionary<Type, Func<Control>>
        {
            [typeof(BoxContainer)] = () => new BoxContainer(Orientation.Horizontal),
            [typeof(Slider)] = () => new Slider(Orientation.Horizontal),
            [typeof(ScrollBar)] = () => new ScrollBar(Orientation.Horizontal),
            [typeof(SplitContainer)] = () => new SplitContainer(Orientation.Horizontal),
            [typeof(FlowContainer)] = () => new FlowContainer(Orientation.Horizontal),
        };
        foreach (var type in new[] { controlType.Assembly, typeof(VideoStreamPlayer).Assembly }
                     .SelectMany(assembly => assembly.GetTypes())
                     .Where(type => type.IsPublic && !type.IsAbstract && controlType.IsAssignableFrom(type))
                     .Where(type => type.GetConstructor(Type.EmptyTypes) != null || explicitFactories.ContainsKey(type))
                     .OrderBy(type => GetCategory(type))
                     .ThenBy(type => type.Name))
        {
            stories.Add(new ComponentStory(
                GetCategory(type),
                type.Name,
                type == typeof(VideoStreamPlayer)
                    ? "Optional [color=#30b9a4]Forma.Media[/color] video control configured for autoplay, looping, and responsive expansion. Assign a content-loaded Video to begin playback."
                    : $"Interactive example of [color=#30b9a4]{type.FullName}[/color]. Use the property inspector to change its public writable values at runtime.",
                () => CreateExample(type, texture, explicitFactories)));
        }
        return stories;
    }

    private static Control CreateExample(Type type, Texture2D texture, IReadOnlyDictionary<Type, Func<Control>> explicitFactories)
    {
        var control = explicitFactories.TryGetValue(type, out var factory) ? factory() : (Control)Activator.CreateInstance(type);
        control.Name = type.Name;
        control.TooltipText = type.FullName;
        control.CustomMinimumSize = IsLargeSurface(type) ? new Vector2(560, 360) : new Vector2(300, 64);
        SeedExample(control, type.Name, texture);
        return control;
    }

    private static void SeedExample(Control control, string name, Texture2D texture)
    {
        if (control is VideoStreamPlayer videoPlayer)
        {
            videoPlayer.Autoplay = true;
            videoPlayer.Loop = true;
            videoPlayer.Expand = true;
            videoPlayer.Volume = .75f;
            videoPlayer.AddChild(new ColorRect
            {
                Size = new Vector2(560, 315),
                Color = new Color(16, 20, 27),
            });
            videoPlayer.AddChild(new Label
            {
                Position = new Vector2(180, 124),
                Size = new Vector2(200, 68),
                Text = "VIDEO\nNO STREAM",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }
        if (control is Popup embeddedPopup)
        {
            embeddedPopup.Modal = false;
            embeddedPopup.HideOnOutsideClick = false;
            embeddedPopup.Visible = true;
        }
        if (control is FileDialog fileDialog)
        {
            fileDialog.Title = "Open project asset";
            fileDialog.DialogText = "Select a file from the project";
            fileDialog.AddFilter("*.cs;*.png;*.json");
            fileDialog.Visible = true;
            return;
        }
        if (control is AcceptDialog dialog)
        {
            dialog.Title = name;
            dialog.DialogText = control is ConfirmationDialog ? "Continue with this action?" : "This is a modal message.";
            dialog.Visible = true;
            return;
        }
        if (control is PopupMenu popupMenu)
        {
            popupMenu.AddItem("New scene", 1);
            popupMenu.AddCheckItem("Show grid", 2);
            popupMenu.SetItemChecked(1, true);
            popupMenu.AddSeparator();
            popupMenu.AddItem("Close", 3);
            popupMenu.Visible = true;
            return;
        }
        if (control is MenuBar menuBar)
        {
            var file = menuBar.AddMenu("File");
            file.Menu.AddItem("New");
            file.Menu.AddItem("Open");
            file.Menu.AddSeparator();
            file.Menu.AddItem("Save");
            var edit = menuBar.AddMenu("Edit");
            edit.Menu.AddItem("Undo");
            edit.Menu.AddItem("Redo");
            return;
        }
        if (control is MenuButton menuButton)
        {
            menuButton.Text = "Build";
            menuButton.Menu.AddItem("Build solution");
            menuButton.Menu.AddItem("Rebuild");
            menuButton.Menu.AddItem("Clean");
            return;
        }
        if (control is OptionButton optionButton)
        {
            optionButton.AddItem("DesktopGL");
            optionButton.AddItem("WindowsDX");
            optionButton.AddItem("Android");
            optionButton.Select(0);
            return;
        }
        if (control is TabContainer tabContainer)
        {
            tabContainer.AddChild(CreatePage("Scene", new Color(37, 70, 70)));
            tabContainer.AddChild(CreatePage("Inspector", new Color(64, 55, 37)));
            tabContainer.AddChild(CreatePage("Output", new Color(43, 49, 65)));
            return;
        }
        if (control is TabBar tabBar)
        {
            tabBar.AddTab("Scene");
            tabBar.AddTab("Inspector");
            tabBar.AddTab("Output");
            tabBar.AddTab("Debugger");
            tabBar.CloseDisplayPolicy = TabCloseDisplayPolicy.ActiveOnly;
            return;
        }
        if (control is Tree tree)
        {
            tree.Columns = 2;
            tree.ColumnTitlesVisible = true;
            tree.SetColumnTitle(0, "Node");
            tree.SetColumnTitle(1, "Visible");
            var root = tree.CreateItem();
            root.SetText(0, "World");
            var player = root.CreateChild();
            player.SetText(0, "Player");
            player.SetCellMode(1, TreeCellMode.Check);
            player.SetChecked(1, true);
            var camera = player.CreateChild();
            camera.SetText(0, "Camera2D");
            camera.SetCellMode(1, TreeCellMode.Check);
            camera.SetChecked(1, true);
            return;
        }
        if (control is ItemList itemList)
        {
            itemList.AddItem("Player.tscn", texture);
            itemList.AddItem("World.cs", texture);
            itemList.AddItem("palette.png", texture);
            itemList.Select(0);
            itemList.MaxColumns = 2;
            return;
        }
        if (control is GraphEdit graphEdit)
        {
            var source = new GraphNode { Name = "Input", Title = "Input", Position = new Vector2(32, 54), Size = new Vector2(150, 84) };
            source.AddOutputPort("value", 1, new Color(48, 185, 164));
            var output = new GraphNode { Name = "Output", Title = "Output", Position = new Vector2(310, 180), Size = new Vector2(150, 84) };
            output.AddInputPort("value", 1, new Color(48, 185, 164));
            graphEdit.AddChild(source);
            graphEdit.AddChild(output);
            graphEdit.ConnectNode("Input", 0, "Output", 0);
            return;
        }
        if (control is GraphNode graphNode)
        {
            graphNode.Title = name;
            graphNode.AddInputPort("input", 1, new Color(246, 185, 73));
            graphNode.AddOutputPort("result", 1, new Color(48, 185, 164));
            graphNode.AddChild(new Label { Text = "Process value", CustomMinimumSize = new Vector2(180, 32) });
            return;
        }
        if (control is CodeEdit codeEdit)
        {
            codeEdit.Text = "using Forma;\n\nvar button = new Button\n{\n    Text = \"Run game\",\n};";
            codeEdit.DrawLineNumbers = true;
            codeEdit.DrawMinimap = true;
            codeEdit.SetLineWrappingMode(TextEditLineWrappingMode.Boundary);
            return;
        }
        if (control is RichTextLabel richText)
        {
            richText.AppendBbcode("[color=#30b9a4][b]Forma[/b][/color][br]Rich text supports [i]formatting[/i], links, lists, tables, and selection.");
            richText.SelectionEnabled = true;
            richText.ScrollActive = true;
            return;
        }
        if (control is TextEdit textEdit)
        {
            textEdit.Text = "A multiline editor built with Forma.\n\nSelect text, move the caret, and edit this document.";
            textEdit.SetLineWrappingMode(TextEditLineWrappingMode.Boundary);
            return;
        }
        if (control is SpinBox spinBox)
        {
            spinBox.MinValue = 8;
            spinBox.MaxValue = 96;
            spinBox.Value = 24;
            spinBox.Step = 1;
            spinBox.Prefix = "Font size: ";
            return;
        }
        if (control is LineEdit lineEdit)
        {
            lineEdit.Text = "Editable component value";
            lineEdit.PlaceholderText = "Type here";
            lineEdit.ClearButtonEnabled = true;
            return;
        }
        if (control is ColorPicker colorPicker)
        {
            colorPicker.Color = new Color(48, 185, 164);
            colorPicker.AddPreset(new Color(246, 185, 73));
            colorPicker.AddPreset(new Color(91, 126, 246));
            return;
        }
        if (control is ColorPickerButton colorPickerButton)
        {
            colorPickerButton.Color = new Color(48, 185, 164);
            colorPickerButton.Picker.AddPreset(new Color(246, 185, 73));
            return;
        }
        if (control is ColorPresetButton presetButton)
        {
            presetButton.Color = new Color(246, 185, 73);
            return;
        }
        if (control is TextureProgressBar textureProgress)
        {
            textureProgress.Under = texture;
            textureProgress.Progress = texture;
            textureProgress.Value = 68;
            textureProgress.TintUnder = new Color(80, 86, 98);
            textureProgress.TintProgress = new Color(48, 185, 164);
            return;
        }
        if (control is TextureButton textureButton)
        {
            textureButton.TextureNormal = texture;
            textureButton.StretchMode = TextureButtonStretchMode.KeepAspectCentered;
            return;
        }
        if (control is NinePatchRect ninePatch)
        {
            ninePatch.Texture = texture;
            ninePatch.PatchMargin = new Thickness(8);
            return;
        }
        if (control is TextureRect textureRect)
        {
            textureRect.Texture = texture;
            textureRect.StretchMode = TextureStretchMode.Tile;
            textureRect.ExpandMode = TextureRectExpandMode.IgnoreSize;
            return;
        }
        if (control is SubViewportContainer subViewport)
        {
            subViewport.Stretch = true;
            subViewport.StretchShrink = 2;
            subViewport.ViewportClearColor = new Color(17, 21, 27);
            subViewport.ViewportContext.Add(new ColorRect { Position = new Vector2(16, 16), Size = new Vector2(180, 100), Color = new Color(48, 185, 164) });
            return;
        }
        if (control is VirtualJoystick joystick)
        {
            joystick.CustomMinimumSize = new Vector2(180, 180);
            joystick.BackgroundColor = new Color(43, 52, 66);
            joystick.KnobColor = new Color(48, 185, 164);
            return;
        }
        if (control is FoldableContainer foldable)
        {
            foldable.Title = "Transform";
            foldable.AddChild(new Label { Text = "Position   120, 64", CustomMinimumSize = new Vector2(280, 30) });
            foldable.AddChild(new Label { Text = "Rotation   0 degrees", CustomMinimumSize = new Vector2(280, 30) });
            return;
        }
        if (control is ScrollContainer scroll)
        {
            var content = new VBoxContainer { Separation = 6, CustomMinimumSize = new Vector2(500, 600) };
            for (var index = 1; index <= 14; index++) content.AddChild(new Button { Text = $"Scrollable row {index}", CustomMinimumSize = new Vector2(480, 32) });
            scroll.AddChild(content);
            return;
        }
        if (control is AspectRatioContainer aspectRatio)
        {
            aspectRatio.Ratio = 16f / 9;
            aspectRatio.AddChild(new ColorRect { Color = new Color(48, 185, 164), CustomMinimumSize = new Vector2(240, 135), HorizontalSizeFlags = SizeFlags.Fill, VerticalSizeFlags = SizeFlags.Fill });
            return;
        }
        if (control is SplitContainer split)
        {
            split.AddChild(CreatePage("Primary pane", new Color(37, 70, 70)));
            split.AddChild(CreatePage("Secondary pane", new Color(64, 55, 37)));
            split.SplitOffset = 260;
            return;
        }
        if (control is FlowContainer flow)
        {
            for (var index = 1; index <= 9; index++) flow.AddChild(new Button { Text = $"Item {index}", CustomMinimumSize = new Vector2(92, 34) });
            return;
        }
        if (control is GridContainer grid)
        {
            grid.Columns = 3;
            for (var index = 1; index <= 9; index++) grid.AddChild(new Button { Text = index.ToString(CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(84, 44) });
            return;
        }
        if (control is BoxContainer box)
        {
            box.Separation = 8;
            box.AddChild(new Button { Text = "One", CustomMinimumSize = new Vector2(96, 38) });
            box.AddChild(new Button { Text = "Two", CustomMinimumSize = new Vector2(96, 38) });
            box.AddChild(new Button { Text = "Three", CustomMinimumSize = new Vector2(96, 38) });
            return;
        }
        if (control is Container container)
        {
            container.AddChild(new ColorRect { Position = new Vector2(24, 24), Size = new Vector2(180, 80), Color = new Color(48, 185, 164) });
            container.AddChild(new Label { Position = new Vector2(42, 50), Size = new Vector2(140, 28), Text = name });
            return;
        }
        if (control is Label label)
        {
            label.Text = $"{name}\nForma";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AutowrapMode = LabelAutowrapMode.Word;
            return;
        }
        if (control is BaseButton button)
        {
            button.Text = name;
            if (control is CheckBox checkBox) checkBox.ButtonPressed = true;
            return;
        }
        if (control is ProgressBar progressBar)
        {
            progressBar.Value = 62;
            return;
        }
        if (control is Slider slider)
        {
            slider.Value = 62;
            slider.TickCount = 6;
            return;
        }
        if (control is Forma.Range range)
        {
            range.Value = 62;
            return;
        }
        if (control is ColorRect colorRect)
        {
            colorRect.Color = new Color(48, 185, 164);
            return;
        }
        if (control is ReferenceRect referenceRect)
        {
            referenceRect.BorderColor = new Color(246, 185, 73);
            referenceRect.BorderWidth = 3;
            return;
        }
        if (control is Panel panel) panel.BackgroundColor = new Color(43, 52, 66);
    }

    private static Panel CreatePage(string name, Color color)
    {
        var page = new Panel { Name = name, BackgroundColor = color, CustomMinimumSize = new Vector2(240, 120) };
        page.AddChild(new Label { Text = name, Position = new Vector2(18, 18), Size = new Vector2(180, 28) });
        return page;
    }

    private static bool IsLargeSurface(Type type) =>
        typeof(Container).IsAssignableFrom(type) ||
        type == typeof(Tree) || type == typeof(ItemList) || type == typeof(TextEdit) ||
        type == typeof(CodeEdit) || type == typeof(RichTextLabel) || type == typeof(GraphEdit) ||
        type == typeof(ColorPicker) || type == typeof(VideoStreamPlayer) ||
        typeof(Popup).IsAssignableFrom(type);

    private static string GetCategory(Type type)
    {
        if (type == typeof(VideoStreamPlayer)) return "Media";
        if (typeof(Popup).IsAssignableFrom(type) || type.Name.Contains("Dialog", StringComparison.Ordinal)) return "Overlays";
        if (type.Name.Contains("Text", StringComparison.Ordinal) || type == typeof(Label) || type == typeof(LineEdit) || type == typeof(CodeEdit)) return "Text";
        if (type.Name.Contains("Graph", StringComparison.Ordinal) || type == typeof(Tree) || type == typeof(ItemList) || type.Name.Contains("Tab", StringComparison.Ordinal) || type.Name.Contains("Menu", StringComparison.Ordinal)) return "Data & navigation";
        if (type.Name.Contains("Texture", StringComparison.Ordinal) || type == typeof(ColorRect) || type.Name.Contains("Color", StringComparison.Ordinal)) return "Visuals";
        if (typeof(BaseButton).IsAssignableFrom(type) || typeof(Forma.Range).IsAssignableFrom(type)) return "Inputs";
        if (typeof(Container).IsAssignableFrom(type)) return "Layout";
        return "Specialized";
    }
}

public static class FontApplicator
{
    public static void Apply(Control control, SpriteFont font, SpriteFont codeFont)
    {
        switch (control)
        {
            case CodeEdit codeEdit: codeEdit.Font = codeFont; break;
            case TextEdit textEdit: textEdit.Font = font; break;
            case Label label: label.Font = font; break;
            case MenuButton menuButton: menuButton.Font = font; Apply(menuButton.Menu, font, codeFont); break;
            case BaseButton button: button.Font = font; break;
            case PopupMenu popupMenu: popupMenu.Font = font; break;
            case LineEdit lineEdit: lineEdit.Font = font; break;
            case SpinBox spinBox: spinBox.Font = font; break;
            case ProgressBar progressBar: progressBar.Font = font; break;
            case AcceptDialog dialog: dialog.Font = font; break;
            case Tree tree: tree.Font = font; break;
            case TabContainer tabs: tabs.Font = font; break;
            case GraphNode graphNode: graphNode.Font = font; break;
            case ItemList itemList: itemList.Font = font; break;
            case TabBar tabBar: tabBar.Font = font; break;
        }
        foreach (var child in control.Children) Apply(child, font, codeFont);
    }
}