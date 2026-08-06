// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma
{
    /// <summary>Template-free extension point for custom backend-neutral retained drawing.</summary>
    public abstract class DrawingElement : Control
    {
        protected abstract void Draw(DrawingContext context);

        internal override void Draw(UIRenderContext context)
        {
            Draw(context.Drawing);
            base.Draw(context);
        }
    }
}