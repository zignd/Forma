# Migrating from the MonoGame Fork

Forma replaces the UI source previously compiled into the zignd MonoGame fork. The current migration
is an assembly and namespace move, not an API redesign.

## Install

Forma `0.1.0-alpha.1` targets .NET 10 and compiles against MonoGame 3.8.5. After configuring the
package source that contains Forma, add Forma and exactly one application backend:

```sh
dotnet add package Forma --version 0.1.0-alpha.1
dotnet add package MonoGame.Framework.DesktopGL --version 3.8.5
```

Add the optional media package when migrating `VideoStreamPlayer`:

```sh
dotnet add package Forma.Media --version 0.1.0-alpha.1
```

Remove direct compilation of `MonoGame.Framework/UI/*.cs` from the consuming project. Keep game
content in the game project; Forma's core package contains no catalog fonts or native backend files.

Forma and Forma.Media compile against the published `MonoGame.Framework.DesktopGL`,
`MonoGame.Framework.WindowsDX`, and `MonoGame.Framework.Native` 3.8.5 reference surfaces. Use
WindowsDX from a Windows-targeted application and Native only with its required native runtime
components. The Forma packages deliberately keep their compile-time backend references private so
they do not pull a conflicting backend into an application.

Validate all three reference configurations with:

```sh
bash scripts/check-backend-references.sh
```

For coordinated source development against a local MonoGame project, build Forma with:

```sh
dotnet build Forma.slnx \
  -p:MonoGameProjectPath=../MonoGame/MonoGame.Framework/MonoGame.Framework.DesktopGL.csproj
```

## Namespace

Replace the old namespace import:

```csharp
using Microsoft.Xna.Framework.UI;
```

with:

```csharp
using Forma;
```

For every public type in the inventory below, the mapping is exactly:

```text
Microsoft.Xna.Framework.UI.<TypeName> -> Forma.<TypeName>
```

No compatibility assembly keeps types under `Microsoft.Xna.Framework.UI`. Fully qualified names,
reflection strings, serializers, and generated code must be updated along with source imports.

## Type Inventory

The retained source snapshot and Forma each declare these 185 public types. This list includes the
source-gated media type described below.

```text
AcceptDialog
AspectRatioAlignment
AspectRatioContainer
AspectRatioMode
AutoTranslateMode
BaseButton
BoxAlignment
BoxContainer
Button
ButtonActionMode
ButtonGroup
ButtonMouseMask
CenterContainer
CheckBox
CheckButton
CodeCompletionKind
CodeCompletionOption
CodeEdit
CodeHighlightColorRegion
CodeHighlighter
ColorPicker
ColorPickerButton
ColorPickerDialog
ColorPickerMode
ColorPickerPopupPanel
ColorPickerShape
ColorPresetButton
ColorRect
ConfirmationDialog
Container
Control
FileDialog
FileDialogAccess
FileDialogCustomization
FileDialogDisplayMode
FileDialogMode
FileDialogOption
FileDialogSortOption
FlowAlignment
FlowContainer
FlowLastWrapAlignment
FocusMode
FoldableContainer
FoldableGroup
GraphConnection
GraphEdit
GraphEditArranger
GraphEditFilter
GraphEditGridPattern
GraphEditMinimap
GraphEditPanningScheme
GraphElement
GraphFrame
GraphNode
GridContainer
GrowDirection
HBoxContainer
HFlowContainer
HorizontalAlignment
HScrollBar
HSeparator
HSlider
HSplitContainer
ItemList
ItemListEntry
ItemListIconMode
ItemListScrollHintMode
ItemListSelectionMode
Label
LabelAutowrapMode
LabelJustificationFlags
LabelTextOverrunBehavior
LabelVisibleCharactersBehavior
LayoutDirection
LineEdit
LineEditMenuOption
LinkButton
LinkButtonUnderlineMode
MarginContainer
MenuBar
MenuButton
MouseFilter
NinePatchAxisStretchMode
NinePatchRect
OptionButton
OptionButtonItem
Orientation
Panel
PanelContainer
PointerButton
Popup
PopupHideReason
PopupMenu
PopupMenuCheckableType
PopupMenuItem
PopupMenuItemKind
PopupMenuItems
PopupMenuShortcut
PopupPanel
PopupSystemMenu
ProgressBar
ProgressBarFillMode
Range
RectangleF
ReferenceRect
RichTextDocument
RichTextEffect
RichTextLabel
RichTextListType
RichTextMetaRegion
RichTextSelectionMode
RichTextSpan
ScrollBar
ScrollBarVisibility
ScrollContainer
ScrollContainerScrollHintMode
Separator
Side
SizeFlags
Slider
SliderTickPosition
SpinBox
SpinBoxLineEdit
SplitContainer
SplitContainerDragger
SplitContainerDraggerVisibility
SplitContainerMultiDragger
StructuredTextParser
StyleBox
StyleBoxEmpty
StyleBoxFlat
StyleBoxTexture
SubViewportContainer
SyntaxHighlighter
SyntaxHighlightSpan
TabBar
TabBarAlignment
TabBarItem
TabBarSizingMode
TabCloseDisplayPolicy
TabContainer
TextDirection
TextEdit
TextEditCaret
TextEditEditAction
TextEditGutter
TextEditGutterType
TextEditLineWrappingMode
TextEditMenuOption
TextSearchFlags
TextureButton
TextureButtonClickMask
TextureButtonLayout
TextureButtonStretchMode
TextureProgressBar
TextureProgressFillMode
TextureProgressNinePatchRegion
TextureProgressRegion
TextureRect
TextureRectExpandMode
TextureRectLayout
TextureStretchMode
Theme
Thickness
Tree
TreeCellMode
TreeDropModeFlags
TreeItem
TreeItemButton
TreeItemCustomDrawCallback
TreeScrollHintMode
TreeSelectMode
UIComponent
UIContext
UIRenderContext
VBoxContainer
VerticalAlignment
VFlowContainer
VideoStreamPlayer
ViewPanner
VirtualJoystick
VScrollBar
VSeparator
VSlider
VSplitContainer
```

## Optional Media

`VideoStreamPlayer` retains its `Forma.VideoStreamPlayer` type name in the optional `Forma.Media`
assembly rather than the stock-compatible core assembly. The package's default backend uses public
stock MonoGame video APIs. At runtime it detects the fork-only public
`VideoPlayer.SetPlayPosition(TimeSpan)` method and enables seeking when available; on stock MonoGame,
seeking is unsupported without affecting playback.

Applications with another playback implementation can inject `IVideoPlaybackBackend` through the
additional `VideoStreamPlayer(IVideoPlaybackBackend)` constructor. The core `Forma` package contains
no media API, compile-time symbols, or fork-only references.

## Licensing Boundary

Forma is independent from Microsoft, the MonoGame Foundation, and the Godot project, and none of
those projects endorses it. MonoGame remains a separately licensed dependency. Godot-adapted
behavior and all other third-party material retain their notices in `THIRD-PARTY-NOTICES.md` and
`docs/provenance.md`.