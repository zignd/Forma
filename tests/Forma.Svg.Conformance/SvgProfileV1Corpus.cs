// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

namespace Forma.Tests
{
    internal readonly record struct SvgProfileFixture(string Name, string Svg);

    internal static class SvgProfileV1Corpus
    {
        internal static IReadOnlyList<SvgProfileFixture> All { get; } = new SvgProfileFixture[]
        {
            new("path", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><path d='M0 0h8v8H0z' fill='#f00'/></svg>"),
            new("linear-gradient", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><linearGradient id='g'><stop stop-color='#f00'/><stop offset='1' stop-color='#00f'/></linearGradient></defs><rect width='8' height='8' fill='url(#g)'/></svg>"),
            new("clip", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><clipPath id='c'><circle cx='4' cy='4' r='3'/></clipPath></defs><rect width='8' height='8' clip-path='url(#c)' fill='#0f0'/></svg>"),
            new("transform", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='4' height='4' transform='translate(2 2) rotate(15 2 2)' fill='#00f'/></svg>"),
            new("local-use", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><path id='p' d='M1 1h6v6H1z'/></defs><use href='#p' fill='#fc0'/></svg>"),
            new("current-color", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8' color='#40c080'><rect width='8' height='8' fill='currentColor'/></svg>"),
            new("shapes", "<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><g fill='#f80' stroke='#048' stroke-width='1' opacity='.8'><rect x='1' y='1' width='4' height='4' rx='1'/><circle cx='9' cy='3' r='2'/><ellipse cx='13' cy='3' rx='2' ry='1'/><line x1='1' y1='8' x2='5' y2='8'/><polyline points='7,9 9,7 11,9'/><polygon points='12,9 14,7 15,9'/></g></svg>"),
            new("styles-and-dashes", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><style>.paint{fill:#2ac;stroke:#fff;stroke-width:1;stroke-linecap:round;stroke-linejoin:bevel;stroke-dasharray:2 1}</style><path class='paint' d='M1 7V1H7' fill-rule='evenodd'/></svg>"),
            new("stroke-opacity", "<svg xmlns='http://www.w3.org/2000/svg' width='12' height='12'><path d='M1 10L6 1L11 10Z' fill='#f00' fill-opacity='.5' stroke='#00f' stroke-opacity='.75' stroke-width='2' stroke-linecap='square' stroke-linejoin='miter' stroke-miterlimit='4' stroke-dasharray='3 1'/></svg>"),
            new("nested-transforms", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='4' height='4' transform='translate(2 2) scale(.8) skewX(10) skewY(5) matrix(1 0 0 1 0 0)' fill='#c0f'/></svg>"),
            new("radial-gradient", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><radialGradient id='g' spreadMethod='reflect' gradientTransform='scale(.8)'><stop stop-color='#fff' stop-opacity='.9'/><stop offset='1' stop-color='#000'/></radialGradient></defs><circle cx='4' cy='4' r='4' fill='url(#g)'/></svg>"),
            new("mask", "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><mask id='m'><rect width='4' height='8' fill='#fff'/></mask></defs><rect width='8' height='8' fill='#0cf' mask='url(#m)'/></svg>"),
            new("view-box-meet", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 5' preserveAspectRatio='xMidYMid meet'><rect width='10' height='5' fill='#fff'/></svg>"),
            new("view-box-slice", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 5' preserveAspectRatio='xMidYMid slice'><rect width='10' height='5' fill='#fff'/></svg>"),
            new("view-box-none", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10' viewBox='0 0 10 5' preserveAspectRatio='none'><rect width='10' height='5' fill='#fff'/></svg>"),
        };
    }
}