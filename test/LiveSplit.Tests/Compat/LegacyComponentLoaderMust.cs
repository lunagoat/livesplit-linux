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

        ComponentManager.RaceProviderFactories ??= new System.Collections.Generic.Dictionary<string, IRaceProviderFactory>();
        LiveSplit.Hosting.PlatformServices.Current ??= new LiveSplit.Hosting.ConsolePlatformServices();
        var lines = new System.Collections.Generic.List<string>();
        foreach (string path in Directory.EnumerateFiles(DllDirectory, "*.dll", SearchOption.AllDirectories).OrderBy(p => p))
        {
            string stage = "load";
            string rel = Path.GetRelativePath(DllDirectory, path);
            try
            {
                if (!LegacyComponentLoader.IsLegacy(path))
                {
                    continue;
                }

                System.Reflection.Assembly asm = LegacyComponentLoader.Load(path);
                var attr = (ComponentFactoryAttribute)Attribute.GetCustomAttribute(asm, typeof(ComponentFactoryAttribute));
                if (attr == null)
                {
                    lines.Add($"SKIP\t{rel}\tno ComponentFactory attribute (library)");
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
                lines.Add($"OK\t{rel}");
            }
            catch (Exception ex)
            {
                Exception e = ex is System.Reflection.TargetInvocationException { InnerException: not null } t ? t.InnerException : ex;
                string first = e.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
                lines.Add($"FAIL\t{rel}\t{stage}\t{e.GetType().Name}: {e.Message.Split('\n')[0]}\t{first}");
            }
        }

        File.WriteAllLines(report, lines);
    }
}
