using Forma.Xaml.Compiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using XamlX.TypeSystem;

namespace Forma.Xaml.Build;

public abstract class FormaXamlTask : Microsoft.Build.Utilities.Task
{
    protected bool LogDiagnostics(IEnumerable<FormaDiagnostic> diagnostics)
    {
        var success = true;
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Location;
            if (diagnostic.Severity == FormaDiagnosticSeverity.Error)
            {
                Log.LogError("Forma XAML", diagnostic.Code, null, location.FilePath, location.Line, location.Column, location.EndLine, location.EndColumn, diagnostic.Message);
                success = false;
            }
            else if (diagnostic.Severity == FormaDiagnosticSeverity.Warning)
                Log.LogWarning("Forma XAML", diagnostic.Code, null, location.FilePath, location.Line, location.Column, location.EndLine, location.EndColumn, diagnostic.Message);
            else
                Log.LogMessage(MessageImportance.Normal, diagnostic.ToString());
        }
        return success;
    }

    protected static string ReadSource(ITaskItem item) => File.ReadAllText(item.GetMetadata("FullPath"));
}

public sealed class DiscoverFormaXaml : FormaXamlTask
{
    [Required] public ITaskItem[] XamlFiles { get; set; } = [];
    public bool RequireCompiledBindings { get; set; }
    [Output] public ITaskItem[] Roots { get; private set; } = [];

    public override bool Execute()
    {
        var parser = new FormaXamlParser();
        var roots = new List<ITaskItem>();
        var success = true;
        foreach (var file in XamlFiles)
        {
            var result = parser.Parse(ReadSource(file), file.ItemSpec, new FormaXamlParseOptions { RequireCompiledBindings = RequireCompiledBindings });
            success &= LogDiagnostics(result.Diagnostics);
            if (result.Document == null) continue;
            var root = new TaskItem(file);
            root.SetMetadata("RootClass", result.Document.RootClass ?? string.Empty);
            roots.Add(root);
        }

        foreach (var duplicate in roots.Where(root => !string.IsNullOrWhiteSpace(root.GetMetadata("RootClass"))).GroupBy(root => root.GetMetadata("RootClass"), StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            foreach (var item in duplicate)
                Log.LogError("Forma XAML", FormaDiagnosticCodes.DuplicateRootClass, null, item.ItemSpec, 1, 1, 0, 0, $"Multiple implicit XAML files target root class '{duplicate.Key}'.");
            success = false;
        }
        Roots = roots.ToArray();
        return success;
    }
}

public sealed class CompileFormaXaml : FormaXamlTask
{
    [Required] public ITaskItem[] XamlFiles { get; set; } = [];
    [Required] public string TargetAssembly { get; set; } = string.Empty;
    public string? TargetPdb { get; set; }
    public ITaskItem[] References { get; set; } = [];
    public bool RequireCompiledBindings { get; set; }
    public bool ValidateOnly { get; set; }

