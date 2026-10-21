using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using LiveSplit.Model;
using LiveSplit.Options;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LiveSplit.Compat;

/// <summary>
/// Loads component assemblies that were built for Windows .NET Framework LiveSplit
/// (auto splitters such as LiveSplit.Celeste.dll). Their IL is rewritten in memory so the
/// references to System.Windows.Forms and System.Drawing land on this port's types, the few
/// Core members that changed are bridged, and their kernel32/psapi/... P/Invokes go to
/// libwin32shim.so.
/// </summary>
public static class LegacyComponentLoader
{
    public const string WinFormsStubAssembly = "LiveSplit.Compat.WinForms";
    public const string Win32ShimLibrary = "libwin32shim.so";

    private static readonly object Gate = new();
    private static bool resolverInstalled;
    private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Directories = new(StringComparer.Ordinal);

    private static readonly string[] Win32Libraries =
    [
        "kernel32", "user32", "psapi", "ntdll", "gdi32", "dbghelp", "winmm", "advapi32", "shell32", "version", "shlwapi", "ole32",
    ];

    /// <summary>
    /// True for assemblies built against the full .NET Framework (they reference mscorlib) or
    /// against the Windows-only WinForms/System.Drawing.Common packages.
    /// </summary>
    public static bool IsLegacy(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata)
            {
                return false;
            }

