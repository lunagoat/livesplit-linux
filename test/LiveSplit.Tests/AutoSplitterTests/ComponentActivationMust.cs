using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using Xunit;

namespace LiveSplit.Tests.AutoSplitterTests;

[Collection("Environment")]
public class ComponentActivationMust
{
    private sealed class RecordingServices : ConsolePlatformServices
    {
        public string LastMessage { get; private set; }

        public override DialogResult ShowMessage(object owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            LastMessage = text;
            return DialogResult.OK;
        }
    }

    [Fact]
    public void ExplainWhyAComponentThatCannotBeLoadedDoesNotActivate()
    {
        string source = Path.Combine(Path.GetTempPath(), "ls-src-" + Guid.NewGuid().ToString("N"));
        string basePath = Path.Combine(Path.GetTempPath(), "ls-base-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(basePath, ComponentManager.PATH_COMPONENTS));
        string dll = Path.Combine(source, "LiveSplit.Fake.dll");
        File.WriteAllText(dll, "not an assembly");

        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.Add(new Segment("Split"));
        var state = new LiveSplitState(run, null, new Layout(), new LayoutSettings(), new StandardSettingsFactory().Create());

        IPlatformServices previousServices = PlatformServices.Current;
        string previousBase = ComponentManager.BasePath;
        var services = new RecordingServices();
        PlatformServices.Current = services;
        ComponentManager.BasePath = basePath;
        try
        {
            ComponentManager.LoadLayoutComponent(null, state); // initialises ComponentFactories
            var splitter = new AutoSplitter
            {
                Type = AutoSplitterType.Component,
                URLs = [new Uri(dll).AbsoluteUri],
                Games = ["Fake"],
            };

            splitter.Activate(state);

            Assert.False(splitter.IsActivated);
            Assert.False(splitter.IsDownloaded);
            Assert.True(services.LastMessage?.Contains("Windows-only") == true, services.LastMessage);
        }
        finally
        {
            PlatformServices.Current = previousServices;
            ComponentManager.BasePath = previousBase;
            Directory.Delete(source, true);
            Directory.Delete(basePath, true);
        }
    }
}
