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
        ImplementStubBehaviors(targetModule, supportAsm);

        // 8. Write outputs
        string outAssemblyPath = Path.Combine(options.OutDir, "LiveSplit.Compat.WinForms.dll");
        winFormsAsm.Write(outAssemblyPath);

        // Copy Support assembly
        string outSupportPath = Path.Combine(options.OutDir, "LiveSplit.Compat.WinForms.Support.dll");
        File.Copy(supportDllPath, outSupportPath, overwrite: true);

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

            // Check if class has accessible instance constructor
            bool hasAccessibleCtor = t.Methods.Any(m => m.IsConstructor && !m.IsStatic && (m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly));
            if (!hasAccessibleCtor)
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

    private static void ImplementStubBehaviors(ModuleDefinition module, AssemblyDefinition supportAsm)
    {
        var supportType = supportAsm.MainModule.Types.First(t => t.Name == "StubSupport");

        MethodReference ImportSupport(string name)
        {
            var method = supportType.Methods.First(m => m.Name == name);
            return module.ImportReference(method);
        }

        var colAdd = ImportSupport("CollectionAdd");
        var colAddRange = ImportSupport("CollectionAddRange");
        var colRemove = ImportSupport("CollectionRemove");
        var colRemoveAt = ImportSupport("CollectionRemoveAt");
        var colClear = ImportSupport("CollectionClear");
        var colCount = ImportSupport("CollectionCount");
        var colContains = ImportSupport("CollectionContains");
        var colIndexOf = ImportSupport("CollectionIndexOf");
        var colGetItem = ImportSupport("CollectionGetItem");
        var colSetItem = ImportSupport("CollectionSetItem");
        var colGetEnumerator = ImportSupport("CollectionGetEnumerator");
        var colInsert = ImportSupport("CollectionInsert");

        var clampNumeric = ImportSupport("ClampNumeric");
        var invokeDelegate = ImportSupport("InvokeDelegate");
        var fireEvent = ImportSupport("FireEvent");

        var setClip = ImportSupport("SetClipboardText");
        var getClip = ImportSupport("GetClipboardText");
        var hasClip = ImportSupport("ContainsClipboardText");
        var clrClip = ImportSupport("ClearClipboard");

        var getStartup = ImportSupport("get_StartupPath");
        var getExec = ImportSupport("get_ExecutablePath");
        var getProd = ImportSupport("get_ProductVersion");
        var getSelItem = ImportSupport("GetSelectedItem");
        var setCollectionOwner = ImportSupport("SetCollectionOwner");

        var delegateCombine = module.ImportReference(typeof(Delegate).GetMethod("Combine", new[] { typeof(Delegate), typeof(Delegate) })!);
        var delegateRemove = module.ImportReference(typeof(Delegate).GetMethod("Remove", new[] { typeof(Delegate), typeof(Delegate) })!);

        foreach (var type in module.Types.Where(t => t.Name != "<Module>"))
        {
            ImplementTypeBehaviors(type, module, supportAsm,
                colAdd, colAddRange, colRemove, colRemoveAt, colClear, colCount, colContains, colIndexOf, colGetItem, colSetItem, colGetEnumerator, colInsert,
                clampNumeric, invokeDelegate, fireEvent, setClip, getClip, hasClip, clrClip, getStartup, getExec, getProd, getSelItem, setCollectionOwner,
                delegateCombine, delegateRemove);
        }
    }

    private static bool IsCollectionType(TypeDefinition t)
    {
        var curr = t;
        while (curr != null)
        {
            if (curr.Name == "ArrangedElementCollection" ||
                curr.Name == "BaseCollection" ||
                curr.Name == "ControlCollection" ||
                curr.Name == "TableLayoutStyleCollection" ||
                curr.Name == "ObjectCollection" ||
                curr.Name == "ListViewItemCollection" ||
                curr.Name == "TreeNodeCollection" ||
                curr.Name == "ToolStripItemCollection" ||
                curr.Name == "BindingsCollection")
                return true;

            if (curr.BaseType == null) break;
            curr = curr.BaseType.Resolve();
        }
        return false;
    }

    private static void ImplementTypeBehaviors(TypeDefinition type, ModuleDefinition module, AssemblyDefinition supportAsm,
        MethodReference colAdd, MethodReference colAddRange, MethodReference colRemove, MethodReference colRemoveAt,
        MethodReference colClear, MethodReference colCount, MethodReference colContains, MethodReference colIndexOf,
        MethodReference colGetItem, MethodReference colSetItem, MethodReference colGetEnumerator, MethodReference colInsert,
        MethodReference clampNumeric, MethodReference invokeDelegate, MethodReference fireEvent,
        MethodReference setClip, MethodReference getClip, MethodReference hasClip, MethodReference clrClip,
        MethodReference getStartup, MethodReference getExec, MethodReference getProd, MethodReference getSelItem, MethodReference setCollectionOwner,
        MethodReference delegateCombine, MethodReference delegateRemove)
    {
        if (type.IsInterface)
        {
            foreach (var m in type.Methods)
            {
                if (m.HasBody)
                {
                    m.Body = new MethodBody(m);
                    PushDefaultValue(m.Body.Instructions, m.Body, m.ReturnType, module);
                    m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                }
            }
            foreach (var nt in type.NestedTypes)
            {
                ImplementTypeBehaviors(nt, module, supportAsm,
                    colAdd, colAddRange, colRemove, colRemoveAt, colClear, colCount, colContains, colIndexOf, colGetItem, colSetItem, colGetEnumerator, colInsert,
                    clampNumeric, invokeDelegate, fireEvent, setClip, getClip, hasClip, clrClip, getStartup, getExec, getProd, getSelItem, setCollectionOwner,
                    delegateCombine, delegateRemove);
            }
            return;
        }

        if (type.IsEnum || IsDelegate(type))
        {
            foreach (var nt in type.NestedTypes)
            {
                ImplementTypeBehaviors(nt, module, supportAsm,
                    colAdd, colAddRange, colRemove, colRemoveAt, colClear, colCount, colContains, colIndexOf, colGetItem, colSetItem, colGetEnumerator, colInsert,
                    clampNumeric, invokeDelegate, fireEvent, setClip, getClip, hasClip, clrClip, getStartup, getExec, getProd, getSelItem, setCollectionOwner,
                    delegateCombine, delegateRemove);
            }
            return;
        }

        bool isCollection = IsCollectionType(type);

        // 1. Process Fields for property backing
        var backingFields = new Dictionary<string, FieldDefinition>();
        foreach (var prop in type.Properties)
        {
            if (prop.Parameters.Count == 0) // Not an indexer
            {
                string fieldName = $"<{prop.Name}>k__BackingField";
                var bf = type.Fields.FirstOrDefault(f => f.Name == fieldName);
                if (bf == null)
                {
                    var fieldAttr = FieldAttributes.Private;
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

        // 2. Process Events
        var eventFields = new Dictionary<string, FieldDefinition>();
        foreach (var evt in type.Events)
        {
            string fieldName = $"<{evt.Name}>k__BackingField";
            var ef = type.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (ef == null)
            {
                var fieldAttr = FieldAttributes.Private;
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
                il.Add(Instruction.Create(OpCodes.Call, delegateCombine));
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
                il.Add(Instruction.Create(OpCodes.Call, delegateRemove));
                il.Add(Instruction.Create(OpCodes.Castclass, evt.EventType));
                il.Add(Instruction.Create(OpCodes.Stfld, ef));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
        }

        // 3. Process Constructors
        foreach (var ctor in type.Methods.Where(m => m.IsConstructor && !m.IsStatic))
        {
            ImplementConstructor(ctor, type, module, backingFields, setCollectionOwner, isCollection);
        }

        // 4. Process Properties
        var propertyMethods = new HashSet<MethodDefinition>();
        foreach (var prop in type.Properties)
        {
            if (prop.GetMethod != null) propertyMethods.Add(prop.GetMethod);
            if (prop.SetMethod != null) propertyMethods.Add(prop.SetMethod);
            ImplementProperty(prop, type, module, backingFields, eventFields, clampNumeric, fireEvent, getSelItem, isCollection, colCount, setCollectionOwner, colGetItem, colSetItem);
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

            ImplementMethod(m, type, module, isCollection,
                colAdd, colAddRange, colRemove, colRemoveAt, colClear, colCount, colContains, colIndexOf, colGetItem, colSetItem, colGetEnumerator, colInsert,
                invokeDelegate, setClip, getClip, hasClip, clrClip, getStartup, getExec, getProd);
        }

        // Process Nested Types
        foreach (var nt in type.NestedTypes)
        {
            ImplementTypeBehaviors(nt, module, supportAsm,
                colAdd, colAddRange, colRemove, colRemoveAt, colClear, colCount, colContains, colIndexOf, colGetItem, colSetItem, colGetEnumerator, colInsert,
                clampNumeric, invokeDelegate, fireEvent, setClip, getClip, hasClip, clrClip, getStartup, getExec, getProd, getSelItem, setCollectionOwner,
                delegateCombine, delegateRemove);
        }
    }

    private static void ImplementConstructor(MethodDefinition ctor, TypeDefinition type, ModuleDefinition module,
        Dictionary<string, FieldDefinition> backingFields, MethodReference setCollectionOwner, bool isCollection)
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
                    baseCtor = module.ImportReference(accessibleCtors[0]);
                }
            }

            if (baseCtor == null)
            {
                var objCtor = module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)!);
                baseCtor = objCtor;
            }

            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            foreach (var p in baseCtor.Parameters)
            {
                PushDefaultValue(il, ctor.Body, p.ParameterType, module);
            }
            il.Add(Instruction.Create(OpCodes.Call, baseCtor));
        }

        if (isCollection && ctor.Parameters.Count >= 1)
        {
            il.Add(Instruction.Create(OpCodes.Ldarg_0));
            il.Add(Instruction.Create(OpCodes.Ldarg_1));
            il.Add(Instruction.Create(OpCodes.Call, setCollectionOwner));
        }

        // Default initializations
        if (type.Name == "NumericUpDown")
        {
            if (backingFields.TryGetValue("Minimum", out var minFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_0));
                il.Add(Instruction.Create(OpCodes.Newobj, module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
                il.Add(Instruction.Create(OpCodes.Stfld, minFld));
            }
            if (backingFields.TryGetValue("Maximum", out var maxFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4, 100));
                il.Add(Instruction.Create(OpCodes.Newobj, module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
                il.Add(Instruction.Create(OpCodes.Stfld, maxFld));
            }
            if (backingFields.TryGetValue("Increment", out var incFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Newobj, module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!)));
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
        else if (type.Name == "Control" || InheritsFrom(type, "Control"))
        {
            if (backingFields.TryGetValue("Visible", out var visFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Stfld, visFld));
            }
            if (backingFields.TryGetValue("Enabled", out var enFld))
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldc_I4_1));
                il.Add(Instruction.Create(OpCodes.Stfld, enFld));
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
            if (curr.Name == baseName) return true;
            if (curr.BaseType == null) break;
            curr = curr.BaseType.Resolve();
        }
        return false;
    }

    private static void ImplementProperty(PropertyDefinition prop, TypeDefinition type, ModuleDefinition module,
        Dictionary<string, FieldDefinition> backingFields, Dictionary<string, FieldDefinition> eventFields,
        MethodReference clampNumeric, MethodReference fireEvent, MethodReference getSelItem,
        bool isCollection, MethodReference colCount, MethodReference setCollectionOwner,
        MethodReference colGetItem, MethodReference colSetItem)
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
                    il.Add(Instruction.Create(OpCodes.Call, colGetItem));
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
                    PushDefaultValue(il, gm.Body, gm.ReturnType, module);
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
                    il.Add(Instruction.Create(OpCodes.Call, colSetItem));
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
            m.Body.Instructions.Add(Instruction.Create(OpCodes.Call, colCount));
            m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        if (type.FullName == "System.Windows.Forms.Padding")
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

                il.Add(Instruction.Create(OpCodes.Call, getSelItem));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            if (prop.SetMethod != null && selBf != null)
            {
                var m = prop.SetMethod;
                m.Body = new MethodBody(m);
                var il = m.Body.Instructions;
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, selBf));
                il.Add(Instruction.Create(OpCodes.Ret));
            }
            return;
        }

        if (!backingFields.TryGetValue(prop.Name, out var bf)) return;

        bool isStatic = (prop.GetMethod != null && prop.GetMethod.IsStatic) || (prop.SetMethod != null && prop.SetMethod.IsStatic);

        // Getter
        if (prop.GetMethod != null)
        {
            var m = prop.GetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;

            MethodReference lazyCtor = FindLazyConstructor(prop, type, module);
            if (lazyCtor != null && !isStatic)
            {
                var retLabel = Instruction.Create(OpCodes.Nop);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldfld, bf));
                il.Add(Instruction.Create(OpCodes.Dup));
                il.Add(Instruction.Create(OpCodes.Brtrue_S, retLabel));
                il.Add(Instruction.Create(OpCodes.Pop));

                il.Add(Instruction.Create(OpCodes.Ldarg_0)); // this for stfld
                if (lazyCtor.Parameters.Count == 1)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0)); // owner for ctor
                }
                il.Add(Instruction.Create(OpCodes.Newobj, lazyCtor));
                if (prop.Name == "Controls")
                {
                    il.Add(Instruction.Create(OpCodes.Dup));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Call, setCollectionOwner));
                }
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

        // Setter
        if (prop.SetMethod != null)
        {
            var m = prop.SetMethod;
            m.Body = new MethodBody(m);
            var il = m.Body.Instructions;

            if (type.Name == "NumericUpDown" && prop.Name == "Value")
            {
                backingFields.TryGetValue("Minimum", out var minFld);
                backingFields.TryGetValue("Maximum", out var maxFld);
                eventFields.TryGetValue("ValueChanged", out var valChangedEvt);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1)); // value
                if (minFld != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldfld, minFld)); }
                else { il.Add(Instruction.Create(OpCodes.Ldc_I4_0)); il.Add(Instruction.Create(OpCodes.Newobj, module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!))); }
                if (maxFld != null) { il.Add(Instruction.Create(OpCodes.Ldarg_0)); il.Add(Instruction.Create(OpCodes.Ldfld, maxFld)); }
                else { il.Add(Instruction.Create(OpCodes.Ldc_I4, 100)); il.Add(Instruction.Create(OpCodes.Newobj, module.ImportReference(typeof(decimal).GetConstructor(new[] { typeof(int) })!))); }
                il.Add(Instruction.Create(OpCodes.Call, clampNumeric));

                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                if (valChangedEvt != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, valChangedEvt));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                    il.Add(Instruction.Create(OpCodes.Call, fireEvent));
                }

                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else if ((type.Name == "CheckBox" || type.Name == "RadioButton") && prop.Name == "Checked")
            {
                eventFields.TryGetValue("CheckedChanged", out var chkChangedEvt);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                if (chkChangedEvt != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, chkChangedEvt));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                    il.Add(Instruction.Create(OpCodes.Call, fireEvent));
                }

                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else if (type.Name == "TextBoxBase" && prop.Name == "Text")
            {
                eventFields.TryGetValue("TextChanged", out var txtChangedEvt);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                if (txtChangedEvt != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, txtChangedEvt));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                    il.Add(Instruction.Create(OpCodes.Call, fireEvent));
                }

                il.Add(Instruction.Create(OpCodes.Ret));
            }
            else if (type.Name == "ComboBox" && prop.Name == "SelectedIndex")
            {
                eventFields.TryGetValue("SelectedIndexChanged", out var selChangedEvt);

                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Stfld, bf));

                if (selChangedEvt != null)
                {
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldfld, selChangedEvt));
                    il.Add(Instruction.Create(OpCodes.Ldarg_0));
                    il.Add(Instruction.Create(OpCodes.Ldnull));
                    il.Add(Instruction.Create(OpCodes.Call, fireEvent));
                }

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

    private static MethodReference FindLazyConstructor(PropertyDefinition prop, TypeDefinition declaringType, ModuleDefinition module)
    {
        if (prop.PropertyType.IsValueType) return null;
        string propName = prop.Name;
        if (propName == "Parent" || propName == "Site" || propName == "Container" || propName == "TopLevelControl") return null;
        var pType = prop.PropertyType.Resolve();
        if (pType == null || pType.Scope == null) return null;

        MethodReference SafeImport(MethodDefinition m) => m.DeclaringType?.Module == module ? m : module.ImportReference(m);

        if (propName == "Controls")
        {
            if (declaringType.Name == "TableLayoutPanel")
            {
                var tlcc = module.Types.FirstOrDefault(t => t.Name == "TableLayoutControlCollection");
                if (tlcc != null)
                {
                    var c = tlcc.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
                    if (c != null) return SafeImport(c);
                }
            }
            var cc = module.Types.FirstOrDefault(t => t.Name == "ControlCollection");
            if (cc != null)
            {
                var c = cc.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
                if (c != null) return SafeImport(c);
            }
        }

        if (propName == "DataBindings")
        {
            var cbc = module.Types.FirstOrDefault(t => t.Name == "ControlBindingsCollection");
            if (cbc != null)
            {
                var c = cbc.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
                if (c != null) return SafeImport(c);
            }
        }

        if (propName == "ColumnStyles")
        {
            var colStyles = module.Types.FirstOrDefault(t => t.Name == "TableLayoutColumnStyleCollection");
            if (colStyles != null)
            {
                var c = colStyles.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 0)
                    ?? colStyles.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
                if (c != null) return SafeImport(c);
            }
        }

        if (propName == "RowStyles")
        {
            var rowStyles = module.Types.FirstOrDefault(t => t.Name == "TableLayoutRowStyleCollection");
            if (rowStyles != null)
            {
                var c = rowStyles.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 0)
                    ?? rowStyles.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
                if (c != null) return SafeImport(c);
            }
        }

        if (pType.Name == "ObjectCollection")
        {
            var ctorWithOwner = pType.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1);
            if (ctorWithOwner != null) return SafeImport(ctorWithOwner);
        }

        if (pType.IsClass && !pType.IsValueType && !pType.IsAbstract && !pType.IsInterface && pType.FullName != "System.String")
        {
            var defaultCtor = pType.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            if (defaultCtor != null) return SafeImport(defaultCtor);

            var ownerCtor = pType.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 1);
            if (ownerCtor != null) return SafeImport(ownerCtor);
        }

        return null;
    }

    private static void ImplementMethod(MethodDefinition m, TypeDefinition type, ModuleDefinition module, bool isCollection,
        MethodReference colAdd, MethodReference colAddRange, MethodReference colRemove, MethodReference colRemoveAt,
        MethodReference colClear, MethodReference colCount, MethodReference colContains, MethodReference colIndexOf,
        MethodReference colGetItem, MethodReference colSetItem, MethodReference colGetEnumerator, MethodReference colInsert,
        MethodReference invokeDelegate, MethodReference setClip, MethodReference getClip, MethodReference hasClip, MethodReference clrClip,
        MethodReference getStartup, MethodReference getExec, MethodReference getProd)
    {
        m.Body = new MethodBody(m);
        var il = m.Body.Instructions;

        if (isCollection)
        {
            if (m.Name == "Add")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colAdd));
                if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                else if (m.ReturnType.FullName != "System.Int32")
                {
                    il.Add(Instruction.Create(OpCodes.Pop));
                    PushDefaultValue(il, m.Body, m.ReturnType, module);
                }
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "AddRange")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, colAddRange));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Remove")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colRemove));
                if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "RemoveAt")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, colRemoveAt));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Clear")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, colClear));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Contains")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colContains));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "IndexOf")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                if (m.Parameters[0].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[0].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colIndexOf));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Insert")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldarg_2));
                if (m.Parameters[1].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[1].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colInsert));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_Count")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, colCount));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_Item" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Int32")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Call, colGetItem));
                if (m.ReturnType.IsValueType) il.Add(Instruction.Create(OpCodes.Unbox_Any, m.ReturnType));
                else il.Add(Instruction.Create(OpCodes.Castclass, m.ReturnType));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "set_Item" && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.Int32")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldarg_2));
                if (m.Parameters[1].ParameterType.IsValueType)
                    il.Add(Instruction.Create(OpCodes.Box, m.Parameters[1].ParameterType));
                il.Add(Instruction.Create(OpCodes.Call, colSetItem));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "GetEnumerator")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, colGetEnumerator));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "Control" || InheritsFrom(type, "Control"))
        {
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
                il.Add(Instruction.Create(OpCodes.Ldnull));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Invoke" || m.Name == "BeginInvoke")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_1));
                il.Add(Instruction.Create(OpCodes.Ldnull));
                il.Add(Instruction.Create(OpCodes.Call, invokeDelegate));
                if (m.ReturnType.FullName == "System.Void") il.Add(Instruction.Create(OpCodes.Pop));
                else if (m.ReturnType.FullName != "System.Object")
                {
                    il.Add(Instruction.Create(OpCodes.Pop));
                    PushDefaultValue(il, m.Body, m.ReturnType, module);
                }
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
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
                il.Add(Instruction.Create(OpCodes.Call, getStartup));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_ExecutablePath")
            {
                il.Add(Instruction.Create(OpCodes.Call, getExec));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "get_ProductVersion")
            {
                il.Add(Instruction.Create(OpCodes.Call, getProd));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
        }

        if (type.Name == "Clipboard")
        {
            if (m.Name == "SetText")
            {
                il.Add(Instruction.Create(OpCodes.Ldarg_0));
                il.Add(Instruction.Create(OpCodes.Call, setClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "GetText")
            {
                il.Add(Instruction.Create(OpCodes.Call, getClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "ContainsText")
            {
                il.Add(Instruction.Create(OpCodes.Call, hasClip));
                il.Add(Instruction.Create(OpCodes.Ret));
                return;
            }
            if (m.Name == "Clear")
            {
                il.Add(Instruction.Create(OpCodes.Call, clrClip));
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
            il.Add(Instruction.Create(OpCodes.Ldc_I4_2)); // DialogResult.Cancel
            il.Add(Instruction.Create(OpCodes.Ret));
            return;
        }

        PushDefaultValue(il, m.Body, m.ReturnType, module);
        il.Add(Instruction.Create(OpCodes.Ret));
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
