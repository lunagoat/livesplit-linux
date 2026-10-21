using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunSavers;
using LiveSplit.Hosting;
using LiveSplit.UI.Components;
using LiveSplit.Updates;
using LiveSplit.Web.Share;
using System;
using System.IO;
using System.Windows.Forms;
using Xunit;

namespace LiveSplit.Tests.Platform;

public class CoreServicesMust
{
    private sealed class RecordingServices : ConsolePlatformServices
    {
        public string LastMessage { get; private set; }

        public override DialogResult ShowMessage(object owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            LastMessage = text;
            return DialogResult.Yes;
        }

        public override string ChooseSaveFile(object owner, string title, string filter, string suggestedName)
        {
            return SavePath;
        }

        public string SavePath { get; set; }
    }

    [Fact]
    public void FallBackToACancellingStubWithoutAUserInterface()
    {
        IPlatformServices previous = PlatformServices.Current;
        PlatformServices.Current = new ConsolePlatformServices();
        try
        {
            string value = "initial";

            Assert.Equal(DialogResult.Cancel, LiveSplit.UI.InputBox.Show("title", "prompt", ref value));
            Assert.Equal("initial", value);
            Assert.Null(PlatformServices.Current.ChooseSaveFile(null, "t", "f", "n"));
        }
        finally
        {
            PlatformServices.Current = previous;
        }
    }

    [Fact]
    public void RouteDialogsThroughTheInstalledServices()
    {
        IPlatformServices previous = PlatformServices.Current;
        var services = new RecordingServices();
        PlatformServices.Current = services;
        try
        {
            DialogResult result = PlatformServices.Current.ShowMessage(null, "hello", "caption", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            Assert.Equal(DialogResult.Yes, result);
            Assert.Equal("hello", services.LastMessage);
        }
        finally
        {
            PlatformServices.Current = previous;
        }
    }

    [Fact]
    public void ExportRunsToExcelThroughTheSaveDialog()
    {
        IPlatformServices previous = PlatformServices.Current;
        string path = Path.Combine(Path.GetTempPath(), "livesplit-" + Guid.NewGuid().ToString("N") + ".xlsx");
        PlatformServices.Current = new RecordingServices { SavePath = path };
        try
        {
            var run = new Run(new StandardComparisonGeneratorsFactory()) { GameName = "Game", CategoryName = "Any%" };
            run.Add(new Segment("One"));

            Assert.True(Excel.Instance.SubmitRun(run));

            byte[] header = File.ReadAllBytes(path)[..2];
            Assert.Equal([(byte)'P', (byte)'K'], header);
        }
        finally
        {
            PlatformServices.Current = previous;
            File.Delete(path);
        }
    }

    [Fact]
    public void ExposeAUserAgent()
    {
        Assert.StartsWith("LiveSplit/", UpdateHelper.UserAgent);
    }

    [Fact]
    public void LoadNoComponentsFromAMissingDirectory()
    {
        string previous = ComponentManager.BasePath;
        ComponentManager.BasePath = Path.Combine(Path.GetTempPath(), "livesplit-missing-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Empty(ComponentManager.LoadAllFactories<IComponentFactory>());
        }
        finally
        {
            ComponentManager.BasePath = previous;
        }
    }

    [Fact]
    public void AssociateNoFilesByDefault()
    {
        Assert.True(FileAssociation.Current.IsRegistered);
        FileAssociation.Current.Register();
    }
}