    public override bool Execute()
    {
        var parser = new FormaXamlParser();
        var documents = new List<(ITaskItem Item, string Source, FormaXamlDocument Document)>();
        var success = true;
        foreach (var file in XamlFiles)
        {
            var source = ReadSource(file);
            var result = parser.Parse(source, file.ItemSpec, new FormaXamlParseOptions { RequireCompiledBindings = RequireCompiledBindings });
            success &= LogDiagnostics(result.Diagnostics);
            if (result.Document != null) documents.Add((file, source, result.Document));
        }
        foreach (var duplicate in documents.Where(item => item.Document.RootClass != null).GroupBy(item => item.Document.RootClass!, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            foreach (var item in duplicate)
                Log.LogError("Forma XAML", FormaDiagnosticCodes.DuplicateRootClass, null, item.Item.ItemSpec, 1, 1, 0, 0, $"Multiple implicit XAML files target root class '{duplicate.Key}'.");
            success = false;
        }
        if (!success || ValidateOnly || documents.Count == 0) return success;

        try
        {
            var referencePaths = References.Select(reference => reference.GetMetadata("FullPath"))
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Append(TargetAssembly)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            using var typeSystem = new CecilTypeSystem(referencePaths, TargetAssembly);
            var assembly = typeSystem.TargetAssemblyDefinition;
            var module = assembly.MainModule;
            var compiler = new FormaXamlCompiler(typeSystem, assembly.Name.Name);
            foreach (var item in documents)
                CompileDocument(compiler, typeSystem, module, item.Source, item.Item.ItemSpec, item.Document);
            var writeSymbols = !string.IsNullOrWhiteSpace(TargetPdb) && File.Exists(TargetPdb);
            assembly.Write(TargetAssembly, new WriterParameters
            {
                WriteSymbols = writeSymbols,
                SymbolWriterProvider = writeSymbols ? new PortablePdbWriterProvider() : null,
            });
            Log.LogMessage(MessageImportance.High, $"Compiled {documents.Count} Forma XAML document(s) into {TargetAssembly}.");
            return true;
        }
        catch (FormaXamlCompilationException exception)
        {
            return LogDiagnostics(exception.Diagnostics);
        }
        catch (Exception exception)
        {
            Log.LogError("Forma XAML", FormaDiagnosticCodes.Emission, null, TargetAssembly, 1, 1, 0, 0, exception.ToString());
            return false;
        }
    }

    private static void CompileDocument(FormaXamlCompiler compiler, CecilTypeSystem typeSystem, ModuleDefinition module, string source, string sourcePath, FormaXamlDocument document)
    {
        var suffix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourcePath))).Substring(0, 16);
        var generatedType = new TypeDefinition("Forma.Xaml.Generated", $"View_{suffix}", TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        var contextType = new TypeDefinition("Forma.Xaml.Generated", $"View_{suffix}Context", TypeAttributes.Class | TypeAttributes.NotPublic, module.TypeSystem.Object);
        module.Types.Add(generatedType);
        module.Types.Add(contextType);
        compiler.CompileCecil(source, sourcePath, typeSystem, generatedType, contextType, new FormaXamlParseOptions());
        if (document.RootClass == null) return;
        var rootType = FindType(module, document.RootClass) ?? throw new InvalidOperationException($"x:Class type '{document.RootClass}' was not found in {module.Assembly.Name.Name}.");
        RegisterPopulate(typeSystem, module, generatedType, rootType, document);
    }

    private static TypeDefinition? FindType(ModuleDefinition module, string fullName) =>
        module.Types.SelectMany(AllTypes).FirstOrDefault(type => type.FullName.Replace('/', '+') == fullName || type.FullName == fullName);

