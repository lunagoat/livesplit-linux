using System;
using System.IO;
using System.Linq;
using LiveSplit.Compat;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace LiveSplit.Tests.Compat;

public class LegacyComponentLoaderMust
{
    // Directory of downloaded (untrusted) .NET Framework component DLLs. These tests only
    // inspect and rewrite them; nothing from them is executed.
    private static string DllDirectory => Environment.GetEnvironmentVariable("LIVESPLIT_TEST_COMPONENT_DLLS");

    [Fact]
    public void RewriteEveryLegacyComponentWithoutLeavingWinFormsOrUpdateManagerReferences()
    {
        if (string.IsNullOrEmpty(DllDirectory) || !Directory.Exists(DllDirectory))
        {
            return;
        }

        int rewritten = 0;
        foreach (string path in Directory.EnumerateFiles(DllDirectory, "*.dll", SearchOption.AllDirectories))
        {
            if (!LegacyComponentLoader.IsLegacy(path))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = LegacyComponentLoader.Rewrite(path);
            }
            catch (Exception ex)
            {
                throw new Exception($"{Path.GetFileName(path)}: {ex.Message}", ex);
            }

            using var module = ModuleDefinition.ReadModule(new MemoryStream(bytes));
            string[] refs = module.AssemblyReferences.Select(r => r.Name).ToArray();
            Assert.DoesNotContain("UpdateManager", refs);
            Assert.DoesNotContain("System.Windows.Forms", refs);
            rewritten++;
        }

