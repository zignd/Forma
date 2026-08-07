using Forma.QuickStart;

var maximumFrames = 0;
string? screenshotPath = null;
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
        default:
            throw new ArgumentException($"Unknown or invalid argument: {args[index]}");
    }
}

#if FORMA_QUICKSTART_FNA
Environment.SetEnvironmentVariable("FNA_GRAPHICS_ENABLE_HIGHDPI", "1");
#endif
using var game = new QuickStartGame(maximumFrames, screenshotPath);
game.Run();
