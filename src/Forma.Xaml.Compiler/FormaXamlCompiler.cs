using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using Forma.Xaml;
using Mono.Cecil;
using XamlX;
using XamlX.Ast;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.Transform.Transformers;
using XamlX.TypeSystem;

namespace Forma.Xaml.Compiler;

public sealed record FormaCompiledCallbacks(Func<IServiceProvider?, object> Build, Action<IServiceProvider?, object> Populate);

public sealed class FormaXamlCompiler
{
    private static readonly Regex BuildDirectivePattern = new("\\s+x:(?:Class|DataType)\\s*=\\s*(['\\\"]).*?\\1", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex NameDirectivePattern = new("x:Name(?=\\s*=)", RegexOptions.CultureInvariant);
    private static readonly Regex BindingAttributePattern = new("\\s+[A-Za-z_][A-Za-z0-9_.]*\\s*=\\s*(['\\\"])\\{Binding.*?\\}\\1", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private readonly IXamlTypeSystem _typeSystem;
    private readonly TransformerConfiguration _configuration;
    private readonly FormaXamlParser _parser = new();

    public FormaXamlCompiler(IXamlTypeSystem typeSystem, string defaultAssemblyName)
    {
        _typeSystem = typeSystem;
        var mappings = new XamlLanguageTypeMappings(typeSystem)
        {
            XmlnsAttributes = { typeSystem.GetType(typeof(XmlnsDefinitionAttribute).FullName!) },
            IAddChild = typeSystem.GetType(typeof(IAddChild).FullName!),
            IAddChildOfT = typeSystem.GetType(typeof(IAddChild<>).FullName!),
        };
        _configuration = new TransformerConfiguration(
            typeSystem,
            typeSystem.FindAssembly(defaultAssemblyName),
            mappings,
            customValueConverter: ConvertValue);
        _configuration.KnownDirectives.Add((XamlNamespaces.Xaml2006, "Class"));
        _configuration.KnownDirectives.Add((XamlNamespaces.Xaml2006, "Name"));
        _configuration.KnownDirectives.Add((XamlNamespaces.Xaml2006, "Key"));
        _configuration.KnownDirectives.Add((XamlNamespaces.Xaml2006, "DataType"));
    }

    public static FormaXamlCompiler CreateSre(string? defaultAssemblyName = null)
    {
        _ = typeof(Control).Assembly;
        _ = typeof(System.ComponentModel.TypeConverterAttribute).Assembly;
        _ = typeof(Uri).Assembly;
        return new FormaXamlCompiler(new SreTypeSystem(), defaultAssemblyName ?? typeof(Control).Assembly.GetName().Name!);
    }

    public FormaXamlParseResult Parse(string source, string sourcePath, FormaXamlParseOptions? options = null) =>
        _parser.Parse(source, sourcePath, options);

    public FormaCompiledCallbacks CompileSre(string source, string sourcePath, FormaXamlParseOptions? options = null)
    {
        if (_typeSystem is not SreTypeSystem typeSystem) throw new InvalidOperationException("SRE compilation requires SreTypeSystem.");
        RequireSemanticSuccess(source, sourcePath, options);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"Forma.Xaml.Generated.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Forma.Xaml.Generated.dll");
        var generated = module.DefineType($"GeneratedView_{Guid.NewGuid():N}", System.Reflection.TypeAttributes.Public);
        var context = module.DefineType($"{generated.Name}Context", System.Reflection.TypeAttributes.NotPublic);
        var compiler = CreateCompiler();
        var contextType = compiler.CreateContextType(typeSystem.CreateTypeBuilder(context));
        var generatedBuilder = typeSystem.CreateTypeBuilder(generated);
        var document = ParseAndTransform(compiler, PrepareForEmission(source));
        compiler.Compile(document, generatedBuilder, contextType, "Populate", "Build", "XamlNamespaceInfo", XamlNamespaces.Forma, new StringFileSource(sourcePath, source));
        var runtimeType = generated.CreateType()!;
        var provider = Expression.Parameter(typeof(IServiceProvider));
        var target = Expression.Parameter(typeof(object));
        var buildMethod = runtimeType.GetMethod("Build")!;
        var populateMethod = runtimeType.GetMethod("Populate")!;
        var emittedBuild = Expression.Lambda<Func<IServiceProvider?, object>>(
            Expression.Convert(Expression.Call(buildMethod, provider), typeof(object)), provider).Compile();
        var populate = Expression.Lambda<Action<IServiceProvider?, object>>(
            Expression.Call(populateMethod, provider, Expression.Convert(target, populateMethod.GetParameters()[1].ParameterType)), provider, target).Compile();
        object Build(IServiceProvider? serviceProvider)
        {
            var value = emittedBuild(serviceProvider);
            if (value is Control control) NameScope.CreateForTree(control);
            return value;
        }
        return new FormaCompiledCallbacks(Build, populate);
    }

    public void CompileCecil(string source, string sourcePath, CecilTypeSystem typeSystem, TypeDefinition generatedType, TypeDefinition contextType, FormaXamlParseOptions? options = null)
    {
        RequireSemanticSuccess(source, sourcePath, options);
        var compiler = CreateCompiler();
        var contextBuilder = compiler.CreateContextType(typeSystem.CreateTypeBuilder(contextType));
        var document = ParseAndTransform(compiler, PrepareForEmission(source));
        compiler.Compile(document, typeSystem.CreateTypeBuilder(generatedType), contextBuilder, "Populate", "Build", "XamlNamespaceInfo", XamlNamespaces.Forma, new StringFileSource(sourcePath, source));
    }

    private void RequireSemanticSuccess(string source, string sourcePath, FormaXamlParseOptions? options)
    {
        var result = Parse(source, sourcePath, options);
        if (!result.Success) throw new FormaXamlCompilationException(result.Diagnostics);
    }

    private XamlILCompiler CreateCompiler()
    {
        var compiler = new XamlILCompiler(_configuration, new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>(), true)
        {
            EnableIlVerification = true,
        };
        var directiveIndex = compiler.Transformers.FindIndex(transformer => transformer is KnownDirectivesTransformer);
        compiler.Transformers.Insert(directiveIndex + 1, new FormaDirectiveTransformer());
        var constructionIndex = compiler.Transformers.FindIndex(transformer => transformer is ConstructableObjectTransformer);
        compiler.Transformers.Insert(constructionIndex, new FormaDirectiveTransformer());
        compiler.SimplificationTransformers.Insert(0, new FormaDirectiveTransformer());
        compiler.Emitters.Insert(0, new FormaDirectiveEmitter());
        return compiler;
    }

    private static XamlDocument ParseAndTransform(XamlILCompiler compiler, string source)
    {
        var document = XDocumentXamlParser.Parse(source);
        compiler.Transform(document);
        document.Root = document.Root.Visit(new RemoveDirectivesVisitor());
        return document;
    }

    private static string PrepareForEmission(string source)
    {
        var withoutBuildDirectives = BuildDirectivePattern.Replace(source, match =>
            new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()));
        var withoutBindings = BindingAttributePattern.Replace(withoutBuildDirectives, match =>
            new string(match.Value.Select(character => character is '\r' or '\n' ? character : ' ').ToArray()));
        return NameDirectivePattern.Replace(withoutBindings, "  Name");
    }

