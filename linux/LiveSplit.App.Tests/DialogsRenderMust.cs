using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.View.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.AppTests;

public class DialogsRenderMust
{
    [AvaloniaFact]
    public void RenderAllDialogsHeadlessly()
    {
        ComponentCatalog.Initialize();

        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", "Celeste - Any% (1.2.1.5).lss"));
        IRun run = new StandardFormatsRunFactory(stream).Create(new StandardComparisonGeneratorsFactory());
        var state = new LiveSplitState(run, null, null, null, new Settings { ActiveAutoSplitters = [], ComparisonGeneratorStates = new Dictionary<string, bool>() });

        var layout = new Layout { Settings = new StandardLayoutSettingsFactory().Create() };
        var timerComp = ComponentManager.LoadLayoutComponent("LiveSplit.Timer.dll", state);
        if (timerComp != null) layout.LayoutComponents.Add(timerComp);
        var splitsComp = ComponentManager.LoadLayoutComponent("LiveSplit.Splits.dll", state);
        if (splitsComp != null) layout.LayoutComponents.Add(splitsComp);
        if (layout.LayoutComponents.Count == 0)
            layout.LayoutComponents.Add(new LayoutComponent("", new SeparatorComponent()));

        // =========================================================
        // 1. LayoutEditorDialog (Vertical & Horizontal)
        // =========================================================
        var editorDialog = new LayoutEditorDialog(new LayoutEditorRequest
        {
            Layout = layout,
            State = state
        });
        editorDialog.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(editorDialog, "layout-editor");

        // Toggle Horizontal
        var rbHorizontal = editorDialog.GetVisualDescendants().OfType<RadioButton>().FirstOrDefault(r => r.Content as string == "Horizontal");
        if (rbHorizontal != null)
        {
            rbHorizontal.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Capture(editorDialog, "layout-editor-horizontal");
        }
        editorDialog.Close();
        Dispatcher.UIThread.RunJobs();

        // =========================================================
        // 2. LayoutSettingsDialog (Layout & Component tab)
        // =========================================================
        var layoutSettingsDlg = new LayoutSettingsDialog(layout.Settings, layout);
        layoutSettingsDlg.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(layoutSettingsDlg, "layout-settings-general");

        var layoutTabs = layoutSettingsDlg.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (layoutTabs != null && layoutTabs.ItemCount > 1)
        {
            layoutTabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Capture(layoutSettingsDlg, "layout-settings-component");
        }
        layoutSettingsDlg.Close();
        Dispatcher.UIThread.RunJobs();

        // =========================================================
        // 3. SettingsDialog (Hotkeys, Comparisons, Racing)
        // =========================================================
        var settings = new Settings
        {
            ActiveAutoSplitters = [],
            ComparisonGeneratorStates = new Dictionary<string, bool>()
        };

        var settingsDialog = new SettingsDialog(new SettingsDialogRequest
        {
            Settings = settings,
            CurrentHotkeyProfile = "Default"
        });
        settingsDialog.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(settingsDialog, "settings-hotkeys");

        var settingsTabs = settingsDialog.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        if (settingsTabs != null && settingsTabs.ItemCount > 2)
        {
            settingsTabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Capture(settingsDialog, "settings-comparisons");

            settingsTabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Capture(settingsDialog, "settings-racing");
        }
        settingsDialog.Close();
        Dispatcher.UIThread.RunJobs();

        // =========================================================
        // 4. RaceProviderManagingDialog
        // =========================================================
        var raceDialog = new RaceProviderManagingDialog(settings.RaceProvider);
        raceDialog.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(raceDialog, "race-providers");
        raceDialog.Close();
        Dispatcher.UIThread.RunJobs();

        // =========================================================
        // 5. ShareRunDialog (Twitter & Speedrun.com)
        // =========================================================
        var shareDialog = new ShareRunDialog(new ShareDialogRequest
        {
            State = state,
            Settings = state.Settings,
            MakeScreenShot = () => new System.Drawing.Bitmap(100, 100)
        });
        shareDialog.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(shareDialog, "share-twitter");

        var combo = shareDialog.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
        if (combo != null && combo.Items.Cast<object>().Any(i => i.ToString() == "Speedrun.com"))
        {
            combo.SelectedItem = "Speedrun.com";
            Dispatcher.UIThread.RunJobs();
            Capture(shareDialog, "share-speedruncom");
        }
        shareDialog.Close();
        Dispatcher.UIThread.RunJobs();

        // =========================================================
        // 6. SpeedrunComSubmitDialog
        // =========================================================
        var submitDialog = new SpeedrunComSubmitDialog(run.Metadata);
        submitDialog.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(submitDialog, "speedruncom-submit");
        submitDialog.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        string[] outputDirs = [
            Environment.GetEnvironmentVariable("LS_DIALOGS_TEST_OUT") ?? "/tmp/livesplit-dialogs-tests",
            "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ui-dialogs"
        ];
        foreach (string output in outputDirs)
        {
            Directory.CreateDirectory(output);
            int width = Math.Max(100, (int)window.Bounds.Width);
            int height = Math.Max(100, (int)window.Bounds.Height);
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
            bitmap.Render(window);
            string filePath = Path.Combine(output, name + ".png");
            bitmap.Save(filePath);
            Assert.True(new FileInfo(filePath).Length > 1000);
        }
    }
}
