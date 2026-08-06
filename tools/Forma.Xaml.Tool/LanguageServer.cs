// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Forma.Xaml.Compiler;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace Forma.Xaml.Tool;

internal sealed class StdioLanguageServer : IDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly object _writeLock = new();
    private readonly FormaLanguageWorkspace _workspace = new();
    private readonly Dictionary<string, Timer> _diagnosticTimers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public StdioLanguageServer(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public void Run()
    {
        while (ReadMessage() is { } message)
        {
            var method = message["method"]?.GetValue<string>();
            if (method == "exit") return;
            var parameters = message["params"] as JsonObject;
            if (message["id"] is { } id)
            {
                WriteMessage(new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id.DeepClone(),
                    ["result"] = HandleRequest(method, parameters),
                });
            }
            else HandleNotification(method, parameters);
        }
    }

    private JsonNode? HandleRequest(string? method, JsonObject? parameters) => method switch
    {
        "initialize" => Initialize(parameters),
        "shutdown" => null,
        "textDocument/completion" => _workspace.Completion(parameters),
        "textDocument/hover" => _workspace.Hover(parameters),
        "textDocument/definition" => _workspace.Definition(parameters),
        "textDocument/references" => _workspace.References(parameters),
        "textDocument/rename" => _workspace.Rename(parameters),
        "textDocument/formatting" => _workspace.Formatting(parameters),
        _ => null,
    };

    private JsonNode Initialize(JsonObject? parameters)
    {
        var rootUri = parameters?["rootUri"]?.GetValue<string>();
        var rootPath = parameters?["rootPath"]?.GetValue<string>();
        _workspace.SetRoot(rootUri != null ? FormaLanguageWorkspace.UriToPath(rootUri) : rootPath);
        return new JsonObject
        {
            ["capabilities"] = new JsonObject
            {
                ["textDocumentSync"] = 1,
                ["completionProvider"] = new JsonObject { ["triggerCharacters"] = new JsonArray("<", " ", ".", ":", "{", "=") },
                ["hoverProvider"] = true,
                ["definitionProvider"] = true,
                ["referencesProvider"] = true,
                ["renameProvider"] = new JsonObject { ["prepareProvider"] = false },
                ["documentFormattingProvider"] = true,
            },
            ["serverInfo"] = new JsonObject { ["name"] = "Forma XAML Language Server", ["version"] = "1.0" },
        };
    }

    private void HandleNotification(string? method, JsonObject? parameters)
    {
        if (method == "textDocument/didOpen")
        {
            var document = parameters?["textDocument"]!.AsObject();
            var uri = document?["uri"]?.GetValue<string>();
            if (uri != null) { _workspace.Update(uri, document!["text"]!.GetValue<string>()); ScheduleDiagnostics(uri); }
        }
        else if (method == "textDocument/didChange")
        {
            var uri = parameters?["textDocument"]?["uri"]?.GetValue<string>();
            var changes = parameters?["contentChanges"]?.AsArray();
            if (uri != null && changes?.LastOrDefault()?["text"] is { } text) { _workspace.Update(uri, text.GetValue<string>()); ScheduleDiagnostics(uri); }
        }
        else if (method == "textDocument/didClose")
        {
            var uri = parameters?["textDocument"]?["uri"]?.GetValue<string>();
            if (uri != null) { _workspace.Close(uri); PublishDiagnostics(uri, []); }
        }
        else if (method == "workspace/didChangeWatchedFiles") _workspace.ReloadSchema();
    }

    private void ScheduleDiagnostics(string uri)
    {
        lock (_diagnosticTimers)
        {
            if (_diagnosticTimers.Remove(uri, out var previous)) previous.Dispose();
            _diagnosticTimers[uri] = new Timer(_ => PublishDiagnostics(uri, _workspace.Diagnostics(uri)), null, 150, Timeout.Infinite);
        }
    }

    private void PublishDiagnostics(string uri, JsonArray diagnostics) => WriteMessage(new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["method"] = "textDocument/publishDiagnostics",
        ["params"] = new JsonObject { ["uri"] = uri, ["diagnostics"] = diagnostics },
    });

    private JsonObject? ReadMessage()
    {
        var length = 0;
        while (true)
        {
            var line = ReadLine();
            if (line == null) return null;
            if (line.Length == 0) break;
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(line["Content-Length:".Length..].Trim());
        }
        if (length <= 0) return null;
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = _input.Read(buffer, offset, length - offset);
            if (read == 0) return null;
            offset += read;
        }
        return JsonNode.Parse(buffer)!.AsObject();
    }

    private string? ReadLine()
    {
        var bytes = new List<byte>();
        while (true)
        {
            var value = _input.ReadByte();
            if (value < 0) return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray());
            if (value == '\n') return Encoding.ASCII.GetString(bytes.ToArray()).TrimEnd('\r');
            bytes.Add((byte)value);
        }
    }

    private void WriteMessage(JsonObject message)
    {
        if (_disposed) return;
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        lock (_writeLock)
        {
            _output.Write(header);
            _output.Write(body);
            _output.Flush();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_diagnosticTimers) foreach (var timer in _diagnosticTimers.Values) timer.Dispose();
        _workspace.Dispose();
    }
}

