using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Racetime;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.Model;
using LiveSplit.Racetime.View;
using LiveSplit.Services;
using LiveSplit.View.Dialogs;
using System;
using System.IO;
using System.Windows.Forms;
using Xunit;

namespace LiveSplit.AppTests;

public class RacetimeWindowMust
{
    private static void RenderWindow(Window window, string filename)
    {
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        string scratchDir = "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ui-compsettings";
        string output = Environment.GetEnvironmentVariable("LS_SETTINGS_TEST_OUT")
            ?? (Directory.Exists(scratchDir) ? scratchDir : "/tmp/livesplit-settings-views");
        Directory.CreateDirectory(output);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(output, filename));
    }

    [AvaloniaFact]
    public void RenderChannelWindowLoadingAndLoaded()
    {
        var state = new LiveSplitState(null, null, null, null, null);
        var model = new TimerModel { CurrentState = state };
        var channel = new RacetimeChannel(state, model, new RacetimeSettings());
        using var form = new ChannelForm(channel, "oot/weekly-race", false);

        var window = new RacetimeChannelWindow(form);
        RenderWindow(window, "RacetimeChannel-Connecting.png");

        Assert.Equal("Connecting to weekly-race", window.Title);

        form.PageLoaded(true);
        Dispatcher.UIThread.RunJobs();
        RenderWindow(window, "RacetimeChannel-Loaded.png");

        window.Close();
    }

    [AvaloniaFact]
    public void ClosingProtocolHonorsRequestClose()
    {
        var state = new LiveSplitState(null, null, null, null, null);
        var model = new TimerModel { CurrentState = state };
        var channel = new RacetimeChannel(state, model, new RacetimeSettings());
        var form = new ChannelForm(channel, "sm64/star-race", false);

        var window = new RacetimeChannelWindow(form);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Normal close when not racing completes without prompt
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void RacetimeIntegrationHooksLauncher()
    {
        RacetimeIntegration.Initialize();
        Assert.IsType<RacetimeRaceDialogLauncher>(AppServices.RaceDialog);
    }
}
