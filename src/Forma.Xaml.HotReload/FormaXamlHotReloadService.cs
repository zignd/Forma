// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Forma.Xaml.Compiler;

namespace Forma.Xaml.HotReload;

public sealed class FormaXamlHotReloadService : IDisposable
{
    private readonly UIContext _context;
    private readonly string _developmentRoot;
    private readonly ConcurrentDictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<Replacement> _replacements = new();
    private readonly FileSystemWatcher? _watcher;
    private readonly IDisposable _frameCallback;
    private bool _disposed;

    public FormaXamlHotReloadService(UIContext context, string developmentRoot, bool watchFiles = true)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
            throw new PlatformNotSupportedException("Forma XAML hot reload requires dynamic code and is unavailable under NativeAOT.");
        if (AppContext.TryGetSwitch("Forma.Xaml.Trimmed", out var trimmed) && trimmed)
            throw new PlatformNotSupportedException("Forma XAML hot reload is disabled for trimmed applications.");
        _developmentRoot = Path.GetFullPath(developmentRoot ?? throw new ArgumentNullException(nameof(developmentRoot)));
        Directory.CreateDirectory(_developmentRoot);
        if (watchFiles)
        {
            _watcher = new FileSystemWatcher(_developmentRoot, "*.xaml")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += FileChanged;
            _watcher.Created += FileChanged;
            _watcher.Renamed += FileRenamed;
        }
        _frameCallback = context.RegisterFrameBoundaryCallback(_ => DrainReplacements());
    }

    public event Action<IReadOnlyList<FormaDiagnostic>>? DiagnosticsChanged;

    public IDisposable Register<T>(string source, Func<T> current, Action<T, T> replace) where T : Control
    {
        if (current == null) throw new ArgumentNullException(nameof(current));
        if (replace == null) throw new ArgumentNullException(nameof(replace));
        return RegisterCore(source, () => current(), (oldValue, newValue) =>
        {
            if (newValue is not T typed) throw new InvalidOperationException($"Reloaded root '{newValue.GetType().FullName}' is not assignable to '{typeof(T).FullName}'.");
            replace((T)oldValue, typed);
        });
    }

    public IDisposable Register<T>(string source, T current, Action<T, T> replace) where T : Control
    {
        var value = current ?? throw new ArgumentNullException(nameof(current));
        return Register(source, () => value, (oldValue, newValue) => { replace(oldValue, newValue); value = newValue; });
    }

    public Task RequestReloadAsync(string source) => RequestReloadAsync(NormalizeSource(source), TimeSpan.Zero);

    private IDisposable RegisterCore(string source, Func<Control> current, Action<Control, Control> replace)
    {
        ThrowIfDisposed();
        var key = NormalizeSource(source);
        var registration = new Registration(key, current, replace, RemoveRegistration);
        if (!_registrations.TryAdd(key, registration)) throw new InvalidOperationException($"A hot-reload root is already registered for '{key}'.");
        return registration;
    }

    private void FileChanged(object sender, FileSystemEventArgs args) => _ = RequestReloadAsync(NormalizeSource(Path.GetRelativePath(_developmentRoot, args.FullPath)), TimeSpan.FromMilliseconds(150));
    private void FileRenamed(object sender, RenamedEventArgs args) => FileChanged(sender, args);

    private async Task RequestReloadAsync(string source, TimeSpan debounce)
    {
        ThrowIfDisposed();
        if (!_registrations.TryGetValue(source, out var registration)) return;
        var version = Interlocked.Increment(ref registration.Version);
        if (debounce > TimeSpan.Zero) await Task.Delay(debounce).ConfigureAwait(false);
        if (_disposed || version != Volatile.Read(ref registration.Version)) return;
        var expected = registration.Current();
        try
        {
            var file = Path.Combine(_developmentRoot, source.Replace('/', Path.DirectorySeparatorChar));
            var callbacks = FormaXamlCompiler.CreateSre(expected.GetType().Assembly.GetName().Name!).CompileSre(await File.ReadAllTextAsync(file).ConfigureAwait(false), source);
            if (callbacks.Build(null) is not Control replacement) throw new InvalidOperationException("Hot-reloaded XAML root is not a Control.");
            if (version != Volatile.Read(ref registration.Version)) return;
            _replacements.Enqueue(new Replacement(registration, version, expected, replacement));
            DiagnosticsChanged?.Invoke(Array.Empty<FormaDiagnostic>());
        }
        catch (FormaXamlCompilationException exception)
        {
            DiagnosticsChanged?.Invoke(exception.Diagnostics);
        }
        catch (Exception exception)
        {
            DiagnosticsChanged?.Invoke(new[] { new FormaDiagnostic(FormaDiagnosticCodes.Emission, FormaDiagnosticSeverity.Error, exception.Message, new FormaSourceLocation(source, 1, 1)) });
        }
    }

    private void DrainReplacements()
    {
        while (_replacements.TryDequeue(out var replacement))
        {
            var registration = replacement.Registration;
            if (registration.Disposed || replacement.Version != Volatile.Read(ref registration.Version)) continue;
            var current = registration.Current();
            if (!ReferenceEquals(current, replacement.Expected)) continue;
            replacement.Value.DataContext = current.DataContext;
            try { registration.Replace(current, replacement.Value); }
            catch (Exception exception)
            {
                DiagnosticsChanged?.Invoke(new[] { new FormaDiagnostic(FormaDiagnosticCodes.Emission, FormaDiagnosticSeverity.Error, exception.Message, new FormaSourceLocation(registration.Source, 1, 1)) });
            }
        }
    }

    private string NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("A project-relative XAML source path is required.", nameof(source));
        var normalized = source.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(source) || normalized.Split('/').Any(segment => segment == "..")) throw new ArgumentException("Hot-reload source paths must be project-relative.", nameof(source));
        return normalized;
    }

    private void RemoveRegistration(Registration registration)
    {
        _registrations.TryRemove(new KeyValuePair<string, Registration>(registration.Source, registration));
        Interlocked.Increment(ref registration.Version);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FormaXamlHotReloadService));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        _frameCallback.Dispose();
        foreach (var registration in _registrations.Values) registration.Dispose();
        _registrations.Clear();
        while (_replacements.TryDequeue(out _)) { }
    }

    private sealed class Registration : IDisposable
    {
        private readonly Action<Registration> _dispose;
        public Registration(string source, Func<Control> current, Action<Control, Control> replace, Action<Registration> dispose)
        { Source = source; Current = current; Replace = replace; _dispose = dispose; }
        public string Source { get; }
        public Func<Control> Current { get; }
        public Action<Control, Control> Replace { get; }
        public long Version;
        public bool Disposed { get; private set; }
        public void Dispose() { if (Disposed) return; Disposed = true; _dispose(this); }
    }

    private sealed record Replacement(Registration Registration, long Version, Control Expected, Control Value);
}