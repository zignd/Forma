// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Text;
using System.Text.Json.Nodes;
using Forma.Xaml.Tool;

namespace Forma.Xaml.Tool.Tests;

public sealed class LanguageServerTest
{
    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"forma-lsp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, true);

    [Test]
    public void StdioServer_AdvertisesCompleteV1Capabilities()
    {
        var input = new MemoryStream();
        Write(input, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "initialize", ["params"] = new JsonObject() });
        Write(input, new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "exit" });
        input.Position = 0;
        using var output = new MemoryStream();
        using (var server = new StdioLanguageServer(input, output)) server.Run();
        output.Position = 0;
        var response = Read(output);
        var capabilities = response["result"]!["capabilities"]!;
        Assert.Multiple(() =>
        {
            Assert.That(capabilities["completionProvider"], Is.Not.Null);
            Assert.That(capabilities["hoverProvider"]!.GetValue<bool>(), Is.True);
            Assert.That(capabilities["definitionProvider"]!.GetValue<bool>(), Is.True);
            Assert.That(capabilities["referencesProvider"]!.GetValue<bool>(), Is.True);
            Assert.That(capabilities["renameProvider"], Is.Not.Null);
            Assert.That(capabilities["documentFormattingProvider"]!.GetValue<bool>(), Is.True);
        });
    }

    [Test]
    public void SemanticWorkspace_ProvidesDiagnosticsSchemaSymbolsAndFormatting()
    {
        const string uri = "file:///Views/Hud.xaml";
        const string source = """
            <Control xmlns="https://forma.dev/xaml" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Name="Root" Classes="hud">
                            <Control.Resources>
                                <ResourceDictionary>
                                    <Style x:Key="HudStyle" Selector="Control.hud" />
                                </ResourceDictionary>
                            </Control.Resources>
              <Control x:Name="Score" ThemeOverride="{StaticResource HudStyle}" />
              <Vector2Timeline TargetName="Score" Property="Position"><KeyFrame Time="0:0:0" Value="0,0" /></Vector2Timeline>
            </Control>
            """;
        using var workspace = new FormaLanguageWorkspace();
        workspace.Update(uri, source);

        var references = workspace.References(At(uri, source, "TargetName=\"Score\"", "Score"));
        var definition = workspace.Definition(At(uri, source, "TargetName=\"Score\"", "Score"));
        var rename = workspace.Rename(At(uri, source, "TargetName=\"Score\"", "Score", "Metric"));
        var resourceReferences = workspace.References(At(uri, source, "StaticResource HudStyle", "HudStyle"));
        var resourceRename = workspace.Rename(At(uri, source, "StaticResource HudStyle", "HudStyle", "MetricStyle"));
        var classReferences = workspace.References(At(uri, source, "Selector=\"Control.hud\"", "hud"));
        var classRename = workspace.Rename(At(uri, source, "Selector=\"Control.hud\"", "hud", "metric"));
        var hover = workspace.Hover(At(uri, source, "<Control xmlns", "Control"));
        var memberHover = workspace.Hover(At(uri, source, "ThemeOverride=", "ThemeOverride"));
        var formatting = workspace.Formatting(new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri } });
        Assert.Multiple(() =>
        {
            Assert.That(workspace.Diagnostics(uri), Is.Empty);
            Assert.That(references.AsArray(), Has.Count.EqualTo(2));
            Assert.That(definition.AsArray(), Has.Count.EqualTo(2));
            Assert.That(rename!["changes"]![uri]!.AsArray(), Has.Count.EqualTo(2));
            Assert.That(resourceReferences.AsArray(), Has.Count.EqualTo(2));
            Assert.That(resourceRename!["changes"]![uri]!.AsArray(), Has.Count.EqualTo(2));
            Assert.That(classReferences.AsArray(), Has.Count.EqualTo(2));
            Assert.That(classRename!["changes"]![uri]!.AsArray(), Has.Count.EqualTo(2));
            Assert.That(hover!["contents"]!["value"]!.GetValue<string>(), Does.Contain("Forma.Control"));
            Assert.That(memberHover!["contents"]!["value"]!.GetValue<string>(), Does.Contain("Theme"));
            Assert.That(formatting.AsArray(), Has.Count.EqualTo(1));
            Assert.That(formatting[0]!["newText"]!.GetValue<string>(), Does.Contain("    <Style"));
        });

        workspace.Update(uri, "<Control xmlns='https://forma.dev/xaml' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:Uid='bad' />");
        Assert.That(workspace.Diagnostics(uri).Select(item => item!["code"]!.GetValue<string>()), Does.Contain("FXAML1003"));
    }

    [Test]
    public void Completion_UsesFormaSchemaAndRoslynProjectSymbols()
    {
        File.WriteAllText(Path.Combine(_directory, "Game.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        File.WriteAllText(Path.Combine(_directory, "ViewModel.cs"), "namespace Game; public sealed class ViewModel { public string Score { get; set; } = string.Empty; }");
        using var workspace = new FormaLanguageWorkspace();
        workspace.SetRoot(_directory);
        const string uri = "file:///Views/Hud.xaml";

        const string elementSource = "<But";
        workspace.Update(uri, elementSource);
        var elements = Labels(workspace.Completion(AtEnd(uri, elementSource)));
        const string memberSource = "<Button xmlns='https://forma.dev/xaml' ";
        workspace.Update(uri, memberSource);
        var members = Labels(workspace.Completion(AtEnd(uri, memberSource)));
        const string enumSource = "<Control xmlns='https://forma.dev/xaml' HorizontalSizeFlags='";
        workspace.Update(uri, enumSource);
        var enumValues = Labels(workspace.Completion(AtEnd(uri, enumSource)));
        const string resourceSource = "<Control xmlns='https://forma.dev/xaml' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Style x:Key='Accent' Selector='Control.hud' /><Control ThemeOverride='{StaticResource ";
        workspace.Update(uri, resourceSource);
        var resources = Labels(workspace.Completion(AtEnd(uri, resourceSource)));
        const string classSource = "<Control xmlns='https://forma.dev/xaml'><Control Classes='hud' /><Control Classes='";
        workspace.Update(uri, classSource);
        var classes = Labels(workspace.Completion(AtEnd(uri, classSource)));
        const string bindingSource = "<Label xmlns='https://forma.dev/xaml' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:DataType='Game.ViewModel' Text='{Binding ";
        workspace.Update(uri, bindingSource);
        var bindings = Labels(workspace.Completion(AtEnd(uri, bindingSource)));
        var definition = workspace.Definition(At(uri, bindingSource, "Game.ViewModel", "ViewModel"));

        Assert.Multiple(() =>
        {
            Assert.That(elements, Does.Contain("Button"));
            Assert.That(members, Does.Contain("Text"));
            Assert.That(members, Does.Contain("Pressed"));
            Assert.That(enumValues, Does.Contain("Fill"));
            Assert.That(resources, Does.Contain("Accent"));
            Assert.That(classes, Does.Contain("hud"));
            Assert.That(bindings, Does.Contain("Score"));
            Assert.That(bindings, Does.Contain("Mode="));
            Assert.That(definition.AsArray(), Has.Count.EqualTo(1));
            Assert.That(definition[0]!["uri"]!.GetValue<string>(), Does.EndWith("ViewModel.cs"));
        });
    }

    private static string[] Labels(JsonNode completion) => completion["items"]!.AsArray().Select(item => item!["label"]!.GetValue<string>()).ToArray();

    private static JsonObject AtEnd(string uri, string source) => new()
    {
        ["textDocument"] = new JsonObject { ["uri"] = uri },
        ["position"] = Position(source, source.Length),
    };

    private static JsonObject At(string uri, string source, string scope, string word, string? newName = null)
    {
        var scopeIndex = source.IndexOf(scope, StringComparison.Ordinal);
        var index = source.IndexOf(word, scopeIndex, StringComparison.Ordinal);
        var result = new JsonObject { ["textDocument"] = new JsonObject { ["uri"] = uri }, ["position"] = Position(source, index + 1) };
        if (newName != null) result["newName"] = newName;
        return result;
    }

    private static JsonObject Position(string source, int index)
    {
        var prefix = source[..index];
        var line = prefix.Count(character => character == '\n');
        var newline = prefix.LastIndexOf('\n');
        return new JsonObject { ["line"] = line, ["character"] = newline < 0 ? index : index - newline - 1 };
    }

    private static void Write(Stream stream, JsonObject message)
    {
        var body = Encoding.UTF8.GetBytes(message.ToJsonString());
        var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        stream.Write(header);
        stream.Write(body);
    }

    private static JsonObject Read(Stream stream)
    {
        var header = new List<byte>();
        while (Encoding.ASCII.GetString(header.ToArray()).IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0) header.Add((byte)stream.ReadByte());
        var text = Encoding.ASCII.GetString(header.ToArray());
        var length = int.Parse(text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Single(line => line.StartsWith("Content-Length:", StringComparison.Ordinal))["Content-Length:".Length..]);
        var body = new byte[length];
        stream.ReadExactly(body);
        return JsonNode.Parse(body)!.AsObject();
    }
}