    private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(AllTypes)) yield return nested;
    }

    private static void RegisterPopulate(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, TypeDefinition rootType, FormaXamlDocument document)
    {
        var formaReference = module.AssemblyReferences.Single(reference => reference.Name == "Forma");
        var formaAssembly = typeSystem.Resolve(formaReference);
        var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
        var loaderType = formaAssembly.MainModule.GetType("Forma.Xaml.FormaXamlLoader");
        var generatedPopulate = generatedType.Methods.Single(method => method.Name == "Populate");
        var serviceProvider = module.ImportReference(typeof(IServiceProvider));
        var wrapper = new MethodDefinition("__Populate", MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig, module.TypeSystem.Void);
        wrapper.Parameters.Add(new ParameterDefinition("serviceProvider", ParameterAttributes.None, serviceProvider));
        wrapper.Parameters.Add(new ParameterDefinition("instance", ParameterAttributes.None, rootType));
        generatedType.Methods.Add(wrapper);
        var il = wrapper.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, generatedPopulate);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "CreateForTree")));
        il.Emit(OpCodes.Pop);
        EmitBindings(typeSystem, module, generatedType, wrapper, rootType, document);
        il.Emit(OpCodes.Ret);

        var actionDefinition = module.ImportReference(typeof(Action<,>));
        var actionType = new GenericInstanceType(actionDefinition);
        actionType.GenericArguments.Add(serviceProvider);
        actionType.GenericArguments.Add(rootType);
        var actionConstructor = new MethodReference(".ctor", module.TypeSystem.Void, actionType) { HasThis = true };
        actionConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        actionConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
        var register = new GenericInstanceMethod(module.ImportReference(loaderType.Methods.Single(method => method.Name == "RegisterPopulate")));
        register.GenericArguments.Add(rootType);

        var moduleType = module.Types.Single(type => type.Name == "<Module>");
        var initializer = moduleType.Methods.FirstOrDefault(method => method.Name == ".cctor");
        if (initializer == null)
        {
            initializer = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            initializer.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            moduleType.Methods.Add(initializer);
        }
        var processor = initializer.Body.GetILProcessor();
        var insertion = initializer.Body.Instructions[0];
        processor.InsertBefore(insertion, Instruction.Create(OpCodes.Ldnull));
        processor.InsertBefore(insertion, Instruction.Create(OpCodes.Ldftn, wrapper));
        processor.InsertBefore(insertion, Instruction.Create(OpCodes.Newobj, actionConstructor));
        processor.InsertBefore(insertion, Instruction.Create(OpCodes.Call, register));
    }

    private static void EmitBindings(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, TypeDefinition rootType, FormaXamlDocument document)
    {
        var bindingNodes = document.DescendantsAndSelf()
            .SelectMany(node => node.Members.Where(member => !member.IsDirective && member.Value.StartsWith("{Binding", StringComparison.Ordinal)).Select(member => (Node: node, Member: member)))
            .ToArray();
        if (bindingNodes.Length == 0) return;
        var sourceType = ResolveDataType(typeSystem, module, document)
            ?? throw new InvalidOperationException("Compiled binding emission requires a resolvable x:DataType.");
        var formaAssembly = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
        var controlType = formaAssembly.MainModule.GetType("Forma.Control");
        var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
        var compiledBindingType = formaAssembly.MainModule.GetType("Forma.Xaml.CompiledBinding");
        var bindingAdaptersType = formaAssembly.MainModule.GetType("Forma.Xaml.BindingTargetAdapters");
        var findOrdinal = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByOrdinal" && method.IsPublic && method.Parameters.Count == 2));
        var findName = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByName" && method.IsPublic && method.Parameters.Count == 2));
        var attachDefinition = module.ImportReference(compiledBindingType.Methods.Single(method => method.Name == "AttachOneWay"));
        var attachTwoWayDefinition = module.ImportReference(compiledBindingType.Methods.Single(method => method.Name == "AttachTwoWay"));
        var controls = document.DescendantsAndSelf().Where(node => IsControl(ResolveObjectType(typeSystem, module, node, document), controlType)).ToArray();
        var body = wrapper.Body.GetILProcessor();

        for (var index = 0; index < bindingNodes.Length; index++)
        {
            var binding = bindingNodes[index];
            var targetOrdinal = Array.IndexOf(controls, binding.Node);
            if (targetOrdinal < 0) throw new InvalidOperationException($"Binding target '{binding.Node.TypeName}' is not a Control.");
            var targetType = ResolveObjectType(typeSystem, module, binding.Node, document)
                ?? throw new InvalidOperationException($"Binding target type '{binding.Node.TypeName}' was not resolved.");
            var targetProperty = FindProperty(targetType, binding.Member.Name)
                ?? throw new InvalidOperationException($"Binding target property '{targetType.FullName}.{binding.Member.Name}' was not found.");
            var path = ParseBindingPath(binding.Member.Value);
            var sourceProperty = FindProperty(sourceType, path)
                ?? throw new InvalidOperationException($"Binding source property '{sourceType.FullName}.{path}' was not found.");
            if (sourceProperty.PropertyType.FullName != targetProperty.PropertyType.FullName)
                throw new InvalidOperationException($"Binding '{path}' type '{sourceProperty.PropertyType.FullName}' is incompatible with '{binding.Member.Name}' type '{targetProperty.PropertyType.FullName}'.");
            if (sourceProperty.GetMethod == null || targetProperty.GetMethod == null || targetProperty.SetMethod == null)
                throw new InvalidOperationException($"Binding '{path}' requires readable source and readable/writable target properties.");

            var valueType = module.ImportReference(sourceProperty.PropertyType);
            var read = DefineSourceGetter(module, generatedType, index, sourceType, sourceProperty, valueType);
            var getTarget = DefineTargetGetter(module, generatedType, index, targetType, targetProperty, valueType);
            var setTarget = DefineTargetSetter(module, generatedType, index, targetType, targetProperty, valueType);
            var funcSource = MakeDelegateType(module, typeof(Func<,>), module.ImportReference(sourceType), valueType);
            var funcTarget = MakeDelegateType(module, typeof(Func<,>), module.TypeSystem.Object, valueType);
            var actionTarget = MakeDelegateType(module, typeof(Action<,>), module.TypeSystem.Object, valueType);
            var twoWay = ParseBindingOption(binding.Member.Value, "Mode") == "TwoWay";
            var attach = new GenericInstanceMethod(twoWay ? attachTwoWayDefinition : attachDefinition);
            attach.GenericArguments.Add(module.ImportReference(sourceType));
            attach.GenericArguments.Add(valueType);

            body.Emit(OpCodes.Ldarg_1);
            body.Emit(OpCodes.Ldarg_1);
            var targetName = binding.Node.FindDirective("Name");
            if (string.IsNullOrWhiteSpace(targetName))
            {
                body.Emit(OpCodes.Ldc_I4, targetOrdinal);
                body.Emit(OpCodes.Call, findOrdinal);
            }
            else
            {
                body.Emit(OpCodes.Ldstr, targetName);
                body.Emit(OpCodes.Call, findName);
            }
            EmitDelegate(body, funcSource, read);
            if (twoWay)
            {
                if (sourceProperty.SetMethod == null) throw new InvalidOperationException($"Two-way binding source '{sourceType.FullName}.{path}' is read-only.");
                var adapterName = ResolveBindingAdapter(targetType, targetProperty)
                    ?? throw new InvalidOperationException($"Two-way binding target '{targetType.FullName}.{targetProperty.Name}' is unsupported.");
                var write = DefineSourceSetter(module, generatedType, index, sourceType, sourceProperty, valueType);
                var actionSource = MakeDelegateType(module, typeof(Action<,>), module.ImportReference(sourceType), valueType);
                EmitDelegate(body, actionSource, write);
                body.Emit(OpCodes.Ldstr, path);
                body.Emit(OpCodes.Ldsfld, module.ImportReference(bindingAdaptersType.Fields.Single(field => field.Name == adapterName)));
                body.Emit(OpCodes.Ldc_I4, (int)ParseUpdateSourceTrigger(binding.Member.Value));
            }
            else
            {
                body.Emit(OpCodes.Ldstr, path);
                EmitDelegate(body, funcTarget, getTarget);
                EmitDelegate(body, actionTarget, setTarget);
            }
            body.Emit(OpCodes.Call, attach);
            body.Emit(OpCodes.Pop);
        }
    }

    private static TypeDefinition? ResolveDataType(CecilTypeSystem typeSystem, ModuleDefinition module, FormaXamlDocument document)
    {
        var dataType = document.DataType;
        if (string.IsNullOrWhiteSpace(dataType)) return null;
        var parts = dataType.Split(':', 2);
        if (parts.Length == 1) return FindType(module, parts[0]);
        if (!document.Namespaces.TryGetValue(parts[0], out var xmlNamespace) || !xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal)) return null;
        var definition = xmlNamespace.Substring("clr-namespace:".Length).Split(';');
        var clrNamespace = definition[0];
        var assemblyName = definition.Skip(1).FirstOrDefault(value => value.StartsWith("assembly=", StringComparison.Ordinal))?.Substring("assembly=".Length) ?? module.Assembly.Name.Name;
        var assembly = assemblyName == module.Assembly.Name.Name ? module.Assembly : typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == assemblyName));
        return assembly.MainModule.GetType($"{clrNamespace}.{parts[1]}");
    }

    private static TypeDefinition? ResolveObjectType(CecilTypeSystem typeSystem, ModuleDefinition module, FormaXamlObject node, FormaXamlDocument document)
    {
        if (node.TypeName.Contains('.')) return null;
        if (node.XmlNamespace == Forma.Xaml.XamlNamespaces.Forma)
        {
            var forma = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
            return forma.MainModule.GetType($"Forma.{node.TypeName}") ?? forma.MainModule.GetType($"Forma.Xaml.{node.TypeName}");
        }
        if (!node.XmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal)) return null;
        var definition = node.XmlNamespace.Substring("clr-namespace:".Length).Split(';');
        var assemblyName = definition.Skip(1).FirstOrDefault(value => value.StartsWith("assembly=", StringComparison.Ordinal))?.Substring("assembly=".Length) ?? module.Assembly.Name.Name;
        var assembly = assemblyName == module.Assembly.Name.Name ? module.Assembly : typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == assemblyName));
        return assembly.MainModule.GetType($"{definition[0]}.{node.TypeName}");
    }

    private static bool IsControl(TypeDefinition? type, TypeDefinition controlType)
    {
        for (var current = type; current != null; current = current.BaseType?.Resolve()) if (current.FullName == controlType.FullName) return true;
        return false;
    }

    private static PropertyDefinition? FindProperty(TypeDefinition type, string name)
    {
        for (var current = type; current != null; current = current.BaseType?.Resolve())
        {
            var property = current.Properties.FirstOrDefault(candidate => candidate.Name == name);
            if (property != null) return property;
        }
        return null;
    }

    private static string ParseBindingPath(string expression)
    {
        var body = expression.Substring("{Binding".Length, expression.Length - "{Binding".Length - 1).Trim();
        var path = body.Split(',')[0].Trim();
        if (path.StartsWith("Path=", StringComparison.Ordinal)) path = path.Substring("Path=".Length).Trim();
        if (string.IsNullOrWhiteSpace(path) || path.Contains('.')) throw new InvalidOperationException($"Binding path '{path}' is not supported by this emitter.");
        return path;
    }

    private static string? ParseBindingOption(string expression, string option)
    {
        var body = expression.Substring("{Binding".Length, expression.Length - "{Binding".Length - 1);
        foreach (var part in body.Split(',').Skip(1))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && part.Substring(0, separator).Trim() == option) return part.Substring(separator + 1).Trim();
        }
        return null;
    }

    private static UpdateSourceTrigger ParseUpdateSourceTrigger(string expression) =>
        Enum.TryParse<UpdateSourceTrigger>(ParseBindingOption(expression, "UpdateSourceTrigger"), out var trigger) ? trigger : UpdateSourceTrigger.Default;

    private static string? ResolveBindingAdapter(TypeDefinition targetType, PropertyDefinition property)
    {
        if (property.Name == "Text" && IsType(targetType, "Forma.LineEdit")) return "LineEditText";
        if (property.Name == "Value" && IsType(targetType, "Forma.Range")) return "RangeValue";
        if (property.Name == "ButtonPressed" && IsType(targetType, "Forma.BaseButton")) return "ButtonPressed";
        if (property.Name == "Checked" && IsType(targetType, "Forma.CheckBox")) return "CheckBoxChecked";
        if (property.Name == "Selected" && IsType(targetType, "Forma.OptionButton")) return "OptionButtonSelected";
        return null;
    }

    private static bool IsType(TypeDefinition type, string fullName)
    {
        for (var current = type; current != null; current = current.BaseType?.Resolve()) if (current.FullName == fullName) return true;
        return false;
    }

    private static MethodDefinition DefineSourceGetter(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition sourceType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition($"__BindingRead{index}", MethodAttributes.Private | MethodAttributes.Static, valueType);
        method.Parameters.Add(new ParameterDefinition("source", ParameterAttributes.None, module.ImportReference(sourceType)));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, module.ImportReference(property.GetMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static MethodDefinition DefineSourceSetter(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition sourceType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition($"__BindingWrite{index}", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("source", ParameterAttributes.None, module.ImportReference(sourceType)));
        method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, valueType));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, module.ImportReference(property.SetMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static MethodDefinition DefineTargetGetter(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition targetType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition($"__BindingGetTarget{index}", MethodAttributes.Private | MethodAttributes.Static, valueType);
        method.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, module.TypeSystem.Object));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, module.ImportReference(targetType));
        il.Emit(OpCodes.Callvirt, module.ImportReference(property.GetMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static MethodDefinition DefineTargetSetter(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition targetType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition($"__BindingSetTarget{index}", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, module.TypeSystem.Object));
        method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, valueType));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, module.ImportReference(targetType));
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, module.ImportReference(property.SetMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static GenericInstanceType MakeDelegateType(ModuleDefinition module, Type openType, params TypeReference[] arguments)
    {
        var type = new GenericInstanceType(module.ImportReference(openType));
        foreach (var argument in arguments) type.GenericArguments.Add(argument);
        return type;
    }

    private static void EmitDelegate(ILProcessor il, GenericInstanceType delegateType, MethodDefinition method)
    {
        var constructor = new MethodReference(".ctor", method.Module.TypeSystem.Void, delegateType) { HasThis = true };
        constructor.Parameters.Add(new ParameterDefinition(method.Module.TypeSystem.Object));
        constructor.Parameters.Add(new ParameterDefinition(method.Module.TypeSystem.IntPtr));
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldftn, method);
        il.Emit(OpCodes.Newobj, constructor);
    }
}