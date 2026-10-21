using LiveSplit.ASL;
using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
namespace LiveSplit.Components.Tests;

public class ScriptableAutoSplitTests
{
    [Fact]
    public async Task ExecuteRealScriptAgainstSpawnedProcessMemory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "asl-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string name = "lsasl" + Guid.NewGuid().ToString("N")[..6];
        string executable = Path.Combine(dir, name);
        string source = executable + ".c";
        await File.WriteAllTextAsync(source, """
            #include <stdio.h>
            volatile int value = 123;
            int main(void) {
                printf("%p\n", (void*)&value); fflush(stdout);
                int next;
                while (scanf("%d", &next) == 1) { value = next; puts("ok"); fflush(stdout); }
                return 0;
            }
            """);
        using var compiler = Process.Start(new ProcessStartInfo("cc")
        {
            ArgumentList = { "-no-pie", source, "-o", executable },
            RedirectStandardError = true
        });
        await compiler.WaitForExitAsync();
        Assert.True(compiler.ExitCode == 0, await compiler.StandardError.ReadToEndAsync());
        using var helper = Process.Start(new ProcessStartInfo(executable) { RedirectStandardInput = true, RedirectStandardOutput = true });
        try
        {
            string addressText = await helper.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            long address = Convert.ToInt64(addressText, 16);
            int offset = checked((int)(address - helper.MainModuleWow64Safe().BaseAddress.ToInt64()));
            Assert.Equal(123, helper.ReadValue<int>(new IntPtr(address)));
            // The .exe suffix deliberately exercises Core's Wine/Proton name matching.
            var script = ASLParser.Parse($$"""
                state("{{name}}.exe") { int value: {{offset}}; }
                startup { settings.Add("parent", true, "Parent"); settings.Add("child", true, "Child", "parent"); }
                start { return current.value == 123; }
                split { return old.value != current.value && current.value == 456; }
                reset { return current.value == 789; }
                gameTime { return TimeSpan.FromSeconds(current.value); }
                """);
            var state = TestSupport.CreateState();
            script.RunStartup(state);
            try
            {
                script.Update(state); // attach/init
                script.Update(state); // start
                Assert.Equal(TimerPhase.Running, state.CurrentPhase);
                await helper.StandardInput.WriteLineAsync("456"); await helper.StandardInput.FlushAsync();
                Assert.Equal("ok", await helper.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
                script.Update(state);
                Assert.Equal(1, state.CurrentSplitIndex);
                Assert.InRange(state.CurrentTime.GameTime.Value.TotalSeconds, 456, 457);
                await helper.StandardInput.WriteLineAsync("789"); await helper.StandardInput.FlushAsync();
                Assert.Equal("ok", await helper.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)));
                script.Update(state);
                Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
            }
            finally { script.RunShutdown(state); }
        }
        finally { helper.Kill(); await helper.WaitForExitAsync(); Directory.Delete(dir, true); }
    }

    [Fact]
    public void PreserveTreeStateAndAncestorDependenciesAcrossReloadAndXml()
    {
        var model = new ComponentSettings { ScriptPath = "/tmp/test.asl" };
        var settings = new ASLSettings();
        settings.AddBasicSetting("start");
        settings.AddSetting("parent", true, "Parent", null);
        settings.AddSetting("child", true, "Child", "parent");
        model.SetASLSettings(settings);
        var parent = Assert.Single(model.CustomSettings);
        var child = Assert.Single(parent.Children);
        var events = new System.Collections.Generic.List<string>();
        child.PropertyChanged += (_, e) => events.Add(e.PropertyName);
        parent.Value = false;
        Assert.False(child.IsEnabled); Assert.True(child.Value);
        Assert.Contains(nameof(child.IsEnabled), events);
        Assert.False(settings.Reader["child"]);
        var doc = new XmlDocument();
        string xml = model.GetSettings(doc).OuterXml;
        var loaded = new ComponentSettings(); doc.LoadXml(xml); loaded.SetSettings(doc.DocumentElement);
        Assert.Equal(xml, loaded.GetSettings(new XmlDocument()).OuterXml);
        model.ResetASLSettings(); model.SetASLSettings(settings);
        Assert.False(model.CustomSettings[0].Value);
        model.SetAll(null); Assert.True(model.CustomSettings[0].Value);
    }

    [Fact]
    public void ParseBracesInCommentsAndStringsAndKeepCompilerLineNumbers()
    {
        var script = ASLParser.Parse("startup { var text = \"}\"; /* } */ settings.Add(text); }");
        Assert.NotNull(script);
        var error = Assert.Throws<ASLCompilerException>(() => ASLParser.Parse("startup\n{\n  missingFunction();\n}"));
        Assert.Contains("Line 3", error.Message);
        Assert.Throws<FormatException>(() => ASLParser.Parse("startup {"));
    }
}
