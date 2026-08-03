using Forma;
using Microsoft.Xna.Framework;

namespace Forma.Catalog;

public sealed class CatalogPreviewContainer : Container
{
    public override Vector2 GetMinimumSize()
    {
        var minimum = CustomMinimumSize;
        foreach (var child in Children)
            if (child.Visible) minimum = Vector2.Max(minimum, child.GetMinimumSize());
        return minimum;
    }

    protected override void ArrangeChildren()
    {
        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            var childSize = child.GetMinimumSize();
            var expandHorizontal = (child.HorizontalSizeFlags & SizeFlags.Expand) != 0;
            var expandVertical = (child.VerticalSizeFlags & SizeFlags.Expand) != 0;
            if (expandHorizontal) childSize.X = Size.X;
            if (expandVertical) childSize.Y = Size.Y;
            child.Size = Vector2.Max(Vector2.Zero, childSize);
            child.Position = new Vector2(
                expandHorizontal ? 0 : MathF.Floor((Size.X - childSize.X) / 2),
                expandVertical ? 0 : MathF.Floor((Size.Y - childSize.Y) / 2));
        }
    }
}