        Assert.True(rewritten > 100, $"only {rewritten} legacy components were rewritten");
    }

    [Fact]
    public void RedirectProcessHandleToThePid()
    {
        // Build a tiny "legacy" assembly (references mscorlib) whose Run() returns Process.Handle.
        string dir = Path.Combine(Path.GetTempPath(), "ls-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "Synthetic.dll");
        try
        {
            var asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Synthetic", new Version(1, 0, 0, 0)), "Synthetic", ModuleKind.Dll);
            ModuleDefinition module = asm.MainModule;
            module.AssemblyReferences.Add(new AssemblyNameReference("mscorlib", new Version(4, 0, 0, 0)));
            var type = new TypeDefinition("Synthetic", "Probe", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed, module.ImportReference(typeof(object)));
            var method = new MethodDefinition("Run", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.ImportReference(typeof(IntPtr)));
            method.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(System.Diagnostics.Process))));
            ILProcessor il = method.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Callvirt, module.ImportReference(typeof(System.Diagnostics.Process).GetProperty("Handle").GetMethod));
            il.Emit(OpCodes.Ret);
            type.Methods.Add(method);
            module.Types.Add(type);
            asm.Write(path);

            Assert.True(LegacyComponentLoader.IsLegacy(path));
            System.Reflection.Assembly loaded = LegacyComponentLoader.Load(path);
            var run = loaded.GetType("Synthetic.Probe").GetMethod("Run");

            using System.Diagnostics.Process self = System.Diagnostics.Process.GetCurrentProcess();
            Assert.Equal((IntPtr)self.Id, (IntPtr)run.Invoke(null, [self]));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReadProcessMemoryThroughTheWin32Shim()
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, LegacyComponentLoader.Win32ShimLibrary)))
        {
            return;
        }

        // A synthetic "legacy" assembly that P/Invokes kernel32!ReadProcessMemory like the real splitters do.
        string dir = Path.Combine(Path.GetTempPath(), "ls-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "SyntheticNative.dll");
        try
        {
            var asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("SyntheticNative", new Version(1, 0, 0, 0)), "SyntheticNative", ModuleKind.Dll);
            ModuleDefinition module = asm.MainModule;
            module.AssemblyReferences.Add(new AssemblyNameReference("mscorlib", new Version(4, 0, 0, 0)));
            var type = new TypeDefinition("Synthetic", "Native", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed, module.ImportReference(typeof(object)));
            var read = new MethodDefinition("ReadProcessMemory",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.PInvokeImpl | Mono.Cecil.MethodAttributes.HideBySig,
                module.ImportReference(typeof(int)))
            {
                ImplAttributes = Mono.Cecil.MethodImplAttributes.PreserveSig,
                PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvWinapi | PInvokeAttributes.SupportsLastError, "ReadProcessMemory", new ModuleReference("kernel32.dll")),
            };
            module.ModuleReferences.Add(read.PInvokeInfo.Module);
            read.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(IntPtr))));
            read.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(IntPtr))));
            read.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(IntPtr))));
            read.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(IntPtr))));
            read.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(IntPtr))));
            type.Methods.Add(read);
            module.Types.Add(type);
            asm.Write(path);

            System.Reflection.Assembly loaded = LegacyComponentLoader.Load(path);
            var method = loaded.GetType("Synthetic.Native").GetMethod("ReadProcessMemory");

            byte[] source = [1, 2, 3, 4, 5, 6, 7, 8];
            byte[] target = new byte[8];
            var pinnedSource = System.Runtime.InteropServices.GCHandle.Alloc(source, System.Runtime.InteropServices.GCHandleType.Pinned);
            var pinnedTarget = System.Runtime.InteropServices.GCHandle.Alloc(target, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                using System.Diagnostics.Process self = System.Diagnostics.Process.GetCurrentProcess();
                object result = method.Invoke(null, [(IntPtr)self.Id, pinnedSource.AddrOfPinnedObject(), pinnedTarget.AddrOfPinnedObject(), (IntPtr)8, IntPtr.Zero]);
                Assert.Equal(1, (int)result);
                Assert.Equal(source, target);
            }
            finally
            {
                pinnedSource.Free();
                pinnedTarget.Free();
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string FindDll(string name) =>
        string.IsNullOrEmpty(DllDirectory) || !Directory.Exists(DllDirectory)
            ? null
            : Directory.EnumerateFiles(DllDirectory, name, SearchOption.AllDirectories).FirstOrDefault();

    [Fact]
    public void RunTheCelesteAutoSplitterWithoutAGame()
    {
        string path = FindDll("LiveSplit.Celeste.dll");
        if (path == null)
        {
            return;
        }

        IComponentFactory factory = ComponentManager.LoadFactory<IComponentFactory>(path);
        Assert.NotNull(factory);

        ComponentManager.RaceProviderFactories ??= new System.Collections.Generic.Dictionary<string, IRaceProviderFactory>();
        LiveSplit.Hosting.PlatformServices.Current ??= new LiveSplit.Hosting.ConsolePlatformServices();
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.Add(new Segment("Split"));
        var state = new LiveSplitState(run, null, new Layout(), new LayoutSettings(), new StandardSettingsFactory().Create());

        IComponent component = factory.Create(state);
        Assert.NotNull(component);
        Assert.False(string.IsNullOrEmpty(component.ComponentName));
        Assert.NotNull(component.GetSettings(new System.Xml.XmlDocument()));
        Assert.NotNull(component.GetSettingsControl(LayoutMode.Vertical));

        component.Update(null, state, 0, 0, LayoutMode.Vertical);
        System.Threading.Thread.Sleep(300);
        component.Update(null, state, 0, 0, LayoutMode.Vertical);
        component.Dispose();
    }

    // Loads and exercises every component in the directory, writing a report to
    // $LIVESPLIT_SWEEP_REPORT. Executes their code, so it only runs when asked (and should be
    // run in a sandbox).
    [Fact]
    public void SweepEveryLegacyComponent()
    {
        string report = Environment.GetEnvironmentVariable("LIVESPLIT_SWEEP_REPORT");
        if (string.IsNullOrEmpty(report) || string.IsNullOrEmpty(DllDirectory))
        {
            return;
        }

        // A component that kills the process (e.g. stack overflow) leaves a START without a DONE
        // in the progress file; it is recorded as CRASH on the next run and skipped.
        string progress = report + ".progress";
        var done = new System.Collections.Generic.HashSet<string>();
        string pending = null;
        if (File.Exists(progress))
        {
            foreach (string line in File.ReadAllLines(progress))
            {
                string[] parts = line.Split('\t');
                if (parts[0] == "START")
                {
                    pending = parts[1];
                }
                else if (parts[0] == "DONE")
                {
                    done.Add(parts[1]);
                    pending = null;
                }
            }

            if (pending != null)
            {
                File.AppendAllText(report, $"CRASH\t{pending}\tprocess died while running it\n");
                File.AppendAllText(progress, $"DONE\t{pending}\n");
                done.Add(pending);
            }
        }

        ComponentManager.RaceProviderFactories ??= new System.Collections.Generic.Dictionary<string, IRaceProviderFactory>();
        LiveSplit.Hosting.PlatformServices.Current ??= new LiveSplit.Hosting.ConsolePlatformServices();

        // The app's UI thread has a synchronization context that components capture; give the
        // test thread one too. Sibling components (LiveSplit.Text, ...) come from the app's Components folder.
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Threading.SynchronizationContext());
        string sweepBase = Environment.GetEnvironmentVariable("LIVESPLIT_SWEEP_BASE");
        if (!string.IsNullOrEmpty(sweepBase))
        {
            ComponentManager.BasePath = sweepBase;
        }

        foreach (string path in Directory.EnumerateFiles(DllDirectory, "*.dll", SearchOption.AllDirectories).OrderBy(p => p))
        {
            string rel = Path.GetRelativePath(DllDirectory, path);
            if (done.Contains(rel))
            {
                continue;
            }

            File.AppendAllText(progress, $"START\t{rel}\n");
            string stage = "load";
            string result = null;
            try
            {
                if (!LegacyComponentLoader.IsLegacy(path))
                {
                    File.AppendAllText(progress, $"DONE\t{rel}\n");
                    continue;
                }

                System.Reflection.Assembly asm = LegacyComponentLoader.Load(path);
                var attr = (ComponentFactoryAttribute)Attribute.GetCustomAttribute(asm, typeof(ComponentFactoryAttribute));
                if (attr == null)
                {
                    File.AppendAllText(report, $"SKIP\t{rel}\tno ComponentFactory attribute (library)\n");
                    File.AppendAllText(progress, $"DONE\t{rel}\n");
                    continue;
                }

                stage = "factory";
                var factory = (IComponentFactory)Activator.CreateInstance(attr.ComponentFactoryClassType);
                var run = new Run(new StandardComparisonGeneratorsFactory());
                run.Add(new Segment("Split"));
                var state = new LiveSplitState(run, null, new Layout(), new LayoutSettings(), new StandardSettingsFactory().Create());
                stage = "create";
                IComponent component = factory.Create(state);
                stage = "settings";
                component.GetSettings(new System.Xml.XmlDocument());
                stage = "settings-control";
                component.GetSettingsControl(LayoutMode.Vertical);
                stage = "update";
                component.Update(null, state, 0, 0, LayoutMode.Vertical);
                stage = "dispose";
                component.Dispose();
                result = $"OK\t{rel}";
            }
            catch (Exception ex)
            {
                Exception e = ex is System.Reflection.TargetInvocationException { InnerException: not null } t ? t.InnerException : ex;
                string first = e.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
                result = $"FAIL\t{rel}\t{stage}\t{e.GetType().Name}: {e.Message.Split('\n')[0]}\t{first}";
            }

            File.AppendAllText(report, result + "\n");
            File.AppendAllText(progress, $"DONE\t{rel}\n");
        }

        File.AppendAllText(report, "FINISHED\n");
    }
}
