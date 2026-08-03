// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using XamlX;
using XamlX.Ast;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.Transform.Transformers;
using XamlX.TypeSystem;
using CecilTypeAttributes = Mono.Cecil.TypeAttributes;
using ReflectionTypeAttributes = System.Reflection.TypeAttributes;

namespace Forma.XamlSpike;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var outputDirectory = ParseOutputDirectory(args);
            _ = typeof(System.ComponentModel.TypeConverterAttribute).Assembly;
            _ = typeof(Uri).Assembly;
            _ = typeof(SpikeRoot).Assembly;
            var sre = new SpikeCompiler(new SreTypeSystem());
            VerifySreBuildAndPopulate(sre);
            VerifyCustomDiagnostic(sre);
            VerifyCecilEmission(outputDirectory);
            Console.WriteLine("Forma XAML feasibility: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string? ParseOutputDirectory(string[] args)
    {
        if (args.Length == 0)
            return null;
        if (args.Length == 2 && args[0] == "--emit")
            return Path.GetFullPath(args[1]);
        throw new ArgumentException("Usage: Forma.XamlSpike [--emit <output-directory>]");
    }

    private static void VerifySreBuildAndPopulate(SpikeCompiler compiler)
    {
        const string xaml = """
            <SpikeRoot xmlns='https://forma.dev/xaml'
                       Title='{Echo Value=Ready}'
                       Activated='OnActivated'>
                <SpikeLeaf Text='Child' />
            </SpikeRoot>
            """;

        var callbacks = compiler.CompileSre(xaml, generateBuildMethod: true);
        var built = RequireType<SpikeRoot>(callbacks.Create!(null), "SRE Build root");
        Require(built.Title == "Ready", "custom markup extension did not provide the title");
        Require(built.Children.Count == 1 && built.Children[0] is SpikeLeaf { Text: "Child" },
            "IAddChild<SpikeControl> did not add the child");
        built.RaiseActivated();
        Require(built.ActivationCount == 1, "CLR event handler was not attached");

        var existing = new SpikeRoot { ConstructorState = "retained" };
        var populate = compiler.CompileSre(xaml, generateBuildMethod: false);
        populate.Populate(null, existing);
        Require(existing.ConstructorState == "retained", "populate replaced constructor-initialized state");
        Require(existing.Title == "Ready" && existing.Children.Count == 1,
            "populate did not apply properties and children to the existing root");
        Console.WriteLine("SRE build/populate, IAddChild, events, and markup extensions: PASS");
    }

    private static void VerifyCustomDiagnostic(SpikeCompiler compiler)
    {
        const string invalid = """
            <SpikeRoot xmlns='https://forma.dev/xaml'>
                <SpikeLeaf Text='FORBIDDEN' />
            </SpikeRoot>
            """;

        try
        {
            compiler.Transform(invalid);
            throw new InvalidOperationException("custom diagnostic did not reject forbidden text");
        }
        catch (XamlTransformException exception)
        {
            Require(exception.Message.Contains("FXSP001", StringComparison.Ordinal),
                "custom diagnostic code was not preserved");
            Require(exception.LineNumber == 2 && exception.LinePosition > 0,
                $"custom diagnostic location was {exception.LineNumber}:{exception.LinePosition}");
        }

        Console.WriteLine("custom AST diagnostic with line/column: PASS");
    }

    private static void VerifyCecilEmission(string? outputDirectory)
    {
        var references = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")?.ToString()?.Split(Path.PathSeparator)
            ?? throw new InvalidOperationException("trusted platform assembly list unavailable");
        var runtime = typeof(SpikeRoot).Assembly.Location;
        using var typeSystem = new CecilTypeSystem(references.Append(runtime), targetPath: null);
        var compiler = new SpikeCompiler(typeSystem);
        var assembly = typeSystem.CreateAndRegisterAssembly("Forma.XamlSpike.Generated", new Version(1, 0), ModuleKind.Dll);
        var generatedType = new TypeDefinition(
            "Forma.XamlSpike.Generated",
            "GeneratedView",
            CecilTypeAttributes.Class | CecilTypeAttributes.Public,
            assembly.MainModule.TypeSystem.Object);
        var contextType = new TypeDefinition(
            "Forma.XamlSpike.Generated",
            "GeneratedViewContext",
            CecilTypeAttributes.Class | CecilTypeAttributes.NotPublic,
            assembly.MainModule.TypeSystem.Object);
        assembly.MainModule.Types.Add(generatedType);
        assembly.MainModule.Types.Add(contextType);

        const string xaml = "<SpikeRoot xmlns='https://forma.dev/xaml'><SpikeLeaf Text='Cecil' /></SpikeRoot>";
        compiler.CompileCecil(xaml, typeSystem, generatedType, contextType, "SpikeView.xaml");

        using var assemblyStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        assembly.Write(assemblyStream, new WriterParameters
        {
            WriteSymbols = true,
            SymbolStream = pdbStream,
            SymbolWriterProvider = new PortablePdbWriterProvider(),
        });

        Require(assemblyStream.Length > 0, "Cecil assembly was empty");
        Require(pdbStream.Length > 0, "portable PDB was empty");
        Require(generatedType.Methods.Any(method => method.Name == "Build"), "Cecil Build method was not emitted");
        Require(generatedType.Methods.Any(method => method.Name == "Populate"), "Cecil Populate method was not emitted");
        var forbiddenReference = assembly.MainModule.AssemblyReferences.FirstOrDefault(reference =>
            reference.Name is "XamlX" or "XamlX.IL.Cecil" or "Mono.Cecil");
        Require(forbiddenReference == null,
            $"generated assembly references compiler dependency {forbiddenReference?.Name}");

        if (outputDirectory != null)
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllBytes(Path.Combine(outputDirectory, "Forma.XamlSpike.Generated.dll"), assemblyStream.ToArray());
            File.WriteAllBytes(Path.Combine(outputDirectory, "Forma.XamlSpike.Generated.pdb"), pdbStream.ToArray());
            Console.WriteLine($"Generated fixture: {outputDirectory}");
        }

        Console.WriteLine("Cecil IL and portable PDB emission: PASS");
    }

    private static T RequireType<T>(object value, string description)
    {
        if (value is T typed)
            return typed;
        throw new InvalidOperationException($"{description} was {value?.GetType().FullName ?? "null"}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

internal sealed class SpikeCompiler
{
    public const string XmlNamespace = "https://forma.dev/xaml";
    private readonly IXamlTypeSystem _typeSystem;
    private readonly TransformerConfiguration _configuration;

    public SpikeCompiler(IXamlTypeSystem typeSystem)
    {
        _typeSystem = typeSystem;
        _configuration = new TransformerConfiguration(
            typeSystem,
            typeSystem.FindAssembly(typeof(SpikeRoot).Assembly.GetName().Name!),
            new XamlLanguageTypeMappings(typeSystem)
            {
                XmlnsAttributes = { typeSystem.GetType(typeof(XmlnsDefinitionAttribute).FullName!) },
                IAddChild = typeSystem.GetType(typeof(IAddChild).FullName!),
                IAddChildOfT = typeSystem.GetType(typeof(IAddChild<>).FullName!),
            });
    }

    public (Func<IServiceProvider?, object>? Create, Action<IServiceProvider?, object> Populate) CompileSre(
        string xaml,
        bool generateBuildMethod)
    {
        if (_typeSystem is not SreTypeSystem typeSystem)
            throw new InvalidOperationException("SRE compilation requires SreTypeSystem");

        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Forma.XamlSpike.Generated.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Forma.XamlSpike.Generated.dll");
        var generated = module.DefineType($"GeneratedView_{Guid.NewGuid():N}", ReflectionTypeAttributes.Public);
        var context = module.DefineType($"{generated.Name}Context", ReflectionTypeAttributes.NotPublic);
        var compiler = CreateCompiler();
        var contextType = compiler.CreateContextType(typeSystem.CreateTypeBuilder(context));
        var generatedBuilder = typeSystem.CreateTypeBuilder(generated);
        var document = ParseAndTransform(compiler, xaml);
        compiler.Compile(document, generatedBuilder, contextType, "Populate", generateBuildMethod ? "Build" : null,
            "XamlNamespaceInfo", XmlNamespace, new StringFileSource("SpikeView.xaml", xaml));
        var runtimeType = generated.CreateType()!;

        Func<IServiceProvider?, object>? create = null;
        if (generateBuildMethod)
        {
            var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
            create = Expression.Lambda<Func<IServiceProvider?, object>>(
                Expression.Convert(Expression.Call(runtimeType.GetMethod("Build")!, serviceProvider), typeof(object)),
                serviceProvider).Compile();
        }

        var target = Expression.Parameter(typeof(object));
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var populateMethod = runtimeType.GetMethod("Populate")!;
        var populate = Expression.Lambda<Action<IServiceProvider?, object>>(
            Expression.Call(populateMethod, provider, Expression.Convert(target, populateMethod.GetParameters()[1].ParameterType)),
            provider,
            target).Compile();
        return (create, populate);
    }

    public void Transform(string xaml)
    {
        ParseAndTransform(CreateCompiler(), xaml);
    }

    public void CompileCecil(
        string xaml,
        CecilTypeSystem typeSystem,
        TypeDefinition generated,
        TypeDefinition context,
        string filePath)
    {
        var compiler = CreateCompiler();
        var contextType = compiler.CreateContextType(typeSystem.CreateTypeBuilder(context));
        var document = ParseAndTransform(compiler, xaml);
        compiler.Compile(document, typeSystem.CreateTypeBuilder(generated), contextType, "Populate", "Build",
            "XamlNamespaceInfo", XmlNamespace, new StringFileSource(filePath, xaml));
    }

    private XamlILCompiler CreateCompiler()
    {
        var compiler = new XamlILCompiler(
            _configuration,
            new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>(),
            fillWithDefaults: true)
        {
            EnableIlVerification = true,
        };
        var insertionIndex = compiler.Transformers.FindIndex(transformer => transformer is TypeReferenceResolver);
        compiler.Transformers.Insert(insertionIndex, new ForbiddenTextTransformer());
        return compiler;
    }

    private static XamlDocument ParseAndTransform(XamlILCompiler compiler, string xaml)
    {
        var document = XDocumentXamlParser.Parse(xaml);
        compiler.Transform(document);
        return document;
    }
}

internal sealed class ForbiddenTextTransformer : IXamlAstTransformer
{
    public IXamlAstNode Transform(AstTransformationContext context, IXamlAstNode node)
    {
        if (node is XamlAstTextNode { Text: "FORBIDDEN" } text)
            throw new XamlTransformException("FXSP001: forbidden spike value", text);
        return node;
    }
}

internal sealed class StringFileSource : IFileSource
{
    public StringFileSource(string filePath, string contents)
    {
        FilePath = filePath;
        FileContents = Encoding.UTF8.GetBytes(contents);
    }

    public string FilePath { get; }
    public byte[] FileContents { get; }
}