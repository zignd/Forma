// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;

Control[] controls =
[
    new Label { Text = "Analyzer consumer" },
    new Button { Text = "Continue" },
    new VideoStreamPlayer(),
];

var nativeDiagnostics = DynamicTextNativeDiagnostics.Current;
var svgHealth = SvgBackendDefaults.Verify();
Console.WriteLine($"{controls.Length} controls; {VideoStreamPlayer.RuntimeCapabilities}; {nativeDiagnostics.RuntimeIdentifier}; {svgHealth.Name} {svgHealth.Version}");