using Forma;
using Microsoft.Xna.Framework;

namespace Forma.Catalog;

public sealed class CatalogVisualResources : Control
{
    public Color PanelBackground { get; set; }
    public Color PreviewBackground { get; set; }
    public Color PreviewBorder { get; set; }
    public Color AccentColor { get; set; }
    public Color MutedTextColor { get; set; }
    public Color WarningColor { get; set; }
    public string ActionSelector { get; set; } = string.Empty;
    public string ActionHoverSelector { get; set; } = string.Empty;
    public string ToggleCheckedSelector { get; set; } = string.Empty;
    public Thickness ActionMargins { get; set; }
    public Thickness HoverMargins { get; set; }
    public Thickness CheckedMargins { get; set; }
    public string PulseTargetName { get; set; } = string.Empty;
    public Vector2 PulseFrom { get; set; }
    public Vector2 PulseTo { get; set; }
    public TimeSpan PulseDuration { get; set; }
}