using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.ASL;
using LiveSplit.AutoSplittingRuntime;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.UI.Components;
using LiveSplit.View.ComponentSettings;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace LiveSplit.AppTests;

public class NewViewsRenderMust
{
    private static void RenderView(Control view, string filename)
    {
        var window = new Window
        {
            Content = view,
            Width = 720,
            Height = 900,
            Background = new SolidColorBrush(Color.Parse("#202020"))
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        view.Measure(new Size(720, double.PositiveInfinity));
        window.Height = Math.Max(100, Math.Ceiling(view.DesiredSize.Height));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        string scratchDir = "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ui-compsettings";
        string output = Environment.GetEnvironmentVariable("LS_SETTINGS_TEST_OUT")
            ?? (Directory.Exists(scratchDir) ? scratchDir : "/tmp/livesplit-settings-views");
        Directory.CreateDirectory(output);

        using var bitmap = new RenderTargetBitmap(new PixelSize(720, (int)Math.Ceiling(window.Height)), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(output, filename));
        window.Content = null;
        window.Close();
    }

    [AvaloniaFact]
    public void RenderPopulatedAutoSplittingRuntime()
    {
        var model = new LiveSplit.AutoSplittingRuntime.ComponentSettings(null);
        model.ScriptPath = "/home/runner/splits/autosplitter.wasm";

        var titleWidget = new ASRWidget(model, "header_sec", "title", "Game Mission Settings", "Section description", 0);
        var boolWidget1 = new ASRWidget(model, "split_prologue", "bool", "Split on Prologue Complete", "Splits when exiting the prologue area", 0);
        boolWidget1.BooleanValue = true;
        var boolWidget2 = new ASRWidget(model, "split_boss1", "bool", "Split on First Boss Defeated", "Splits on boss HP reaching 0", 0);
        boolWidget2.BooleanValue = false;
        var choiceWidget = new ASRWidget(model, "difficulty", "choice", "Game Difficulty", "Select active route difficulty", 0);
        choiceWidget.Choices.Add(new ASRChoice("easy", "Casual / Normal"));
        choiceWidget.Choices.Add(new ASRChoice("hard", "Nightmare / Extreme"));
        choiceWidget.StringValue = "hard";
        var fileWidget = new ASRWidget(model, "custom_log", "file-select", "Game Log File", "Path to game debug log", 0);
        typeof(ASRWidget).GetProperty("Filter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(fileWidget, "*.log");
        fileWidget.StringValue = "/tmp/game.log";

        model.Items.Add(titleWidget);
        model.Items.Add(boolWidget1);
        model.Items.Add(boolWidget2);
        model.Items.Add(choiceWidget);
        model.Items.Add(fileWidget);

        var view = new AutoSplittingRuntimeView(model);
        RenderView(view, "AutoSplittingRuntime-Populated.png");
    }

    [AvaloniaFact]
    public void RenderPopulatedScriptableAutoSplit()
    {
        var model = new LiveSplit.UI.Components.ComponentSettings("/home/runner/splits/celeste.asl");
        model.SetGameVersion("1.4.0.0");

        var aslSettings = new ASLSettings();
        aslSettings.BasicSettings["start"] = new ASLSetting("start", true, "Start", null) { ToolTip = "Start timer automatically" };
        aslSettings.BasicSettings["split"] = new ASLSetting("split", true, "Split", null) { ToolTip = "Split on chapters" };
        aslSettings.BasicSettings["reset"] = new ASLSetting("reset", false, "Reset", null) { ToolTip = "Reset on title screen" };

        var chapter1 = new ASLSetting("ch1", true, "Prologue & Chapter 1", null) { ToolTip = "Forsaken City splits" };
        var ch1_cassette = new ASLSetting("ch1_cassette", true, "Collect Cassette Tape", "ch1") { ToolTip = "Splits when picking up cassette" };
        var ch1_heart = new ASLSetting("ch1_heart", false, "Collect Crystal Heart", "ch1") { ToolTip = "Splits when picking up crystal heart" };
        var chapter2 = new ASLSetting("ch2", true, "Chapter 2 - Old Site", null) { ToolTip = "Old site splits" };

        aslSettings.OrderedSettings.Add(chapter1);
        aslSettings.OrderedSettings.Add(ch1_cassette);
        aslSettings.OrderedSettings.Add(ch1_heart);
        aslSettings.OrderedSettings.Add(chapter2);

        model.SetASLSettings(aslSettings);

        var view = new ScriptableAutoSplitView(model);
        RenderView(view, "ScriptableAutoSplit-Populated.png");
    }

    [AvaloniaFact]
    public void RenderPopulatedTheRun()
    {
        var model = new CollectorSettings();
        model.UploadKey = "live_therun_key_987654321";

        var setStatusMethod = typeof(CollectorSettings).GetMethod("SetStatus", BindingFlags.NonPublic | BindingFlags.Instance);
        var validatedProp = typeof(CollectorSettings).GetProperty("ValidatedUsername");
        validatedProp?.SetValue(model, "SuperSpeedrunner");
        setStatusMethod?.Invoke(model, [CollectorSettings.ConnectionStatus.Connected]);

        var view = new TheRunView(model);
        RenderView(view, "TheRun-Populated.png");
    }
}
