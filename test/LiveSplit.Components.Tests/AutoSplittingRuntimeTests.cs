using LiveSplit.AutoSplittingRuntime;
using LiveSplit.Model;
using System;
using System.IO;
using System.Xml;
using Xunit;
namespace LiveSplit.Components.Tests;

public class AutoSplittingRuntimeTests
{
    [Fact]
    public void LoadNativeLibraryCreateRuntimeAndStepRealWasm()
    {
        ASRLoader.LoadASR();
        string path = Path.Combine(Path.GetTempPath(), "asr-" + Guid.NewGuid() + ".wasm");
        // (module (memory (export "memory") 1) (func (export "update")))
        File.WriteAllBytes(path, Convert.FromHexString("0061736D01000000010401600000030201000503010001071302066D656D6F727902000675706461746500000A040102000B"));
        var state = TestSupport.CreateState();
        var settings = new ComponentSettings(new TimerModel { CurrentState = state });
        try
        {
            settings.ScriptPath = path;
            Assert.Null(settings.Error);
            Assert.NotNull(settings.runtime);
            Assert.True(settings.runtime.Step());
            settings.runtime.SettingsMapSetBool("enabled", true);
            settings.runtime.SettingsMapSetString("choice", "second");
            string xml = settings.GetSettings(new XmlDocument()).OuterXml;
            var loaded = new ComponentSettings(new TimerModel { CurrentState = state });
            try
            {
                var doc = new XmlDocument(); doc.LoadXml(xml); loaded.SetSettings(doc.DocumentElement);
                Assert.Equal(xml, loaded.GetSettings(new XmlDocument()).OuterXml);
            }
            finally { loaded.runtime?.Dispose(); loaded.previousMap?.Dispose(); loaded.previousWidgets?.Dispose(); }
        }
        finally { settings.runtime?.Dispose(); settings.previousMap?.Dispose(); settings.previousWidgets?.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void PreserveAllWasmMapKindsWithoutLoadedScript()
    {
        ASRLoader.LoadASR();
        var settings = new ComponentSettings(new TimerModel { CurrentState = TestSupport.CreateState() });
        var doc = new XmlDocument();
        doc.LoadXml("""
            <Settings><Version>1.0</Version><ScriptPath></ScriptPath><CustomSettings><Setting id="nested" type="map"><Setting id="items" type="list"><Setting type="i64">42</Setting><Setting type="f64">1.25</Setting><Setting type="bool">True</Setting><Setting type="string" value="text" /></Setting></Setting></CustomSettings></Settings>
            """);
        try
        {
            settings.SetSettings(doc.DocumentElement);
            Assert.Equal(doc.DocumentElement.OuterXml, settings.GetSettings(new XmlDocument()).OuterXml);
            var names = new System.Collections.Generic.List<string>();
            settings.PropertyChanged += (_, e) => names.Add(e.PropertyName);
            settings.ScriptPath = "/no/such/file.wasm";
            Assert.Contains(nameof(settings.ScriptPath), names);
            Assert.NotNull(settings.Error);
        }
        finally { settings.previousMap?.Dispose(); }
    }
}