internal sealed class FormaLanguageWorkspace : IDisposable
{
    private static readonly Regex WordPattern = new("[A-Za-z_][A-Za-z0-9_.]*", RegexOptions.CultureInvariant);
    private static readonly string[] PseudoStates =
    [
        "hover", "focus", "focus-within", "disabled", "pressed", "checked", "selected", "current",
        "expanded", "collapsed", "ascending", "descending",
    ];
    private static readonly string[] RelativeSources = ["Self", "TemplatedParent", "FindAncestor"];
    private static readonly Type[] FormaTypes = typeof(Control).Assembly.GetExportedTypes()
        .Where(type => type.Namespace?.StartsWith("Forma", StringComparison.Ordinal) == true && (type.IsClass || type.IsEnum))
        .OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
    private readonly Dictionary<string, string> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly FormaXamlParser _parser = new();
    private readonly List<Compilation> _compilations = [];
    private FileSystemWatcher? _schemaWatcher;
    private Timer? _schemaTimer;
    private string? _root;

    public void SetRoot(string? root)
    {
        _root = string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
        ReloadSchema();
        if (_root == null || !Directory.Exists(_root)) return;
        _schemaWatcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        FileSystemEventHandler changed = (_, args) => SchemaFileChanged(args.FullPath);
        RenamedEventHandler renamed = (_, args) => SchemaFileChanged(args.FullPath);
        _schemaWatcher.Changed += changed;
        _schemaWatcher.Created += changed;
        _schemaWatcher.Deleted += changed;
        _schemaWatcher.Renamed += renamed;
    }

    public void Update(string uri, string text) => _documents[uri] = text;
    public void Close(string uri) => _documents.Remove(uri);

    public JsonArray Diagnostics(string uri)
    {
        var result = new JsonArray();
        if (!_documents.TryGetValue(uri, out var source)) return result;
        foreach (var diagnostic in _parser.Parse(source, UriToPath(uri), new FormaXamlParseOptions { RequireCompiledBindings = true }).Diagnostics)
        {
            result.Add(new JsonObject
            {
                ["range"] = Range(diagnostic.Location.Line - 1, diagnostic.Location.Column - 1, 1),
                ["severity"] = diagnostic.Severity switch { FormaDiagnosticSeverity.Error => 1, FormaDiagnosticSeverity.Warning => 2, _ => 3 },
                ["code"] = diagnostic.Code,
                ["source"] = "forma-xaml",
                ["message"] = diagnostic.Message,
            });
        }
        return result;
    }

