// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Forma
{
    internal sealed class DefaultThemeSvgSourceProvider : IDefaultThemeSvgSourceProvider
    {
        internal static readonly DefaultThemeSvgSourceProvider Instance = new DefaultThemeSvgSourceProvider();
        private readonly Dictionary<string, SvgImageSource> _sources = new Dictionary<string, SvgImageSource>(StringComparer.Ordinal);

        public SvgImageSource GetSource(string name)
        {
            lock (_sources)
            {
                if (_sources.TryGetValue(name, out var source)) return source;
                source = SvgImageSource.FromManifestResource(typeof(DefaultThemeSvgSourceProvider).Assembly, $"Forma.ThemeIcons.Svg.{name}.svg");
                _sources.Add(name, source);
                return source;
            }
        }
    }
}