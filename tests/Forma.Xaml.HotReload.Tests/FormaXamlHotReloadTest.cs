using Forma.Xaml.HotReload;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Forma.Xaml.HotReload.Tests;

public class FormaXamlHotReloadTest
{
    private string _directory = null!;
    [SetUp] public void SetUp() { _directory = Path.Combine(Path.GetTempPath(), $"forma-hotreload-{Guid.NewGuid():N}"); Directory.CreateDirectory(_directory); }
    [TearDown] public void TearDown() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    [Test]
    public async Task Reload_AppliesAtFrameBoundaryAndPreservesDataContext()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "View.xaml"), "<Control xmlns='https://forma.dev/xaml' Name='New'><Control Name='Child' /></Control>");
        using var context = new UIContext();
        var model = new object();
        Control current = new Control { Name = "Old", DataContext = model };
        context.Add(current);
        using var service = new FormaXamlHotReloadService(context, _directory, watchFiles: false);
        var callbackThread = -1;
        using var registration = service.Register("View.xaml", () => current, (oldValue, newValue) =>
        {
            callbackThread = Environment.CurrentManagedThreadId;
            context.Remove(oldValue);
            context.Add(newValue);
            current = newValue;
        });

        await service.RequestReloadAsync("View.xaml");
        Assert.That(current.Name, Is.EqualTo("Old"));
        var updateThread = Environment.CurrentManagedThreadId;
        context.Update(new GameTime(), new MouseState(), new KeyboardState());
        Assert.Multiple(() =>
        {
            Assert.That(current.Name, Is.EqualTo("New"));
            Assert.That(current.DataContext, Is.SameAs(model));
            Assert.That(NameScope.GetNameScope(current)?.Find<Control>("Child"), Is.SameAs(current.Children[0]));
            Assert.That(callbackThread, Is.EqualTo(updateThread));
        });
    }

    [Test]
    public async Task InvalidThenValidReload_LeavesTreeAndRecovers()
    {
        var file = Path.Combine(_directory, "View.xaml");
        await File.WriteAllTextAsync(file, "<Control xmlns='https://forma.dev/xaml'>");
        using var context = new UIContext();
        Control current = new Control { Name = "Old" };
        context.Add(current);
        using var service = new FormaXamlHotReloadService(context, _directory, watchFiles: false);
        var diagnostics = new List<IReadOnlyList<Forma.Xaml.Compiler.FormaDiagnostic>>();
        service.DiagnosticsChanged += value => diagnostics.Add(value);
        using var registration = service.Register("View.xaml", () => current, (oldValue, newValue) => current = newValue);

        await service.RequestReloadAsync("View.xaml");
        context.Update(new GameTime(), new MouseState(), new KeyboardState());
        Assert.That(current.Name, Is.EqualTo("Old"));
        Assert.That(diagnostics.Last(), Is.Not.Empty);
        await File.WriteAllTextAsync(file, "<Control xmlns='https://forma.dev/xaml' Name='Recovered' />");
        await service.RequestReloadAsync("View.xaml");
        context.Update(new GameTime(), new MouseState(), new KeyboardState());
        Assert.That(current.Name, Is.EqualTo("Recovered"));
        Assert.That(diagnostics.Last(), Is.Empty);
    }

    [Test]
    public async Task StaleRootAndDisposedService_DiscardQueuedReplacement()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory, "View.xaml"), "<Control xmlns='https://forma.dev/xaml' Name='New' />");
        using var context = new UIContext();
        Control current = new Control { Name = "Old" };
        using var service = new FormaXamlHotReloadService(context, _directory, watchFiles: false);
        using var registration = service.Register("View.xaml", () => current, (_, newValue) => current = newValue);
        await service.RequestReloadAsync("View.xaml");
        current = new Control { Name = "External" };
        context.Update(new GameTime(), new MouseState(), new KeyboardState());
        Assert.That(current.Name, Is.EqualTo("External"));
        service.Dispose();
        Assert.That(() => service.RequestReloadAsync("View.xaml"), Throws.TypeOf<ObjectDisposedException>());
    }

    [Test]
    public async Task FileWatcher_DebouncesRapidSavesAndAppliesLatest()
    {
        var file = Path.Combine(_directory, "View.xaml");
        await File.WriteAllTextAsync(file, "<Control xmlns='https://forma.dev/xaml' Name='Initial' />");
        using var context = new UIContext();
        Control current = new Control { Name = "Old" };
        using var service = new FormaXamlHotReloadService(context, _directory);
        using var registration = service.Register("View.xaml", () => current, (_, newValue) => current = newValue);
        var compiled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DiagnosticsChanged += diagnostics => { if (diagnostics.Count == 0) compiled.TrySetResult(); };

        await File.WriteAllTextAsync(file, "<Control xmlns='https://forma.dev/xaml' Name='First' />");
        await File.WriteAllTextAsync(file, "<Control xmlns='https://forma.dev/xaml' Name='Latest' />");
        await compiled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(current.Name, Is.EqualTo("Old"));
        context.Update(new GameTime(), new MouseState(), new KeyboardState());
        Assert.That(current.Name, Is.EqualTo("Latest"));
    }
}