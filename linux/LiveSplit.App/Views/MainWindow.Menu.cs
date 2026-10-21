using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Server;
using LiveSplit.Services;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.Web.SRL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View;

public partial class MainWindow
{
    // The right click menu. Same items, same order as TimerForm.Designer.cs.
    protected ContextMenu RightClickMenu { get; private set; }

    private MenuItem editSplitsMenuItem;
    private MenuItem openSplitsMenuItem;
    private MenuItem saveSplitsMenuItem;
    private MenuItem saveSplitsAsMenuItem;
    private MenuItem closeSplitsMenuItem;
    private Separator toolStripSeparator5;
    private MenuItem controlMenuItem;
    private MenuItem comparisonMenuItem;
    private Separator toolStripSeparator1;
    private MenuItem shareMenuItem;
    private Separator endRaceSection;
    private MenuItem editLayoutMenuItem;
    private MenuItem openLayoutMenuItem;
    private MenuItem saveLayoutMenuItem;
    private MenuItem saveLayoutAsMenuItem;
    private Separator toolStripSeparator2;
    private MenuItem settingsMenuItem;
    private Separator toolStripSeparator4;
    private MenuItem aboutMenuItem;
    private MenuItem exitMenuItem;
    private MenuItem splitMenuItem;
    private MenuItem resetMenuItem;
    private MenuItem undoSplitMenuItem;
    private MenuItem skipSplitMenuItem;
    private MenuItem pauseMenuItem;
    private MenuItem undoPausesMenuItem;
    private MenuItem hotkeysMenuItem;
    private MenuItem tcpServerMenuItem;
    private MenuItem webSocketMenuItem;

    private MenuItem languageMenuItem;
    private MenuItem followSystemLanguageMenuItem;
    private readonly Dictionary<MenuItem, AppLanguage> languageMenuItems = [];

    // English source texts of the static items, re-translated whenever the menu opens.
    private readonly Dictionary<MenuItem, string> menuSourceTexts = [];

    private readonly List<Action> racesToRefresh = [];
    private bool shouldRefreshRaces;
    private readonly List<MenuItem> raceProviderMenuItems = [];
    protected IEnumerable<RaceProviderAPI> RaceProvider { get; set; }

    /// <summary>Avalonia treats '_' in a header as the access key marker; WinForms used '&amp;'.</summary>
    private static string Esc(string text)
    {
        return text?.Replace("_", "__");
    }

    private MenuItem MakeItem(string text, Action click = null, bool enabled = true, bool toggle = false)
    {
        var item = new MenuItem
        {
            Header = Esc(T(text)),
            IsEnabled = enabled,
        };

        if (toggle)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
        }

        menuSourceTexts[item] = text;
        if (click != null)
        {
            item.Click += (_, _) => Safely(click);
        }

