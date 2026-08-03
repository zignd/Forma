using System.Text.Json;
using Forma.Xaml.Compiler;

namespace Forma.Xaml.Tool;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            return args[0] switch
            {
                "validate" => Validate(args.Skip(1).ToArray()),
                "watch" => Watch(args.Skip(1).ToArray()),
                "schema" => Schema(args.Skip(1).ToArray()),
                "lsp" => Lsp(args.Skip(1).ToArray()),
                "--help" or "-h" or "help" => Usage(0),
                _ => Usage(),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static int Validate(string[] args)
    {
        var options = ToolOptions.Parse(args);
        if (options.Paths.Count == 0) return Usage();
        var diagnostics = ValidateFiles(DiscoverFiles(options.Paths), options.RequireCompiledBindings);
        WriteDiagnostics(diagnostics, options.Format);
        return diagnostics.Any(diagnostic => diagnostic.Severity == FormaDiagnosticSeverity.Error) ? 1 : 0;
    }

    private static int Watch(string[] args)
    {
        var options = ToolOptions.Parse(args);
        if (options.Paths.Count == 0) return Usage();
        var files = DiscoverFiles(options.Paths).ToArray();
        WriteDiagnostics(ValidateFiles(files, options.RequireCompiledBindings), options.Format);
        if (options.Once) return 0;
        var directories = files.Select(Path.GetDirectoryName).Where(path => path != null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        using var finished = new ManualResetEventSlim();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; finished.Set(); };
        using var debounce = new Timer(_ => WriteDiagnostics(ValidateFiles(files, options.RequireCompiledBindings), options.Format));
        var watchers = directories.Select(directory =>
        {
            var watcher = new FileSystemWatcher(directory!, "*.xaml") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName, EnableRaisingEvents = true };
            FileSystemEventHandler changed = (_, _) => debounce.Change(150, Timeout.Infinite);
            RenamedEventHandler renamed = (_, _) => debounce.Change(150, Timeout.Infinite);
            watcher.Changed += changed;
            watcher.Created += changed;
            watcher.Deleted += changed;
            watcher.Renamed += renamed;
            return watcher;
        }).ToArray();
        finished.Wait();
        foreach (var watcher in watchers) watcher.Dispose();
        return 0;
    }

    private static int Schema(string[] args)
    {
        if (args.Length != 0 && args is not ["--json"]) return Usage();
        var schema = new
        {
            namespaceUri = Forma.Xaml.XamlNamespaces.Forma,
            directives = new[] { "x:Class", "x:Name", "x:Key", "x:DataType" },
            markupExtensions = new[] { "Binding", "StaticResource", "DynamicResource" },
            selectors = new[] { "type", ".class", "Type.class", "#name", ":hover", ":focus", ":disabled", ":pressed", ":checked" },
            timelines = new[] { "FloatTimeline", "ColorTimeline", "Vector2Timeline", "ThicknessTimeline" },
            triggers = new[] { "PropertyTrigger", "EventTrigger" },
        };
        Console.WriteLine(JsonSerializer.Serialize(schema, JsonOptions));
        return 0;
    }

    private static int Lsp(string[] args)
    {
        if (args is not ["--stdio"]) return Usage();
        using var server = new StdioLanguageServer(Console.OpenStandardInput(), Console.OpenStandardOutput());
        server.Run();
        return 0;
    }

    internal static IReadOnlyList<FormaDiagnostic> ValidateFiles(IEnumerable<string> files, bool requireCompiledBindings)
    {
        var parser = new FormaXamlParser();
        var diagnostics = new List<FormaDiagnostic>();
        foreach (var file in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!File.Exists(file))
            {
                diagnostics.Add(new FormaDiagnostic(FormaDiagnosticCodes.XmlSyntax, FormaDiagnosticSeverity.Error, "File does not exist.", new FormaSourceLocation(file, 1, 1)));
                continue;
            }
            diagnostics.AddRange(parser.Parse(File.ReadAllText(file), file, new FormaXamlParseOptions { RequireCompiledBindings = requireCompiledBindings }).Diagnostics);
        }
        return diagnostics;
    }

    internal static IEnumerable<string> DiscoverFiles(IEnumerable<string> paths)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in paths)
        {
            var path = Path.GetFullPath(input);
            if (File.Exists(path) && Path.GetExtension(path).Equals(".xaml", StringComparison.OrdinalIgnoreCase)) files.Add(path);
            else
            {
                var directory = Directory.Exists(path) ? path : File.Exists(path) && Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(path)! : null;
                if (directory == null) { files.Add(path); continue; }
                foreach (var file in Directory.EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories).Where(file => !file.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))) files.Add(Path.GetFullPath(file));
            }
        }
        return files;
    }

    private static void WriteDiagnostics(IReadOnlyList<FormaDiagnostic> diagnostics, string format)
    {
        if (format == "human")
        {
            foreach (var diagnostic in diagnostics) Console.WriteLine(diagnostic);
            if (diagnostics.Count == 0) Console.WriteLine("Forma XAML validation succeeded.");
            return;
        }
        if (format == "json")
        {
            Console.WriteLine(JsonSerializer.Serialize(diagnostics, JsonOptions));
            return;
        }
        var results = diagnostics.Select(diagnostic => new
        {
            ruleId = diagnostic.Code,
            level = diagnostic.Severity.ToString().ToLowerInvariant(),
            message = new { text = diagnostic.Message },
            locations = new[] { new { physicalLocation = new { artifactLocation = new { uri = diagnostic.Location.FilePath }, region = new { startLine = diagnostic.Location.Line, startColumn = diagnostic.Location.Column } } } },
        });
        var sarif = new { version = "2.1.0", runs = new[] { new { tool = new { driver = new { name = "Forma.Xaml.Tool" } }, results } } };
        Console.WriteLine(JsonSerializer.Serialize(sarif, JsonOptions));
    }

    private static int Usage(int exitCode = 2)
    {
        Console.Error.WriteLine("Usage: forma-xaml validate [--format human|json|sarif] [--require-compiled-bindings] <project|directory|file...>");
        Console.Error.WriteLine("       forma-xaml watch [--once] [--format human|json|sarif] <project|directory|file...>");
        Console.Error.WriteLine("       forma-xaml schema [--json]");
        Console.Error.WriteLine("       forma-xaml lsp --stdio");
        return exitCode;
    }

    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private sealed class ToolOptions
    {
        public List<string> Paths { get; } = [];
        public string Format { get; private set; } = "human";
        public bool RequireCompiledBindings { get; private set; }
        public bool Once { get; private set; }

        public static ToolOptions Parse(string[] args)
        {
            var result = new ToolOptions();
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] == "--format" && index + 1 < args.Length) result.Format = args[++index];
                else if (args[index] == "--require-compiled-bindings") result.RequireCompiledBindings = true;
                else if (args[index] == "--once") result.Once = true;
                else if (args[index] is "--configuration" or "--reference") { if (++index >= args.Length) throw new ArgumentException($"{args[index - 1]} requires a value."); }
                else if (args[index].StartsWith('-')) throw new ArgumentException($"Unknown option '{args[index]}'.");
                else result.Paths.Add(args[index]);
            }
            if (result.Format is not ("human" or "json" or "sarif")) throw new ArgumentException($"Unknown output format '{result.Format}'.");
            return result;
        }
    }
}