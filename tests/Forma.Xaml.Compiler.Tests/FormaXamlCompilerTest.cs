// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml.Compiler;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using XamlX.TypeSystem;

namespace Forma.Xaml.Compiler.Tests;

public class FormaXamlCompilerTest
{
    [Test]
    public void Parser_ProducesNormalizedSemanticDocument()
    {
        const string source = """
            <Control xmlns="https://forma.dev/xaml"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     x:Class="Game.Hud"
                     x:DataType="Game.HudModel"
                     x:Name="Root"
                     Position="1,2" />
            """;
        var result = new FormaXamlParser().Parse(source, "Views/Hud.xaml", new FormaXamlParseOptions { RequireCompiledBindings = true });
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Document!.RootClass, Is.EqualTo("Game.Hud"));
            Assert.That(result.Document.Root.FindDirective("Name"), Is.EqualTo("Root"));
            Assert.That(result.Document.Root.FindMember("Position"), Is.EqualTo("1,2"));
        });
    }

    [Test]
    public void Parser_ReportsStableSemanticDiagnostics()
    {
        const string source = """
            <Control xmlns="https://forma.dev/xaml" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Text="{Binding Value}">
                <Control x:Name="Duplicate" />
                <Control x:Name="Duplicate" x:Uid="unsupported" />
                <Style Selector="Control Control" />
                <Vector2Timeline TargetName="Missing" Property="Position" />
            </Control>
            """;
        var result = new FormaXamlParser().Parse(source, "Invalid.xaml", new FormaXamlParseOptions { RequireCompiledBindings = true });
        var codes = result.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(codes, Does.Contain(FormaDiagnosticCodes.UnsupportedDirective));
            Assert.That(codes, Does.Contain(FormaDiagnosticCodes.DuplicateName));
            Assert.That(codes, Does.Contain(FormaDiagnosticCodes.CompiledBinding));
            Assert.That(codes, Does.Contain(FormaDiagnosticCodes.Selector));
            Assert.That(codes, Does.Contain(FormaDiagnosticCodes.Storyboard));
            Assert.That(result.Diagnostics, Has.All.Matches<FormaDiagnostic>(diagnostic => diagnostic.Location.Line > 0 && diagnostic.Location.Column > 0));
        });
    }

    [Test]
    public void Parser_NormalizesCanonicalBindingStyleTriggerAndStoryboardShapes()
    {
        const string source = """
            <Control xmlns="https://forma.dev/xaml"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                     x:Class="Game.Hud"
                     x:DataType="Game.HudModel"
                     x:Name="Root">
                <ResourceDictionary>
                    <Style x:Key="TimerStyle" Selector="Control.low-time" />
                    <Storyboard x:Key="Pulse" AutoReverse="True" RepeatBehavior="Forever">
                        <Vector2Timeline TargetName="Timer" Property="Position">
                            <KeyFrame Time="0:0:0" Value="0,0" />
                            <KeyFrame Time="0:0:1" Value="10,0" Easing="CubicInOut" />
                        </Vector2Timeline>
                    </Storyboard>
                </ResourceDictionary>
                <Control x:Name="Timer" Position="{Binding Offset, Mode=OneWay}" />
                <PropertyTrigger Binding="{Binding IsLowTime}" Value="True" />
                <EventTrigger SourceName="Root" Event="Attached" />
            </Control>
            """;
        var result = new FormaXamlParser().Parse(source, "Views/Hud.xaml", new FormaXamlParseOptions { RequireCompiledBindings = true });
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Document!.DescendantsAndSelf().Any(node => node.TypeName == "Style"), Is.True);
            Assert.That(result.Document.DescendantsAndSelf().Any(node => node.TypeName == "Storyboard"), Is.True);
            Assert.That(result.Document.DescendantsAndSelf().Any(node => node.TypeName == "PropertyTrigger"), Is.True);
        });
    }

    [Test]
    public void Parser_ReportsDuplicateAndMissingStaticResources()
    {
        const string source = """
            <Control xmlns="https://forma.dev/xaml" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Control.Resources>
                    <ResourceDictionary>
                        <ResourceDictionary x:Key="Duplicate" />
                        <ResourceDictionary x:Key="Duplicate" />
                    </ResourceDictionary>
                </Control.Resources>
                <Control DataContext="{StaticResource Missing}" />
            </Control>
            """;
        var result = new FormaXamlParser().Parse(source, "InvalidResources.xaml");
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics.Count(diagnostic => diagnostic.Code == FormaDiagnosticCodes.Resource), Is.EqualTo(2));
            Assert.That(result.Diagnostics, Has.Some.Matches<FormaDiagnostic>(diagnostic => diagnostic.Message.Contains("duplicated", StringComparison.Ordinal)));
            Assert.That(result.Diagnostics, Has.Some.Matches<FormaDiagnostic>(diagnostic => diagnostic.Message.Contains("was not found", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Parser_ReportsMalformedStyleSetter()
    {
        const string source = """
            <Control xmlns="https://forma.dev/xaml">
                <Style Selector="Control"><Setter Property="TooltipText" /></Style>
            </Control>
            """;
        var result = new FormaXamlParser().Parse(source, "InvalidStyle.xaml");
        Assert.That(result.Diagnostics, Has.Some.Matches<FormaDiagnostic>(diagnostic =>
            diagnostic.Code == FormaDiagnosticCodes.Selector && diagnostic.Message.Contains("Property and Value", StringComparison.Ordinal)));
    }

    [Test]
    public void SreCompiler_BuildsFormaTreeAndUsesTypedConverter()
    {
        const string source = "<Control xmlns='https://forma.dev/xaml' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:Class='Game.View' Position='12,34'><Control Name='Child' /></Control>";
        var callbacks = FormaXamlCompiler.CreateSre().CompileSre(source, "SreView.xaml");
        var root = (Control)callbacks.Build(null);
        Assert.Multiple(() =>
        {
            Assert.That(root.Position, Is.EqualTo(new Vector2(12, 34)));
            Assert.That(root.Children, Has.Count.EqualTo(1));
            Assert.That(root.Children[0].Name, Is.EqualTo("Child"));
        });
    }

    [Test]
    public void CecilCompiler_ConsumesClassDirectiveAndEmitsMethods()
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!.ToString()!.Split(Path.PathSeparator).Append(typeof(Control).Assembly.Location);
        using var typeSystem = new CecilTypeSystem(references, null);
        var assembly = typeSystem.CreateAndRegisterAssembly("Forma.Xaml.Compiler.TestOutput", new Version(1, 0), ModuleKind.Dll);
        var generated = new TypeDefinition("Generated", "View", TypeAttributes.Class | TypeAttributes.Public, assembly.MainModule.TypeSystem.Object);
        var context = new TypeDefinition("Generated", "ViewContext", TypeAttributes.Class | TypeAttributes.NotPublic, assembly.MainModule.TypeSystem.Object);
        assembly.MainModule.Types.Add(generated);
        assembly.MainModule.Types.Add(context);
        const string source = "<Control xmlns='https://forma.dev/xaml' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:Class='Game.View' Position='12,34' />";
        new FormaXamlCompiler(typeSystem, typeof(Control).Assembly.GetName().Name!).CompileCecil(source, "CecilView.xaml", typeSystem, generated, context);
        Assert.That(generated.Methods.Select(method => method.Name), Does.Contain("Populate"));
    }

    [Test]
    public void BuildTask_EmitsTypedAdvancedConstructCallsWithoutReflectionFallback()
    {
        var testDirectory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        var configuration = testDirectory.Parent!.Name;
        var runtime = testDirectory.Parent.Parent!.Name;
        var repository = testDirectory;
        while (repository != null && !File.Exists(Path.Combine(repository.FullName, "Directory.Build.props")))
            repository = repository.Parent;
        Assert.That(repository, Is.Not.Null, "Could not locate the Forma repository root.");
        var fixturePath = Path.Combine(
            repository!.FullName,
            "tests",
            "Forma.Xaml.Build.Integration",
            "bin",
            runtime,
            configuration,
            "net10.0",
            "Forma.Xaml.Build.Integration.dll");
        Assert.That(File.Exists(fixturePath), Is.True, $"Injected fixture was not built at '{fixturePath}'.");

        using var fixture = AssemblyDefinition.ReadAssembly(fixturePath);
        var calls = fixture.MainModule.Types
            .SelectMany(AllTypes)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<MethodReference>()
            .Select(method => $"{method.DeclaringType.FullName}.{method.Name}")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(calls, Does.Contain("Forma.Xaml.ResourceDictionary.Add"));
            Assert.That(calls, Does.Contain("Forma.Xaml.Style.AddSetter"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledBinding.AttachOneWay"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledBinding.AttachTwoWay"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledEvent.Attach"));
            Assert.That(calls, Does.Contain("Forma.Xaml.Storyboard.AddTimeline"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledStoryboardTrigger.AttachEvent"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledStoryboardTrigger.AttachProperty"));
            Assert.That(calls, Does.Contain("Forma.Xaml.CompiledStoryboardTrigger.AttachStopEvent"));
            Assert.That(calls, Has.None.StartsWith("System.Reflection."));
        });
    }

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }
}