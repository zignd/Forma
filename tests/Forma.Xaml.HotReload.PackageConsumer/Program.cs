// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma;
using Forma.Xaml.HotReload;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

var developmentRoot = Path.Combine(Path.GetTempPath(), $"forma-hot-reload-consumer-{Guid.NewGuid():N}");
Directory.CreateDirectory(developmentRoot);
try
{
    File.WriteAllText(
        Path.Combine(developmentRoot, "View.xaml"),
        "<Control xmlns='https://forma.dev/xaml' Name='Reloaded' />");

    using var context = new UIContext();
    Control current = new Control { Name = "Initial" };
    context.Add(current);
    using var service = new FormaXamlHotReloadService(context, developmentRoot, watchFiles: false);
    using var registration = service.Register("View.xaml", () => current, (oldValue, replacement) =>
    {
        context.Remove(oldValue);
        context.Add(replacement);
        current = replacement;
    });

    service.RequestReloadAsync("View.xaml").GetAwaiter().GetResult();
    context.Update(new GameTime(), new MouseState(), new KeyboardState());
    if (current.Name != "Reloaded")
        throw new InvalidOperationException("Packaged XAML hot reload did not compile and replace the view.");
}
finally
{
    Directory.Delete(developmentRoot, recursive: true);
}