    private static bool ConvertValue(AstTransformationContext context, IXamlAstValueNode node, IXamlType type, out IXamlAstValueNode result)
    {
        result = null!;
        if (node is not XamlAstTextNode) return false;
        var methodName = type.FullName switch
        {
            "Microsoft.Xna.Framework.Color" => nameof(XamlValueConverter.ParseColor),
            "Microsoft.Xna.Framework.Vector2" => nameof(XamlValueConverter.ParseVector2),
            "Forma.Thickness" => nameof(XamlValueConverter.ParseThickness),
            _ => null,
        };
        if (methodName == null) return false;
        var converterType = context.Configuration.TypeSystem.GetType(typeof(XamlValueConverter).FullName!);
        var method = converterType.Methods.First(candidate => candidate.IsPublic && candidate.IsStatic && candidate.Name == methodName);
        result = new XamlStaticOrTargetedReturnMethodCallNode(node, method, new[] { node });
        return true;
    }

    private sealed class StringFileSource : IFileSource
    {
        public StringFileSource(string filePath, string source) { FilePath = filePath; FileContents = Encoding.UTF8.GetBytes(source); }
        public string FilePath { get; }
        public byte[] FileContents { get; }
    }

    private sealed class RemoveDirectivesVisitor : IXamlAstVisitor
    {
        public IXamlAstNode Visit(IXamlAstNode node) => node is XamlAstXmlDirective directive ? new XamlManipulationGroupNode(directive) : node;
        public void Push(IXamlAstNode node) { }
        public void Pop() { }
    }
}

internal sealed class FormaDirectiveTransformer : IXamlAstTransformer
{
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal) { "Class", "Name", "Key", "DataType" };

    public IXamlAstNode Transform(AstTransformationContext context, IXamlAstNode node)
    {
        if (node is XamlAstXmlDirective directive && directive.Namespace == XamlNamespaces.Xaml2006 && !Supported.Contains(directive.Name))
            throw new XamlTransformException($"{FormaDiagnosticCodes.UnsupportedDirective}: directive x:{directive.Name} is not supported", directive);
        if (node is XamlAstXmlDirective { Namespace: XamlNamespaces.Xaml2006, Name: "Class" or "DataType" } consumed)
            return new XamlManipulationGroupNode(consumed);
        return node;
    }
}

internal sealed class FormaDirectiveEmitter : IXamlILAstNodeEmitter
{
    public XamlILNodeEmitResult? Emit(IXamlAstNode node, XamlEmitContext<IXamlILEmitter, XamlILNodeEmitResult> context, IXamlILEmitter codeGen) =>
        node is XamlAstXmlDirective ? XamlILNodeEmitResult.Void(0) : null;
}