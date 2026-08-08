// SPDX-License-Identifier: MIT

using System.Globalization;
using Forma;
using Forma.QuickStart;

var maximumFrames = 0;
string? screenshotPath = null;
var viewKind = QuickStartViewKind.CSharp;
var displayScale = 1f;
for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--frames" when index + 1 < args.Length && int.TryParse(args[++index], out var frames) && frames > 0:
            maximumFrames = frames;
            break;
        case "--screenshot" when index + 1 < args.Length:
            screenshotPath = args[++index];
            break;
        case "--xaml":
            viewKind = QuickStartViewKind.Xaml;
            break;
        case "--settings-form":
            viewKind = QuickStartViewKind.SettingsForm;
            break;
        case "--responsive-hud":
            viewKind = QuickStartViewKind.ResponsiveHud;
            break;
        case "--inventory-list":
            viewKind = QuickStartViewKind.InventoryList;
            break;
        case "--dialog-workflow":
            viewKind = QuickStartViewKind.DialogWorkflow;
            break;
        case "--data-grid":
            viewKind = QuickStartViewKind.DataGrid;
            break;
        case "--theme-control":
            viewKind = QuickStartViewKind.ThemeControl;
            break;
        case "--dynamic-text":
            viewKind = QuickStartViewKind.DynamicText;
            break;
        case "--runtime-svg":
            viewKind = QuickStartViewKind.RuntimeSvg;
            break;
        case "--display-scale" when index + 1 < args.Length
            && float.TryParse(args[++index], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
            && float.IsFinite(scale)
            && scale > 0:
            displayScale = scale;
            break;
        default:
            throw new ArgumentException($"Unknown or invalid argument: {args[index]}");
    }
}

#if FORMA_QUICKSTART_FNA
Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
#endif
if (viewKind == QuickStartViewKind.RuntimeSvg) _ = SvgSkiaBackendDefaults.Verify();
using var game = new QuickStartGame(maximumFrames, screenshotPath, viewKind, displayScale);
game.Run();
