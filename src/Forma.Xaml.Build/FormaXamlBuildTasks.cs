// Copyright (c) 2026 Igor Hipólito Vieira
// SPDX-License-Identifier: MIT

using Forma.Xaml.Compiler;
using System.Globalization;
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
        var rootType = document.RootClass == null ? null : FindType(module, document.RootClass)
            ?? throw new InvalidOperationException($"x:Class type '{document.RootClass}' was not found in {module.Assembly.Name.Name}.");
        var eventMemberNames = FindEventMemberNames(typeSystem, module, document);
        compiler.CompileCecil(source, sourcePath, typeSystem, generatedType, contextType, new FormaXamlParseOptions(), eventMemberNames);
        if (rootType == null) return;
        RegisterPopulate(typeSystem, module, generatedType, rootType, document);
    }

    private static IReadOnlyCollection<string> FindEventMemberNames(CecilTypeSystem typeSystem, ModuleDefinition module, FormaXamlDocument document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in document.DescendantsAndSelf())
        {
            var type = ResolveObjectType(typeSystem, module, node, document);
            if (type == null) continue;
            foreach (var member in node.Members.Where(member => !member.IsDirective))
                if (FindEvent(type, member.Name) != null) names.Add(member.Name);
        }
        return names;
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
        EmitStyles(typeSystem, module, generatedType, wrapper, document);
        EmitStoryboardsAndTriggers(typeSystem, module, generatedType, wrapper, document);
        EmitResourceReferences(typeSystem, module, generatedType, wrapper, document);
        EmitEvents(typeSystem, module, generatedType, wrapper, rootType, document);
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

    private static void EmitStoryboardsAndTriggers(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, FormaXamlDocument document)
    {
        var storyboardNodes = document.DescendantsAndSelf().Where(node => node.TypeName == "Storyboard").ToArray();
        var triggerNodes = document.DescendantsAndSelf().Where(node => node.TypeName == "EventTrigger").ToArray();
        var propertyTriggerNodes = document.DescendantsAndSelf().Where(node => node.TypeName == "PropertyTrigger").ToArray();
        if (storyboardNodes.Length == 0 && triggerNodes.Length == 0 && propertyTriggerNodes.Length == 0) return;
        var formaAssembly = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
        var controlType = formaAssembly.MainModule.GetType("Forma.Control");
        var resourceDictionaryType = formaAssembly.MainModule.GetType("Forma.Xaml.ResourceDictionary");
        var storyboardType = formaAssembly.MainModule.GetType("Forma.Xaml.Storyboard");
        var repeatBehaviorType = formaAssembly.MainModule.GetType("Forma.Xaml.RepeatBehavior");
        var xamlPropertyDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.XamlProperty`1");
        var keyFrameDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.KeyFrame`1");
        var timelineDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.Timeline`1");
        var compiledTriggerType = formaAssembly.MainModule.GetType("Forma.Xaml.CompiledStoryboardTrigger");
        var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
        var findOrdinal = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByOrdinal" && method.IsPublic && method.Parameters.Count == 2));
        var findName = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByName" && method.IsPublic && method.Parameters.Count == 2));
        var storyboardConstructor = module.ImportReference(storyboardType.Methods.Single(method => method.IsConstructor));
        var addTimeline = module.ImportReference(storyboardType.Methods.Single(method => method.Name == "AddTimeline"));
        var resourcesGetter = module.ImportReference(FindProperty(controlType, "Resources")!.GetMethod);
        var addResource = module.ImportReference(resourceDictionaryType.Methods.Single(method => method.Name == "Add" && method.Parameters.Count == 2 && method.Parameters[0].ParameterType.FullName == "System.String"));
        var attachTriggerDefinition = module.ImportReference(compiledTriggerType.Methods.Single(method => method.Name == "AttachEvent"));
        var attachStopTriggerDefinition = module.ImportReference(compiledTriggerType.Methods.Single(method => method.Name == "AttachStopEvent"));
        var attachPropertyTriggerDefinition = module.ImportReference(compiledTriggerType.Methods.Single(method => method.Name == "AttachProperty"));
        var controls = document.DescendantsAndSelf().Where(node => IsControl(ResolveObjectType(typeSystem, module, node, document), controlType)).ToArray();
        var body = wrapper.Body.GetILProcessor();
        var storyboards = new Dictionary<string, VariableDefinition>(StringComparer.Ordinal);
        wrapper.Body.InitLocals = true;

        for (var storyboardIndex = 0; storyboardIndex < storyboardNodes.Length; storyboardIndex++)
        {
            var storyboardNode = storyboardNodes[storyboardIndex];
            var key = storyboardNode.FindDirective("Key") ?? throw new InvalidOperationException("Storyboard requires x:Key.");
            var owner = FindResourceOwner(storyboardNode) ?? document.Root;
            var storyboardVariable = new VariableDefinition(module.ImportReference(storyboardType));
            wrapper.Body.Variables.Add(storyboardVariable);
            body.Emit(OpCodes.Newobj, storyboardConstructor);
            body.Emit(OpCodes.Stloc, storyboardVariable);
            if (bool.TryParse(storyboardNode.FindMember("AutoReverse"), out var autoReverse))
            {
                body.Emit(OpCodes.Ldloc, storyboardVariable);
                body.Emit(autoReverse ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Callvirt, module.ImportReference(FindProperty(storyboardType, "AutoReverse")!.SetMethod));
            }
            if (Enum.TryParse<FillBehavior>(storyboardNode.FindMember("FillBehavior"), true, out var fillBehavior))
            {
                body.Emit(OpCodes.Ldloc, storyboardVariable);
                body.Emit(OpCodes.Ldc_I4, (int)fillBehavior);
                body.Emit(OpCodes.Callvirt, module.ImportReference(FindProperty(storyboardType, "FillBehavior")!.SetMethod));
            }
            if (storyboardNode.FindMember("RepeatBehavior") is string repeatBehavior)
            {
                body.Emit(OpCodes.Ldloc, storyboardVariable);
                if (string.Equals(repeatBehavior, "Forever", StringComparison.OrdinalIgnoreCase))
                    body.Emit(OpCodes.Call, module.ImportReference(FindProperty(repeatBehaviorType, "ForeverValue")!.GetMethod));
                else if (double.TryParse(repeatBehavior, NumberStyles.Float, CultureInfo.InvariantCulture, out var repeatCount) && repeatCount > 0)
                {
                    body.Emit(OpCodes.Ldc_R8, repeatCount);
                    body.Emit(OpCodes.Call, module.ImportReference(repeatBehaviorType.Methods.Single(method => method.Name == "ForCount")));
                }
                else
                    throw new InvalidOperationException($"Storyboard RepeatBehavior '{repeatBehavior}' must be a positive count or Forever.");
                body.Emit(OpCodes.Callvirt, module.ImportReference(FindProperty(storyboardType, "RepeatBehavior")!.SetMethod));
            }

            var timelines = storyboardNode.Children.Where(child => child.TypeName.EndsWith("Timeline", StringComparison.Ordinal)).ToArray();
            for (var timelineIndex = 0; timelineIndex < timelines.Length; timelineIndex++)
            {
                var timelineNode = timelines[timelineIndex];
                var targetName = timelineNode.FindMember("TargetName") ?? throw new InvalidOperationException("Timeline requires TargetName.");
                var propertyName = timelineNode.FindMember("Property") ?? throw new InvalidOperationException("Timeline requires Property.");
                var targetNode = document.DescendantsAndSelf().SingleOrDefault(node => node.FindDirective("Name") == targetName)
                    ?? throw new InvalidOperationException($"Storyboard target '{targetName}' was not found.");
                var targetType = ResolveObjectType(typeSystem, module, targetNode, document)
                    ?? throw new InvalidOperationException($"Storyboard target type '{targetNode.TypeName}' was not resolved.");
                var property = FindProperty(targetType, propertyName)
                    ?? throw new InvalidOperationException($"Storyboard property '{targetType.FullName}.{propertyName}' was not found.");
                if (property.GetMethod == null || property.SetMethod == null)
                    throw new InvalidOperationException($"Storyboard property '{targetType.FullName}.{propertyName}' must be readable and writable.");
                var valueType = module.ImportReference(property.PropertyType);
                ValidateTimelineValueType(timelineNode.TypeName, valueType);
                var getTarget = DefineTargetGetter(module, generatedType, $"__TimelineGetTarget{storyboardIndex}_{timelineIndex}", targetType, property, valueType);
                var setTarget = DefineTargetSetter(module, generatedType, $"__TimelineSetTarget{storyboardIndex}_{timelineIndex}", targetType, property, valueType);
                var funcTarget = MakeDelegateType(module, typeof(Func<,>), module.TypeSystem.Object, valueType);
                var actionTarget = MakeDelegateType(module, typeof(Action<,>), module.TypeSystem.Object, valueType);
                var xamlPropertyType = new GenericInstanceType(module.ImportReference(xamlPropertyDefinition));
                xamlPropertyType.GenericArguments.Add(valueType);
                var xamlPropertyConstructor = MakeClosedMethod(module, xamlPropertyDefinition.Methods.Single(method => method.IsConstructor), xamlPropertyType);
                var timelineType = formaAssembly.MainModule.GetType($"Forma.Xaml.{timelineNode.TypeName}");
                var timelineVariable = new VariableDefinition(module.ImportReference(timelineType));
                wrapper.Body.Variables.Add(timelineVariable);
                body.Emit(OpCodes.Ldstr, targetName);
                body.Emit(OpCodes.Ldstr, propertyName);
                EmitDelegate(body, funcTarget, getTarget);
                EmitDelegate(body, actionTarget, setTarget);
                body.Emit(OpCodes.Newobj, xamlPropertyConstructor);
                body.Emit(OpCodes.Newobj, module.ImportReference(timelineType.Methods.Single(method => method.IsConstructor)));
                body.Emit(OpCodes.Stloc, timelineVariable);

                var keyFrameType = new GenericInstanceType(module.ImportReference(keyFrameDefinition));
                keyFrameType.GenericArguments.Add(valueType);
                var keyFrameConstructor = MakeClosedMethod(module, keyFrameDefinition.Methods.Single(method => method.IsConstructor), keyFrameType);
                var closedTimelineType = new GenericInstanceType(module.ImportReference(timelineDefinition));
                closedTimelineType.GenericArguments.Add(valueType);
                var addKeyFrame = MakeClosedMethod(module, timelineDefinition.Methods.Single(method => method.Name == "AddKeyFrame"), closedTimelineType);
                foreach (var keyFrame in timelineNode.Children.Where(child => child.TypeName == "KeyFrame"))
                {
                    body.Emit(OpCodes.Ldloc, timelineVariable);
                    EmitTimeSpan(body, module, keyFrame.FindMember("Time") ?? throw new InvalidOperationException("KeyFrame requires Time."));
                    EmitStyleValue(body, module, formaAssembly, valueType, keyFrame.FindMember("Value") ?? throw new InvalidOperationException("KeyFrame requires Value."));
                    var easing = Enum.TryParse<Easing>(keyFrame.FindMember("Easing"), true, out var parsedEasing) ? parsedEasing : Easing.Linear;
                    body.Emit(OpCodes.Ldc_I4, (int)easing);
                    body.Emit(OpCodes.Newobj, keyFrameConstructor);
                    body.Emit(OpCodes.Callvirt, addKeyFrame);
                }
                body.Emit(OpCodes.Ldloc, storyboardVariable);
                body.Emit(OpCodes.Ldloc, timelineVariable);
                body.Emit(OpCodes.Callvirt, addTimeline);
            }

            EmitFindControl(body, owner, Array.IndexOf(controls, owner), findOrdinal, findName);
            body.Emit(OpCodes.Callvirt, resourcesGetter);
            body.Emit(OpCodes.Ldstr, key);
            body.Emit(OpCodes.Ldloc, storyboardVariable);
            body.Emit(OpCodes.Callvirt, addResource);
            storyboards.Add(key, storyboardVariable);
        }

        for (var triggerIndex = 0; triggerIndex < triggerNodes.Length; triggerIndex++)
        {
            var trigger = triggerNodes[triggerIndex];
            var sourceName = trigger.FindMember("SourceName") ?? throw new InvalidOperationException("EventTrigger requires SourceName.");
            var eventName = trigger.FindMember("Event") ?? throw new InvalidOperationException("EventTrigger requires Event.");
            var action = trigger.Children.SingleOrDefault(child => child.TypeName is "BeginStoryboard" or "StopStoryboard")
                ?? throw new InvalidOperationException("EventTrigger requires BeginStoryboard or StopStoryboard.");
            _ = TryParseResourceReference(action.FindMember("Storyboard") ?? string.Empty, out var dynamic, out var storyboardKey);
            if (dynamic || !storyboards.TryGetValue(storyboardKey, out var storyboardVariable))
                throw new InvalidOperationException($"EventTrigger storyboard '{storyboardKey}' was not found.");
            var sourceNode = document.DescendantsAndSelf().SingleOrDefault(node => node.FindDirective("Name") == sourceName)
                ?? throw new InvalidOperationException($"EventTrigger source '{sourceName}' was not found.");
            var sourceType = ResolveObjectType(typeSystem, module, sourceNode, document)
                ?? throw new InvalidOperationException($"EventTrigger source type '{sourceNode.TypeName}' was not resolved.");
            var eventDefinition = FindEvent(sourceType, eventName)
                ?? throw new InvalidOperationException($"Event '{sourceType.FullName}.{eventName}' was not found.");
            var handlerType = module.ImportReference(eventDefinition.EventType);
            var add = DefineTriggerEventAccessor(module, generatedType, triggerIndex, sourceType, eventDefinition, handlerType, true);
            var remove = DefineTriggerEventAccessor(module, generatedType, triggerIndex, sourceType, eventDefinition, handlerType, false);
            var factory = DefineTriggerHandlerFactory(module, generatedType, triggerIndex, eventDefinition, handlerType);
            var actionType = MakeDelegateType(module, typeof(Action<,>), module.ImportReference(sourceType), handlerType);
            var factoryType = MakeDelegateType(module, typeof(Func<,>), module.ImportReference(typeof(Action)), handlerType);
            var attach = new GenericInstanceMethod(action.TypeName == "StopStoryboard" ? attachStopTriggerDefinition : attachTriggerDefinition);
            attach.GenericArguments.Add(module.ImportReference(sourceType));
            attach.GenericArguments.Add(handlerType);
            body.Emit(OpCodes.Ldarg_1);
            EmitFindControl(body, sourceNode, Array.IndexOf(controls, sourceNode), findOrdinal, findName);
            body.Emit(OpCodes.Castclass, module.ImportReference(sourceType));
            EmitDelegate(body, actionType, add);
            EmitDelegate(body, actionType, remove);
            EmitDelegate(body, factoryType, factory);
            body.Emit(OpCodes.Ldloc, storyboardVariable);
            body.Emit(OpCodes.Call, attach);
            body.Emit(OpCodes.Pop);
        }

        TypeDefinition? dataType = null;
        if (propertyTriggerNodes.Length > 0)
            dataType = ResolveDataType(typeSystem, module, document)
                ?? throw new InvalidOperationException("PropertyTrigger requires a resolvable x:DataType.");
        for (var triggerIndex = 0; triggerIndex < propertyTriggerNodes.Length; triggerIndex++)
        {
            var sourceType = dataType!;
            var trigger = propertyTriggerNodes[triggerIndex];
            var binding = trigger.FindMember("Binding") ?? throw new InvalidOperationException("PropertyTrigger requires Binding.");
            var path = ParseBindingPath(binding);
            var sourceProperty = FindProperty(sourceType, path)
                ?? throw new InvalidOperationException($"PropertyTrigger source property '{sourceType.FullName}.{path}' was not found.");
            if (sourceProperty.GetMethod == null)
                throw new InvalidOperationException($"PropertyTrigger source property '{sourceType.FullName}.{path}' must be readable.");
            var action = trigger.Children.SingleOrDefault(child => child.TypeName == "BeginStoryboard")
                ?? throw new InvalidOperationException("PropertyTrigger requires BeginStoryboard.");
            _ = TryParseResourceReference(action.FindMember("Storyboard") ?? string.Empty, out var dynamic, out var storyboardKey);
            if (dynamic || !storyboards.TryGetValue(storyboardKey, out var storyboardVariable))
                throw new InvalidOperationException($"PropertyTrigger storyboard '{storyboardKey}' was not found.");
            var valueType = module.ImportReference(sourceProperty.PropertyType);
            var read = DefineSourceGetter(module, generatedType, $"__TriggerRead{triggerIndex}", sourceType, sourceProperty, valueType);
            var funcSource = MakeDelegateType(module, typeof(Func<,>), module.ImportReference(sourceType), valueType);
            var attach = new GenericInstanceMethod(attachPropertyTriggerDefinition);
            attach.GenericArguments.Add(module.ImportReference(sourceType));
            attach.GenericArguments.Add(valueType);
            body.Emit(OpCodes.Ldarg_1);
            EmitDelegate(body, funcSource, read);
            body.Emit(OpCodes.Ldstr, path);
            EmitStyleValue(body, module, formaAssembly, valueType, trigger.FindMember("Value") ?? throw new InvalidOperationException("PropertyTrigger requires Value."));
            body.Emit(OpCodes.Ldloc, storyboardVariable);
            body.Emit(OpCodes.Call, attach);
            body.Emit(OpCodes.Pop);
        }
    }

    private static void EmitEvents(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, TypeDefinition rootType, FormaXamlDocument document)
    {
        var formaAssembly = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
        var controlType = formaAssembly.MainModule.GetType("Forma.Control");
        var compiledEventType = formaAssembly.MainModule.GetType("Forma.Xaml.CompiledEvent");
        var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
        var findOrdinal = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByOrdinal" && method.IsPublic && method.Parameters.Count == 2));
        var findName = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByName" && method.IsPublic && method.Parameters.Count == 2));
        var attachDefinition = module.ImportReference(compiledEventType.Methods.Single(method => method.Name == "Attach"));
        var controls = document.DescendantsAndSelf().Where(node => IsControl(ResolveObjectType(typeSystem, module, node, document), controlType)).ToArray();
        var body = wrapper.Body.GetILProcessor();
        var eventIndex = 0;

        foreach (var node in document.DescendantsAndSelf())
        {
            var targetType = ResolveObjectType(typeSystem, module, node, document);
            if (targetType == null || !IsControl(targetType, controlType)) continue;
            foreach (var member in node.Members.Where(member => !member.IsDirective))
            {
                var eventDefinition = FindEvent(targetType, member.Name);
                if (eventDefinition == null) continue;
                var handler = FindMethod(rootType, member.Value)
                    ?? throw new InvalidOperationException($"Event handler '{rootType.FullName}.{member.Value}' was not found.");
                ValidateEventHandler(eventDefinition, handler);
                var handlerBridge = DefineEventHandlerBridge(module, rootType, eventIndex, handler);
                var handlerType = module.ImportReference(eventDefinition.EventType);
                var add = DefineEventAccessor(module, generatedType, eventIndex, targetType, eventDefinition, handlerType, true);
                var remove = DefineEventAccessor(module, generatedType, eventIndex, targetType, eventDefinition, handlerType, false);
                var actionType = MakeDelegateType(module, typeof(Action<,>), module.ImportReference(targetType), handlerType);
                var handlerConstructor = new MethodReference(".ctor", module.TypeSystem.Void, handlerType) { HasThis = true };
                handlerConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
                handlerConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
                var attach = new GenericInstanceMethod(attachDefinition);
                attach.GenericArguments.Add(module.ImportReference(targetType));
                attach.GenericArguments.Add(handlerType);
                body.Emit(OpCodes.Ldarg_1);
                EmitFindControl(body, node, Array.IndexOf(controls, node), findOrdinal, findName);
                body.Emit(OpCodes.Castclass, module.ImportReference(targetType));
                body.Emit(OpCodes.Ldarg_1);
                body.Emit(OpCodes.Ldftn, handlerBridge);
                body.Emit(OpCodes.Newobj, handlerConstructor);
                EmitDelegate(body, actionType, add);
                EmitDelegate(body, actionType, remove);
                body.Emit(OpCodes.Call, attach);
                body.Emit(OpCodes.Pop);
                eventIndex++;
            }
        }
    }

    private static void EmitResourceReferences(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, FormaXamlDocument document)
    {
        var references = document.DescendantsAndSelf()
            .Where(node => !HasAncestor(node, "Style") && !HasAncestor(node, "EventTrigger") && !HasAncestor(node, "PropertyTrigger"))
            .SelectMany(node => node.Members.Where(member => !member.IsDirective && TryParseResourceReference(member.Value, out _, out _)).Select(member => (Node: node, Member: member)))
            .ToArray();
        if (references.Length == 0) return;
        var formaAssembly = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
        var controlType = formaAssembly.MainModule.GetType("Forma.Control");
        var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
        var staticResourceType = formaAssembly.MainModule.GetType("Forma.Xaml.StaticResource");
        var dynamicResourceType = formaAssembly.MainModule.GetType("Forma.Xaml.DynamicResource");
        var xamlPropertyDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.XamlProperty`1");
        var findOrdinal = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByOrdinal" && method.IsPublic && method.Parameters.Count == 2));
        var findName = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByName" && method.IsPublic && method.Parameters.Count == 2));
        var resolveDefinition = module.ImportReference(staticResourceType.Methods.Single(method => method.Name == "Resolve"));
        var attachDefinition = module.ImportReference(dynamicResourceType.Methods.Single(method => method.Name == "Attach"));
        var controls = document.DescendantsAndSelf().Where(node => IsControl(ResolveObjectType(typeSystem, module, node, document), controlType)).ToArray();
        var body = wrapper.Body.GetILProcessor();
        wrapper.Body.InitLocals = true;

        for (var index = 0; index < references.Length; index++)
        {
            var reference = references[index];
            _ = TryParseResourceReference(reference.Member.Value, out var dynamic, out var key);
            var targetOrdinal = Array.IndexOf(controls, reference.Node);
            if (targetOrdinal < 0) throw new InvalidOperationException($"Resource target '{reference.Node.TypeName}' is not a Control.");
            var targetType = ResolveObjectType(typeSystem, module, reference.Node, document)
                ?? throw new InvalidOperationException($"Resource target type '{reference.Node.TypeName}' was not resolved.");
            var targetProperty = FindProperty(targetType, reference.Member.Name)
                ?? throw new InvalidOperationException($"Resource target property '{targetType.FullName}.{reference.Member.Name}' was not found.");
            if (targetProperty.GetMethod == null || targetProperty.SetMethod == null)
                throw new InvalidOperationException($"Resource target property '{targetType.FullName}.{targetProperty.Name}' must be readable and writable.");
            var valueType = module.ImportReference(targetProperty.PropertyType);
            var targetVariable = new VariableDefinition(module.ImportReference(controlType));
            wrapper.Body.Variables.Add(targetVariable);
            EmitFindControl(body, reference.Node, targetOrdinal, findOrdinal, findName);
            body.Emit(OpCodes.Stloc, targetVariable);

            if (!dynamic)
            {
                var resolve = new GenericInstanceMethod(resolveDefinition);
                resolve.GenericArguments.Add(valueType);
                body.Emit(OpCodes.Ldloc, targetVariable);
                body.Emit(OpCodes.Castclass, module.ImportReference(targetType));
                body.Emit(OpCodes.Ldloc, targetVariable);
                body.Emit(OpCodes.Ldstr, key);
                body.Emit(OpCodes.Call, resolve);
                body.Emit(OpCodes.Callvirt, module.ImportReference(targetProperty.SetMethod));
                continue;
            }

            var getTarget = DefineTargetGetter(module, generatedType, $"__ResourceGetTarget{index}", targetType, targetProperty, valueType);
            var setTarget = DefineTargetSetter(module, generatedType, $"__ResourceSetTarget{index}", targetType, targetProperty, valueType);
            var funcTarget = MakeDelegateType(module, typeof(Func<,>), module.TypeSystem.Object, valueType);
            var actionTarget = MakeDelegateType(module, typeof(Action<,>), module.TypeSystem.Object, valueType);
            var xamlPropertyType = new GenericInstanceType(module.ImportReference(xamlPropertyDefinition));
            xamlPropertyType.GenericArguments.Add(valueType);
            var propertyConstructorDefinition = xamlPropertyDefinition.Methods.Single(method => method.IsConstructor && !method.IsStatic);
            var propertyConstructor = new MethodReference(".ctor", module.TypeSystem.Void, xamlPropertyType) { HasThis = true };
            foreach (var parameter in propertyConstructorDefinition.Parameters)
                propertyConstructor.Parameters.Add(new ParameterDefinition(module.ImportReference(parameter.ParameterType, propertyConstructor)));
            var attach = new GenericInstanceMethod(attachDefinition);
            attach.GenericArguments.Add(valueType);
            body.Emit(OpCodes.Ldarg_1);
            body.Emit(OpCodes.Ldloc, targetVariable);
            body.Emit(OpCodes.Ldstr, targetProperty.Name);
            EmitDelegate(body, funcTarget, getTarget);
            EmitDelegate(body, actionTarget, setTarget);
            body.Emit(OpCodes.Newobj, propertyConstructor);
            body.Emit(OpCodes.Ldstr, key);
            body.Emit(OpCodes.Ldnull);
            body.Emit(OpCodes.Ldc_I4, (int)XamlValueLayer.Local);
            body.Emit(OpCodes.Ldc_I8, 0L);
            body.Emit(OpCodes.Call, attach);
            body.Emit(OpCodes.Pop);
        }
    }

    private static void EmitBindings(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, TypeDefinition rootType, FormaXamlDocument document)
    {
        var bindingNodes = document.DescendantsAndSelf()
            .Where(node => node.TypeName != "PropertyTrigger" && !HasAncestor(node, "PropertyTrigger"))
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
            EmitFindControl(body, binding.Node, targetOrdinal, findOrdinal, findName);
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

    private static EventDefinition? FindEvent(TypeDefinition type, string name)
    {
        for (var current = type; current != null; current = current.BaseType?.Resolve())
        {
            var eventDefinition = current.Events.FirstOrDefault(candidate => candidate.Name == name);
            if (eventDefinition != null) return eventDefinition;
        }
        return null;
    }

    private static MethodDefinition? FindMethod(TypeDefinition type, string name)
    {
        for (var current = type; current != null; current = current.BaseType?.Resolve())
        {
            var method = current.Methods.FirstOrDefault(candidate => candidate.Name == name && !candidate.IsStatic);
            if (method != null) return method;
        }
        return null;
    }

    private static void ValidateEventHandler(EventDefinition eventDefinition, MethodDefinition handler)
    {
        var invoke = eventDefinition.EventType.Resolve().Methods.Single(method => method.Name == "Invoke");
        if (handler.ReturnType.FullName != invoke.ReturnType.FullName || handler.Parameters.Count != invoke.Parameters.Count ||
            handler.Parameters.Where((parameter, index) => parameter.ParameterType.FullName != invoke.Parameters[index].ParameterType.FullName).Any())
            throw new InvalidOperationException($"Event handler '{handler.FullName}' is incompatible with '{eventDefinition.EventType.FullName}'.");
    }

    private static MethodDefinition DefineEventAccessor(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition targetType, EventDefinition eventDefinition, TypeReference handlerType, bool add)
    {
        var method = new MethodDefinition($"__Event{(add ? "Add" : "Remove")}{index}", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, module.ImportReference(targetType)));
        method.Parameters.Add(new ParameterDefinition("handler", ParameterAttributes.None, handlerType));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, module.ImportReference(add ? eventDefinition.AddMethod : eventDefinition.RemoveMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static MethodDefinition DefineEventHandlerBridge(ModuleDefinition module, TypeDefinition rootType, int index, MethodDefinition handler)
    {
        var method = new MethodDefinition($"__FormaEventHandler{index}", MethodAttributes.Assembly | MethodAttributes.HideBySig, module.ImportReference(handler.ReturnType));
        foreach (var parameter in handler.Parameters)
            method.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, module.ImportReference(parameter.ParameterType)));
        rootType.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        for (var parameterIndex = 0; parameterIndex < method.Parameters.Count; parameterIndex++)
            il.Emit(OpCodes.Ldarg, parameterIndex + 1);
        il.Emit(OpCodes.Call, module.ImportReference(handler));
        il.Emit(OpCodes.Ret);
        return method;
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

    private static bool TryParseResourceReference(string expression, out bool dynamic, out string key)
    {
        const string staticPrefix = "{StaticResource ";
        const string dynamicPrefix = "{DynamicResource ";
        dynamic = expression.StartsWith(dynamicPrefix, StringComparison.Ordinal);
        var prefix = dynamic ? dynamicPrefix : staticPrefix;
        if (!dynamic && !expression.StartsWith(staticPrefix, StringComparison.Ordinal) || !expression.EndsWith('}'))
        {
            key = string.Empty;
            return false;
        }
        key = expression.Substring(prefix.Length, expression.Length - prefix.Length - 1).Trim();
        return key.Length > 0 && !key.Contains(',');
    }

    private static void EmitStyles(CecilTypeSystem typeSystem, ModuleDefinition module, TypeDefinition generatedType, MethodDefinition wrapper, FormaXamlDocument document)
    {
            var styleNodes = document.DescendantsAndSelf().Where(node => node.TypeName == "Style").ToArray();
            if (styleNodes.Length == 0) return;
            var formaAssembly = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
            var controlType = formaAssembly.MainModule.GetType("Forma.Control");
            var resourceDictionaryType = formaAssembly.MainModule.GetType("Forma.Xaml.ResourceDictionary");
            var styleType = formaAssembly.MainModule.GetType("Forma.Xaml.Style");
            var styleSetterDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.StyleSetter`1");
            var xamlPropertyDefinition = formaAssembly.MainModule.GetType("Forma.Xaml.XamlProperty`1");
            var styleEngineType = formaAssembly.MainModule.GetType("Forma.Xaml.StyleEngine");
            var staticResourceType = formaAssembly.MainModule.GetType("Forma.Xaml.StaticResource");
            var nameScopeType = formaAssembly.MainModule.GetType("Forma.Xaml.NameScope");
            var findOrdinal = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByOrdinal" && method.IsPublic && method.Parameters.Count == 2));
            var findName = module.ImportReference(nameScopeType.Methods.Single(method => method.Name == "FindControlByName" && method.IsPublic && method.Parameters.Count == 2));
            var styleConstructor = module.ImportReference(styleType.Methods.Single(method => method.IsConstructor && !method.IsStatic));
            var addSetter = module.ImportReference(styleType.Methods.Single(method => method.Name == "AddSetter"));
            var resourcesGetter = module.ImportReference(FindProperty(controlType, "Resources")!.GetMethod);
            var addResource = module.ImportReference(resourceDictionaryType.Methods.Single(method => method.Name == "Add" && method.Parameters.Count == 2 && method.Parameters[0].ParameterType.FullName == "System.String"));
            var attach = module.ImportReference(styleEngineType.Methods.Single(method => method.Name == "Attach"));
            var controls = document.DescendantsAndSelf().Where(node => IsControl(ResolveObjectType(typeSystem, module, node, document), controlType)).ToArray();
            var emitted = new List<(FormaXamlObject Owner, VariableDefinition Style)>();
            var body = wrapper.Body.GetILProcessor();
            wrapper.Body.InitLocals = true;

            for (var styleIndex = 0; styleIndex < styleNodes.Length; styleIndex++)
            {
                var styleNode = styleNodes[styleIndex];
                var selector = styleNode.FindMember("Selector") ?? throw new InvalidOperationException("Style requires Selector.");
                var owner = FindResourceOwner(styleNode) ?? document.Root;
                var styleVariable = new VariableDefinition(module.ImportReference(styleType));
                wrapper.Body.Variables.Add(styleVariable);
                body.Emit(OpCodes.Ldstr, selector);
                body.Emit(OpCodes.Newobj, styleConstructor);
                body.Emit(OpCodes.Stloc, styleVariable);
                var targetType = ResolveStyleTargetType(typeSystem, module, document, selector, controlType)
                    ?? throw new InvalidOperationException($"Style selector target type '{selector}' was not resolved.");
                var setters = styleNode.Children.Where(child => child.TypeName == "Setter").ToArray();
                for (var setterIndex = 0; setterIndex < setters.Length; setterIndex++)
                {
                    var setter = setters[setterIndex];
                    var propertyName = setter.FindMember("Property") ?? throw new InvalidOperationException("Setter requires Property.");
                    var value = setter.FindMember("Value") ?? throw new InvalidOperationException("Setter requires Value.");
                    var property = FindProperty(targetType, propertyName)
                        ?? throw new InvalidOperationException($"Style setter property '{targetType.FullName}.{propertyName}' was not found.");
                    if (property.GetMethod == null || property.SetMethod == null)
                        throw new InvalidOperationException($"Style setter property '{targetType.FullName}.{propertyName}' must be readable and writable.");
                    var valueType = module.ImportReference(property.PropertyType);
                    var getTarget = DefineTargetGetter(module, generatedType, $"__StyleGetTarget{styleIndex}_{setterIndex}", targetType, property, valueType);
                    var setTarget = DefineTargetSetter(module, generatedType, $"__StyleSetTarget{styleIndex}_{setterIndex}", targetType, property, valueType);
                    var funcTarget = MakeDelegateType(module, typeof(Func<,>), module.TypeSystem.Object, valueType);
                    var actionTarget = MakeDelegateType(module, typeof(Action<,>), module.TypeSystem.Object, valueType);
                    var xamlPropertyType = new GenericInstanceType(module.ImportReference(xamlPropertyDefinition));
                    xamlPropertyType.GenericArguments.Add(valueType);
                    var xamlPropertyConstructor = MakeClosedMethod(module, xamlPropertyDefinition.Methods.Single(method => method.IsConstructor && !method.IsStatic), xamlPropertyType);
                    var styleSetterType = new GenericInstanceType(module.ImportReference(styleSetterDefinition));
                    styleSetterType.GenericArguments.Add(valueType);
                    body.Emit(OpCodes.Ldloc, styleVariable);
                    body.Emit(OpCodes.Ldstr, propertyName);
                    EmitDelegate(body, funcTarget, getTarget);
                    EmitDelegate(body, actionTarget, setTarget);
                    body.Emit(OpCodes.Newobj, xamlPropertyConstructor);
                    MethodDefinition styleSetterConstructorDefinition;
                    if (TryParseResourceReference(value, out var dynamic, out var resourceKey))
                    {
                        if (dynamic) throw new InvalidOperationException("DynamicResource is not supported in a style setter; use StaticResource or a control-local DynamicResource.");
                        var resolve = new GenericInstanceMethod(module.ImportReference(staticResourceType.Methods.Single(method => method.Name == "Resolve")));
                        resolve.GenericArguments.Add(valueType);
                        var resourceGetter = DefineStyleResourceGetter(module, generatedType, styleIndex, setterIndex, controlType, valueType, resourceKey, resolve);
                        var funcControl = MakeDelegateType(module, typeof(Func<,>), module.ImportReference(controlType), valueType);
                        EmitDelegate(body, funcControl, resourceGetter);
                        styleSetterConstructorDefinition = styleSetterDefinition.Methods.Single(method => method.IsConstructor && method.Parameters.Count == 2 && method.Parameters[1].ParameterType is GenericInstanceType);
                    }
                    else
                    {
                        EmitStyleValue(body, module, formaAssembly, valueType, value);
                        styleSetterConstructorDefinition = styleSetterDefinition.Methods.Single(method => method.IsConstructor && method.Parameters.Count == 2 && method.Parameters[1].ParameterType is GenericParameter);
                    }
                    var styleSetterConstructor = MakeClosedMethod(module, styleSetterConstructorDefinition, styleSetterType);
                    body.Emit(OpCodes.Newobj, styleSetterConstructor);
                    body.Emit(OpCodes.Callvirt, addSetter);
                }

                var key = styleNode.FindDirective("Key");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    EmitFindControl(body, owner, Array.IndexOf(controls, owner), findOrdinal, findName);
                    body.Emit(OpCodes.Callvirt, resourcesGetter);
                    body.Emit(OpCodes.Ldstr, key);
                    body.Emit(OpCodes.Ldloc, styleVariable);
                    body.Emit(OpCodes.Callvirt, addResource);
                }
                emitted.Add((owner, styleVariable));
            }

            foreach (var group in emitted.GroupBy(item => item.Owner))
            {
                EmitFindControl(body, group.Key, Array.IndexOf(controls, group.Key), findOrdinal, findName);
                body.Emit(OpCodes.Ldc_I4, group.Count());
                body.Emit(OpCodes.Newarr, module.ImportReference(styleType));
                var index = 0;
                foreach (var item in group)
                {
                    body.Emit(OpCodes.Dup);
                    body.Emit(OpCodes.Ldc_I4, index++);
                    body.Emit(OpCodes.Ldloc, item.Style);
                    body.Emit(OpCodes.Stelem_Ref);
                }
                body.Emit(OpCodes.Call, attach);
                body.Emit(OpCodes.Pop);
            }
    }

    private static FormaXamlObject? FindResourceOwner(FormaXamlObject node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
            if (current.TypeName.EndsWith(".Resources", StringComparison.Ordinal) && current.Parent != null)
                return current.Parent;
        return null;
    }

    private static bool HasAncestor(FormaXamlObject node, string typeName)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
            if (current.TypeName == typeName) return true;
        return false;
    }

    private static TypeDefinition? ResolveStyleTargetType(CecilTypeSystem typeSystem, ModuleDefinition module, FormaXamlDocument document, string selector, TypeDefinition controlType)
    {
        var length = selector.IndexOfAny(['.', '#', ':']);
        var typeName = length < 0 ? selector : selector.Substring(0, length);
        if (string.IsNullOrWhiteSpace(typeName)) return controlType;
        var local = module.Types.SelectMany(AllTypes).FirstOrDefault(type => type.Name == typeName);
        if (local != null) return local;
        var forma = typeSystem.Resolve(module.AssemblyReferences.Single(reference => reference.Name == "Forma"));
        return forma.MainModule.Types.SelectMany(AllTypes).FirstOrDefault(type => type.Name == typeName);
    }

    private static void EmitStyleValue(ILProcessor body, ModuleDefinition module, AssemblyDefinition formaAssembly, TypeReference valueType, string value)
    {
        switch (valueType.FullName)
        {
            case "System.String": body.Emit(OpCodes.Ldstr, value); return;
            case "System.Boolean": body.Emit(bool.Parse(value) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); return;
            case "System.Int32": body.Emit(OpCodes.Ldc_I4, int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)); return;
            case "System.Single": body.Emit(OpCodes.Ldc_R4, float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)); return;
            case "System.Double": body.Emit(OpCodes.Ldc_R8, double.Parse(value, System.Globalization.CultureInfo.InvariantCulture)); return;
        }
        var converterType = formaAssembly.MainModule.GetType("Forma.Xaml.XamlValueConverter");
        var methodName = valueType.FullName switch
        {
            "Microsoft.Xna.Framework.Color" => "ParseColor",
            "Microsoft.Xna.Framework.Vector2" => "ParseVector2",
            "Forma.Thickness" => "ParseThickness",
            _ => null,
        };
        if (methodName == null) throw new InvalidOperationException($"Style value type '{valueType.FullName}' is not supported by compiled setters.");
        body.Emit(OpCodes.Ldstr, value);
        body.Emit(OpCodes.Call, module.ImportReference(converterType.Methods.Single(method => method.Name == methodName)));
    }

    private static void ValidateTimelineValueType(string timelineTypeName, TypeReference valueType)
    {
        var expected = timelineTypeName switch
        {
            "FloatTimeline" => "System.Single",
            "ColorTimeline" => "Microsoft.Xna.Framework.Color",
            "Vector2Timeline" => "Microsoft.Xna.Framework.Vector2",
            "ThicknessTimeline" => "Forma.Thickness",
            _ => throw new InvalidOperationException($"Timeline type '{timelineTypeName}' is not supported."),
        };
        if (valueType.FullName != expected)
            throw new InvalidOperationException($"Timeline '{timelineTypeName}' cannot animate '{valueType.FullName}'.");
    }

    private static void EmitTimeSpan(ILProcessor body, ModuleDefinition module, string value)
    {
        var time = TimeSpan.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        body.Emit(OpCodes.Ldc_I8, time.Ticks);
        body.Emit(OpCodes.Newobj, module.ImportReference(typeof(TimeSpan).GetConstructor([typeof(long)])!));
    }

    private static MethodDefinition DefineTriggerEventAccessor(ModuleDefinition module, TypeDefinition owner, int index, TypeDefinition targetType, EventDefinition eventDefinition, TypeReference handlerType, bool add)
    {
        var method = new MethodDefinition($"__TriggerEvent{(add ? "Add" : "Remove")}{index}", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        method.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, module.ImportReference(targetType)));
        method.Parameters.Add(new ParameterDefinition("handler", ParameterAttributes.None, handlerType));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, module.ImportReference(add ? eventDefinition.AddMethod : eventDefinition.RemoveMethod));
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static MethodDefinition DefineTriggerHandlerFactory(ModuleDefinition module, TypeDefinition owner, int index, EventDefinition eventDefinition, TypeReference handlerType)
    {
        var invoke = eventDefinition.EventType.Resolve().Methods.Single(method => method.Name == "Invoke");
        if (invoke.ReturnType.FullName != "System.Void")
            throw new InvalidOperationException($"Event trigger '{eventDefinition.FullName}' must use a void delegate.");
        var handler = new MethodDefinition($"__TriggerEventHandler{index}", MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
        handler.Parameters.Add(new ParameterDefinition("action", ParameterAttributes.None, module.ImportReference(typeof(Action))));
        foreach (var parameter in invoke.Parameters)
            handler.Parameters.Add(new ParameterDefinition(parameter.Name, parameter.Attributes, module.ImportReference(parameter.ParameterType)));
        owner.Methods.Add(handler);
        var handlerIl = handler.Body.GetILProcessor();
        handlerIl.Emit(OpCodes.Ldarg_0);
        handlerIl.Emit(OpCodes.Callvirt, module.ImportReference(typeof(Action).GetMethod(nameof(Action.Invoke))!));
        handlerIl.Emit(OpCodes.Ret);

        var factory = new MethodDefinition($"__TriggerEventFactory{index}", MethodAttributes.Private | MethodAttributes.Static, handlerType);
        factory.Parameters.Add(new ParameterDefinition("action", ParameterAttributes.None, module.ImportReference(typeof(Action))));
        owner.Methods.Add(factory);
        var handlerConstructor = new MethodReference(".ctor", module.TypeSystem.Void, handlerType) { HasThis = true };
        handlerConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        handlerConstructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.IntPtr));
        var factoryIl = factory.Body.GetILProcessor();
        factoryIl.Emit(OpCodes.Ldarg_0);
        factoryIl.Emit(OpCodes.Ldftn, handler);
        factoryIl.Emit(OpCodes.Newobj, handlerConstructor);
        factoryIl.Emit(OpCodes.Ret);
        return factory;
    }

    private static MethodReference MakeClosedMethod(ModuleDefinition module, MethodDefinition definition, GenericInstanceType declaringType)
    {
        var method = new MethodReference(definition.Name, module.ImportReference(definition.ReturnType), declaringType)
        {
            HasThis = definition.HasThis,
            ExplicitThis = definition.ExplicitThis,
            CallingConvention = definition.CallingConvention,
        };
        foreach (var parameter in definition.Parameters)
            method.Parameters.Add(new ParameterDefinition(module.ImportReference(parameter.ParameterType, method)));
        return method;
    }

    private static MethodDefinition DefineStyleResourceGetter(ModuleDefinition module, TypeDefinition owner, int styleIndex, int setterIndex, TypeDefinition controlType, TypeReference valueType, string key, MethodReference resolve)
    {
        var method = new MethodDefinition($"__StyleResourceValue{styleIndex}_{setterIndex}", MethodAttributes.Private | MethodAttributes.Static, valueType);
        method.Parameters.Add(new ParameterDefinition("target", ParameterAttributes.None, module.ImportReference(controlType)));
        owner.Methods.Add(method);
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, key);
        il.Emit(OpCodes.Call, resolve);
        il.Emit(OpCodes.Ret);
        return method;
    }

    private static void EmitFindControl(ILProcessor body, FormaXamlObject node, int ordinal, MethodReference findOrdinal, MethodReference findName)
    {
        body.Emit(OpCodes.Ldarg_1);
        var targetName = node.FindDirective("Name");
        if (string.IsNullOrWhiteSpace(targetName))
        {
            body.Emit(OpCodes.Ldc_I4, ordinal);
            body.Emit(OpCodes.Call, findOrdinal);
        }
        else
        {
            body.Emit(OpCodes.Ldstr, targetName);
            body.Emit(OpCodes.Call, findName);
        }
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
        => DefineSourceGetter(module, owner, $"__BindingRead{index}", sourceType, property, valueType);

    private static MethodDefinition DefineSourceGetter(ModuleDefinition module, TypeDefinition owner, string name, TypeDefinition sourceType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition(name, MethodAttributes.Private | MethodAttributes.Static, valueType);
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
        => DefineTargetGetter(module, owner, $"__BindingGetTarget{index}", targetType, property, valueType);

    private static MethodDefinition DefineTargetGetter(ModuleDefinition module, TypeDefinition owner, string name, TypeDefinition targetType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition(name, MethodAttributes.Private | MethodAttributes.Static, valueType);
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
        => DefineTargetSetter(module, owner, $"__BindingSetTarget{index}", targetType, property, valueType);

    private static MethodDefinition DefineTargetSetter(ModuleDefinition module, TypeDefinition owner, string name, TypeDefinition targetType, PropertyDefinition property, TypeReference valueType)
    {
        var method = new MethodDefinition(name, MethodAttributes.Private | MethodAttributes.Static, module.TypeSystem.Void);
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