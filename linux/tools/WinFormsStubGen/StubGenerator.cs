using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WinFormsStubGen;

public class GeneratorOptions
{
    public string RefDir { get; set; } = "";
    public string CorePath { get; set; } = "";
    public string DrawingPath { get; set; } = "";
    public string OutDir { get; set; } = "";
}

public class SafeAssemblyResolver : DefaultAssemblyResolver
{
    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        try
        {
            return base.Resolve(name);
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        try
        {
            return base.Resolve(name, parameters);
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }
}

public static class StubGenerator
{
    public static void Generate(GeneratorOptions options)
    {
        if (string.IsNullOrEmpty(options.RefDir) || !Directory.Exists(options.RefDir))
            throw new ArgumentException($"Invalid ref-dir: {options.RefDir}");
        if (string.IsNullOrEmpty(options.CorePath) || !File.Exists(options.CorePath))
            throw new ArgumentException($"Invalid core path: {options.CorePath}");
        if (string.IsNullOrEmpty(options.DrawingPath) || !File.Exists(options.DrawingPath))
            throw new ArgumentException($"Invalid drawing path: {options.DrawingPath}");
        if (string.IsNullOrEmpty(options.OutDir))
            throw new ArgumentException("Output dir not specified");

        Directory.CreateDirectory(options.OutDir);

        var resolver = new SafeAssemblyResolver();
        resolver.AddSearchDirectory(options.RefDir);
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(options.CorePath))!);
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(options.DrawingPath))!);
        resolver.AddSearchDirectory(AppContext.BaseDirectory);

        string dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");
        string netCoreShared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App", "10.0.13");
        if (Directory.Exists(netCoreShared)) resolver.AddSearchDirectory(netCoreShared);

        string nugetHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        string nugetRef = Path.Combine(nugetHome, "microsoft.windowsdesktop.app.ref", "10.0.13", "ref", "net10.0");
        if (Directory.Exists(nugetRef)) resolver.AddSearchDirectory(nugetRef);

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadingMode = ReadingMode.Immediate
        };

        // 1. Read assemblies
        var winFormsAsm = AssemblyDefinition.ReadAssembly(Path.Combine(options.RefDir, "System.Windows.Forms.dll"), readerParams);
        var primAsm = AssemblyDefinition.ReadAssembly(Path.Combine(options.RefDir, "System.Windows.Forms.Primitives.dll"), readerParams);
        var coreAsm = AssemblyDefinition.ReadAssembly(options.CorePath, readerParams);
        var drawingAsm = AssemblyDefinition.ReadAssembly(options.DrawingPath, readerParams);

        // Locate Support assembly
        string supportDllPath = typeof(LiveSplit.Compat.WinForms.Support.StubSupport).Assembly.Location;
        var supportAsm = AssemblyDefinition.ReadAssembly(supportDllPath, readerParams);

        var targetModule = winFormsAsm.MainModule;

        // Assembly metadata setup
        winFormsAsm.Name.Name = "LiveSplit.Compat.WinForms";
        targetModule.Name = "LiveSplit.Compat.WinForms.dll";
        targetModule.Kind = ModuleKind.Dll;

        // Remove ReferenceAssemblyAttribute and RefSafetyRulesAttribute
        RemoveRefAssemblyAttributes(winFormsAsm);
        RemoveRefAssemblyAttributes(targetModule);

        // 2. Merge public types from Primitives
        var primPublicTypes = primAsm.MainModule.Types.Where(t => t.IsPublic && t.Name != "<Module>").ToList();
        var existingTypeNames = targetModule.Types.Select(t => t.FullName).ToHashSet();

        var primReplacementMap = new Dictionary<string, TypeReference>();
        var clonedTypes = new List<(TypeDefinition src, TypeDefinition dst)>();
        foreach (var pt in primPublicTypes)
        {
            if (!existingTypeNames.Contains(pt.FullName))
            {
                var dst = new TypeDefinition(pt.Namespace, pt.Name, pt.Attributes);
                targetModule.Types.Add(dst);
                primReplacementMap[dst.FullName] = dst;
                existingTypeNames.Add(dst.FullName);
                clonedTypes.Add((pt, dst));
            }
        }

        foreach (var (src, dst) in clonedTypes)
        {
            PopulateClonedType(src, dst, targetModule, primReplacementMap);
        }

        // Retarget all references in targetModule pointing to merged primitive types
        ReplaceDroppedTypeReferences(targetModule, primReplacementMap);

        // Add missing System.Windows.Forms.Integration.ElementHost if not present
        if (!existingTypeNames.Contains("System.Windows.Forms.Integration.ElementHost"))
        {
            AddElementHostType(targetModule);
            existingTypeNames.Add("System.Windows.Forms.Integration.ElementHost");
        }

        // 3. Skip/drop types present as public types in LiveSplit.Core
        var corePublicTypes = coreAsm.MainModule.Types.Where(t => t.IsPublic).ToDictionary(t => t.FullName, t => t);
        var coreAsmRef = targetModule.AssemblyReferences.FirstOrDefault(ar => ar.Name == coreAsm.Name.Name);
        if (coreAsmRef == null)
        {
            coreAsmRef = new AssemblyNameReference(coreAsm.Name.Name, coreAsm.Name.Version)
            {
                PublicKeyToken = coreAsm.Name.PublicKeyToken
            };
            targetModule.AssemblyReferences.Add(coreAsmRef);
        }

        var droppedCoreTypes = new Dictionary<string, TypeReference>();
        foreach (var kvp in corePublicTypes)
        {
            string fullName = kvp.Key;
            var coreTypeDef = kvp.Value;
            var tr = new TypeReference(coreTypeDef.Namespace, coreTypeDef.Name, targetModule, coreAsmRef, coreTypeDef.IsValueType);
            droppedCoreTypes[fullName] = tr;
        }

        var typesToDrop = targetModule.Types.Where(t => droppedCoreTypes.ContainsKey(t.FullName)).ToList();
        foreach (var t in typesToDrop)
        {
            targetModule.Types.Remove(t);
            // Add exported type for forwarder
            var expType = new ExportedType(t.Namespace, t.Name, targetModule, coreAsmRef)
            {
                Attributes = TypeAttributes.Public | TypeAttributes.Forwarder
            };
            targetModule.ExportedTypes.Add(expType);
        }

        // Retarget type references in the module pointing to dropped types
        foreach (var tr in targetModule.GetTypeReferences())
        {
            if (droppedCoreTypes.ContainsKey(tr.FullName))
            {
                tr.Scope = coreAsmRef;
            }
        }

        // Replace all references in members that pointed to dropped TypeDefinitions
        ReplaceDroppedTypeReferences(targetModule, droppedCoreTypes);

        // 4. Retarget System.Drawing.Common type references
        string bclPrimitivesPath = Path.Combine(netCoreShared, "System.Drawing.Primitives.dll");
        HashSet<string> bclDrawingPrimitivesTypes = new HashSet<string>();
        if (File.Exists(bclPrimitivesPath))
        {
            var bclPrim = AssemblyDefinition.ReadAssembly(bclPrimitivesPath, readerParams);
            bclDrawingPrimitivesTypes = bclPrim.MainModule.Types.Where(t => t.IsPublic).Select(t => t.FullName).ToHashSet();
        }
        else
        {
            bclDrawingPrimitivesTypes = new HashSet<string>
            {
                "System.Drawing.Color", "System.Drawing.ColorTranslator", "System.Drawing.KnownColor",
                "System.Drawing.Point", "System.Drawing.PointF", "System.Drawing.Rectangle",
                "System.Drawing.RectangleF", "System.Drawing.Size", "System.Drawing.SizeF",
                "System.Drawing.SystemColors"
            };
        }

        var lsDrawingTypes = drawingAsm.MainModule.Types.Where(t => t.IsPublic).Select(t => t.FullName).ToHashSet();
        var lsDrawingAsmRef = targetModule.AssemblyReferences.FirstOrDefault(ar => ar.Name == drawingAsm.Name.Name);
        if (lsDrawingAsmRef == null)
        {
            lsDrawingAsmRef = new AssemblyNameReference(drawingAsm.Name.Name, drawingAsm.Name.Version)
            {
                PublicKeyToken = drawingAsm.Name.PublicKeyToken
            };
            targetModule.AssemblyReferences.Add(lsDrawingAsmRef);
        }

        var drawingPrimitivesAsmRef = targetModule.AssemblyReferences.FirstOrDefault(ar => ar.Name == "System.Drawing.Primitives");
        if (drawingPrimitivesAsmRef == null)
        {
            drawingPrimitivesAsmRef = new AssemblyNameReference("System.Drawing.Primitives", new Version(10, 0, 0, 0))
            {
                PublicKeyToken = new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a }
            };
            targetModule.AssemblyReferences.Add(drawingPrimitivesAsmRef);
        }

        var supportDrawingTypes = supportAsm.MainModule.Types.Where(t => t.IsPublic).Select(t => t.FullName).ToHashSet();
        var supportAsmRef = targetModule.AssemblyReferences.FirstOrDefault(ar => ar.Name == supportAsm.Name.Name);
        if (supportAsmRef == null)
        {
            supportAsmRef = new AssemblyNameReference(supportAsm.Name.Name, supportAsm.Name.Version)
            {
                PublicKeyToken = supportAsm.Name.PublicKeyToken
            };
            targetModule.AssemblyReferences.Add(supportAsmRef);
        }

        var drawingReplacementMap = new Dictionary<string, TypeReference>();
        foreach (var tr in targetModule.GetTypeReferences())
        {
            if (tr.Scope?.Name == "System.Drawing.Common")
            {
                if (bclDrawingPrimitivesTypes.Contains(tr.FullName))
                {
                    tr.Scope = drawingPrimitivesAsmRef;
                }
                else if (lsDrawingTypes.Contains(tr.FullName))
                {
                    tr.Scope = lsDrawingAsmRef;
                }
                else if (supportDrawingTypes.Contains(tr.FullName))
                {
                    tr.Scope = supportAsmRef;
                    var rep = new TypeReference(tr.Namespace, tr.Name, targetModule, supportAsmRef, tr.IsValueType);
                    drawingReplacementMap[tr.FullName] = rep;
                }
            }
        }
        if (drawingReplacementMap.Count > 0)
        {
            ReplaceDroppedTypeReferences(targetModule, drawingReplacementMap);
        }

        // 4.5 Clean missing assembly references (internal types, non-existing platform assemblies)
        var missingAsmNames = new HashSet<string>
        {
            "System.Windows.Forms.Primitives",
            "System.Private.Windows.Core",
            "System.Private.Windows.GdiPlus",
            "Accessibility",
            "System.Configuration.ConfigurationManager",
            "Microsoft.Win32.SystemEvents",
            "System.Formats.Nrbf"
        };
        CleanMissingAssemblyReferences(targetModule, missingAsmNames);

        // 5. Clean custom attributes that reference dropped types, missing assemblies or ref markers
        CleanCustomAttributes(targetModule, missingAsmNames);

        // 6. Make all types instantiable and subclassable
        MakeTypesInstantiable(targetModule);

        // 7. Implement methods, properties, events, collections, constructors
        ImplementStubBehaviors(targetModule, supportAsm, drawingAsm);

        // 8. Write outputs
        string outAssemblyPath = Path.Combine(options.OutDir, "LiveSplit.Compat.WinForms.dll");
        winFormsAsm.Write(outAssemblyPath);

        // Copy Support assembly
        string outSupportPath = Path.Combine(options.OutDir, "LiveSplit.Compat.WinForms.Support.dll");
        File.Copy(supportDllPath, outSupportPath, overwrite: true);

        // Generate LiveSplit.View.dll
        GenerateLiveSplitView(options, winFormsAsm, coreAsm, drawingAsm);

        // Copy LiveSplit.Core and LiveSplit.Drawing to outDir if not present
        string outCore = Path.Combine(options.OutDir, Path.GetFileName(options.CorePath));
        if (!File.Exists(outCore) || Path.GetFullPath(outCore) != Path.GetFullPath(options.CorePath))
        {
            File.Copy(options.CorePath, outCore, overwrite: true);
        }
        string outDrawing = Path.Combine(options.OutDir, Path.GetFileName(options.DrawingPath));
        if (!File.Exists(outDrawing) || Path.GetFullPath(outDrawing) != Path.GetFullPath(options.DrawingPath))
        {
            File.Copy(options.DrawingPath, outDrawing, overwrite: true);
        }
    }

    private static TypeReference ReplaceTypeRef(TypeReference tr, Dictionary<string, TypeReference> replacementMap)
    {
        if (tr == null) return null!;
        if (replacementMap.TryGetValue(tr.FullName, out var rep)) return rep;

        if (tr.IsByReference)
        {
            var elem = ReplaceTypeRef(((ByReferenceType)tr).ElementType, replacementMap);
            return new ByReferenceType(elem);
        }
        if (tr.IsPointer)
        {
            var elem = ReplaceTypeRef(((PointerType)tr).ElementType, replacementMap);
            return new PointerType(elem);
        }
        if (tr.IsArray)
        {
            var arr = (ArrayType)tr;
            var elem = ReplaceTypeRef(arr.ElementType, replacementMap);
            return new ArrayType(elem, arr.Rank);
        }
        if (tr.IsGenericInstance)
        {
            var git = (GenericInstanceType)tr;
            var elem = ReplaceTypeRef(git.ElementType, replacementMap);
            var res = new GenericInstanceType(elem);
            foreach (var arg in git.GenericArguments)
                res.GenericArguments.Add(ReplaceTypeRef(arg, replacementMap));
            return res;
        }

        return tr;
    }

    private static void ReplaceDroppedTypeReferences(ModuleDefinition module, Dictionary<string, TypeReference> replacementMap)
    {
        foreach (var type in module.Types)
        {
            ReplaceInType(type, replacementMap);
        }
    }

    private static void ReplaceInType(TypeDefinition type, Dictionary<string, TypeReference> replacementMap)
    {
        if (type.BaseType != null)
            type.BaseType = ReplaceTypeRef(type.BaseType, replacementMap);

        for (int i = 0; i < type.Interfaces.Count; i++)
        {
            type.Interfaces[i] = new InterfaceImplementation(ReplaceTypeRef(type.Interfaces[i].InterfaceType, replacementMap));
        }

        foreach (var f in type.Fields)
        {
            f.FieldType = ReplaceTypeRef(f.FieldType, replacementMap);
        }

        foreach (var p in type.Properties)
        {
            p.PropertyType = ReplaceTypeRef(p.PropertyType, replacementMap);
        }

        foreach (var e in type.Events)
        {
            e.EventType = ReplaceTypeRef(e.EventType, replacementMap);
        }

        foreach (var m in type.Methods)
        {
            m.ReturnType = ReplaceTypeRef(m.ReturnType, replacementMap);
            foreach (var p in m.Parameters)
            {
                p.ParameterType = ReplaceTypeRef(p.ParameterType, replacementMap);
            }
        }

        foreach (var nt in type.NestedTypes)
        {
            ReplaceInType(nt, replacementMap);
        }
    }

    private static void RemoveRefAssemblyAttributes(ICustomAttributeProvider provider)
    {
        if (!provider.HasCustomAttributes) return;
        var toRemove = provider.CustomAttributes.Where(ca => 
            ca.AttributeType.Name == "ReferenceAssemblyAttribute" ||
            ca.AttributeType.Name == "RefSafetyRulesAttribute").ToList();
        foreach (var ca in toRemove) provider.CustomAttributes.Remove(ca);
    }

    private static bool IsAccessible(TypeDefinition t) =>
        t.IsPublic || t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly;

    private static bool IsAccessibleMember(MethodDefinition m) =>
        m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;

    private static bool IsAccessibleField(FieldDefinition f) =>
        f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly;

    private static bool IsAccessibleProperty(PropertyDefinition p) =>
        (p.GetMethod != null && IsAccessibleMember(p.GetMethod)) ||
        (p.SetMethod != null && IsAccessibleMember(p.SetMethod));

    private static bool IsAccessibleEvent(EventDefinition e) =>
        (e.AddMethod != null && IsAccessibleMember(e.AddMethod)) ||
        (e.RemoveMethod != null && IsAccessibleMember(e.RemoveMethod));

    private static bool ReferencesAnyMissingAssembly(TypeReference tr, HashSet<string> missingAsmNames)
    {
        if (tr == null) return false;
        if (tr.DeclaringType != null && ReferencesAnyMissingAssembly(tr.DeclaringType, missingAsmNames)) return true;
        if (tr.IsByReference) return ReferencesAnyMissingAssembly(((ByReferenceType)tr).ElementType, missingAsmNames);
        if (tr.IsPointer) return ReferencesAnyMissingAssembly(((PointerType)tr).ElementType, missingAsmNames);
        if (tr.IsArray) return ReferencesAnyMissingAssembly(((ArrayType)tr).ElementType, missingAsmNames);
        if (tr.IsRequiredModifier)
        {
            var rmt = (RequiredModifierType)tr;
            return ReferencesAnyMissingAssembly(rmt.ElementType, missingAsmNames) || ReferencesAnyMissingAssembly(rmt.ModifierType, missingAsmNames);
        }
        if (tr.IsOptionalModifier)
        {
            var omt = (OptionalModifierType)tr;
            return ReferencesAnyMissingAssembly(omt.ElementType, missingAsmNames) || ReferencesAnyMissingAssembly(omt.ModifierType, missingAsmNames);
        }
        if (tr.IsPinned) return ReferencesAnyMissingAssembly(((PinnedType)tr).ElementType, missingAsmNames);
        if (tr.IsSentinel) return ReferencesAnyMissingAssembly(((SentinelType)tr).ElementType, missingAsmNames);
        if (tr.IsFunctionPointer)
        {
            var fpt = (FunctionPointerType)tr;
            if (ReferencesAnyMissingAssembly(fpt.ReturnType, missingAsmNames)) return true;
            foreach (var p in fpt.Parameters)
                if (ReferencesAnyMissingAssembly(p.ParameterType, missingAsmNames)) return true;
            return false;
        }
        if (tr.IsGenericInstance)
        {
            var git = (GenericInstanceType)tr;
            if (ReferencesAnyMissingAssembly(git.ElementType, missingAsmNames)) return true;
            foreach (var arg in git.GenericArguments)
                if (ReferencesAnyMissingAssembly(arg, missingAsmNames)) return true;
            return false;
        }
        return tr.Scope != null && missingAsmNames.Contains(tr.Scope.Name);
    }

    private static void CleanMissingAssemblyReferences(ModuleDefinition targetModule, HashSet<string> missingAsmNames)
    {
        void CleanType(TypeDefinition t)
        {
            // Clean interfaces
            for (int i = t.Interfaces.Count - 1; i >= 0; i--)
            {
                if (ReferencesAnyMissingAssembly(t.Interfaces[i].InterfaceType, missingAsmNames))
                {
                    t.Interfaces.RemoveAt(i);
                }
            }

            // Clean base type
            if (t.BaseType != null && ReferencesAnyMissingAssembly(t.BaseType, missingAsmNames))
            {
                t.BaseType = targetModule.TypeSystem.Object;
            }

            // Clean methods
            for (int i = t.Methods.Count - 1; i >= 0; i--)
            {
                var m = t.Methods[i];
                if (ReferencesAnyMissingAssembly(m.ReturnType, missingAsmNames) ||
                    m.Parameters.Any(p => ReferencesAnyMissingAssembly(p.ParameterType, missingAsmNames)))
                {
                    t.Methods.RemoveAt(i);
                }
                else
                {
                    for (int j = m.Overrides.Count - 1; j >= 0; j--)
                    {
                        if (ReferencesAnyMissingAssembly(m.Overrides[j].DeclaringType, missingAsmNames))
                        {
                            m.Overrides.RemoveAt(j);
                        }
                    }
                }
            }

            // Clean properties
            for (int i = t.Properties.Count - 1; i >= 0; i--)
            {
                var p = t.Properties[i];
                if (ReferencesAnyMissingAssembly(p.PropertyType, missingAsmNames))
                {
                    t.Properties.RemoveAt(i);
                    if (p.GetMethod != null) t.Methods.Remove(p.GetMethod);
                    if (p.SetMethod != null) t.Methods.Remove(p.SetMethod);
                }
            }

            // Clean fields
            for (int i = t.Fields.Count - 1; i >= 0; i--)
            {
                var f = t.Fields[i];
                if (ReferencesAnyMissingAssembly(f.FieldType, missingAsmNames))
                {
                    t.Fields.RemoveAt(i);
                }
            }

            // Clean events
            for (int i = t.Events.Count - 1; i >= 0; i--)
            {
                var e = t.Events[i];
                if (ReferencesAnyMissingAssembly(e.EventType, missingAsmNames))
                {
                    t.Events.RemoveAt(i);
                    if (e.AddMethod != null) t.Methods.Remove(e.AddMethod);
                    if (e.RemoveMethod != null) t.Methods.Remove(e.RemoveMethod);
                }
            }

            for (int i = t.NestedTypes.Count - 1; i >= 0; i--)
            {
                var nt = t.NestedTypes[i];
                if (IsDelegate(nt))
                {
                    bool delegateBroken = nt.Methods.Any(m =>
                        ReferencesAnyMissingAssembly(m.ReturnType, missingAsmNames) ||
                        m.Parameters.Any(p => ReferencesAnyMissingAssembly(p.ParameterType, missingAsmNames)));
                    if (delegateBroken)
                    {
                        t.NestedTypes.RemoveAt(i);
                        continue;
                    }
                }
                CleanType(nt);
            }
        }

        for (int i = targetModule.Types.Count - 1; i >= 0; i--)
        {
            var t = targetModule.Types[i];
            if (t.Name == "<Module>") continue;
            if (IsDelegate(t))
            {
                bool delegateBroken = t.Methods.Any(m =>
                    ReferencesAnyMissingAssembly(m.ReturnType, missingAsmNames) ||
                    m.Parameters.Any(p => ReferencesAnyMissingAssembly(p.ParameterType, missingAsmNames)));
                if (delegateBroken)
                {
                    targetModule.Types.RemoveAt(i);
                    continue;
                }
            }
            CleanType(t);
        }

        // Clean ExportedTypes
        for (int i = targetModule.ExportedTypes.Count - 1; i >= 0; i--)
        {
            var exp = targetModule.ExportedTypes[i];
            if (exp.Scope != null && missingAsmNames.Contains(exp.Scope.Name))
            {
                targetModule.ExportedTypes.RemoveAt(i);
            }
        }

        // Retarget any leftover TypeReferences pointing to missing assemblies to System.Runtime
        var systemRuntime = targetModule.AssemblyReferences.FirstOrDefault(a => a.Name == "System.Runtime");
        foreach (var tr in targetModule.GetTypeReferences())
        {
            if (ReferencesAnyMissingAssembly(tr, missingAsmNames))
            {
                tr.DeclaringType = null;
                if (systemRuntime != null)
                {
                    tr.Scope = systemRuntime;
                }
                int arity = tr.GenericParameters.Count;
                if (arity > 0)
                {
                    tr.Namespace = "System";
                    tr.Name = $"Action`{arity}";
                }
                else if (tr.IsValueType)
                {
                    tr.Namespace = "System";
                    tr.Name = "Int32";
                }
                else
                {
                    tr.Namespace = "System";
                    tr.Name = "Object";
                }
            }
        }

        // Remove missing assemblies from AssemblyReferences
        for (int i = targetModule.AssemblyReferences.Count - 1; i >= 0; i--)
        {
            var ar = targetModule.AssemblyReferences[i];
            if (missingAsmNames.Contains(ar.Name))
            {
                targetModule.AssemblyReferences.RemoveAt(i);
            }
        }
    }

    private static void CleanCustomAttributes(ModuleDefinition module, HashSet<string> missingAsmNames)
    {
        CleanCustomAttributesOnProvider(module.Assembly, missingAsmNames);
        CleanCustomAttributesOnProvider(module, missingAsmNames);

        foreach (var t in module.Types)
        {
            if (t.Name == "<Module>") continue;
            CleanTypeAttributes(t, missingAsmNames);
        }
    }

    private static bool IsDrawingCommon(TypeReference tr)
    {
        if (tr == null) return false;
        if (tr.Scope?.Name == "System.Drawing.Common") return true;
        if (tr.DeclaringType != null && IsDrawingCommon(tr.DeclaringType)) return true;
        return false;
    }

    private static void CleanCustomAttributesOnProvider(ICustomAttributeProvider p, HashSet<string> missingAsmNames)
    {
        if (!p.HasCustomAttributes) return;
        var toRemove = p.CustomAttributes.Where(ca =>
            ca.AttributeType.Name == "ReferenceAssemblyAttribute" ||
            ca.AttributeType.Name == "RefSafetyRulesAttribute" ||
            ca.AttributeType.Name.StartsWith("Nullable") ||
            IsDrawingCommon(ca.AttributeType) ||
            ReferencesAnyMissingAssembly(ca.AttributeType, missingAsmNames) ||
            (ca.Constructor != null && (IsDrawingCommon(ca.Constructor.DeclaringType) || ReferencesAnyMissingAssembly(ca.Constructor.DeclaringType, missingAsmNames)))).ToList();
        foreach (var ca in toRemove) p.CustomAttributes.Remove(ca);
    }

    private static void CleanTypeAttributes(TypeDefinition t, HashSet<string> missingAsmNames)
    {
        CleanCustomAttributesOnProvider(t, missingAsmNames);

        foreach (var m in t.Methods)
        {
            CleanCustomAttributesOnProvider(m, missingAsmNames);
            CleanCustomAttributesOnProvider(m.MethodReturnType, missingAsmNames);
            foreach (var p in m.Parameters) CleanCustomAttributesOnProvider(p, missingAsmNames);
        }
        foreach (var f in t.Fields) CleanCustomAttributesOnProvider(f, missingAsmNames);
        foreach (var p in t.Properties) CleanCustomAttributesOnProvider(p, missingAsmNames);
        foreach (var e in t.Events) CleanCustomAttributesOnProvider(e, missingAsmNames);

        foreach (var nt in t.NestedTypes)
        {
            CleanTypeAttributes(nt, missingAsmNames);
        }
    }

    private static void MakeTypesInstantiable(ModuleDefinition module)
    {
        foreach (var t in module.Types)
        {
            if (t.Name == "<Module>") continue;
            MakeTypeInstantiable(t);
        }
    }

    private static bool IsDelegate(TypeDefinition t)
    {
        var curr = t.BaseType;
        while (curr != null)
        {
            if (curr.FullName == "System.MulticastDelegate" || curr.FullName == "System.Delegate")
                return true;
            try
            {
                var resolved = curr.Resolve();
                if (resolved == null || resolved == curr) break;
                curr = resolved.BaseType;
            }
            catch
            {
                break;
            }
        }
        return false;
    }

    private static void MakeTypeInstantiable(TypeDefinition t)
    {
        if (t.IsInterface || t.IsValueType || t.IsEnum || IsDelegate(t))
        {
            foreach (var nt in t.NestedTypes)
            {
                MakeTypeInstantiable(nt);
            }
            return;
        }

        if (t.IsClass && !(t.IsAbstract && t.IsSealed))
        {
            t.Attributes &= ~TypeAttributes.Abstract;

            // Check if class has accessible parameterless instance constructor
            bool hasParameterlessCtor = t.Methods.Any(m => m.IsConstructor && !m.IsStatic && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly) && m.Parameters.Count == 0);
            if (!hasParameterlessCtor)
            {
                var defaultCtor = new MethodDefinition(".ctor",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    t.Module.TypeSystem.Void);
                defaultCtor.Body = new MethodBody(defaultCtor);
                t.Methods.Add(defaultCtor);
            }
        }

        if (t.IsClass && t.Interfaces.Any(i => i.InterfaceType.FullName == "System.IDisposable"))
        {
            bool hasDispose = t.Methods.Any(m => m.Name == "Dispose" && m.Parameters.Count == 0);
            if (!hasDispose)
            {
                var dispMethod = new MethodDefinition("Dispose",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                    t.Module.TypeSystem.Void);
                dispMethod.Body = new MethodBody(dispMethod);
                dispMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                t.Methods.Add(dispMethod);
            }
        }

        foreach (var m in t.Methods)
        {
            if (m.IsAbstract)
            {
                m.Attributes &= ~MethodAttributes.Abstract;
            }
        }

        foreach (var nt in t.NestedTypes)
        {
            MakeTypeInstantiable(nt);
        }
    }

    private static void AddElementHostType(ModuleDefinition module)
    {
        var controlType = module.Types.FirstOrDefault(t => t.FullName == "System.Windows.Forms.Control");
        var hostType = new TypeDefinition("System.Windows.Forms.Integration", "ElementHost",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            controlType ?? module.TypeSystem.Object);

        // .ctor()
        var ctor = new MethodDefinition(".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            module.TypeSystem.Void);
        ctor.Body = new MethodBody(ctor);
        hostType.Methods.Add(ctor);

        // static EnableModelessKeyboardInterop(object window)
        var enableMethod = new MethodDefinition("EnableModelessKeyboardInterop",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        enableMethod.Parameters.Add(new ParameterDefinition("window", ParameterAttributes.None, module.TypeSystem.Object));
        enableMethod.Body = new MethodBody(enableMethod);
        enableMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        hostType.Methods.Add(enableMethod);

        // Child property (object)
        var childProp = new PropertyDefinition("Child", PropertyAttributes.None, module.TypeSystem.Object);
        var getChild = new MethodDefinition("get_Child",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            module.TypeSystem.Object);
        getChild.Body = new MethodBody(getChild);
        var setChild = new MethodDefinition("set_Child",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        setChild.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.Object));
        setChild.Body = new MethodBody(setChild);
        childProp.GetMethod = getChild;
        childProp.SetMethod = setChild;
        hostType.Methods.Add(getChild);
        hostType.Methods.Add(setChild);
        hostType.Properties.Add(childProp);

        module.Types.Add(hostType);
    }

    private static TypeReference ImportType(TypeReference tr, ModuleDefinition targetModule, Dictionary<string, TypeReference> replacementMap = null)
    {
        if (tr == null) return null!;
        if (replacementMap != null && replacementMap.TryGetValue(tr.FullName, out var rep)) return rep;

        if (tr.IsByReference) return new ByReferenceType(ImportType(((ByReferenceType)tr).ElementType, targetModule, replacementMap));
        if (tr.IsPointer) return new PointerType(ImportType(((PointerType)tr).ElementType, targetModule, replacementMap));
        if (tr.IsArray)
        {
            var arr = (ArrayType)tr;
            return new ArrayType(ImportType(arr.ElementType, targetModule, replacementMap), arr.Rank);
        }
        if (tr.IsGenericInstance)
        {
            var git = (GenericInstanceType)tr;
            var res = new GenericInstanceType(ImportType(git.ElementType, targetModule, replacementMap));
            foreach (var arg in git.GenericArguments) res.GenericArguments.Add(ImportType(arg, targetModule, replacementMap));
            return res;
        }
        if (tr is GenericParameter gp) return gp;

        var existing = targetModule.Types.FirstOrDefault(t => t.FullName == tr.FullName);
        if (existing != null) return existing;

        return targetModule.ImportReference(tr);
    }

    private static void PopulateClonedType(TypeDefinition src, TypeDefinition dst, ModuleDefinition targetModule, Dictionary<string, TypeReference> replacementMap)
    {
        if (src.BaseType != null) dst.BaseType = ImportType(src.BaseType, targetModule, replacementMap);

        foreach (var iface in src.Interfaces)
            dst.Interfaces.Add(new InterfaceImplementation(ImportType(iface.InterfaceType, targetModule, replacementMap)));

        foreach (var gp in src.GenericParameters)
            dst.GenericParameters.Add(new GenericParameter(gp.Name, dst));

        foreach (var f in src.Fields)
        {
            var df = new FieldDefinition(f.Name, f.Attributes, ImportType(f.FieldType, targetModule, replacementMap));
            if (f.HasConstant) df.Constant = f.Constant;
            dst.Fields.Add(df);
        }

        var methodMap = new Dictionary<MethodDefinition, MethodDefinition>();
        foreach (var m in src.Methods)
        {
            var dm = new MethodDefinition(m.Name, m.Attributes, ImportType(m.ReturnType, targetModule, replacementMap));
            dm.ImplAttributes = m.ImplAttributes;
            foreach (var gp in m.GenericParameters) dm.GenericParameters.Add(new GenericParameter(gp.Name, dm));
            foreach (var p in m.Parameters)
            {
                var dp = new ParameterDefinition(p.Name, p.Attributes, ImportType(p.ParameterType, targetModule, replacementMap));
                if (p.HasConstant) dp.Constant = p.Constant;
                dm.Parameters.Add(dp);
            }
            dst.Methods.Add(dm);
            methodMap[m] = dm;
        }

        foreach (var p in src.Properties)
        {
            var dp = new PropertyDefinition(p.Name, p.Attributes, ImportType(p.PropertyType, targetModule, replacementMap));
            if (p.GetMethod != null && methodMap.TryGetValue(p.GetMethod, out var gm)) dp.GetMethod = gm;
            if (p.SetMethod != null && methodMap.TryGetValue(p.SetMethod, out var sm)) dp.SetMethod = sm;
            dst.Properties.Add(dp);
        }

        foreach (var e in src.Events)
        {
            var de = new EventDefinition(e.Name, e.Attributes, ImportType(e.EventType, targetModule, replacementMap));
            if (e.AddMethod != null && methodMap.TryGetValue(e.AddMethod, out var am)) de.AddMethod = am;
            if (e.RemoveMethod != null && methodMap.TryGetValue(e.RemoveMethod, out var rm)) de.RemoveMethod = rm;
            dst.Events.Add(de);
        }

        foreach (var nt in src.NestedTypes)
        {
            var dnt = new TypeDefinition(nt.Namespace, nt.Name, nt.Attributes);
            dst.NestedTypes.Add(dnt);
            if (replacementMap != null) replacementMap[dnt.FullName] = dnt;
            PopulateClonedType(nt, dnt, targetModule, replacementMap);
        }
    }

    private sealed class StubContext
    {
        public ModuleDefinition Module { get; set; } = null!;
        public AssemblyDefinition SupportAsm { get; set; } = null!;
        public MethodReference ColAdd { get; set; } = null!;
        public MethodReference ColAddEmpty { get; set; } = null!;
        public MethodReference ColAddKeyText { get; set; } = null!;
        public MethodReference ColAddRange { get; set; } = null!;
        public MethodReference ColRemove { get; set; } = null!;
        public MethodReference ColRemoveAt { get; set; } = null!;
        public MethodReference ColClear { get; set; } = null!;
        public MethodReference ColCount { get; set; } = null!;
        public MethodReference ColContains { get; set; } = null!;
        public MethodReference ColIndexOf { get; set; } = null!;
        public MethodReference ColGetItem { get; set; } = null!;
        public MethodReference ColGetItemByName { get; set; } = null!;
        public MethodReference ColSetItem { get; set; } = null!;
        public MethodReference ColGetEnumerator { get; set; } = null!;
        public MethodReference ColInsert { get; set; } = null!;
        public MethodReference ColCopyTo { get; set; } = null!;
        public MethodReference TlpAdd { get; set; } = null!;
        public MethodReference CtrlBindingsAdd { get; set; } = null!;
        public MethodReference DgvRowAdd { get; set; } = null!;
        public MethodReference DgvColAdd { get; set; } = null!;
        public MethodReference DgvRowCountSet { get; set; } = null!;
        public MethodReference DgvColumnCountSet { get; set; } = null!;
        public MethodReference DgvBandGetIndex { get; set; } = null!;
        public MethodReference TsmiAdd { get; set; } = null!;
        public MethodReference ClampNumeric { get; set; } = null!;
        public MethodReference InvokeDelegate { get; set; } = null!;
        public MethodReference FireEvent { get; set; } = null!;
        public MethodReference ValuesEqual { get; set; } = null!;
        public MethodReference SetClip { get; set; } = null!;
        public MethodReference GetClip { get; set; } = null!;
        public MethodReference HasClip { get; set; } = null!;
        public MethodReference ClrClip { get; set; } = null!;
        public MethodReference GetStartup { get; set; } = null!;
        public MethodReference GetExec { get; set; } = null!;
        public MethodReference GetProd { get; set; } = null!;
        public MethodReference GetSelItem { get; set; } = null!;
        public MethodReference SetCollectionOwner { get; set; } = null!;
        public MethodReference GetDefaultFont { get; set; } = null!;
        public MethodReference CreateGraphics { get; set; } = null!;
        public MethodReference DelegateCombine { get; set; } = null!;
        public MethodReference DelegateRemove { get; set; } = null!;
        public MethodReference FontCtor { get; set; } = null!;
        public MethodReference InitPrimaryScreen { get; set; } = null!;
        public MethodReference TreeNodeGetIndex { get; set; } = null!;
        public MethodReference TreeNodeGetLevel { get; set; } = null!;
        public MethodReference TreeNodeGetFullPath { get; set; } = null!;
        public MethodReference TreeNodeGetPrevNode { get; set; } = null!;
        public MethodReference TreeNodeGetNextNode { get; set; } = null!;
        public MethodReference TreeNodeGetFirstNode { get; set; } = null!;
        public MethodReference TreeNodeGetLastNode { get; set; } = null!;
        public MethodReference TreeNodeGetPrevVisibleNode { get; set; } = null!;
        public MethodReference TreeNodeGetNextVisibleNode { get; set; } = null!;
        public MethodReference TreeNodeGetTreeView { get; set; } = null!;
        public MethodReference TreeNodeExpand { get; set; } = null!;
        public MethodReference TreeNodeCollapse { get; set; } = null!;
        public MethodReference TreeNodeToggle { get; set; } = null!;
        public MethodReference TreeNodeGetBounds { get; set; } = null!;
        public MethodReference TreeNodeCollectionFind { get; set; } = null!;
        public MethodReference TreeViewSetSelectedNode { get; set; } = null!;
    }

    private static void ImplementStubBehaviors(ModuleDefinition module, AssemblyDefinition supportAsm, AssemblyDefinition drawingAsm)
    {
        var supportType = supportAsm.MainModule.Types.First(t => t.Name == "StubSupport");

        MethodReference ImportSupport(string name, int paramCount = -1)
        {
            var method = paramCount >= 0
                ? supportType.Methods.First(m => m.Name == name && m.Parameters.Count == paramCount)
                : supportType.Methods.First(m => m.Name == name);
            return module.ImportReference(method);
        }

        var fontType = drawingAsm.MainModule.Types.First(t => t.FullName == "System.Drawing.Font");
        var fontCtor = fontType.Methods.First(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 2
            && m.Parameters[0].ParameterType.FullName == "System.String"
            && m.Parameters[1].ParameterType.FullName == "System.Single");

        var ctx = new StubContext
        {
            Module = module,
            SupportAsm = supportAsm,
            ColAdd = ImportSupport("CollectionAdd"),
            ColAddEmpty = ImportSupport("CollectionAddEmpty"),
            ColAddKeyText = ImportSupport("CollectionAddKeyText"),
            ColAddRange = ImportSupport("CollectionAddRange"),
            ColRemove = ImportSupport("CollectionRemove"),
            ColRemoveAt = ImportSupport("CollectionRemoveAt"),
            ColClear = ImportSupport("CollectionClear"),
            ColCount = ImportSupport("CollectionCount"),
            ColContains = ImportSupport("CollectionContains"),
            ColIndexOf = ImportSupport("CollectionIndexOf"),
            ColGetItem = ImportSupport("CollectionGetItem"),
            ColGetItemByName = ImportSupport("CollectionGetItemByName"),
            ColSetItem = ImportSupport("CollectionSetItem"),
            ColGetEnumerator = ImportSupport("CollectionGetEnumerator"),
            ColInsert = ImportSupport("CollectionInsert"),
            ColCopyTo = ImportSupport("CollectionCopyTo"),
            TlpAdd = ImportSupport("TableLayoutControlCollectionAdd"),
            CtrlBindingsAdd = ImportSupport("ControlBindingsCollectionAdd"),
            DgvRowAdd = ImportSupport("DgvRowCollectionAdd"),
            DgvColAdd = ImportSupport("DgvColumnCollectionAdd"),
            DgvRowCountSet = ImportSupport("DgvRowCountSet"),
            DgvColumnCountSet = ImportSupport("DgvColumnCountSet"),
            DgvBandGetIndex = ImportSupport("DgvBandGetIndex"),
            TsmiAdd = ImportSupport("ToolStripItemCollectionAdd"),
            ClampNumeric = ImportSupport("ClampNumeric"),
            InvokeDelegate = ImportSupport("InvokeDelegate"),
            FireEvent = ImportSupport("FireEvent", 4),
            ValuesEqual = ImportSupport("ValuesEqual"),
            SetClip = ImportSupport("SetClipboardText"),
            GetClip = ImportSupport("GetClipboardText"),
            HasClip = ImportSupport("ContainsClipboardText"),
            ClrClip = ImportSupport("ClearClipboard"),
            GetStartup = ImportSupport("get_StartupPath"),
            GetExec = ImportSupport("get_ExecutablePath"),
            GetProd = ImportSupport("get_ProductVersion"),
            GetSelItem = ImportSupport("GetSelectedItem"),
            SetCollectionOwner = ImportSupport("SetCollectionOwner"),
            GetDefaultFont = ImportSupport("GetDefaultFont"),
            CreateGraphics = ImportSupport("CreateGraphics"),
            DelegateCombine = module.ImportReference(typeof(Delegate).GetMethod("Combine", new[] { typeof(Delegate), typeof(Delegate) })!),
            DelegateRemove = module.ImportReference(typeof(Delegate).GetMethod("Remove", new[] { typeof(Delegate), typeof(Delegate) })!),
            FontCtor = module.ImportReference(fontCtor),
            InitPrimaryScreen = ImportSupport("InitPrimaryScreen"),
            TreeNodeGetIndex = ImportSupport("TreeNodeGetIndex"),
            TreeNodeGetLevel = ImportSupport("TreeNodeGetLevel"),
            TreeNodeGetFullPath = ImportSupport("TreeNodeGetFullPath"),
            TreeNodeGetPrevNode = ImportSupport("TreeNodeGetPrevNode"),
            TreeNodeGetNextNode = ImportSupport("TreeNodeGetNextNode"),
            TreeNodeGetFirstNode = ImportSupport("TreeNodeGetFirstNode"),
            TreeNodeGetLastNode = ImportSupport("TreeNodeGetLastNode"),
            TreeNodeGetPrevVisibleNode = ImportSupport("TreeNodeGetPrevVisibleNode"),
            TreeNodeGetNextVisibleNode = ImportSupport("TreeNodeGetNextVisibleNode"),
            TreeNodeGetTreeView = ImportSupport("TreeNodeGetTreeView"),
            TreeNodeExpand = ImportSupport("TreeNodeExpand"),
            TreeNodeCollapse = ImportSupport("TreeNodeCollapse"),
            TreeNodeToggle = ImportSupport("TreeNodeToggle"),
            TreeNodeGetBounds = ImportSupport("TreeNodeGetBounds"),
            TreeNodeCollectionFind = ImportSupport("TreeNodeCollectionFind"),
            TreeViewSetSelectedNode = ImportSupport("TreeViewSetSelectedNode")
        };

        var sortedTypes = GetAllTypesFlat(module).OrderBy(GetInheritanceDepth).ToList();
        foreach (var type in sortedTypes)
        {
            ImplementTypeBehaviors(type, ctx);
        }
    }

    private static List<TypeDefinition> GetAllTypesFlat(ModuleDefinition module)
    {
        var list = new List<TypeDefinition>();
        void AddRecursive(TypeDefinition t)
        {
            if (t.Name != "<Module>") list.Add(t);
            foreach (var nt in t.NestedTypes) AddRecursive(nt);
        }
        foreach (var t in module.Types) AddRecursive(t);
        return list;
    }

    private static int GetInheritanceDepth(TypeDefinition t)
    {
        int d = 0;
        var curr = t;
        while (curr.BaseType != null)
        {
            d++;
            try
            {
                var b = curr.BaseType.Resolve();
                if (b == null || b == curr) break;
                curr = b;
            }
            catch { break; }
        }
        return d;
    }

    private static bool IsCollectionType(TypeDefinition t)
    {
        if (t == null) return false;
        string name = t.Name;
        if (name.EndsWith("Collection") || name.EndsWith("CollectionBase"))
            return true;

        var curr = t;
        while (curr != null)
        {
            if (curr.Name is "ArrangedElementCollection" or "BaseCollection" or "ControlCollection"
                or "TableLayoutStyleCollection" or "ObjectCollection" or "ListViewItemCollection"
                or "TreeNodeCollection" or "ToolStripItemCollection" or "BindingsCollection"
                or "DataGridViewRowCollection" or "DataGridViewColumnCollection" or "DataGridViewCellCollection"
                or "MenuItemCollection" or "ColumnHeaderCollection" or "TabPageCollection")
                return true;

            foreach (var iface in curr.Interfaces)
            {
                string ifaceName = iface.InterfaceType.FullName;
                if (ifaceName is "System.Collections.IList" or "System.Collections.ICollection" or "System.Collections.IEnumerable"
                    || ifaceName.StartsWith("System.Collections.Generic.IList`")
                    || ifaceName.StartsWith("System.Collections.Generic.ICollection`")
                    || ifaceName.StartsWith("System.Collections.Generic.IEnumerable`"))
                {
                    return true;
                }
            }

            if (curr.BaseType == null) break;
            try
            {
                var resolved = curr.BaseType.Resolve();
                if (resolved == null || resolved == curr) break;
                curr = resolved;
            }
            catch
            {
                break;
            }
        }
        return false;
    }

    private static FieldReference? FindEventField(TypeDefinition type, string evtName, StubContext ctx)
    {
        var curr = type;
        while (curr != null)
        {
            string fieldName = $"<{evtName}>k__BackingField";
            var ef = curr.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (ef != null)
            {
                return curr == type ? ef : ctx.Module.ImportReference(ef);
            }
            if (curr.BaseType == null) break;
            try
            {
                var b = curr.BaseType.Resolve();
                if (b == null || b == curr) break;
                curr = b;
            }
            catch { break; }
        }
        return null;
    }

    private static FieldReference? FindBackingField(TypeDefinition type, string propName, StubContext ctx)
    {
        var curr = type;
        while (curr != null)
        {
            string fieldName = $"<{propName}>k__BackingField";
            var bf = curr.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (bf != null)
            {
                return curr == type ? bf : ctx.Module.ImportReference(bf);
            }
            if (curr.BaseType == null) break;
            try
            {
                var b = curr.BaseType.Resolve();
                if (b == null || b == curr) break;
                curr = b;
            }
            catch { break; }
        }
        return null;
    }

    private static PropertyDefinition? FindProperty(TypeDefinition type, string propName)
    {
        var curr = type;
        while (curr != null)
        {
            var p = curr.Properties.FirstOrDefault(x => x.Name == propName);
            if (p != null) return p;
            if (curr.BaseType == null) break;
            try
            {
                var b = curr.BaseType.Resolve();
                if (b == null || b == curr) break;
                curr = b;
            }
            catch { break; }
        }
        return null;
    }

    private static TypeReference? GetCollectionElementType(TypeDefinition colType)
    {
        // 1. Check indexer: get_Item(int)
        var intIndexer = colType.Properties.FirstOrDefault(p =>
            p.Parameters.Count == 1 &&
            p.Parameters[0].ParameterType.FullName == "System.Int32" &&
            p.PropertyType.FullName != "System.Object");
        if (intIndexer != null)
        {
            return intIndexer.PropertyType;
        }

        // 2. Check interfaces: IEnumerable<T>, IList<T>, ICollection<T>
        var curr = colType;
        while (curr != null)
        {
            foreach (var iface in curr.Interfaces)
            {
                if (iface.InterfaceType is GenericInstanceType git)
                {
                    string elName = git.ElementType.FullName;
                    if (elName is "System.Collections.Generic.IEnumerable`1"
                        or "System.Collections.Generic.ICollection`1"
                        or "System.Collections.Generic.IList`1")
                    {
                        if (git.GenericArguments.Count == 1 && git.GenericArguments[0].FullName != "System.Object")
                        {
                            return git.GenericArguments[0];
                        }
                    }
                }
            }
            if (curr.BaseType == null) break;
            try
            {
                var resolved = curr.BaseType.Resolve();
                if (resolved == null || resolved == curr) break;
                curr = resolved;
            }
            catch { break; }
        }

        // 3. Check methods named "Add" that return a non-void, non-int, non-object type
        foreach (var m in colType.Methods.Where(m => m.Name == "Add"))
        {
            if (m.ReturnType.FullName is not "System.Void" and not "System.Int32" and not "System.Object")
            {
                return m.ReturnType;
            }
        }

        // 4. Name-based heuristics
        string name = colType.Name;
        if (name.EndsWith("Collection"))
        {
            string elemName = name.Substring(0, name.Length - "Collection".Length);
            var matchingType = colType.Module.Types.FirstOrDefault(t => t.Name == elemName || t.Name == elemName + "Item");
            if (matchingType != null) return matchingType;
        }

        return null;
    }

    private static bool IsParameterTypeCompatible(TypeReference argType, TypeReference paramType)
    {
        if (argType.FullName == paramType.FullName) return true;
        if (paramType.FullName == "System.Object") return true;

        var argDef = argType.Resolve();
        var paramDef = paramType.Resolve();
        if (argDef != null && paramDef != null)
        {
            if (InheritsFrom(argDef, paramDef.FullName)) return true;
            if (paramDef.IsInterface)
            {
                var curr = argDef;
                while (curr != null)
                {
                    if (curr.Interfaces.Any(i => i.InterfaceType.FullName == paramDef.FullName)) return true;
                    if (curr.BaseType == null) break;
                    try
                    {
                        var b = curr.BaseType.Resolve();
                        if (b == null || b == curr) break;
                        curr = b;
                    }
                    catch { break; }
                }
            }
        }
        return false;
    }

    private static MethodReference? FindMatchingConstructor(TypeDefinition elemTypeDef, Mono.Collections.Generic.Collection<ParameterDefinition> parameters, ModuleDefinition module)
    {
        foreach (var ctor in elemTypeDef.Methods.Where(c => c.IsConstructor && !c.IsStatic && (c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly || c.IsAssembly)))
        {
            if (ctor.Parameters.Count == parameters.Count)
            {
                bool match = true;
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (!IsParameterTypeCompatible(parameters[i].ParameterType, ctor.Parameters[i].ParameterType))
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return ctor.DeclaringType.Module == module ? ctor : module.ImportReference(ctor);
                }
            }
        }
        return null;
    }

    private static void ImplementTypeBehaviors(TypeDefinition type, StubContext ctx)
    {
        if (type.IsInterface)
        {
            foreach (var m in type.Methods)
            {
                if (m.HasBody)
                {
                    m.Body = new MethodBody(m);
                    PushDefaultValue(m.Body.Instructions, m.Body, m.ReturnType, ctx.Module);
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            return;
        }

        if (type.IsEnum || IsDelegate(type))
        {
            return;
        }

        bool isCollection = IsCollectionType(type);

        // 1. Process Fields for property backing
        var backingFields = new Dictionary<string, FieldReference>();
        foreach (var prop in type.Properties)
        {
            if (prop.Parameters.Count == 0) // Not an indexer
            {
                var existingBf = FindBackingField(type, prop.Name, ctx);
                if (existingBf != null && existingBf.FieldType.FullName == prop.PropertyType.FullName)
                {
                    backingFields[prop.Name] = existingBf;
                }
                else
                {
                    string fieldName = $"<{prop.Name}>k__BackingField";
                    var bf = type.Fields.FirstOrDefault(f => f.Name == fieldName);
                    if (bf == null)
                    {
                        var fieldAttr = FieldAttributes.Family;
                        if ((prop.GetMethod != null && prop.GetMethod.IsStatic) || (prop.SetMethod != null && prop.SetMethod.IsStatic))
                        {
                            fieldAttr |= FieldAttributes.Static;
                        }
                        bf = new FieldDefinition(fieldName, fieldAttr, prop.PropertyType);
                        type.Fields.Add(bf);
                    }
                    backingFields[prop.Name] = bf;
                }
            }
        }

        // 2. Process Events
        var eventFields = new Dictionary<string, FieldReference>();
        foreach (var evt in type.Events)
        {
            string fieldName = $"<{evt.Name}>k__BackingField";
            var ef = type.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (ef == null)
            {
                var fieldAttr = FieldAttributes.Family;
                if ((evt.AddMethod != null && evt.AddMethod.IsStatic) || (evt.RemoveMethod != null && evt.RemoveMethod.IsStatic))
                {
                    fieldAttr |= FieldAttributes.Static;
                }
                ef = new FieldDefinition(fieldName, fieldAttr, evt.EventType);
                type.Fields.Add(ef);
            }
            eventFields[evt.Name] = ef;

            if (evt.AddMethod != null)
            {
                var m = evt.AddMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, ef));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, ctx.DelegateCombine));
                il.Add(Instruction.Create(OpCodes.Castclass, evt.EventType));
                il.Add(Instruction.Create(OpCodes.Stfld, ef));
                il.Add(Instruction.Create(OpCodes.Ret));
            }

            if (evt.RemoveMethod != null)
            {
                var m = evt.RemoveMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, ef));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, ctx.DelegateRemove));
                il.Add(Instruction.Create(OpCodes.Castclass, evt.EventType));
                il.Add(Instruction.Create(OpCodes.Stfld, ef));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
        }

        // 3. Process Constructors
        foreach (var ctor in type.Methods.Where(m => m.IsConstructor && !m.IsStatic))
        {
            ImplementConstructor(ctor, type, ctx, backingFields, isCollection);
        }

        // 4. Process Properties
        var propertyMethods = new HashSet<MethodDefinition>();
        foreach (var prop in type.Properties)
        {
            if (prop.GetMethod != null) propertyMethods.Add(prop.GetMethod);
            if (prop.SetMethod != null) propertyMethods.Add(prop.SetMethod);
            ImplementProperty(prop, type, ctx, backingFields, eventFields, isCollection);
        }

        var eventMethods = new HashSet<MethodDefinition>();
        foreach (var evt in type.Events)
        {
            if (evt.AddMethod != null) eventMethods.Add(evt.AddMethod);
            if (evt.RemoveMethod != null) eventMethods.Add(evt.RemoveMethod);
        }

        // 5. Process Methods
        foreach (var m in type.Methods)
        {
            if (m.IsConstructor) continue;
            if (propertyMethods.Contains(m)) continue;
            if (eventMethods.Contains(m)) continue;

            ImplementMethod(m, type, ctx, isCollection);
        }
    }

    private static void ImplementConstructor(MethodDefinition ctor, TypeDefinition type, StubContext ctx,
        Dictionary<string, FieldReference> backingFields, bool isCollection)
    {
        ctor.Body = new MethodBody(ctor);
        var il = ctor.Body.Instructions;

        // Base constructor call
        if (type.BaseType != null)
        {
            var baseType = type.BaseType.Resolve();
            MethodReference baseCtor = null;
            if (baseType != null)
            {
                var accessibleCtors = baseType.Methods
                    .Where(m => m.IsConstructor && !m.IsStatic && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly))
                    .OrderBy(m => m.Parameters.Count)
                    .ToList();
                if (accessibleCtors.Count > 0)
                {
                    baseCtor = ctx.Module.ImportReference(accessibleCtors[0]);
                }
            }

            if (baseCtor == null)
            {
                var objCtor = ctx.Module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)!);
                baseCtor = objCtor;
            }

            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            foreach (var p in baseCtor.Parameters)
            {
                PushDefaultValue(il, ctor.Body, p.ParameterType, ctx.Module);
            }
            il.Add(Instruction.Create(OpCodes.Call, baseCtor));
        }

        if (isCollection && ctor.Parameters.Count >= 1)
        {
            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            il.Add(Instruction.Create(OpCodes.Ldarg_1));
            il.Add(Instruction.Create(OpCodes.Call, ctx.SetCollectionOwner));
        }

        // Parameter-to-property mapping (e.g. for EventArgs, Link, models)
        if (ctor.Parameters.Count > 0)
        {
            var matchedProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ctor.Parameters.Count; i++)
            {
                var p = ctor.Parameters[i];
                FieldReference? targetFld = null;
                MethodReference? targetSetter = null;

                if (backingFields.TryGetValue(p.Name, out var bfByName) && !matchedProps.Contains(p.Name))
                {
                    if (IsParameterTypeCompatible(p.ParameterType, bfByName.FieldType))
                    {
                        targetFld = bfByName;
                        matchedProps.Add(p.Name);
                    }
                }
                if (targetFld == null)
                {
                    var kvp = backingFields.FirstOrDefault(kv => !matchedProps.Contains(kv.Key)
                        && string.Equals(kv.Key, p.Name, StringComparison.OrdinalIgnoreCase)
                        && IsParameterTypeCompatible(p.ParameterType, kv.Value.FieldType));
                    if (kvp.Value != null)
                    {
                        targetFld = kvp.Value;
                        matchedProps.Add(kvp.Key);
                    }
                }

                if (targetFld == null && !matchedProps.Contains(p.Name))
                {
                    PropertyDefinition? propDef = null;
                    var curr = type;
                    while (curr != null)
                    {
                        propDef = curr.Properties.FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                        if (propDef != null) break;
                        if (curr.BaseType == null) break;
                        try { curr = curr.BaseType.Resolve(); } catch { break; }
                    }

                    if (propDef?.SetMethod != null && IsParameterTypeCompatible(p.ParameterType, propDef.PropertyType))
                    {
                        targetSetter = ctx.Module.ImportReference(propDef.SetMethod);
                        matchedProps.Add(propDef.Name);
                    }
                }

                if (targetFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg, p));
                    il.Add(Instruction.Create(OpCodes.Stfld, targetFld));
                }
                else if (targetSetter != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg, p));
                    il.Add(Instruction.Create(OpCodes.Callvirt, targetSetter));
                }
            }
        }

        // Initialize Text property if first parameter is string
        if (ctor.Parameters.Count > 0 && ctor.Parameters[0].ParameterType.FullName == "System.String")
        {
            var textFld = FindBackingField(type, "Text", ctx);
            if (textFld != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, textFld));
            }
        }

        // Default initializations
        if (type.Name == "NumericUpDown")
        {
            if (backingFields.TryGetValue("Minimum", out var minFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
                il.Add(Instruction.Create(OpCodes.Stfld, minFld));
            }
            if (backingFields.TryGetValue("Maximum", out var maxFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4, 100));
                il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
                il.Add(Instruction.Create(OpCodes.Stfld, maxFld));
            }
            if (backingFields.TryGetValue("Increment", out var incFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
                il.Add(Instruction.Create(OpCodes.Stfld, incFld));
            }
        }
        else if (type.Name == "ControlCollection" || type.Name == "TableLayoutControlCollection")
        {
            if (ctor.Parameters.Count > 0 && ctor.Parameters[0].ParameterType.Name == "Control")
            {
                if (backingFields.TryGetValue("Owner", out var ownerFld))
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Stfld, ownerFld));
                }
            }
        }
        else if (type.Name == "Control")
        {
            if (backingFields.TryGetValue("Visible", out var visFld))
            {
                var formType = ctx.Module.Types.FirstOrDefault(t => t.FullName == "System.Windows.Forms.Form");
                var tsddType = ctx.Module.Types.FirstOrDefault(t => t.FullName == "System.Windows.Forms.ToolStripDropDown");

                var skipVis = Instruction.Create(OpCodes.Nop);
                if (formType != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Isinst, ctx.Module.ImportReference(formType)));
                    il.Add(Instruction.Create(OpCodes.Brtrue_S, skipVis));
                }
                if (tsddType != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Isinst, ctx.Module.ImportReference(tsddType)));
                    il.Add(Instruction.Create(OpCodes.Brtrue_S, skipVis));
                }

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Stfld, visFld));

                il.Add(skipVis);
            }
            if (backingFields.TryGetValue("Enabled", out var enFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Stfld, enFld));
            }
        }
        else if (type.Name is "DataGridViewTextBoxColumn" or "DataGridViewCheckBoxColumn"
            or "DataGridViewComboBoxColumn" or "DataGridViewButtonColumn"
            or "DataGridViewImageColumn" or "DataGridViewLinkColumn")
        {
            var cellFld = FindBackingField(type, "CellTemplate", ctx);
            string cellTypeName = type.Name switch
            {
                "DataGridViewTextBoxColumn" => "DataGridViewTextBoxCell",
                "DataGridViewCheckBoxColumn" => "DataGridViewCheckBoxCell",
                "DataGridViewComboBoxColumn" => "DataGridViewComboBoxCell",
                "DataGridViewButtonColumn" => "DataGridViewButtonCell",
                "DataGridViewImageColumn" => "DataGridViewImageCell",
                "DataGridViewLinkColumn" => "DataGridViewLinkCell",
                _ => ""
            };
            var cellTypeDef = ctx.Module.Types.FirstOrDefault(t => t.Name == cellTypeName);
            var cellCtor = cellTypeDef?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
            if (cellFld != null && cellCtor != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(cellCtor)));
                il.Add(Instruction.Create(OpCodes.Stfld, cellFld));
            }
        }
        else if (type.Name == "Padding")
        {
            backingFields.TryGetValue("All", out var fAll);
            backingFields.TryGetValue("Left", out var fLeft);
            backingFields.TryGetValue("Top", out var fTop);
            backingFields.TryGetValue("Right", out var fRight);
            backingFields.TryGetValue("Bottom", out var fBottom);

            if (ctor.Parameters.Count == 1) // Padding(int all)
            {
                if (fAll != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fAll)); }
                if (fLeft != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fLeft)); }
                if (fTop != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fTop)); }
                if (fRight != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fRight)); }
                if (fBottom != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fBottom)); }
            }
            else if (ctor.Parameters.Count == 4) // Padding(int left, int top, int right, int bottom)
            {
                if (fLeft != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_1)); il.Add(Instruction.Create(OpCodes.Stfld, fLeft)); }
                if (fTop != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_2)); il.Add(Instruction.Create(OpCodes.Stfld, fTop)); }
                if (fRight != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_3)); il.Add(Instruction.Create(OpCodes.Stfld, fRight)); }
                if (fBottom != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldarg_S, ctor.Parameters[3])); il.Add(Instruction.Create(OpCodes.Stfld, fBottom)); }
            }
        }

        il.Add(Instruction.Create(OpCodes.Ret));
    }

    private static bool InheritsFrom(TypeDefinition t, string baseName)
    {
        var curr = t;
        while (curr != null)
        {
            if (curr.Name == baseName || curr.FullName == baseName) return true;
            if (curr.BaseType == null) break;
            try
            {
                var resolved = curr.BaseType.Resolve();
                if (resolved == null || resolved == curr) break;
                curr = resolved;
            }
            catch
            {
                break;
            }
        }
        return false;
    }

    private static bool ShouldLazilyCreate(PropertyDefinition prop)
    {
        if (prop.PropertyType.IsValueType) return false;
        if (prop.GetMethod == null || prop.GetMethod.IsStatic) return false;

        string propName = prop.Name;
        // Explicit deny-list of property names that are user-assigned or references:
        if (propName is "Parent" or "TopLevelControl" or "ActiveControl" or "ActiveForm"
            or "Owner" or "ContextMenu" or "ContextMenuStrip" or "Tag" or "DataSource"
            or "Site" or "FindForm" or "Container" or "MasterControl" or "TargetControl"
            or "BindingContext" or "DefaultView" or "Component" or "NativeWindow"
            or "InnerList" or "SyncRoot" or "CurrentCell")
        {
            return false;
        }

        var pType = prop.PropertyType.Resolve();
        if (pType == null) return false;

        string pTypeName = pType.Name;
        string pTypeFull = pType.FullName;

        // Specific allowed lazy properties:
        if (propName is "RowTemplate" or "HeaderCell" or "FlatAppearance" or "VerticalScroll" or "HorizontalScroll"
            or "ContentPanel" or "RightToolStripPanel" or "LeftToolStripPanel" or "TopToolStripPanel" or "BottomToolStripPanel") return true;

        // Deny basic types
        if (pTypeFull == "System.String" || pTypeFull == "System.Type") return false;
        if (IsDelegate(pType)) return false;
        if (pType.IsInterface) return false;

        // Deny Drawing resources: Image, Icon, Cursor, Font, Brush, Pen, Color, etc.
        if (pTypeName is "Image" or "Bitmap" or "Icon" or "Cursor" or "Font" or "Brush" or "Pen" or "SolidBrush") return false;
        if (pTypeFull.StartsWith("System.Drawing.")) return false;

        // Allow-list:
        if (pTypeName == "SplitterPanel" || propName is "Panel1" or "Panel2") return true;

        if (pTypeName is "DataGridViewCellStyle" or "TableLayoutSettings" or "ControlBindingsCollection"
            or "ListViewGroupCollection" or "TableLayoutColumnStyleCollection" or "TableLayoutRowStyleCollection"
            or "TableLayoutControlCollection" or "ControlCollection" or "MenuItemCollection"
            or "ToolStripItemCollection" or "TreeNodeCollection" or "TabPageCollection"
            or "DataGridViewRowCollection" or "DataGridViewColumnCollection" or "DataGridViewCellCollection"
            or "ListViewItemCollection" or "ColumnHeaderCollection" or "ObjectCollection"
            or "ImageCollection" or "HeaderCollection" or "AutoCompleteStringCollection"
            or "ListViewSubItemCollection" or "DataGridViewRow" or "DataGridViewColumnHeaderCell"
            or "FlatButtonAppearance" or "VScrollProperties" or "HScrollProperties"
            or "ToolStripContentPanel" or "ToolStripPanel")
        {
            return true;
        }

        if (pTypeName.EndsWith("Collection") || pTypeName.EndsWith("CollectionBase")) return true;

        if (IsCollectionType(pType)) return true;

        if (pTypeName.EndsWith("Settings") || pTypeName.EndsWith("LayoutSettings")) return true;

        return false;
    }

    private static MethodReference FindLazyConstructor(PropertyDefinition prop, TypeDefinition declaringType, ModuleDefinition module)
    {
        if (!ShouldLazilyCreate(prop)) return null;

        var pType = prop.PropertyType.Resolve();
        if (pType == null) return null;

        MethodReference SafeImport(MethodDefinition m) => m.DeclaringType?.Module == module ? m : module.ImportReference(m);

        var ctors = pType.Methods
            .Where(m => m.IsConstructor && !m.IsStatic && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly || m.IsAssembly))
            .OrderBy(m => m.Parameters.Count)
            .ToList();

        if (ctors.Count > 0)
        {
            return SafeImport(ctors[0]);
        }

        return null;
    }

    private static void ImplementProperty(PropertyDefinition prop, TypeDefinition type, StubContext ctx,
        Dictionary<string, FieldReference> backingFields, Dictionary<string, FieldReference> eventFields,
        bool isCollection)
    {
        if (prop.Parameters.Count > 0) // Indexer
        {
            if (prop.GetMethod != null)
            {
                var gm = prop.GetMethod;
                gm.Body = new MethodBody(gm);
                var il = gm.Body.Instructions;
                if (isCollection && gm.Parameters.Count == 1 && gm.Parameters[0].ParameterType.FullName == "System.Int32")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColGetItem));
                    if (gm.ReturnType.FullName != "System.Object")
                    {
                        if (gm.ReturnType.IsValueType)
                            il.Add(Instruction.Create(OpCodes.Unbox_Any, gm.ReturnType));
                        else
                            il.Add(Instruction.Create(OpCodes.Castclass, gm.ReturnType));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
                else if (isCollection && gm.Parameters.Count == 1 && gm.Parameters[0].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColGetItemByName));
                    if (gm.ReturnType.FullName != "System.Object")
                    {
                        if (gm.ReturnType.IsValueType)
                            il.Add(Instruction.Create(OpCodes.Unbox_Any, gm.ReturnType));
                        else
                            il.Add(Instruction.Create(OpCodes.Castclass, gm.ReturnType));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
                else
                {
                    PushDefaultValue(il, gm.Body, gm.ReturnType, ctx.Module);
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            if (prop.SetMethod != null)
            {
                var sm = prop.SetMethod;
                sm.Body = new MethodBody(sm);
                var il = sm.Body.Instructions;
                if (isCollection && sm.Parameters.Count == 2 && sm.Parameters[0].ParameterType.FullName == "System.Int32")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    if (sm.Parameters[1].ParameterType.IsValueType)
                        il.Add(Instruction.Create(OpCodes.Box, sm.Parameters[1].ParameterType));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColSetItem));
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            return;
        }

        if (isCollection && prop.Name == "Count" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
            m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if ((type.Name == "DataGridViewBand" || InheritsFrom(type, "DataGridViewBand")) && prop.Name == "Index")
        {
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.DgvBandGetIndex));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (type.Name == "DataGridView" && prop.Name == "RowCount")
        {
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                var rowsProp = type.Properties.FirstOrDefault(p => p.Name == "Rows");
                if (rowsProp?.GetMethod != null)
                {
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, rowsProp.GetMethod));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            if (prop.SetMethod != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.DgvRowCountSet));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (type.Name == "DataGridView" && prop.Name == "ColumnCount")
        {
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                var colsProp = type.Properties.FirstOrDefault(p => p.Name == "Columns");
                if (colsProp?.GetMethod != null)
                {
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, colsProp.GetMethod));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            if (prop.SetMethod != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.DgvColumnCountSet));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (type.Name == "Padding")
        {
            if (prop.Name == "All")
            {
                backingFields.TryGetValue("All", out var fAll);
                backingFields.TryGetValue("Left", out var fLeft);
                backingFields.TryGetValue("Top", out var fTop);
                backingFields.TryGetValue("Right", out var fRight);
                backingFields.TryGetValue("Bottom", out var fBottom);

                if (prop.GetMethod != null && fAll != null)
                {
                    var m = prop.GetMethod;
                    m.Body = new MethodBody(m);
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, fAll));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }

                if (prop.SetMethod != null)
                {
                    var m = prop.SetMethod;
                    m.Body = new MethodBody(m);
                    if (fAll != null) { m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, fAll)); }
                    if (fLeft != null) { m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, fLeft)); }
                    if (fTop != null) { m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, fTop)); }
                    if (fRight != null) { m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, fRight)); }
                    if (fBottom != null) { m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1)); m.Body.Instructions.Add(Instruction.Create(OpCodes.Stfld, fBottom)); }
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
                return;
            }
        }

        if ((type.Name == "ComboBox" || type.Name == "ListBox") && prop.Name == "SelectedItem")
        {
            backingFields.TryGetValue("SelectedItem", out var selBf);
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;

                backingFields.TryGetValue("SelectedIndex", out var idxFld);
                backingFields.TryGetValue("Items", out var itemsFld);

                if (selBf != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, selBf));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                }

                if (idxFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, idxFld));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_M1));
                }

                if (itemsFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, itemsFld));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                }

                il.Add(Instruction.Create(OpCodes.Call, ctx.GetSelItem));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null && selBf != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                eventFields.TryGetValue("SelectedIndexChanged", out var selChangedEvt);

                var exitLabel = Instruction.Create(OpCodes.Nop);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, selBf));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ValuesEqual));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, exitLabel));

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, selBf));

                if (selChangedEvt != null)
                {
                    var popAndExit = Instruction.Create(OpCodes.Nop);
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, selChangedEvt));
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Brfalse_S, popAndExit));

                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                    il.Add(Instruction.Create(OpCodes.Ldstr, "SelectedIndexChanged"));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.FireEvent));
                    il.Add(Instruction.Create(OpCodes.Br_S, exitLabel));

                    il.Add(popAndExit);
                    il.Add(Instruction.Create(OpCodes.Pop));
                }

                il.Add(exitLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        backingFields.TryGetValue(prop.Name, out var bf);
        bool isStatic = (prop.GetMethod != null && prop.GetMethod.IsStatic) || (prop.SetMethod != null && prop.SetMethod.IsStatic);

        if (type.Name == "Control" && prop.Name == "DefaultFont" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            il.Add(Instruction.Create(OpCodes.Ldstr, "Segoe UI"));
            il.Add(Instruction.Create(OpCodes.Ldc_R4, 9.0f));
            il.Add(Instruction.Create(OpCodes.Newobj, ctx.FontCtor));
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.Name == "Cursors" && isStatic && prop.PropertyType.Name == "Cursor" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            var retLabel = Instruction.Create(OpCodes.Nop);

            if (bf != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                var cursorType = prop.PropertyType.Resolve();
                var cursorCtor = cursorType?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                if (cursorCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(cursorCtor)));
                    il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                }

                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
            }
            else
            {
                var cursorType = prop.PropertyType.Resolve();
                var cursorCtor = cursorType?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                if (cursorCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(cursorCtor)));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                }
            }

            il.Add(retLabel);
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.Name == "Cursor" && prop.Name == "Current" && isStatic)
        {
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                var retLabel = Instruction.Create(OpCodes.Nop);

                if (bf != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                    il.Add(Instruction.Create(OpCodes.Pop));

                    var cursorCtor = type.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                    if (cursorCtor != null)
                    {
                        il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(cursorCtor)));
                        il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                    }

                    il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                }

                il.Add(retLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null && bf != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (type.Name == "Screen" && prop.Name == "PrimaryScreen" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            var retLabel = Instruction.Create(OpCodes.Nop);

            if (bf != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                var screenCtor = type.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                if (screenCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(screenCtor)));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.InitPrimaryScreen));
                    il.Add(Instruction.Create(OpCodes.Castclass, type));
                    il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                }

                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
            }
            else
            {
                il.Add(Instruction.Create(OpCodes.Ldnull));
            }

            il.Add(retLabel);
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.Name == "Screen" && prop.Name == "AllScreens" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            var primProp = type.Properties.FirstOrDefault(p => p.Name == "PrimaryScreen");
            if (primProp?.GetMethod != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Newarr, type));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                il.Add(Instruction.Create(OpCodes.Call, primProp.GetMethod));
                il.Add(Instruction.Create(OpCodes.Stelem_Ref));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "Application" && prop.Name == "OpenForms" && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            var retLabel = Instruction.Create(OpCodes.Nop);

            if (bf != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                var fcType = ctx.Module.Types.FirstOrDefault(t => t.Name == "FormCollection");
                var fcCtor = fcType?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                if (fcCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(fcCtor)));
                    il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                }

                il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
            }
            else
            {
                il.Add(Instruction.Create(OpCodes.Ldnull));
            }

            il.Add(retLabel);
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.Name == "SystemInformation" && isStatic && prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;
            if (prop.Name is "WorkingArea" or "VirtualScreen")
            {
                var rectCtor = prop.PropertyType.Resolve()?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 4);
                if (rectCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1920));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1080));
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(rectCtor)));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
            else if (prop.Name is "PrimaryMonitorSize" or "PrimaryMonitorMaximizedWindowSize")
            {
                var sizeCtor = prop.PropertyType.Resolve()?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 2);
                if (sizeCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1920));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1080));
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(sizeCtor)));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
            else if (prop.Name == "BorderSize")
            {
                var sizeCtor = prop.PropertyType.Resolve()?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 2);
                if (sizeCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(sizeCtor)));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
            else if (prop.Name == "CaptionHeight")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4, 23));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            else if (prop.Name is "VerticalScrollBarWidth" or "HorizontalScrollBarHeight")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4, 17));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            else if (prop.Name is "MousePresent" or "MouseWheelPresent" or "UserInteractive")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            else if (prop.Name == "MonitorCount")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "TreeNode")
        {
            if (prop.Name == "Index" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetIndex));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "Level" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetLevel));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "FullPath" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetFullPath));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "PrevNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetPrevNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "NextNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetNextNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "FirstNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetFirstNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "LastNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetLastNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "PrevVisibleNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetPrevVisibleNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "NextVisibleNode" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetNextVisibleNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "TreeView" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetTreeView));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Castclass, prop.PropertyType));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (prop.Name == "Bounds" && prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                if (bf != null)
                {
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, bf));
                }
                else
                {
                    PushDefaultValue(m.Body.Instructions, m.Body, prop.PropertyType, ctx.Module);
                }
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeGetBounds));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "TreeView" && prop.Name == "SelectedNode")
        {
            if (prop.GetMethod != null && bf != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldfld, bf));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, ctx.TreeViewSetSelectedNode));
                m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (prop.Name == "CellTemplate" && (type.Name == "DataGridViewColumn" || InheritsFrom(type, "DataGridViewColumn")) && !isStatic && bf != null)
        {
            if (prop.GetMethod != null)
            {
                var m = prop.GetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                var retLabel = Instruction.Create(OpCodes.Nop);
                var loadBf = Instruction.Create(OpCodes.Nop);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                void TryCreateCell(string colTypeName, string cellTypeName, Instruction nextLabel)
                {
                    var colTypeDef = ctx.Module.Types.FirstOrDefault(t => t.Name == colTypeName);
                    var cellTypeDef = ctx.Module.Types.FirstOrDefault(t => t.Name == cellTypeName);
                    var cellCtor = cellTypeDef?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 0);
                    if (colTypeDef != null && cellCtor != null)
                    {
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Isinst, ctx.Module.ImportReference(colTypeDef)));
                        il.Add(Instruction.Create(OpCodes.Brfalse, nextLabel));

                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(cellCtor)));
                        il.Add(Instruction.Create(OpCodes.Stfld, bf));
                        il.Add(Instruction.Create(OpCodes.Br, loadBf));
                    }
                }

                var l1 = Instruction.Create(OpCodes.Nop);
                var l2 = Instruction.Create(OpCodes.Nop);
                var l3 = Instruction.Create(OpCodes.Nop);
                var l4 = Instruction.Create(OpCodes.Nop);
                var l5 = Instruction.Create(OpCodes.Nop);
                var l6 = Instruction.Create(OpCodes.Nop);

                TryCreateCell("DataGridViewTextBoxColumn", "DataGridViewTextBoxCell", l1);
                il.Add(l1);
                TryCreateCell("DataGridViewCheckBoxColumn", "DataGridViewCheckBoxCell", l2);
                il.Add(l2);
                TryCreateCell("DataGridViewComboBoxColumn", "DataGridViewComboBoxCell", l3);
                il.Add(l3);
                TryCreateCell("DataGridViewButtonColumn", "DataGridViewButtonCell", l4);
                il.Add(l4);
                TryCreateCell("DataGridViewImageColumn", "DataGridViewImageCell", l5);
                il.Add(l5);
                TryCreateCell("DataGridViewLinkColumn", "DataGridViewLinkCell", l6);
                il.Add(l6);

                il.Add(loadBf);
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));

                il.Add(retLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (bf == null) return;

        // Getter
        if (prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;

            if (prop.Name == "Font" && (type.Name == "Control" || InheritsFrom(type, "Control")) && !isStatic)
            {
                var retLabel = Instruction.Create(OpCodes.Nop);
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldstr, "Segoe UI"));
                il.Add(Instruction.Create(OpCodes.Ldc_R4, 9.0f));
                il.Add(Instruction.Create(OpCodes.Newobj, ctx.FontCtor));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));

                il.Add(retLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else
            {
                MethodReference lazyCtor = FindLazyConstructor(prop, type, ctx.Module);
                if (lazyCtor != null && !isStatic)
                {
                    var retLabel = Instruction.Create(OpCodes.Nop);

                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                    il.Add(Instruction.Create(OpCodes.Pop));

                    il.Add(Instruction.Create(OpCodes.Ldarg_0)); // this for stfld
                    for (int i = 0; i < lazyCtor.Parameters.Count; i++)
                    {
                        var p = lazyCtor.Parameters[i];
                        if (i == 0 && (p.ParameterType.FullName == type.FullName || InheritsFrom(type, p.ParameterType.FullName)))
                        {
                            il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        }
                        else
                        {
                            PushDefaultValue(il, m.Body, p.ParameterType, ctx.Module);
                        }
                    }
                    il.Add(Instruction.Create(OpCodes.Newobj, lazyCtor));
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.SetCollectionOwner));
                    il.Add(Instruction.Create(OpCodes.Stfld, bf));

                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, bf));

                    il.Add(retLabel);
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
                else
                {
                    if (isStatic)
                    {
                        il.Add(Instruction.Create(OpCodes.Ldsfld, bf));
                    }
                    else
                    {
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                }
            }
        }

        // Setter
        if (prop.SetMethod != null)
        {
            var m = prop.SetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;

            // Find associated change event
            string? eventName = null;
            FieldReference? changeEvt = null;
            var ce = FindEventField(type, prop.Name + "Changed", ctx);
            if (ce != null)
            {
                eventName = prop.Name + "Changed";
                changeEvt = ce;
            }
            else if (prop.Name == "Checked" && (ce = FindEventField(type, "CheckedChanged", ctx)) != null)
            {
                eventName = "CheckedChanged";
                changeEvt = ce;
            }
            else if (prop.Name == "Value" && (ce = FindEventField(type, "ValueChanged", ctx)) != null)
            {
                eventName = "ValueChanged";
                changeEvt = ce;
            }
            else if (prop.Name == "Text" && (ce = FindEventField(type, "TextChanged", ctx)) != null)
            {
                eventName = "TextChanged";
                changeEvt = ce;
            }
            else if (prop.Name == "SelectedIndex" && (ce = FindEventField(type, "SelectedIndexChanged", ctx)) != null)
            {
                eventName = "SelectedIndexChanged";
                changeEvt = ce;
            }
            else if (prop.Name == "SelectedValue" && (ce = FindEventField(type, "SelectedValueChanged", ctx)) != null)
            {
                eventName = "SelectedValueChanged";
                changeEvt = ce;
            }
            else if (prop.Name == "CheckState" && (ce = FindEventField(type, "CheckStateChanged", ctx)) != null)
            {
                eventName = "CheckStateChanged";
                changeEvt = ce;
            }

            if (type.Name == "NumericUpDown" && prop.Name == "Value")
            {
                backingFields.TryGetValue("Minimum", out var minFld);
                backingFields.TryGetValue("Maximum", out var maxFld);

                var clampedVar = new VariableDefinition(prop.PropertyType);
                m.Body.Variables.Add(clampedVar);

                il.Add(Instruction.Create(OpCodes.Ldarg_1)); // value
                if (minFld != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldfld, minFld)); }
                else { il.Add(Instruction.Create(OpCodes.Ldc_I4_0)); il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!))); }
                if (maxFld != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldfld, maxFld)); }
                else { il.Add(Instruction.Create(OpCodes.Ldc_I4, 100)); il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!))); }
                il.Add(Instruction.Create(OpCodes.Call, ctx.ClampNumeric));
                il.Add(Instruction.Create(OpCodes.Stloc, clampedVar));

                var exitLabel = Instruction.Create(OpCodes.Nop);

                // Compare old value with clamped value
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                il.Add(Instruction.Create(OpCodes.Box, prop.PropertyType));
                il.Add(Instruction.Create(OpCodes.Ldloc, clampedVar));
                il.Add(Instruction.Create(OpCodes.Box, prop.PropertyType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ValuesEqual));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, exitLabel));

                // Store clamped value
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldloc, clampedVar));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                // Fire event if hooked
                if (changeEvt != null)
                {
                    var popAndExit = Instruction.Create(OpCodes.Nop);
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, changeEvt));
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Brfalse_S, popAndExit));

                    il.Add(Instruction.Create(OpCodes.Ldarg_0)); // sender
                    il.Add(Instruction.Create(OpCodes.Ldnull));  // args
                    il.Add(Instruction.Create(OpCodes.Ldstr, eventName));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.FireEvent));
                    il.Add(Instruction.Create(OpCodes.Br_S, exitLabel));

                    il.Add(popAndExit);
                    il.Add(Instruction.Create(OpCodes.Pop));
                }

                il.Add(exitLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else if (changeEvt != null && !isStatic)
            {
                var exitLabel = Instruction.Create(OpCodes.Nop);

                // Compare old value with new value
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                if (bf.FieldType.IsValueType) il.Add(Instruction.Create(OpCodes.Box, bf.FieldType));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (prop.PropertyType.IsValueType) il.Add(Instruction.Create(OpCodes.Box, prop.PropertyType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ValuesEqual));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, exitLabel));

                // Store new value
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                // Fire event
                var popAndExit = Instruction.Create(OpCodes.Nop);
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, changeEvt));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brfalse_S, popAndExit));

                il.Add(Instruction.Create(OpCodes.Ldarg_0)); // sender
                il.Add(Instruction.Create(OpCodes.Ldnull));  // args
                il.Add(Instruction.Create(OpCodes.Ldstr, eventName));
                il.Add(Instruction.Create(OpCodes.Call, ctx.FireEvent));
                il.Add(Instruction.Create(OpCodes.Br_S, exitLabel));

                il.Add(popAndExit);
                il.Add(Instruction.Create(OpCodes.Pop));

                il.Add(exitLabel);
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else
            {
                if (isStatic)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Stsfld, bf));
                }
                else
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Stfld, bf));
                }
                il.Add(Instruction.Create(OpCodes.Ret));
            }
        }
    }

    private static void ImplementMethod(MethodDefinition m, TypeDefinition type, StubContext ctx, bool isCollection)
    {
        m.Body = new MethodBody(m);
        var il = m.Body.Instructions;

        if (isCollection)
        {
            if (m.Name == "Add")
            {
                if (m.Parameters.Count == 0)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAddEmpty));
                    if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters.Count == 1 && type.Name == "DataGridViewRowCollection" && m.Parameters[0].ParameterType.FullName == "System.Object[]")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.DgvRowAdd));
                    if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters.Count == 2 && type.Name == "DataGridViewColumnCollection" && m.Parameters[0].ParameterType.FullName == "System.String" && m.Parameters[1].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.DgvColAdd));
                    if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.String" && m.Parameters[1].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAddKeyText));
                    if (m.ReturnType.FullName == "System.Void")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                    }
                    else if (m.ReturnType.FullName == "System.Int32")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        il.Add(Instruction.Create(OpCodes.Sub));
                    }
                    else if (m.ReturnType.FullName != "System.Object")
                    {
                        il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters.Count == 3 && type.Name == "TableLayoutControlCollection")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    il.Add(Instruction.Create(OpCodes.Ldarg_3));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.TlpAdd));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters.Count == 3 && type.Name == "ToolStripItemCollection" && m.Parameters[0].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    il.Add(Instruction.Create(OpCodes.Ldarg_3));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.TsmiAdd));
                    if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                    else if (m.ReturnType.FullName != "System.Object") il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (type.Name == "ControlBindingsCollection" && m.Parameters.Count >= 3)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Ldarg_2));
                    il.Add(Instruction.Create(OpCodes.Ldarg_3));
                    if (m.Parameters.Count >= 4 && m.Parameters[3].ParameterType.FullName == "System.Boolean")
                        il.Add(Instruction.Create(OpCodes.Ldarg_S, m.Parameters[3]));
                    else
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    if (m.Parameters.Count >= 5 && m.Parameters[4].ParameterType.FullName.Contains("DataSourceUpdateMode"))
                        il.Add(Instruction.Create(OpCodes.Ldarg_S, m.Parameters[4]));
                    else
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.CtrlBindingsAdd));
                    if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                    else if (m.ReturnType.FullName != "System.Object") il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }

                // General rule for collections:
                var elemTypeRef = GetCollectionElementType(type);
                if (m.Parameters.Count == 1 && (elemTypeRef == null || IsParameterTypeCompatible(m.Parameters[0].ParameterType, elemTypeRef)))
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    if (m.Parameters[0].ParameterType.IsValueType)
                        il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAdd));
                    if (m.ReturnType.FullName == "System.Void")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                    }
                    else if (m.ReturnType.FullName == "System.Int32")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        il.Add(Instruction.Create(OpCodes.Sub));
                    }
                    else if (m.ReturnType.IsValueType)
                    {
                        il.Add(Instruction.Create(OpCodes.Unbox_Any, m.ReturnType));
                    }
                    else if (m.ReturnType.FullName != "System.Object")
                    {
                        il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }

                // Arguments do not match element type: build element by calling element's best-matching constructor
                var elemTypeDef = elemTypeRef?.Resolve();
                MethodReference? matchingCtor = elemTypeDef != null ? FindMatchingConstructor(elemTypeDef, m.Parameters, ctx.Module) : null;
                if (matchingCtor != null)
                {
                    for (int i = 0; i < m.Parameters.Count; i++)
                    {
                        il.Add(Instruction.Create(OpCodes.Ldarg, m.Parameters[i]));
                    }
                    il.Add(Instruction.Create(OpCodes.Newobj, matchingCtor));
                    var elemLocal = new VariableDefinition(matchingCtor.DeclaringType);
                    m.Body.Variables.Add(elemLocal);
                    il.Add(Instruction.Create(OpCodes.Stloc, elemLocal));

                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldloc, elemLocal));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAdd));

                    if (m.ReturnType.FullName == "System.Void")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                    }
                    else if (m.ReturnType.FullName == "System.Int32")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        il.Add(Instruction.Create(OpCodes.Sub));
                    }
                    else
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                        il.Add(Instruction.Create(OpCodes.Ldloc, elemLocal));
                        if (m.ReturnType.FullName != elemLocal.VariableType.FullName && m.ReturnType.FullName != "System.Object")
                        {
                            il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                        }
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }

                // If single string parameter and collection supports string wrapping (e.g. ListViewSubItemCollection)
                if (m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAdd));
                    if (m.ReturnType.FullName == "System.Void")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                    }
                    else if (m.ReturnType.FullName == "System.Int32")
                    {
                        il.Add(Instruction.Create(OpCodes.Pop));
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                        il.Add(Instruction.Create(OpCodes.Sub));
                    }
                    else if (m.ReturnType.FullName != "System.Object")
                    {
                        il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    }
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }

                // If none matches, ignore the call; never cast an argument to element type!
                PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "AddRange")
            {
                if (m.Parameters.Count == 1)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColAddRange));
                }
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Remove")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColRemove));
                if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "RemoveAt")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColRemoveAt));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Clear")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColClear));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Contains")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColContains));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "IndexOf")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColIndexOf));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Insert")
            {
                if (m.Parameters.Count >= 2)
                {
                    var elemTypeRef = GetCollectionElementType(type);
                    if (m.Parameters.Count == 2 && (elemTypeRef == null || IsParameterTypeCompatible(m.Parameters[1].ParameterType, elemTypeRef)))
                    {
                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Ldarg_1));
                        il.Add(Instruction.Create(OpCodes.Ldarg_2));
                        if (m.Parameters[1].ParameterType.IsValueType)
                            il.Add(Instruction.Create(OpCodes.Box, m.Parameters[1].ParameterType));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColInsert));
                        il.Add(Instruction.Create(OpCodes.Ret));
                        return;
                    }

                    var elemTypeDef = elemTypeRef?.Resolve();
                    var subParams = new Mono.Collections.Generic.Collection<ParameterDefinition>();
                    for (int i = 1; i < m.Parameters.Count; i++) subParams.Add(m.Parameters[i]);
                    var matchingCtor = elemTypeDef != null ? FindMatchingConstructor(elemTypeDef, subParams, ctx.Module) : null;
                    if (matchingCtor != null)
                    {
                        for (int i = 1; i < m.Parameters.Count; i++)
                        {
                            il.Add(Instruction.Create(OpCodes.Ldarg, m.Parameters[i]));
                        }
                        il.Add(Instruction.Create(OpCodes.Newobj, matchingCtor));
                        var elemLocal = new VariableDefinition(matchingCtor.DeclaringType);
                        m.Body.Variables.Add(elemLocal);
                        il.Add(Instruction.Create(OpCodes.Stloc, elemLocal));

                        il.Add(Instruction.Create(OpCodes.Ldarg_0));
                        il.Add(Instruction.Create(OpCodes.Ldarg_1));
                        il.Add(Instruction.Create(OpCodes.Ldloc, elemLocal));
                        il.Add(Instruction.Create(OpCodes.Call, ctx.ColInsert));
                        il.Add(Instruction.Create(OpCodes.Ret));
                        return;
                    }
                }

                PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "CopyTo" && m.Parameters.Count == 2)
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldarg_2));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColCopyTo));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_Count")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColCount));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_Item" && m.Parameters.Count == 1)
            {
                if (m.Parameters[0].ParameterType.FullName == "System.Int32")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColGetItem));
                    if (m.ReturnType.IsValueType) il.Add(Instruction.Create(OpCodes.Unbox_Any, m.ReturnType));
                    else if (m.ReturnType.FullName != "System.Object") il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
                if (m.Parameters[0].ParameterType.FullName == "System.String")
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldarg_1));
                    il.Add(Instruction.Create(OpCodes.Call, ctx.ColGetItemByName));
                    if (m.ReturnType.IsValueType) il.Add(Instruction.Create(OpCodes.Unbox_Any, m.ReturnType));
                    else if (m.ReturnType.FullName != "System.Object") il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
            if (m.Name == "set_Item" && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.Int32")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldarg_2));
                if (m.Parameters[1].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[1].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColSetItem));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "GetEnumerator")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.ColGetEnumerator));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "Control" || InheritsFrom(type, "Control"))
        {
            if (m.Name == "Show")
            {
                var visFld = FindBackingField(type, "Visible", ctx);
                if (visFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                    il.Add(Instruction.Create(OpCodes.Stfld, visFld));
                }
                PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Hide")
            {
                var visFld = FindBackingField(type, "Visible", ctx);
                if (visFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Stfld, visFld));
                }
                PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Close")
            {
                var visFld = FindBackingField(type, "Visible", ctx);
                if (visFld != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Stfld, visFld));
                }
                PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "SuspendLayout" || m.Name == "ResumeLayout" || m.Name == "PerformLayout" || m.Name == "Dispose")
            {
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_InvokeRequired")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_IsHandleCreated")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_Handle")
            {
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Conv_I));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "CreateGraphics")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.CreateGraphics));
                il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Invoke" || m.Name == "BeginInvoke")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldnull));
                il.Add(Instruction.Create(OpCodes.Call, ctx.InvokeDelegate));
                if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                else if (m.ReturnType.FullName != "System.Object")
                {
                    il.Add(Instruction.Create(OpCodes.Pop));
                    PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
                }
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "TreeNode")
        {
            if (m.Name == "Expand")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeExpand));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Collapse")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeCollapse));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Toggle")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeToggle));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "TreeNodeCollection" && m.Name == "Find" && m.Parameters.Count == 2)
        {
            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            il.Add(Instruction.Create(OpCodes.Ldarg_1));
            il.Add(Instruction.Create(OpCodes.Ldarg_2));
            il.Add(Instruction.Create(OpCodes.Call, ctx.TreeNodeCollectionFind));
            if (m.ReturnType.FullName != "System.Object")
            {
                il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
            }
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.Name == "Screen")
        {
            if (m.Name is "FromControl" or "FromPoint" or "FromRectangle")
            {
                var primProp = type.Properties.FirstOrDefault(p => p.Name == "PrimaryScreen");
                if (primProp?.GetMethod != null)
                {
                    il.Add(Instruction.Create(OpCodes.Call, primProp.GetMethod));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
            if (m.Name is "GetBounds" or "GetWorkingArea")
            {
                var rectCtor = m.ReturnType.Resolve()?.Methods.FirstOrDefault(c => c.IsConstructor && !c.IsStatic && c.Parameters.Count == 4);
                if (rectCtor != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1920));
                    il.Add(Instruction.Create(OpCodes.Ldc_I4, 1080));
                    il.Add(Instruction.Create(OpCodes.Newobj, ctx.Module.ImportReference(rectCtor)));
                    il.Add(Instruction.Create(OpCodes.Ret));
                    return;
                }
            }
        }

        if (type.Name == "Application")
        {
            if (m.Name == "DoEvents" || m.Name == "EnableVisualStyles")
            {
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_StartupPath")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.GetStartup));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_ExecutablePath")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.GetExec));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_ProductVersion")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.GetProd));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "Clipboard")
        {
            if (m.Name == "SetText")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, ctx.SetClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "GetText")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.GetClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "ContainsText")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.HasClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Clear")
            {
                il.Add(Instruction.Create(OpCodes.Call, ctx.ClrClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "MessageBox" && m.Name == "Show")
        {
            il.Add(Instruction.Create(OpCodes.Ldc_I4_1)); // DialogResult.OK
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if ((type.Name == "Form" || InheritsFrom(type, "Form") || type.Name == "CommonDialog" || InheritsFrom(type, "CommonDialog")) && m.Name == "ShowDialog")
        {
            var visFld = FindBackingField(type, "Visible", ctx);
            if (visFld != null)
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Stfld, visFld));
            }
            il.Add(Instruction.Create(OpCodes.Ldc_I4_2)); // DialogResult.Cancel
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        PushDefaultValue(il, m.Body, m.ReturnType, ctx.Module);
        il.Add(Instruction.Create(OpCodes.Ret));
    }

    private static void GenerateLiveSplitView(GeneratorOptions options, AssemblyDefinition winFormsAsm, AssemblyDefinition coreAsm, AssemblyDefinition drawingAsm)
    {
        var asmName = new AssemblyNameDefinition("LiveSplit.View", new Version(1, 0, 0, 0));
        var viewAsm = AssemblyDefinition.CreateAssembly(asmName, "LiveSplit.View.dll", ModuleKind.Dll);
        var module = viewAsm.MainModule;

        // Base type Form from LiveSplit.Compat.WinForms
        var formType = winFormsAsm.MainModule.Types.FirstOrDefault(t => t.FullName == "System.Windows.Forms.Form");
        var formRef = module.ImportReference(formType);
        var formCtor = formType?.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
        var formCtorRef = formCtor != null ? module.ImportReference(formCtor) : null;

        // 1. LiveSplit.View.RunEditorDialog
        {
            var runEditorType = new TypeDefinition("LiveSplit.View", "RunEditorDialog",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                formRef);
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            ctor.Body = new MethodBody(ctor);
            if (formCtorRef != null)
            {
                ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, formCtorRef));
            }
            ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            runEditorType.Methods.Add(ctor);
            module.Types.Add(runEditorType);
        }

        // 2. LiveSplit.View.ComponentSettingsDialog
        {
            var settingsDialogType = new TypeDefinition("LiveSplit.View", "ComponentSettingsDialog",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                formRef);

            var ctor0 = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            ctor0.Body = new MethodBody(ctor0);
            if (formCtorRef != null)
            {
                ctor0.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                ctor0.Body.Instructions.Add(Instruction.Create(OpCodes.Call, formCtorRef));
            }
            ctor0.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            settingsDialogType.Methods.Add(ctor0);

            var compType = coreAsm.MainModule.Types.FirstOrDefault(t => t.FullName == "LiveSplit.UI.Components.IComponent")
                ?? module.ImportReference(typeof(object)).Resolve();
            var compRef = module.ImportReference(compType);
            var ctor1 = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            ctor1.Parameters.Add(new ParameterDefinition("component", ParameterAttributes.None, compRef));
            ctor1.Body = new MethodBody(ctor1);
            if (formCtorRef != null)
            {
                ctor1.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                ctor1.Body.Instructions.Add(Instruction.Create(OpCodes.Call, formCtorRef));
            }
            ctor1.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            settingsDialogType.Methods.Add(ctor1);

            module.Types.Add(settingsDialogType);
        }

        // 3. LiveSplit.View.SpeedRunsLiveForm
        {
            var srlFormType = new TypeDefinition("LiveSplit.View", "SpeedRunsLiveForm",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                formRef);
            var ctor = new MethodDefinition(".ctor",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                module.TypeSystem.Void);
            ctor.Body = new MethodBody(ctor);
            if (formCtorRef != null)
            {
                ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, formCtorRef));
            }
            ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            srlFormType.Methods.Add(ctor);
            module.Types.Add(srlFormType);
        }

        // 4. LiveSplit.Properties.Resources
        {
            var resType = new TypeDefinition("LiveSplit.Properties", "Resources",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                module.TypeSystem.Object);

            var bmpType = drawingAsm.MainModule.Types.FirstOrDefault(t => t.FullName == "System.Drawing.Bitmap");
            var bmpRef = module.ImportReference(bmpType);

            void AddBitmapProp(string propName)
            {
                var prop = new PropertyDefinition(propName, PropertyAttributes.None, bmpRef);
                var getter = new MethodDefinition($"get_{propName}",
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                    bmpRef);
                getter.Body = new MethodBody(getter);
                getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                getter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                resType.Methods.Add(getter);
                prop.GetMethod = getter;
                resType.Properties.Add(prop);
            }

            AddBitmapProp("Add");
            AddBitmapProp("Remove");
            AddBitmapProp("UpArrow");
            AddBitmapProp("DownArrow");

            module.Types.Add(resType);
        }

        string outViewPath = Path.Combine(options.OutDir, "LiveSplit.View.dll");
        viewAsm.Write(outViewPath);
    }

    private static void PushDefaultValue(Mono.Collections.Generic.Collection<Instruction> il, MethodBody body, TypeReference retType, ModuleDefinition module)
    {
        if (retType.FullName == "System.Void") return;

        if (retType.IsValueType)
        {
            switch (retType.FullName)
            {
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                case "System.Char":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    break;
                case "System.Int64":
                case "System.UInt64":
                    il.Add(Instruction.Create(OpCodes.Ldc_I8, (long)0));
                    break;
                case "System.Single":
                    il.Add(Instruction.Create(OpCodes.Ldc_R4, 0.0f));
                    break;
                case "System.Double":
                    il.Add(Instruction.Create(OpCodes.Ldc_R8, 0.0));
                    break;
                case "System.IntPtr":
                case "System.UIntPtr":
                    il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    il.Add(Instruction.Create(OpCodes.Conv_I));
                    break;
                default:
                    var td = retType.Resolve();
                    if (td != null && td.IsEnum)
                    {
                        il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                    }
                    else
                    {
                        var local = new VariableDefinition(retType);
                        body.Variables.Add(local);
                        body.InitLocals = true;
                        il.Add(Instruction.Create(OpCodes.Ldloca, local));
                        il.Add(Instruction.Create(OpCodes.Initobj, retType));
                        il.Add(Instruction.Create(OpCodes.Ldloc, local));
                    }
                    break;
            }
        }
        else
        {
            il.Add(Instruction.Create(OpCodes.Ldnull));
        }
    }
}