            System.Reflection.Metadata.MetadataReader md = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            return md.AssemblyReferences.Any(r => md.GetString(md.GetAssemblyReference(r).Name) is "mscorlib" or "System.Windows.Forms" or "System.Drawing.Common");
        }
        catch
        {
            return false;
        }
    }

    public static Assembly Load(string path)
    {
        string fullPath = Path.GetFullPath(path);
        lock (Gate)
        {
            InstallResolver();
            if (Loaded.TryGetValue(fullPath, out Assembly existing))
            {
                return existing;
            }

            Directories.Add(Path.GetDirectoryName(fullPath));
            Assembly assembly = LoadRewritten(fullPath);
            Loaded[fullPath] = assembly;
            return assembly;
        }
    }

    private static void InstallResolver()
    {
        if (resolverInstalled)
        {
            return;
        }

        resolverInstalled = true;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            if (name.Name == null)
            {
                return null;
            }

            string stub = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            if ((name.Name.StartsWith("LiveSplit.Compat.", StringComparison.Ordinal) || name.Name == "LiveSplit.View") && File.Exists(stub))
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(stub);
            }

            // Dependencies of legacy components that sit next to them in the Components folder.
            string components = Path.GetFullPath(Path.Combine(LiveSplit.UI.Components.ComponentManager.BasePath ?? LiveSplit.Hosting.Paths.ExecutableDirectory, LiveSplit.UI.Components.ComponentManager.PATH_COMPONENTS));
            string sibling = Path.Combine(components, name.Name + ".dll");
            if (!File.Exists(sibling))
            {
                lock (Gate)
                {
                    sibling = Directories.Select(d => Path.Combine(d, name.Name + ".dll")).FirstOrDefault(File.Exists);
                }
            }

            if (sibling != null && File.Exists(sibling))
            {
                return IsLegacy(sibling) ? Load(sibling) : AssemblyLoadContext.Default.LoadFromAssemblyPath(sibling);
            }

            return null;
        };
    }

    private static Assembly LoadRewritten(string path)
    {
        byte[] rewritten = Rewrite(path);

        // Load from a file next to the original when possible so Assembly.Location (and
        // directories derived from it) behave as the component expects; fall back to memory.
        Assembly assembly = null;
        string sideFile = path + ".compat";
        try
        {
            File.WriteAllBytes(sideFile, rewritten);
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(sideFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        if (assembly == null)
        {
            using var stream = new MemoryStream(rewritten);
            assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        }

        NativeLibrary.SetDllImportResolver(assembly, ResolveNative);
        return assembly;
    }

    private static IntPtr ResolveNative(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string baseName = Path.GetFileNameWithoutExtension(libraryName).ToLowerInvariant();
        if (Array.IndexOf(Win32Libraries, baseName) < 0)
        {
            return IntPtr.Zero;
        }

        string shim = Path.Combine(AppContext.BaseDirectory, Win32ShimLibrary);
        if (File.Exists(shim) && NativeLibrary.TryLoad(shim, out IntPtr handle))
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    private static DefaultAssemblyResolver CreateResolver()
    {
        // Needed so Cecil can resolve enum types while writing (default parameter values).
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(AppContext.BaseDirectory);
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
        return resolver;
    }

    public static byte[] Rewrite(string path)
    {
        var parameters = new ReaderParameters { InMemory = true, ReadSymbols = false, AssemblyResolver = CreateResolver() };
        using ModuleDefinition module = ModuleDefinition.ReadModule(path, parameters);

        Assembly coreAssembly = typeof(LiveSplitState).Assembly;
        Assembly drawingAssembly = typeof(System.Drawing.Graphics).Assembly;
        Assembly primitivesAssembly = typeof(System.Drawing.Color).Assembly;
        HashSet<string> coreWinForms = ExportedNames(coreAssembly, "System.Windows.Forms");
        HashSet<string> primitives = ExportedNames(primitivesAssembly, "System.Drawing");
        HashSet<string> drawing = ExportedNames(drawingAssembly, "System.Drawing");

        var created = new Dictionary<string, AssemblyNameReference>();
        AssemblyNameReference Ref(Assembly assembly) => Ref2(created, module, assembly.GetName());
        AssemblyNameReference stubRef = Ref2(created, module, new AssemblyName(WinFormsStubAssembly) { Version = new Version(1, 0, 0, 0) });

        foreach (AssemblyNameReference r in module.AssemblyReferences.Where(r => r.Name == "LiveSplit.Core").ToList())
        {
            AssemblyName ours = coreAssembly.GetName();
            r.Version = ours.Version;
            r.PublicKey = null;
            r.PublicKeyToken = ours.GetPublicKeyToken() ?? [];
        }

        // 1. Retarget type references.
        foreach (TypeReference type in module.GetTypeReferences().ToList())
        {
            if (type.DeclaringType != null || type.Scope is not AssemblyNameReference scope)
            {
                continue;
            }

            string name = type.FullName;
            switch (scope.Name)
            {
                case "System.Windows.Forms":
                    type.Scope = coreWinForms.Contains(name) ? Ref(coreAssembly) : stubRef;
                    break;
                case "System.Drawing":
                case "System.Drawing.Common":
                    if (primitives.Contains(name))
                    {
                        type.Scope = Ref(primitivesAssembly);
                    }
                    else if (drawing.Contains(name))
                    {
                        type.Scope = Ref(drawingAssembly);
                    }

                    break;
                case "UpdateManager":
                    type.Scope = Ref(coreAssembly);
                    break;
            }
        }

        foreach (AssemblyNameReference old in module.AssemblyReferences.Where(r => r.Name is "System.Windows.Forms" or "UpdateManager").ToList())
        {
            if (!module.GetTypeReferences().Any(t => ReferenceEquals(t.Scope, old)))
            {
                module.AssemblyReferences.Remove(old);
            }
        }

        // 2. Bridge Core API differences.
        TypeSystem types = module.TypeSystem;
        foreach (TypeDefinition type in module.GetTypes())
        {
            foreach (MethodDefinition method in type.Methods)
            {
                // Settings controls are toolkit-neutral objects in this port.
                if (method.Name == "GetSettingsControl" && IsSettingsControlSignature(method))
                {
                    method.ReturnType = types.Object;
                }

                if (!method.HasBody)
                {
                    continue;
                }

                BridgeInstructions(module, method);
            }
        }

        using var output = new MemoryStream();
        module.Write(output);
        return output.ToArray();
    }

    // GetSettingsControl(LayoutMode) returned a WinForms Control (often a component-specific
    // UserControl subclass); the host's version returns object.
    private static bool IsSettingsControlSignature(MethodDefinition method)
    {
        if (method.ReturnType.IsValueType || method.ReturnType.FullName is "System.Object" or "System.Void")
        {
            return false;
        }

        return method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "LiveSplit.UI.LayoutMode";
    }

    private static AssemblyNameReference Ref2(Dictionary<string, AssemblyNameReference> created, ModuleDefinition module, AssemblyName name)
    {
        if (created.TryGetValue(name.Name, out AssemblyNameReference existing))
        {
            return existing;
        }

        var reference = new AssemblyNameReference(name.Name, name.Version ?? new Version(1, 0, 0, 0))
        {
            PublicKeyToken = name.GetPublicKeyToken() ?? [],
        };
        module.AssemblyReferences.Add(reference);
        created[name.Name] = reference;
        return reference;
    }

    private static HashSet<string> ExportedNames(Assembly assembly, string namespacePrefix)
    {
        return assembly.GetExportedTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith(namespacePrefix, StringComparison.Ordinal))
            .Select(t => t.FullName.Replace('+', '/'))
            .ToHashSet();
    }

    private static void BridgeInstructions(ModuleDefinition module, MethodDefinition method)
    {
        ILProcessor il = method.Body.GetILProcessor();
        foreach (Instruction instruction in method.Body.Instructions.ToList())
        {
            if (instruction.OpCode == OpCodes.Isinst && instruction.Operand is TypeReference isinstType
                && isinstType.FullName == "System.Windows.Forms.WindowsFormsSynchronizationContext")
            {
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.AsUiSynchronizationContext)));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            if (instruction.Operand is not MethodReference target)
            {
                continue;
            }

            string owner = target.DeclaringType.FullName;

            // LiveSplitState.Form is an IFormHost here; legacy code expects a WinForms Form.
            if (owner == "LiveSplit.Model.LiveSplitState" && target.Name == "get_Form")
            {
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.GetForm)));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            if (owner == "System.Diagnostics.Process" && target.Name == "get_Handle")
            {
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.GetProcessHandle)));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            if (owner == "System.Threading.Thread" && target.Name == "Abort" && target.Parameters.Count == 0)
            {
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.AbortThread)));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            // Legacy BinaryFormatter resources cannot be read any more; degrade to null.
            if (owner == "System.Resources.ResourceManager" && target.Name == "GetObject" && target.Parameters.Count is 1 or 2
                && target.Parameters[0].ParameterType.FullName == "System.String")
            {
                Type[] signature = target.Parameters.Count == 1
                    ? [typeof(System.Resources.ResourceManager), typeof(string)]
                    : [typeof(System.Resources.ResourceManager), typeof(string), typeof(System.Globalization.CultureInfo)];
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.GetResourceObject), signature));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            // Windows-only NamedPipeClientStream overload.
            if (instruction.OpCode == OpCodes.Newobj && owner == "System.IO.Pipes.NamedPipeClientStream" && target.Parameters.Count == 6
                && target.Parameters[2].ParameterType.FullName == "System.IO.Pipes.PipeAccessRights")
            {
                instruction.Operand = module.ImportReference(typeof(LegacyCompat).GetMethod(nameof(LegacyCompat.CreatePipeClient)));
                instruction.OpCode = OpCodes.Call;
                continue;
            }

            // Settings UI helpers that the host UI now owns: drop the call, keep the stack balanced.
            if (owner == "LiveSplit.UI.SettingsHelper" && target.Name is "ColorButtonClick" or "GetFontDialog")
            {
                Neutralize(il, instruction, target);
            }
        }
    }

    private static void Neutralize(ILProcessor il, Instruction instruction, MethodReference target)
    {
        int pops = target.Parameters.Count + (target.HasThis && instruction.OpCode != OpCodes.Newobj ? 1 : 0);
        Instruction anchor = instruction;
        bool first = true;
        for (int i = 0; i < pops; i++)
        {
            if (first)
            {
                instruction.OpCode = OpCodes.Pop;
                instruction.Operand = null;
                first = false;
            }
            else
            {
                Instruction pop = il.Create(OpCodes.Pop);
                il.InsertAfter(anchor, pop);
                anchor = pop;
            }
        }

        bool returnsValue = target.ReturnType.FullName != "System.Void";
        if (pops == 0)
        {
            instruction.OpCode = returnsValue ? OpCodes.Ldnull : OpCodes.Nop;
            instruction.Operand = null;
        }
        else if (returnsValue)
        {
            il.InsertAfter(anchor, il.Create(OpCodes.Ldnull));
        }
    }
}
