// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Xaml.Build.TemplateInvalid;

public sealed class TemplateInvalidView : Control;

public sealed class TemplateFoundation : Control;

public sealed class TemplateEventControl : Control
{
    public event EventHandler? Triggered
    {
        add { }
        remove { }
    }
}