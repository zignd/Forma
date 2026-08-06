// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml;

namespace Forma.Xaml.Build.Integration.Views;

public sealed class SvgAssetView : Control
{
    public SvgAssetView() => FormaXamlLoader.Load(this);
}