    public JsonNode Completion(JsonObject? parameters)
    {
        if (!TryPosition(parameters, out var uri, out var source, out var offset)) return new JsonArray();
        var before = source[..offset];
        var items = new SortedDictionary<string, (int Kind, string? Detail)>(StringComparer.Ordinal);
        var openTag = Regex.Match(before, @"<(?<type>[A-Za-z_][\w:.]*)(?<body>[^<>]*)$");
        if (!openTag.Success || !openTag.Groups["body"].Value.Any(char.IsWhiteSpace))
        {
            foreach (var type in FormaTypes.Where(IsXamlElementType))
                items[type.Name] = (7, ClassifyElementType(type));
        }
        else
        {
            var type = ResolveType(openTag.Groups["type"].Value.Split(':').Last());
            foreach (var property in type?.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite) ?? [])
                items[property.Name] = (10, property.PropertyType.Name);
            foreach (var eventInfo in type?.GetEvents(BindingFlags.Public | BindingFlags.Instance) ?? []) items[eventInfo.Name] = (23, eventInfo.EventHandlerType?.Name);
            foreach (var attached in AttachedProperties()) items[attached] = (10, "Attached layout property");
            foreach (var directive in new[] { "x:Name", "x:Class", "x:DataType", "x:Key" }) items[directive] = (10, "Forma XAML directive");
            var attribute = Regex.Match(openTag.Groups["body"].Value, """(?<name>[\w:.]+)\s*=\s*['"](?<value>[^'"]*)$""");
            if (attribute.Success)
            {
                var value = attribute.Groups["value"].Value;
                var property = type?.GetProperty(attribute.Groups["name"].Value, BindingFlags.Public | BindingFlags.Instance);
                var enumType = Nullable.GetUnderlyingType(property?.PropertyType ?? typeof(object)) ?? property?.PropertyType;
                if (enumType?.IsEnum == true) foreach (var name in Enum.GetNames(enumType)) items[name] = (20, enumType.Name);
                if (value.StartsWith("{Binding", StringComparison.Ordinal))
                {
                    foreach (var option in new[] { "Mode=", "FallbackValue=", "TargetNullValue=", "StringFormat=", "Converter=", "UpdateSourceTrigger=" }) items[option] = (10, "Binding option");
                    foreach (var member in BindingMembers(source, offset)) items[member] = (10, "Data context member");
                    if (value.Contains("RelativeSource", StringComparison.Ordinal))
                        foreach (var relativeSource in RelativeSources) items[relativeSource] = (20, "Binding relative source");
                }
                if (value.Contains("Resource", StringComparison.Ordinal)) foreach (var key in Symbols(source, """x:Key\s*=\s*['"](?<value>[^'"]+)""")) items[key] = (12, "Resource key");
                if (attribute.Groups["name"].Value is "Classes" or "Selector") foreach (var name in Symbols(source, """Classes\s*=\s*['"](?<value>[^'"]+)""" ).SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))) items[name] = (12, "Style class");
                if (attribute.Groups["name"].Value == "Selector")
                    foreach (var state in PseudoStates) items[$":{state}"] = (20, "Pseudo-state");
                if (attribute.Groups["name"].Value == "TargetType")
                    foreach (var targetType in FormaTypes.Where(candidate => !candidate.IsAbstract && typeof(TemplatedControl).IsAssignableFrom(candidate)))
                        items[targetType.Name] = (7, "Templated control type");
                if (attribute.Groups["name"].Value == "x:DataType")
                    foreach (var dataType in _compilations.SelectMany(compilation => compilation.GlobalNamespace.GetNamespaceTypes()).Where(candidate => candidate.DeclaredAccessibility == Accessibility.Public))
                        items[dataType.ToDisplayString()] = (7, "Compiled data type");
                if (attribute.Groups["name"].Value == "x:Name")
                    foreach (var part in TemplatePartNames(before)) items[part] = (12, "Template part");
            }
        }
        return new JsonObject
        {
            ["isIncomplete"] = false,
            ["items"] = new JsonArray(items.Select(item => (JsonNode)new JsonObject { ["label"] = item.Key, ["kind"] = item.Value.Kind, ["detail"] = item.Value.Detail }).ToArray()),
        };
    }

    public JsonNode? Hover(JsonObject? parameters)
    {
        if (!TryPosition(parameters, out var uri, out var source, out var offset) || WordAt(source, offset) is not { } word) return null;
        var type = ResolveType(word);
        if (type != null) return new JsonObject { ["contents"] = new JsonObject { ["kind"] = "markdown", ["value"] = $"`{type.FullName}`\n\nForma XAML { (type.IsEnum ? "enum" : "element") }." } };
        var owner = ElementTypeAt(source, offset);
        var member = owner?.GetMember(word, BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
        if (member != null) return new JsonObject { ["contents"] = new JsonObject { ["kind"] = "markdown", ["value"] = $"`{owner!.Name}.{member.Name}`\n\n{MemberType(member)}" } };
        var category = SemanticSymbolAt(uri, source, offset)?.Category ?? ClassifySymbol(word, source);
        return category == null ? null : new JsonObject { ["contents"] = new JsonObject { ["kind"] = "markdown", ["value"] = $"Forma XAML **{category}** `{word}`" } };
    }

    public JsonNode Definition(JsonObject? parameters)
    {
        if (!TryPosition(parameters, out var uri, out var source, out var offset) || WordAt(source, offset) is not { } word) return new JsonArray();
        var semantic = SemanticSymbolAt(uri, source, offset);
        var locations = semantic == null ? SymbolLocations(word, ClassifySymbol(word, source)) : SemanticSymbolLocations(semantic);
        if (locations.Count > 0) return locations;
        foreach (var compilation in _compilations)
        {
            var symbol = FindType(compilation, word);
            var location = symbol?.Locations.FirstOrDefault(item => item.IsInSource);
            if (location != null) return new JsonArray(Location(PathToUri(location.SourceTree!.FilePath), location.GetLineSpan().StartLinePosition.Line, location.GetLineSpan().StartLinePosition.Character, word.Length));
        }
        return new JsonArray();
    }

    public JsonNode References(JsonObject? parameters)
    {
        if (!TryPosition(parameters, out var uri, out var source, out var offset) || WordAt(source, offset) is not { } word) return new JsonArray();
        var semantic = SemanticSymbolAt(uri, source, offset);
        return semantic == null ? SymbolLocations(word, ClassifySymbol(word, source)) : SemanticSymbolLocations(semantic);
    }

    public JsonNode? Rename(JsonObject? parameters)
    {
        if (!TryPosition(parameters, out var uri, out var source, out var offset) || WordAt(source, offset) is not { } word) return null;
        var semantic = SemanticSymbolAt(uri, source, offset);
        var category = semantic?.Category ?? ClassifySymbol(word, source);
        var replacement = parameters?["newName"]?.GetValue<string>();
        if (category == null || string.IsNullOrWhiteSpace(replacement) || !Regex.IsMatch(replacement, "^[A-Za-z_][A-Za-z0-9_-]*$")) return null;
        var changes = new JsonObject();
        foreach (var document in _documents)
        {
            var matches = semantic == null
                ? SymbolMatches(document.Value, word, category)
                : BuildSemanticSymbols(document.Key, document.Value).Where(candidate => candidate.HasSameIdentity(semantic))
                    .Select(candidate => new SymbolOccurrence(candidate.Index, candidate.Length));
            var edits = matches.Select(match => (JsonNode)new JsonObject
            {
                ["range"] = RangeAt(document.Value, match.Index, match.Length),
                ["newText"] = replacement,
            }).ToArray();
            if (edits.Length > 0) changes[document.Key] = new JsonArray(edits);
        }
        return new JsonObject { ["changes"] = changes };
    }

    private JsonArray SemanticSymbolLocations(SemanticSymbol symbol)
    {
        var result = new JsonArray();
        foreach (var document in _documents)
            foreach (var candidate in BuildSemanticSymbols(document.Key, document.Value).Where(candidate => candidate.HasSameIdentity(symbol)))
                result.Add(Location(document.Key, document.Value, candidate.Index, candidate.Length));
        return result;
    }

    private static SemanticSymbol? SemanticSymbolAt(string uri, string source, int offset) =>
        BuildSemanticSymbols(uri, source)
            .Where(symbol => offset >= symbol.Index && offset <= symbol.Index + symbol.Length)
            .OrderBy(symbol => symbol.Length)
            .FirstOrDefault();

    private static IReadOnlyList<SemanticSymbol> BuildSemanticSymbols(string uri, string source)
    {
        XDocument document;
        try { document = XDocument.Parse(source, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo); }
        catch (XmlException) { return Array.Empty<SemanticSymbol>(); }
        if (document.Root == null) return Array.Empty<SemanticSymbol>();
        var symbols = new List<SemanticSymbol>();
        var scopeParents = new Dictionary<string, string?>(StringComparer.Ordinal);
        var rootScope = $"{uri}#$root";
        scopeParents[rootScope] = null;
        var templateOrdinal = 0;

        void Add(string name, string category, string scope, int index, int length, bool isDefinition)
        {
            if (!string.IsNullOrWhiteSpace(name)) symbols.Add(new SemanticSymbol(name, category, scope, index, length, isDefinition));
        }

        void Visit(XElement element, string inheritedScope)
        {
            var isTemplate = element.Name.LocalName is "ControlTemplate" or "DataTemplate" or "ItemsPanelTemplate";
            var scope = inheritedScope;
            if (isTemplate)
            {
                scope = $"{uri}#template:{templateOrdinal++}";
                scopeParents[scope] = inheritedScope;
            }
            foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            {
                if (!TryAttributeValueRange(source, attribute, out var valueIndex, out var valueLength)) continue;
                var value = attribute.Value;
                var localName = attribute.Name.LocalName;
                var isDirective = attribute.Name.NamespaceName == XamlNamespaces.Xaml2006;
                if (isDirective && localName == "Name") Add(value, "name", scope, valueIndex, valueLength, true);
                else if (localName is "SourceName" or "TargetName") Add(value, "name", scope, valueIndex, valueLength, false);
                else if (isDirective && localName == "Key") Add(value, "resource", isTemplate ? inheritedScope : scope, valueIndex, valueLength, true);

                foreach (Match match in Regex.Matches(value, @"\{(?:Static|Dynamic)Resource\s+(?<symbol>[^,\s}]+)", RegexOptions.CultureInvariant))
                {
                    var group = match.Groups["symbol"];
                    Add(group.Value, "resource", scope, valueIndex + group.Index, group.Length, false);
                }
                if (localName == "Classes")
                    foreach (Match match in Regex.Matches(value, @"[A-Za-z_][A-Za-z0-9_-]*", RegexOptions.CultureInvariant))
                        Add(match.Value, "class", "*", valueIndex + match.Index, match.Length, false);
                if (localName == "Selector")
                {
                    foreach (Match match in Regex.Matches(value, @"\.(?<symbol>[A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant))
                    {
                        var group = match.Groups["symbol"];
                        Add(group.Value, "class", "*", valueIndex + group.Index, group.Length, true);
                    }
                    foreach (Match match in Regex.Matches(value, @"#(?<symbol>[A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant))
                    {
                        var group = match.Groups["symbol"];
                        Add(group.Value, "name", scope, valueIndex + group.Index, group.Length, false);
                    }
                }
            }
            foreach (var child in element.Elements()) Visit(child, scope);
        }

        Visit(document.Root, rootScope);
        foreach (var reference in symbols.Where(symbol => symbol.Category == "resource" && !symbol.IsDefinition))
        {
            for (var scope = reference.Scope; scope != null; scope = scopeParents.GetValueOrDefault(scope))
            {
                var definition = symbols.FirstOrDefault(candidate => candidate.Category == "resource" && candidate.IsDefinition &&
                    candidate.Name == reference.Name && candidate.Scope == scope);
                if (definition == null) continue;
                reference.Scope = definition.Scope;
                break;
            }
        }
        return symbols;
    }

    private static bool TryAttributeValueRange(string source, XAttribute attribute, out int index, out int length)
    {
        index = 0;
        length = 0;
        if (attribute is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo()) return false;
        var lineStart = 0;
        for (var line = 1; line < lineInfo.LineNumber; line++)
        {
            var newline = source.IndexOf('\n', lineStart);
            if (newline < 0) return false;
            lineStart = newline + 1;
        }
        var attributeStart = Math.Min(lineStart + Math.Max(0, lineInfo.LinePosition - 1), source.Length);
        var equals = source.IndexOf('=', attributeStart);
        if (equals < 0) return false;
        var quote = source.IndexOfAny(new[] { '\'', '"' }, equals + 1);
        if (quote < 0) return false;
        var end = source.IndexOf(source[quote], quote + 1);
        if (end < 0) return false;
        index = quote + 1;
        length = end - index;
        return true;
    }

    public JsonNode Formatting(JsonObject? parameters)
    {
        var uri = parameters?["textDocument"]?["uri"]?.GetValue<string>();
        if (uri == null || !_documents.TryGetValue(uri, out var source)) return new JsonArray();
        try
        {
            var document = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
            foreach (var text in document.DescendantNodes().OfType<XText>().Where(text => string.IsNullOrWhiteSpace(text.Value)).ToArray()) text.Remove();
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, IndentChars = "    ", NewLineChars = "\n", OmitXmlDeclaration = document.Declaration == null })) document.Save(writer);
            var formatted = builder.ToString().TrimEnd() + "\n";
            return formatted == source ? new JsonArray() : new JsonArray(new JsonObject { ["range"] = WholeDocumentRange(source), ["newText"] = formatted });
        }
        catch (XmlException) { return new JsonArray(); }
    }

    public void ReloadSchema()
    {
        _compilations.Clear();
        if (_root == null || !Directory.Exists(_root)) return;
        try
        {
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
            using var workspace = MSBuildWorkspace.Create();
            var projects = Directory.EnumerateFiles(_root, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"));
            foreach (var path in projects)
            {
                var project = workspace.OpenProjectAsync(path).GetAwaiter().GetResult();
                var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
                if (compilation != null) _compilations.Add(compilation);
            }
        }
        catch { }
    }

    private void SchemaFileChanged(string path)
    {
        if (!path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path) != "project.assets.json") return;
        _schemaTimer ??= new Timer(_ => ReloadSchema());
        _schemaTimer.Change(300, Timeout.Infinite);
    }

    private IEnumerable<string> BindingMembers(string source, int offset)
    {
        var parsed = _parser.Parse(source, "document.xaml");
        var matches = Regex.Matches(source[..Math.Min(offset, source.Length)], """x:DataType\s*=\s*['\"](?<type>[^'\"]+)""", RegexOptions.CultureInvariant);
        var dataType = matches.LastOrDefault()?.Groups["type"].Value.Split(':').Last() ?? parsed.Document?.DataType?.Split(':').Last();
        if (dataType == null) return [];
        foreach (var compilation in _compilations)
        {
            var type = FindType(compilation, dataType);
            if (type != null) return type.GetMembers().OfType<IPropertySymbol>().Where(property => property.DeclaredAccessibility == Accessibility.Public).Select(property => property.Name);
        }
        return [];
    }

    private static bool IsXamlElementType(Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters) return false;
        if (typeof(Control).IsAssignableFrom(type) || type.Namespace == "Forma.Xaml") return true;
        return typeof(Brush).IsAssignableFrom(type) || typeof(Geometry).IsAssignableFrom(type) ||
            typeof(DataGridColumn).IsAssignableFrom(type) || type.Name.EndsWith("Definition", StringComparison.Ordinal) ||
            type.Name.EndsWith("Condition", StringComparison.Ordinal);
    }

    private static string ClassifyElementType(Type type)
    {
        if (typeof(TemplatedControl).IsAssignableFrom(type)) return "Templated semantic control";
        if (typeof(Control).IsAssignableFrom(type)) return "Foundational element or presenter";
        if (typeof(Brush).IsAssignableFrom(type)) return "Brush";
        if (typeof(Geometry).IsAssignableFrom(type)) return "Geometry";
        if (typeof(DataGridColumn).IsAssignableFrom(type)) return "DataGrid column";
        return type.FullName ?? type.Name;
    }

    private static IEnumerable<string> AttachedProperties() => FormaTypes
        .SelectMany(owner => owner.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.StartsWith("Set", StringComparison.Ordinal) && method.GetParameters().Length == 2 &&
                typeof(Control).IsAssignableFrom(method.GetParameters()[0].ParameterType))
            .Select(method => $"{owner.Name}.{method.Name[3..]}"))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal);

    private static IEnumerable<string> TemplatePartNames(string sourceBeforeOffset)
    {
        var templates = Regex.Matches(sourceBeforeOffset, """<ControlTemplate\b[^>]*TargetType\s*=\s*['\"](?<type>[^'\"]+)['\"]""", RegexOptions.CultureInvariant);
        var typeName = templates.LastOrDefault()?.Groups["type"].Value.Split(':').Last();
        var targetType = typeName == null ? null : ResolveType(typeName);
        return targetType?.GetCustomAttributes<TemplatePartAttribute>(true).Select(attribute => attribute.Name).Distinct(StringComparer.Ordinal)
            ?? Enumerable.Empty<string>();
    }

    private JsonArray SymbolLocations(string word, string? category)
    {
        var result = new JsonArray();
        if (category == null) return result;
        foreach (var document in _documents)
            foreach (var match in SymbolMatches(document.Value, word, category)) result.Add(Location(document.Key, document.Value, match.Index, match.Length));
        return result;
    }

    private static IEnumerable<SymbolOccurrence> SymbolMatches(string source, string word, string category)
    {
        var escaped = Regex.Escape(word);
        var patterns = category switch
        {
            "name" => new[] { $"""(?:x:Name|SourceName|TargetName)\s*=\s*['"](?<symbol>{escaped})(?=['"])""", $@"#(?<symbol>{escaped})\b" },
            "resource" => new[] { $"""x:Key\s*=\s*['"](?<symbol>{escaped})(?=['"])""", $@"\{{(?:Static|Dynamic)Resource\s+(?<symbol>{escaped})(?=[,\s}}])" },
            "class" => [],
            _ => [],
        };
        var result = patterns.SelectMany(pattern => Regex.Matches(source, pattern, RegexOptions.CultureInvariant).Cast<Match>())
            .Select(match => match.Groups["symbol"])
            .Select(group => new SymbolOccurrence(group.Index, group.Length)).ToList();
        if (category == "class")
        {
            foreach (Match attribute in Regex.Matches(source, """(?:Classes|Selector)\s*=\s*['"](?<value>[^'"]*)""", RegexOptions.CultureInvariant))
            {
                var value = attribute.Groups["value"];
                foreach (Match match in Regex.Matches(value.Value, $@"(?<![A-Za-z0-9_-]){escaped}\b", RegexOptions.CultureInvariant))
                    result.Add(new SymbolOccurrence(value.Index + match.Index, match.Length));
            }
        }
        return result.OrderBy(match => match.Index).DistinctBy(match => match.Index);
    }

    private static string? ClassifySymbol(string word, string source)
    {
        var escaped = Regex.Escape(word);
        if (Regex.IsMatch(source, $"""x:Name\s*=\s*['"]{escaped}['"]|(?:SourceName|TargetName)\s*=\s*['"]{escaped}['"]|#{escaped}\b""")) return "name";
        if (Regex.IsMatch(source, "x:Key\\s*=\\s*['\"]" + escaped + "['\"]|\\{(?:Static|Dynamic)Resource\\s+" + escaped + "(?:[,\\s}])")) return "resource";
        if (Regex.IsMatch(source, $"""(?:Classes|Selector)\s*=\s*['"][^'"]*(?:^|[.\s]){escaped}\b""")) return "class";
        return null;
    }

    private static Type? ResolveType(string name) => FormaTypes.FirstOrDefault(type => type.Name == name);
    private static INamedTypeSymbol? FindType(Compilation compilation, string name) => compilation.GlobalNamespace.GetNamespaceTypes().FirstOrDefault(type => type.Name == name || type.ToDisplayString() == name);
    private static string MemberType(MemberInfo member) => member switch { PropertyInfo property => property.PropertyType.FullName ?? property.PropertyType.Name, EventInfo eventInfo => eventInfo.EventHandlerType?.FullName ?? "event", _ => member.MemberType.ToString() };

    private static Type? ElementTypeAt(string source, int offset)
    {
        var matches = Regex.Matches(source[..Math.Min(offset, source.Length)], @"<(?<name>[A-Za-z_][\w:.]*)[^<>]*").Cast<Match>();
        return ResolveType(matches.LastOrDefault()?.Groups["name"].Value.Split(':').Last() ?? string.Empty);
    }

    private bool TryPosition(JsonObject? parameters, out string uri, out string source, out int offset)
    {
        uri = parameters?["textDocument"]?["uri"]?.GetValue<string>() ?? string.Empty;
        source = string.Empty;
        offset = 0;
        if (parameters?["position"] is not JsonObject position || !_documents.TryGetValue(uri, out var document)) return false;
        source = document;
        return TryOffset(source, position, out offset);
    }

    private static bool TryOffset(string source, JsonObject position, out int offset)
    {
        var line = position["line"]?.GetValue<int>() ?? 0;
        var character = position["character"]?.GetValue<int>() ?? 0;
        offset = 0;
        for (var current = 0; current < line; current++)
        {
            var newline = source.IndexOf('\n', offset);
            if (newline < 0) return false;
            offset = newline + 1;
        }
        offset = Math.Min(offset + character, source.Length);
        return true;
    }

    private static string? WordAt(string source, int offset)
    {
        foreach (Match match in WordPattern.Matches(source)) if (offset >= match.Index && offset <= match.Index + match.Length) return match.Value.Split('.').Last();
        return null;
    }

    private static IEnumerable<string> Symbols(string source, string pattern) => Regex.Matches(source, pattern, RegexOptions.CultureInvariant).Select(match => match.Groups["value"].Value).Distinct(StringComparer.Ordinal);
    private static JsonObject Range(int line, int character, int length) => new() { ["start"] = Position(line, character), ["end"] = Position(line, character + length) };
    private static JsonObject Position(int line, int character) => new() { ["line"] = line, ["character"] = character };
    private static JsonObject WholeDocumentRange(string source) { var lines = source.Split('\n'); return new JsonObject { ["start"] = Position(0, 0), ["end"] = Position(lines.Length - 1, lines[^1].Length) }; }
    private static JsonObject RangeAt(string source, int index, int length) { var start = SourcePosition(source, index); return Range(start.Line, start.Character, length); }
    private static JsonObject Location(string uri, string source, int index, int length) => new() { ["uri"] = uri, ["range"] = RangeAt(source, index, length) };
    private static JsonObject Location(string uri, int line, int character, int length) => new() { ["uri"] = uri, ["range"] = Range(line, character, length) };
    private static (int Line, int Character) SourcePosition(string source, int index) { var prefix = source[..index]; var line = prefix.Count(character => character == '\n'); var newline = prefix.LastIndexOf('\n'); return (line, newline < 0 ? index : index - newline - 1); }
    private sealed class SemanticSymbol
    {
        public SemanticSymbol(string name, string category, string scope, int index, int length, bool isDefinition)
        {
            Name = name;
            Category = category;
            Scope = scope;
            Index = index;
            Length = length;
            IsDefinition = isDefinition;
        }

        public string Name { get; }
        public string Category { get; }
        public string Scope { get; set; }
        public int Index { get; }
        public int Length { get; }
        public bool IsDefinition { get; }
        public bool HasSameIdentity(SemanticSymbol other) =>
            Name == other.Name && Category == other.Category && Scope == other.Scope;
    }
    private readonly record struct SymbolOccurrence(int Index, int Length);
    internal static string UriToPath(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var value) && value.IsFile ? value.LocalPath : uri;
    private static string PathToUri(string path) => new Uri(Path.GetFullPath(path)).AbsoluteUri;

    public void Dispose()
    {
        _schemaWatcher?.Dispose();
        _schemaTimer?.Dispose();
    }
}

internal static class NamespaceSymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetNamespaceTypes(this INamespaceSymbol root)
    {
        foreach (var type in root.GetTypeMembers()) yield return type;
        foreach (var child in root.GetNamespaceMembers()) foreach (var type in child.GetNamespaceTypes()) yield return type;
    }
}