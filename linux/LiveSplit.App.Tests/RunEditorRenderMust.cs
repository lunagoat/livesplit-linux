using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.View.RunEditor;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.AppTests;

public class RunEditorRenderMust
{
    [AvaloniaFact]
    public void RenderDialogTabsAndEditCells()
    {
        Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", "Celeste - Any% (1.2.1.5).lss"));
        IRun run = new StandardFormatsRunFactory(stream).Create(new StandardComparisonGeneratorsFactory());
        var state = new LiveSplitState(run, null, null, null, new Settings { ActiveAutoSplitters = [], ComparisonGeneratorStates = new System.Collections.Generic.Dictionary<string, bool>() });
        var window = new RunEditorWindow(new RunEditorRequest { State = state, AllowChangingSegments = true }, false);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(window, "segments");
        Assert.Contains(window.GetVisualDescendants().OfType<TextBox>(), x => x.Text == run[0].Name);
        TextBox firstName = window.GetVisualDescendants().OfType<TextBox>().First(x => x.Text == run[0].Name);
        firstName.Focus();
        firstName.Text = "Edited in Avalonia";
        window.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Move up").Focus();
        Assert.Equal("Edited in Avalonia", run[0].Name);
        window.Editor.AddComparison("Goal", Run.PersonalBestComparisonName);
        window.RefreshGrid();
        Capture(window, "comparison");
        TabControl tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "metadata");
        tabs.SelectedIndex = 2;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "autosplitter");
        tabs.SelectedIndex = 0;
        var history = window.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Attempt history");
        history.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Capture(window, "history");
        window.Close();
        var attempt = new AttemptEditorWindow(run.AttemptHistory.First());
        attempt.Show();
        Capture(attempt, "attempt");
        attempt.Close();
        var choose = new ChooseComparisonsWindow(new System.Collections.Generic.Dictionary<string, bool> { [HCPComparisonGenerator.ComparisonName] = true }, 20, 5);
        choose.Show();
        Capture(choose, "choose-comparisons");
        choose.Close();
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        string[] outputDirs = [
            Environment.GetEnvironmentVariable("LS_RUNEDITOR_TEST_OUT") ?? "/tmp/livesplit-runeditor-tests",
            "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ui-runeditor"
        ];
        foreach (string output in outputDirs)
        {
            Directory.CreateDirectory(output);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            bitmap.Render(window);
            bitmap.Save(Path.Combine(output, name + ".png"));
            Assert.True(new FileInfo(Path.Combine(output, name + ".png")).Length > 1000);
        }
    }
}