        return item;
    }

    private MenuItem MakeDynamicItem(string rawText, Action click = null)
    {
        var item = new MenuItem { Header = Esc(rawText) };
        if (click != null)
        {
            item.Click += (_, _) => Safely(click);
        }

        return item;
    }

    private static void Safely(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private void SetItemText(MenuItem item, string sourceText)
    {
        menuSourceTexts[item] = sourceText;
        item.Header = Esc(T(sourceText));
    }

    private void BuildMenu()
    {
        RightClickMenu = new ContextMenu();

        editSplitsMenuItem = MakeItem("Edit Splits...", EditSplits);
        openSplitsMenuItem = MakeItem("Open Splits");
        saveSplitsMenuItem = MakeItem("Save Splits", () => SaveSplits(true));
        saveSplitsAsMenuItem = MakeItem("Save Splits As...", () => SaveSplitsAs(true));
        closeSplitsMenuItem = MakeItem("Close Splits", CloseSplits);
        toolStripSeparator5 = new Separator();
        controlMenuItem = MakeItem("Control");
        comparisonMenuItem = MakeItem("Compare Against");
        toolStripSeparator1 = new Separator();
        shareMenuItem = MakeItem("Share...", ShowShareDialog);
        endRaceSection = new Separator();
        editLayoutMenuItem = MakeItem("Edit Layout...", EditLayout);
        openLayoutMenuItem = MakeItem("Open Layout");
        saveLayoutMenuItem = MakeItem("Save Layout", () => SaveLayout());
        saveLayoutAsMenuItem = MakeItem("Save Layout As...", () => SaveLayoutAs());
        toolStripSeparator2 = new Separator();
        settingsMenuItem = MakeItem("Settings", ShowSettingsDialog);
        toolStripSeparator4 = new Separator();
        aboutMenuItem = MakeItem("About", OpenAboutBox);
        exitMenuItem = MakeItem("Exit", Exit);

        splitMenuItem = MakeItem("Start", StartOrSplit);
        resetMenuItem = MakeItem("Reset", Reset, enabled: false);
        undoSplitMenuItem = MakeItem("Undo Split", () => Model.UndoSplit(), enabled: false);
        skipSplitMenuItem = MakeItem("Skip Split", () => Model.SkipSplit(), enabled: false);
        pauseMenuItem = MakeItem("Pause", () => Model.Pause(), enabled: false);
        undoPausesMenuItem = MakeItem("Undo All Pauses", () => Model.UndoAllPauses(), enabled: false);
        hotkeysMenuItem = MakeItem("Global Hotkeys", ToggleGlobalHotkeys, toggle: true);
        tcpServerMenuItem = MakeItem("Start TCP Server", TcpServerMenuItem_Click);
        webSocketMenuItem = MakeItem("Start WebSocket Server", WebSocketMenuItem_Click);

        foreach (Control item in new Control[]
        {
            editSplitsMenuItem,
            openSplitsMenuItem,
            saveSplitsMenuItem,
            saveSplitsAsMenuItem,
            closeSplitsMenuItem,
            toolStripSeparator5,
            controlMenuItem,
            comparisonMenuItem,
            toolStripSeparator1,
            shareMenuItem,
            endRaceSection,
            editLayoutMenuItem,
            openLayoutMenuItem,
            saveLayoutMenuItem,
            saveLayoutAsMenuItem,
            toolStripSeparator2,
            settingsMenuItem,
            toolStripSeparator4,
            aboutMenuItem,
            exitMenuItem,
        })
        {
            RightClickMenu.Items.Add(item);
        }

        RightClickMenu.Opening += RightClickMenu_Opening;
    }

    private void RightClickMenu_Opening(object sender, CancelEventArgs e)
    {
        RebuildControlMenu();
        RebuildComparisonsMenu();
        UpdateLanguageMenuChecks();
        ApplyMenuLocalization();
    }

    private void ApplyMenuLocalization()
    {
        if (!CurrentLanguage.RequiresLocalization)
        {
            return;
        }

        foreach (KeyValuePair<MenuItem, string> pair in menuSourceTexts)
        {
            pair.Key.Header = Esc(T(pair.Value));
        }
    }

    // ---------------------------------------------------------------- language

    private void InitializeLanguageMenu()
    {
        languageMenuItem ??= new MenuItem();
        languageMenuItem.Header = Esc(TK(LocalizationKeys.LanguageMenu, "Language"));
        languageMenuItem.Items.Clear();
        languageMenuItems.Clear();

        followSystemLanguageMenuItem = new MenuItem
        {
            Header = Esc(TK(LocalizationKeys.LanguageFollowSystem, "Follow System")),
            ToggleType = MenuItemToggleType.CheckBox,
        };
        followSystemLanguageMenuItem.Click += (_, _) => SwitchUILanguage(AppLanguage.Auto);
        languageMenuItem.Items.Add(followSystemLanguageMenuItem);
        languageMenuItem.Items.Add(new Separator());

        foreach (AppLanguage language in UiTextCatalog.Languages)
        {
            var languageItem = new MenuItem
            {
                Header = Esc(language.DisplayName),
                ToggleType = MenuItemToggleType.CheckBox,
            };
            languageItem.Click += (s, _) =>
            {
                if (s is MenuItem clicked && languageMenuItems.TryGetValue(clicked, out AppLanguage chosen))
                {
                    SwitchUILanguage(chosen);
                }
            };
            languageMenuItem.Items.Add(languageItem);
            languageMenuItems[languageItem] = language;
        }

        if (RightClickMenu.Items.Contains(languageMenuItem))
        {
            UpdateLanguageMenuChecks();
            return;
        }

        int insertIndex = RightClickMenu.Items.IndexOf(toolStripSeparator4);
        if (insertIndex < 0)
        {
            insertIndex = RightClickMenu.Items.IndexOf(aboutMenuItem);
        }

        if (insertIndex < 0)
        {
            insertIndex = RightClickMenu.Items.Count;
        }

        RightClickMenu.Items.Insert(insertIndex, languageMenuItem);
        UpdateLanguageMenuChecks();
    }

    private void UpdateLanguageMenuChecks()
    {
        bool isAuto = LanguageResolver.IsAuto(Settings?.UILanguage);
        if (followSystemLanguageMenuItem != null)
        {
            followSystemLanguageMenuItem.IsChecked = isAuto;
        }

        foreach (KeyValuePair<MenuItem, AppLanguage> pair in languageMenuItems)
        {
            pair.Key.IsChecked = !isAuto && pair.Value.Equals(ConfiguredLanguage);
        }
    }

    private void SwitchUILanguage(AppLanguage language)
    {
        Settings.UILanguage = LanguageResolver.ToSettingValue(language);
        SaveSettingsToDisk();
        UpdateLanguageMenuChecks();

        ShowMessage(
            TK(LocalizationKeys.LanguageRestartRequired, "Language change will take effect after restarting LiveSplit."),
            TK(LocalizationKeys.LanguageMenu, "Language"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    // ---------------------------------------------------------------- state driven item enabling

    private void CurrentState_OnSwitchComparisonNext(object sender, EventArgs e)
    {
        InvokeIfRequired(RefreshComparisonItems);
    }

    private void CurrentState_OnSwitchComparisonPrevious(object sender, EventArgs e)
    {
        InvokeIfRequired(RefreshComparisonItems);
    }

    private void CurrentState_OnUndoSplit(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            pauseMenuItem.IsEnabled = true;
            splitMenuItem.IsEnabled = true;
            if (CurrentState.CurrentSplitIndex == 0)
            {
                undoSplitMenuItem.IsEnabled = false;
            }

            if (CurrentState.CurrentSplitIndex < CurrentState.Run.Count - 1)
            {
                skipSplitMenuItem.IsEnabled = true;
            }
        });
    }

    private void CurrentState_OnSkipSplit(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            if (CurrentState.CurrentSplitIndex >= CurrentState.Run.Count - 1)
            {
                skipSplitMenuItem.IsEnabled = false;
            }

            undoSplitMenuItem.IsEnabled = true;
        });
    }

    private void CurrentState_OnSplit(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            if (CurrentState.CurrentSplitIndex == CurrentState.Run.Count)
            {
                pauseMenuItem.IsEnabled = false;
                splitMenuItem.IsEnabled = false;
            }

            if (CurrentState.CurrentSplitIndex >= CurrentState.Run.Count - 1)
            {
                skipSplitMenuItem.IsEnabled = false;
            }

            undoSplitMenuItem.IsEnabled = true;
        });
    }

    private void CurrentState_OnStart(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            pauseMenuItem.IsEnabled = true;
            resetMenuItem.IsEnabled = true;
            undoSplitMenuItem.IsEnabled = false;
            skipSplitMenuItem.IsEnabled = true;
            SetItemText(splitMenuItem, "Split");
        });
    }

    private void CurrentState_OnReset(object sender, TimerPhase e)
    {
        RegenerateComparisons();

        InvokeIfRequired(() =>
        {
            if (InTimerOnlyMode)
            {
                IRun timerOnlyRun = new Model.RunFactories.StandardRunFactory().Create(ComparisonGeneratorsFactory);
                timerOnlyRun.Offset = CurrentState.Run.Offset;

                SetRun(timerOnlyRun);
            }

            resetMenuItem.IsEnabled = false;
            pauseMenuItem.IsEnabled = false;
            undoPausesMenuItem.IsEnabled = false;
            undoSplitMenuItem.IsEnabled = false;
            skipSplitMenuItem.IsEnabled = false;
            splitMenuItem.IsEnabled = true;
            SetItemText(splitMenuItem, "Start");
        });
    }

    private void CurrentState_OnResume(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            SetItemText(splitMenuItem, "Split");
            pauseMenuItem.IsEnabled = true;
        });
    }

    private void CurrentState_OnPause(object sender, EventArgs e)
    {
        InvokeIfRequired(() =>
        {
            SetItemText(splitMenuItem, "Resume");
            undoPausesMenuItem.IsEnabled = true;
            pauseMenuItem.IsEnabled = false;
        });
    }

    private void CurrentState_OnUndoAllPauses(object sender, EventArgs e)
    {
        InvokeIfRequired(() => undoPausesMenuItem.IsEnabled = false);
    }

    private void ToggleGlobalHotkeys()
    {
        HotkeyProfile hotkeyProfile = Settings.HotkeyProfiles[CurrentState.CurrentHotkeyProfile];
        hotkeyProfile.GlobalHotkeysEnabled = !hotkeyProfile.GlobalHotkeysEnabled;
        hotkeysMenuItem.IsChecked = hotkeyProfile.GlobalHotkeysEnabled;
    }

    // ---------------------------------------------------------------- servers

    private void UpdateServerMenuItems()
    {
        if (Server == null)
        {
            return;
        }

        Settings.ServerState = Server.ServerState;
        switch (Server.ServerState)
        {
            case ServerStateType.Off:
                tcpServerMenuItem.IsEnabled = true;
                SetItemText(tcpServerMenuItem, "Start TCP Server");
                webSocketMenuItem.IsEnabled = true;
                SetItemText(webSocketMenuItem, "Start WebSocket Server");
                break;
            case ServerStateType.TCP:
                tcpServerMenuItem.IsEnabled = true;
                SetItemText(tcpServerMenuItem, "Stop TCP Server");
                webSocketMenuItem.IsEnabled = false;
                SetItemText(webSocketMenuItem, "Start WebSocket Server");
                break;
            case ServerStateType.Websocket:
                tcpServerMenuItem.IsEnabled = false;
                SetItemText(tcpServerMenuItem, "Start TCP Server");
                webSocketMenuItem.IsEnabled = true;
                SetItemText(webSocketMenuItem, "Stop WebSocket Server");
                break;
        }
    }

    private void TcpServerMenuItem_Click()
    {
        if (Server.ServerState == ServerStateType.Off)
        {
            Server.StartTcp();
        }
        else if (Server.ServerState == ServerStateType.TCP)
        {
            Server.StopTcp();
        }

        UpdateServerMenuItems();
    }

    private void WebSocketMenuItem_Click()
    {
        if (Server.ServerState == ServerStateType.Off)
        {
            Server.StartWs();
        }
        else if (Server.ServerState == ServerStateType.Websocket)
        {
            Server.StopWs();
        }

        UpdateServerMenuItems();
    }

    // ---------------------------------------------------------------- races

    private void UpdateRaceProviderIntegration()
    {
        if (ComponentManager.RaceProviderFactories == null)
        {
            return;
        }

        foreach (MenuItem old in raceProviderMenuItems)
        {
            old.Tag = null;
            RightClickMenu.Items.Remove(old);
        }

        raceProviderMenuItems.Clear();
        racesToRefresh.Clear();

        int menuItemIndex = RightClickMenu.Items.IndexOf(shareMenuItem);

        RaceProvider = [.. ComponentManager.RaceProviderFactories.Select(x => x.Value.Create(Model, Settings.RaceProvider.FirstOrDefault(y => y.Name == x.Key)))];
        foreach (RaceProviderAPI raceProvider in RaceProvider.Reverse())
        {
            if (Settings.RaceProvider.Any(x => x.DisplayName == raceProvider.ProviderName && !x.Enabled))
            {
                continue;
            }

            raceProvider.RacesRefreshedCallback = RacesRefreshed;
            MenuItem raceProviderItem = MakeDynamicItem(string.Format(T("{0} Races"), raceProvider.ProviderName));
            raceProviderItem.Tag = raceProvider;
            raceProviderItem.PointerEntered += (_, _) =>
            {
                raceProvider.RefreshRacesListAsync();
                shouldRefreshRaces = true;
            };
            raceProviderItem.PointerExited += (_, _) => shouldRefreshRaces = false;
            raceProviderItem.Items.Add(MakeDynamicItem(T("New Race..."), () => raceProvider.CreateRace?.Invoke(Model)));
            RightClickMenu.Items.Insert(menuItemIndex + 1, raceProviderItem);
            raceProviderMenuItems.Add(raceProviderItem);
            raceProvider.RefreshRacesListAsync();
        }

        RaceProviderAPI srlRaceProvider = RaceProvider.FirstOrDefault(x => x.ProviderName == "SRL");
        if (srlRaceProvider != null)
        {
            srlRaceProvider.JoinRace = (model, raceId) => AppServices.RaceDialog.JoinRace(this, model, raceId);
            srlRaceProvider.CreateRace = model => AppServices.RaceDialog.NewRace(this, model);
        }
    }

    private static string GetShortenedGameAndGoal(string goal)
    {
        if (goal.Length > 65)
        {
            return goal[..60] + "...";
        }

        return goal;
    }

    private void RacesRefreshed(RaceProviderAPI raceProvider)
    {
        // Called from the refresh thread of the provider.
        var model = Model;
        var entries = new List<(string Text, Action Click, Func<string> Live)>();

        List<IRaceInfo> races = [.. raceProvider.GetRaces()];

        var items = new List<Control>();
        foreach (IRaceInfo race in races)
        {
            if (race.State != 1)
            {
                continue;
            }

            string gameAndGoal = GetShortenedGameAndGoal(string.Format("{0} - {1}", race.GameName, race.Goal));
            int entrants = race.NumEntrants;
            string plural = entrants == 1 ? "" : "s";
            string title = string.Format("{0} ({1} Entrant{2})", gameAndGoal, entrants, plural);

            items.Add(MakeDynamicItem(title, () => raceProvider.JoinRace?.Invoke(model, race.Id)));
        }

        if (items.Count > 0)
        {
            items.Add(new Separator());
        }

        var liveItems = new List<Action>();
        foreach (IRaceInfo race in races)
        {
            if (race.State != 3)
            {
                continue;
            }

            string gameAndGoal = GetShortenedGameAndGoal(string.Format("{0} - {1}", race.GameName, race.Goal));
            var startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            startTime = startTime.AddSeconds(race.Starttime);

            MenuItem tsItem = MakeDynamicItem(string.Empty, () =>
            {
                if (!race.IsParticipant(raceProvider.Username))
                {
                    Settings.RaceViewer.ShowRace(race);
                }
                else
                {
                    raceProvider.JoinRace?.Invoke(model, race.Id);
                }
            });

            void Refresh()
            {
                try
                {
                    UpdateRaceTitle(tsItem, race, startTime, gameAndGoal);
                }
                catch
                {
                }
            }

            Refresh();
            liveItems.Add(Refresh);
            items.Add(tsItem);
        }

        if (items.Count > 0 && items[^1] is not Separator)
        {
            items.Add(new Separator());
        }

        items.Add(MakeDynamicItem(T("New Race..."), () => raceProvider.CreateRace?.Invoke(model)));

        Dispatcher_Post(() =>
        {
            MenuItem racingMenuItem = raceProviderMenuItems.FirstOrDefault(x => x.Tag == raceProvider);
            if (racingMenuItem == null)
            {
                return;
            }

            racingMenuItem.Items.Clear();
            foreach (Control item in items)
            {
                racingMenuItem.Items.Add(item);
            }

            racesToRefresh.Clear();
            racesToRefresh.AddRange(liveItems);
        });
    }

    private static void Dispatcher_Post(Action action)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }

    private void UpdateRaceTitle(MenuItem item, IRaceInfo race, DateTime startTime, string gameAndGoal)
    {
        TimeSpan timeSpan = TimeStamp.CurrentDateTime - startTime;
        if (timeSpan < TimeSpan.Zero)
        {
            timeSpan = TimeSpan.Zero;
        }

        string time = new RegularTimeFormatter().Format(timeSpan);
        string title = string.Format(T("[{0}] {1} ({2}/{3} Finished)"), time, gameAndGoal, race.Finishes, race.NumEntrants - race.Forfeits);
        item.Header = Esc(title);
    }

    // ---------------------------------------------------------------- recent files

    private void UpdateRecentSplits()
    {
        openSplitsMenuItem.Items.Clear();

        List<RecentMenuNode> nodes = RecentMenuModel.BuildSplits(Settings.RecentSplits);
        AddRecentNodes(openSplitsMenuItem, nodes, path => OpenRunFromFile(path));

        if (openSplitsMenuItem.Items.Count > 0)
        {
            openSplitsMenuItem.Items.Add(new Separator());
        }

        openSplitsMenuItem.Items.Add(MakeDynamicItem(T("From File..."), OpenSplits));
        openSplitsMenuItem.Items.Add(MakeDynamicItem(T("From URL..."), OpenSplitsFromUrl));
        openSplitsMenuItem.Items.Add(new Separator());
        openSplitsMenuItem.Items.Add(MakeDynamicItem(T("Edit History"), EditSplitHistory));
    }

    private void UpdateRecentLayouts()
    {
        openLayoutMenuItem.Items.Clear();

        List<RecentMenuNode> nodes = RecentMenuModel.BuildLayouts(Settings.RecentLayouts);
        AddRecentNodes(openLayoutMenuItem, nodes, path => OpenLayoutFromFile(path));

        if (openLayoutMenuItem.Items.Count > 0)
        {
            openLayoutMenuItem.Items.Add(new Separator());
        }

        openLayoutMenuItem.Items.Add(MakeDynamicItem(T("From File..."), OpenLayout));
        openLayoutMenuItem.Items.Add(MakeDynamicItem(T("From URL..."), OpenLayoutFromUrl));
        openLayoutMenuItem.Items.Add(MakeDynamicItem(T("Default"), LoadDefaultLayout));
        openLayoutMenuItem.Items.Add(new Separator());
        openLayoutMenuItem.Items.Add(MakeDynamicItem(T("Edit History"), EditLayoutHistory));
    }

    private void AddRecentNodes(MenuItem parent, IEnumerable<RecentMenuNode> nodes, Action<string> open)
    {
        foreach (RecentMenuNode node in nodes)
        {
            string path = node.Path;
            MenuItem item = MakeDynamicItem(node.Text, path != null ? () => open(path) : null);
            if (path == null)
            {
                AddRecentNodes(item, node.Children, open);
            }

            parent.Items.Add(item);
        }
    }

    // ---------------------------------------------------------------- comparisons

    private void RefreshComparisonItems()
    {
        int numSeparators = 0;
        foreach (Control item in comparisonMenuItem.Items.OfType<Control>().Reverse())
        {
            if (item is Separator)
            {
                numSeparators++;
            }

            if (item is MenuItem toolItem)
            {
                toolItem.IsChecked = numSeparators == 0
                    ? toolItem.Tag is TimingMethod method && method == CurrentState.CurrentTimingMethod
                    : toolItem.Tag is string name && name == CurrentState.CurrentComparison;
            }
        }
    }

    private void RebuildComparisonsMenu()
    {
        comparisonMenuItem.Items.Clear();

        foreach (string customComparison in CurrentState.Run.CustomComparisons)
        {
            AddActionToComparisonsMenu(customComparison);
        }

        if (CurrentState.Run.ComparisonGenerators.Count > 0)
        {
            comparisonMenuItem.Items.Add(new Separator());
        }

        bool raceSeparatorAdded = false;
        foreach (IComparisonGenerator generator in CurrentState.Run.ComparisonGenerators)
        {
            if (!raceSeparatorAdded && generator is SRLComparisonGenerator)
            {
                comparisonMenuItem.Items.Add(new Separator());
                raceSeparatorAdded = true;
            }

            AddActionToComparisonsMenu(generator.Name);
        }

        comparisonMenuItem.Items.Add(new Separator());

        MenuItem realTimeMenuItem = MakeDynamicItem(T("Real Time"), () =>
        {
            CurrentState.CurrentTimingMethod = TimingMethod.RealTime;
            RefreshComparisonItems();
        });
        realTimeMenuItem.ToggleType = MenuItemToggleType.CheckBox;
        realTimeMenuItem.Tag = TimingMethod.RealTime;
        comparisonMenuItem.Items.Add(realTimeMenuItem);

        MenuItem gameTimeMenuItem = MakeDynamicItem(T("Game Time"), () =>
        {
            CurrentState.CurrentTimingMethod = TimingMethod.GameTime;
            RefreshComparisonItems();
        });
        gameTimeMenuItem.ToggleType = MenuItemToggleType.CheckBox;
        gameTimeMenuItem.Tag = TimingMethod.GameTime;
        comparisonMenuItem.Items.Add(gameTimeMenuItem);

        RefreshComparisonItems();
    }

    private void AddActionToComparisonsMenu(string name)
    {
        MenuItem menuItem = MakeDynamicItem(name, () =>
        {
            SwitchComparison(name);
            RefreshComparisonItems();
        });
        menuItem.ToggleType = MenuItemToggleType.CheckBox;
        menuItem.Tag = name;
        comparisonMenuItem.Items.Add(menuItem);
    }

    private void RegenerateComparisons()
    {
        if (CurrentState?.Run != null)
        {
            foreach (IComparisonGenerator generator in CurrentState.Run.ComparisonGenerators)
            {
                generator.Generate(CurrentState.Settings);
            }
        }
    }

    private void SwitchComparisonGenerators()
    {
        IEnumerable<IComparisonGenerator> allGenerators = new StandardComparisonGeneratorsFactory().GetAllGenerators(CurrentState.Run);
        foreach (IComparisonGenerator generator in allGenerators)
        {
            IComparisonGenerator generatorInRun = CurrentState.Run.ComparisonGenerators.FirstOrDefault(x => x.Name == generator.Name);
            if (generatorInRun != null)
            {
                CurrentState.Run.ComparisonGenerators.Remove(generatorInRun);
            }

            if (Settings.ComparisonGeneratorStates[generator.Name])
            {
                CurrentState.Run.ComparisonGenerators.Add(generator);
            }
        }

        SwitchComparison(CurrentState.CurrentComparison);
        RegenerateComparisons();
    }

    private void SwitchComparison(string name)
    {
        if (!CurrentState.Run.Comparisons.Contains(name))
        {
            name = Run.PersonalBestComparisonName;
        }

        CurrentState.CurrentComparison = name;
    }

    // ---------------------------------------------------------------- control submenu

    private void AddActionToControlMenu(string name, Action action)
    {
        controlMenuItem.Items.Add(MakeDynamicItem(name, action));
    }

    private void RebuildControlMenu()
    {
        controlMenuItem.Items.Clear();
        foreach (MenuItem item in new[] { splitMenuItem, resetMenuItem, undoSplitMenuItem, skipSplitMenuItem, pauseMenuItem, undoPausesMenuItem })
        {
            controlMenuItem.Items.Add(item);
        }

        controlMenuItem.Items.Add(new Separator());
        controlMenuItem.Items.Add(hotkeysMenuItem);

        hotkeysMenuItem.IsChecked = Settings.HotkeyProfiles[CurrentState.CurrentHotkeyProfile].GlobalHotkeysEnabled;

        controlMenuItem.Items.Add(new Separator());
        controlMenuItem.Items.Add(tcpServerMenuItem);
        controlMenuItem.Items.Add(webSocketMenuItem);

        IEnumerable<UI.Components.IComponent> components = Layout.Components;
        if (CurrentState.Run.IsAutoSplitterActive())
        {
            components = components.Append(CurrentState.Run.AutoSplitter.Component);
        }

        IEnumerable<IDictionary<string, Action>> componentControls =
            components
            .Select(x => x.ContextMenuControls)
            .Where(x => x is { Count: > 0 });

        foreach (IDictionary<string, Action> componentControlSection in componentControls)
        {
            controlMenuItem.Items.Add(new Separator());

            foreach (KeyValuePair<string, Action> componentControl in componentControlSection)
            {
                AddActionToControlMenu(componentControl.Key, componentControl.Value);
            }
        }
    }
}
