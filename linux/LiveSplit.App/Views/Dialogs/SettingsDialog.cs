using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.View.RunEditor;
using LiveSplit.Web;
using LiveSplit.Web.Share;
using LiveSplit.Web.SRL;
using LiveSplit.Web.SRL.RaceViewers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class SettingsDialog : DialogWindow
{
    private readonly SettingsDialogRequest request;
    public string SelectedHotkeyProfile { get; private set; }

    private readonly ComboBox cmbProfiles;
    private readonly Button btnNewProfile;
    private readonly Button btnRenameProfile;
    private readonly Button btnRemoveProfile;

    // Hotkey captures
    private readonly HotkeyCapture capSplit;
    private readonly HotkeyCapture capReset;
    private readonly HotkeyCapture capSkip;
    private readonly HotkeyCapture capUndo;
    private readonly HotkeyCapture capPause;
    private readonly HotkeyCapture capToggle;
    private readonly HotkeyCapture capPrevComp;
    private readonly HotkeyCapture capNextComp;

    // Hotkey checkboxes & options
    private readonly CheckBox chkGlobalHotkeys;
    private readonly CheckBox chkDeactivateForOtherPrograms;
    private readonly CheckBox chkDoubleTap;
    private readonly NumericUpDown numDelay;
    private readonly CheckBox chkAllowGamepads;

    // Comparisons & Timing
    private readonly CheckBox chkSimpleSob;
    private readonly CheckBox chkWarnOnReset;
    private readonly NumericUpDown numRefreshRate;
    private readonly ComboBox cmbServerStartup;
    private readonly NumericUpDown numServerPort;

    // Racing & Accounts
    private readonly ComboBox cmbRaceViewer;
    private readonly Button btnManageRaceServices;
    private readonly Button btnLogOut;
    private readonly TextBlock lblLogOutStatus;

    private bool updatingProfile;

    private HotkeyProfile CurrentProfile => request.Settings.HotkeyProfiles[SelectedHotkeyProfile];

    public SettingsDialog(SettingsDialogRequest request)
        : base(SettingsPage.T("Settings"))
    {
        this.request = request;

        if (request.Settings.HotkeyProfiles == null || request.Settings.HotkeyProfiles.Count == 0)
        {
            request.Settings.HotkeyProfiles ??= new Dictionary<string, HotkeyProfile>();
            if (!request.Settings.HotkeyProfiles.ContainsKey("Default"))
            {
                request.Settings.HotkeyProfiles["Default"] = new HotkeyProfile();
            }
        }
        request.Settings.RaceProvider ??= [];
        request.Settings.ComparisonGeneratorStates ??= new Dictionary<string, bool>();

        SelectedHotkeyProfile = request.CurrentHotkeyProfile ?? request.Settings.HotkeyProfiles.Keys.FirstOrDefault() ?? "Default";
        if (!request.Settings.HotkeyProfiles.ContainsKey(SelectedHotkeyProfile))
        {
            SelectedHotkeyProfile = request.Settings.HotkeyProfiles.Keys.First();
        }

        Width = 720;
        Height = 680;
        MinWidth = 600;
        MinHeight = 480;
        CanResize = true;

        var tabControl = new TabControl();

        // ========================================================
        // TAB 1: HOTKEYS
        // ========================================================
        var hotkeysStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        // Profile toolbar
        var profileRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        profileRow.Children.Add(new TextBlock { Text = SettingsPage.T("Hotkey Profile:"), VerticalAlignment = VerticalAlignment.Center });

        cmbProfiles = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        cmbProfiles.ItemsSource = request.Settings.HotkeyProfiles.Keys.ToArray();
        cmbProfiles.SelectedItem = SelectedHotkeyProfile;
        Grid.SetColumn(cmbProfiles, 1);
        profileRow.Children.Add(cmbProfiles);

        btnNewProfile = new Button { Content = SettingsPage.T("New...") };
        Grid.SetColumn(btnNewProfile, 2);
        profileRow.Children.Add(btnNewProfile);

        btnRenameProfile = new Button { Content = SettingsPage.T("Rename...") };
        Grid.SetColumn(btnRenameProfile, 3);
        profileRow.Children.Add(btnRenameProfile);

        btnRemoveProfile = new Button { Content = SettingsPage.T("Remove") };
        Grid.SetColumn(btnRemoveProfile, 4);
        profileRow.Children.Add(btnRemoveProfile);

        hotkeysStack.Children.Add(profileRow);

        // Backend notice / evdev input message banner
        var hook = request.Hook;
        bool keyboardUnavailable = hook != null && !hook.IsKeyboardAvailable;
        string statusMsg = hook?.KeyboardStatusMessage;

        if (keyboardUnavailable || !string.IsNullOrEmpty(statusMsg))
        {
            var banner = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#382810")),
                BorderBrush = new SolidColorBrush(Color.Parse("#D99B26")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4)
            };

            var bannerText = new StackPanel { Spacing = 4 };
            bannerText.Children.Add(new TextBlock
            {
                Text = SettingsPage.T("Global Hotkey Notice"),
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#F7CA68"))
            });

            string notice = statusMsg ?? SettingsPage.T("Global keyboard hook is not active.");
            if (notice.Contains("input group") || notice.Contains("EACCES") || notice.Contains("/dev/input"))
            {
                notice = SettingsPage.T("Global hotkeys need read access to /dev/input:\nAdd your user to the input group:\n  sudo usermod -aG input $USER\nthen log out and log in again.");
            }

            bannerText.Children.Add(new TextBlock
            {
                Text = notice,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White
            });

            banner.Child = bannerText;
            hotkeysStack.Children.Add(banner);
        }

        // Hotkey Captures Grid
        var capturesCard = new StackPanel { Spacing = 8 };
        capturesCard.Children.Add(new TextBlock { Text = SettingsPage.T("Hotkeys"), FontWeight = FontWeight.SemiBold });

        var capturesGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*,24,160,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 8
        };

        capSplit = new HotkeyCapture();
        capReset = new HotkeyCapture();
        capSkip = new HotkeyCapture();
        capUndo = new HotkeyCapture();
        capPause = new HotkeyCapture();
        capToggle = new HotkeyCapture();
        capPrevComp = new HotkeyCapture();
        capNextComp = new HotkeyCapture();

        AddCaptureRow(capturesGrid, 0, 0, "Start / Split:", capSplit);
        AddCaptureRow(capturesGrid, 0, 3, "Reset:", capReset);
        AddCaptureRow(capturesGrid, 1, 0, "Skip Split:", capSkip);
        AddCaptureRow(capturesGrid, 1, 3, "Undo Split:", capUndo);
        AddCaptureRow(capturesGrid, 2, 0, "Pause:", capPause);
        AddCaptureRow(capturesGrid, 2, 3, "Toggle Global Hotkeys:", capToggle);
        AddCaptureRow(capturesGrid, 3, 0, "Previous Comparison:", capPrevComp);
        AddCaptureRow(capturesGrid, 3, 3, "Next Comparison:", capNextComp);

        capturesCard.Children.Add(capturesGrid);
        hotkeysStack.Children.Add(Card(capturesCard));

        // Hotkey Options
        var optionsCard = new StackPanel { Spacing = 8 };
        optionsCard.Children.Add(new TextBlock { Text = SettingsPage.T("Hotkey Options"), FontWeight = FontWeight.SemiBold });

        chkGlobalHotkeys = new CheckBox { Content = SettingsPage.T("Global Hotkeys") };
        chkDeactivateForOtherPrograms = new CheckBox { Content = SettingsPage.T("Deactivate For Other Programs") };
        chkDoubleTap = new CheckBox { Content = SettingsPage.T("Double-Tap Prevention") };
        chkAllowGamepads = new CheckBox { Content = SettingsPage.T("Allow Gamepads as Hotkeys") };

        var delayRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,120,Auto"), ColumnSpacing = 8 };
        delayRow.Children.Add(new TextBlock { Text = SettingsPage.T("Hotkey Delay (seconds):"), VerticalAlignment = VerticalAlignment.Center });
        numDelay = new NumericUpDown { Minimum = 0, Maximum = 10, Increment = 0.1m, FormatString = "0.0#" };
        Grid.SetColumn(numDelay, 1);
        delayRow.Children.Add(numDelay);

        var chkWrap = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 16
        };
        chkWrap.Children.Add(chkGlobalHotkeys);
        Grid.SetColumn(chkDeactivateForOtherPrograms, 1);
        chkWrap.Children.Add(chkDeactivateForOtherPrograms);
        Grid.SetRow(chkDoubleTap, 1);
        chkWrap.Children.Add(chkDoubleTap);
        Grid.SetRow(chkAllowGamepads, 1);
        Grid.SetColumn(chkAllowGamepads, 1);
        chkWrap.Children.Add(chkAllowGamepads);

        optionsCard.Children.Add(chkWrap);
        optionsCard.Children.Add(delayRow);
        hotkeysStack.Children.Add(Card(optionsCard));

        var tabHotkeys = new TabItem
        {
            Header = SettingsPage.T("Hotkeys"),
            Content = new ScrollViewer { Content = hotkeysStack }
        };
        tabControl.Items.Add(tabHotkeys);

        // ========================================================
        // TAB 2: COMPARISONS & TIMING
        // ========================================================
        var compStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var compCard = new StackPanel { Spacing = 8 };
        compCard.Children.Add(new TextBlock { Text = SettingsPage.T("Comparisons"), FontWeight = FontWeight.SemiBold });

        var btnChooseComparisons = new Button
        {
            Content = SettingsPage.T("Choose Comparisons..."),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        btnChooseComparisons.Click += async (_, _) =>
        {
            var states = new Dictionary<string, bool>(request.Settings.ComparisonGeneratorStates);
            int historySize = request.Settings.HcpHistorySize;
            int nBest = request.Settings.HcpNBestRuns;

            var chooseWin = new ChooseComparisonsWindow(states, historySize, nBest);
            if (await chooseWin.ShowModalAsync(this) == DialogResult.OK)
            {
                request.Settings.ComparisonGeneratorStates = chooseWin.States;
                request.Settings.HcpHistorySize = chooseWin.HistorySize;
                request.Settings.HcpNBestRuns = chooseWin.NBest;
            }
        };
        compCard.Children.Add(btnChooseComparisons);

        chkSimpleSob = new CheckBox
        {
            Content = SettingsPage.T("Simple Sum of Best Calculation"),
            IsChecked = request.Settings.SimpleSumOfBest
        };
        chkSimpleSob.IsCheckedChanged += (_, _) =>
        {
            request.Settings.SimpleSumOfBest = chkSimpleSob.IsChecked == true;
            request.Host?.NotifySumOfBestModeChanged();
        };
        compCard.Children.Add(chkSimpleSob);

        chkWarnOnReset = new CheckBox
        {
            Content = SettingsPage.T("Warn on Reset"),
            IsChecked = request.Settings.WarnOnReset
        };
        chkWarnOnReset.IsCheckedChanged += (_, _) =>
        {
            request.Settings.WarnOnReset = chkWarnOnReset.IsChecked == true;
        };
        compCard.Children.Add(chkWarnOnReset);

        compStack.Children.Add(Card(compCard));

        var timingCard = new StackPanel { Spacing = 8 };
        timingCard.Children.Add(new TextBlock { Text = SettingsPage.T("Timing & Server"), FontWeight = FontWeight.SemiBold });

        var refreshRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,120"), ColumnSpacing = 8 };
        refreshRow.Children.Add(new TextBlock { Text = SettingsPage.T("Refresh Rate (FPS):"), VerticalAlignment = VerticalAlignment.Center });
        numRefreshRate = new NumericUpDown { Minimum = 20, Maximum = 300, Increment = 1, Value = request.Settings.RefreshRate, FormatString = "0" };
        numRefreshRate.ValueChanged += (_, _) =>
        {
            if (numRefreshRate.Value.HasValue)
                request.Settings.RefreshRate = (int)numRefreshRate.Value.Value;
        };
        Grid.SetColumn(numRefreshRate, 1);
        refreshRow.Children.Add(numRefreshRate);
        timingCard.Children.Add(refreshRow);

        var serverRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,140,16,80,100"), ColumnSpacing = 8 };
        serverRow.Children.Add(new TextBlock { Text = SettingsPage.T("Server Startup:"), VerticalAlignment = VerticalAlignment.Center });
        cmbServerStartup = new ComboBox
        {
            ItemsSource = new[] { "Manual", "Automatic", "Disabled" },
            SelectedIndex = (int)request.Settings.ServerStartup
        };
        cmbServerStartup.SelectionChanged += (_, _) =>
        {
            if (cmbServerStartup.SelectedIndex >= 0)
                request.Settings.ServerStartup = (ServerStartupType)cmbServerStartup.SelectedIndex;
        };
        Grid.SetColumn(cmbServerStartup, 1);
        serverRow.Children.Add(cmbServerStartup);

        var lblPort = new TextBlock { Text = SettingsPage.T("Port:"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lblPort, 3);
        serverRow.Children.Add(lblPort);

        numServerPort = new NumericUpDown { Minimum = 1024, Maximum = 65535, Value = request.Settings.ServerPort, FormatString = "0" };
        numServerPort.ValueChanged += (_, _) =>
        {
            if (numServerPort.Value.HasValue)
                request.Settings.ServerPort = (int)numServerPort.Value.Value;
        };
        Grid.SetColumn(numServerPort, 4);
        serverRow.Children.Add(numServerPort);

        timingCard.Children.Add(serverRow);
        compStack.Children.Add(Card(timingCard));

        var tabComparisons = new TabItem
        {
            Header = SettingsPage.T("Comparisons & Timing"),
            Content = new ScrollViewer { Content = compStack }
        };
        tabControl.Items.Add(tabComparisons);

        // ========================================================
        // TAB 3: RACING & ACCOUNTS
        // ========================================================
        var raceStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        var raceCard = new StackPanel { Spacing = 8 };
        raceCard.Children.Add(new TextBlock { Text = SettingsPage.T("Racing Services"), FontWeight = FontWeight.SemiBold });

        var viewerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,200"), ColumnSpacing = 8 };
        viewerRow.Children.Add(new TextBlock { Text = SettingsPage.T("Race Viewer:"), VerticalAlignment = VerticalAlignment.Center });
        cmbRaceViewer = new ComboBox
        {
            ItemsSource = new[] { "SpeedRunsLive", "Kadgar", "MultiTwitch", "Speedrun.tv" },
            SelectedItem = request.Settings.RaceViewer?.Name ?? "SpeedRunsLive"
        };
        cmbRaceViewer.SelectionChanged += (_, _) =>
        {
            if (cmbRaceViewer.SelectedItem != null)
                request.Settings.RaceViewer = LiveSplit.Web.SRL.RaceViewer.FromName(cmbRaceViewer.SelectedItem.ToString());
        };
        Grid.SetColumn(cmbRaceViewer, 1);
        viewerRow.Children.Add(cmbRaceViewer);
        raceCard.Children.Add(viewerRow);

        btnManageRaceServices = new Button
        {
            Content = SettingsPage.T("Manage Racing Services..."),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        btnManageRaceServices.Click += async (_, _) =>
        {
            var cloned = request.Settings.RaceProvider.Select(x => (RaceProviderSettings)x.Clone()).ToList();
            var dlg = new RaceProviderManagingDialog(cloned);
            if (await dlg.ShowModalAsync(this) == DialogResult.OK)
            {
                request.Settings.RaceProvider = cloned;
            }
        };
        raceCard.Children.Add(btnManageRaceServices);
        raceStack.Children.Add(Card(raceCard));

        var accountsCard = new StackPanel { Spacing = 8 };
        accountsCard.Children.Add(new TextBlock { Text = SettingsPage.T("Accounts"), FontWeight = FontWeight.SemiBold });

        btnLogOut = new Button
        {
            Content = SettingsPage.T("Log Out of All Web Services"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        lblLogOutStatus = new TextBlock
        {
            Text = WebCredentials.AnyCredentialsExist() ? SettingsPage.T("Saved credentials found.") : SettingsPage.T("No saved credentials."),
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center
        };
        var logOutRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        logOutRow.Children.Add(btnLogOut);
        logOutRow.Children.Add(lblLogOutStatus);
        accountsCard.Children.Add(logOutRow);

        btnLogOut.Click += (_, _) =>
        {
            SpeedrunCom.ClearAccessToken();
            Twitch.Instance?.ClearAccessToken();
            WebCredentials.DeleteAllCredentials();
            lblLogOutStatus.Text = SettingsPage.T("Logged out of all services.");
        };

        raceStack.Children.Add(Card(accountsCard));

        var tabRacing = new TabItem
        {
            Header = SettingsPage.T("Racing & Accounts"),
            Content = new ScrollViewer { Content = raceStack }
        };
        tabControl.Items.Add(tabRacing);

        SetBody(tabControl);

        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);

        // Bind profile events
        WireProfileEvents();

        // Load initial profile data
        LoadProfileData();
    }

    private static (TextBlock lbl, HotkeyCapture cap) AddCaptureRow(Grid grid, int row, int col, string label, HotkeyCapture cap)
    {
        var lbl = new TextBlock { Text = SettingsPage.T(label), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, col);
        Grid.SetRow(cap, row);
        Grid.SetColumn(cap, col + 1);
        grid.Children.Add(lbl);
        grid.Children.Add(cap);
        return (lbl, cap);
    }

    private static Border Card(Control child)
    {
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = child
        };
    }

    private void WireProfileEvents()
    {
        cmbProfiles.SelectionChanged += (_, _) =>
        {
            if (updatingProfile || cmbProfiles.SelectedItem == null) return;
            SelectedHotkeyProfile = cmbProfiles.SelectedItem.ToString();
            LoadProfileData();
        };

        btnNewProfile.Click += async (_, _) =>
        {
            var prompt = new PromptWindow(SettingsPage.T("New Hotkey Profile"), SettingsPage.T("Hotkey Profile Name:"), "");
            if (await prompt.ShowModalAsync(this) == DialogResult.OK)
            {
                string name = prompt.Value1?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    if (!request.Settings.HotkeyProfiles.ContainsKey(name))
                    {
                        var newProfile = (HotkeyProfile)CurrentProfile.Clone();
                        request.Settings.HotkeyProfiles.Add(name, newProfile);
                        SelectedHotkeyProfile = name;
                        UpdateProfileCombo();
                    }
                    else
                    {
                        await new MessageBoxWindow(
                            SettingsPage.T("A Hotkey Profile with this name already exists."),
                            SettingsPage.T("Hotkey Profile Already Exists"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        ).ShowModalAsync(this);
                    }
                }
            }
        };

        btnRenameProfile.Click += async (_, _) =>
        {
            var prompt = new PromptWindow(SettingsPage.T("Rename Hotkey Profile"), SettingsPage.T("Hotkey Profile Name:"), SelectedHotkeyProfile);
            if (await prompt.ShowModalAsync(this) == DialogResult.OK)
            {
                string newName = prompt.Value1?.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != SelectedHotkeyProfile)
                {
                    if (!request.Settings.HotkeyProfiles.ContainsKey(newName))
                    {
                        for (int i = 0; i < request.Settings.RecentSplits.Count; i++)
                        {
                            var file = request.Settings.RecentSplits[i];
                            if (file.LastHotkeyProfile == SelectedHotkeyProfile)
                            {
                                var newFile = new RecentSplitsFile(file.Path, file.LastTimingMethod, newName, file.GameName, file.CategoryName);
                                request.Settings.RecentSplits.RemoveAt(i);
                                request.Settings.RecentSplits.Insert(i, newFile);
                            }
                        }

                        var profile = CurrentProfile;
                        request.Settings.HotkeyProfiles.Remove(SelectedHotkeyProfile);
                        request.Settings.HotkeyProfiles[newName] = profile;
                        SelectedHotkeyProfile = newName;
                        UpdateProfileCombo();
                    }
                    else
                    {
                        await new MessageBoxWindow(
                            SettingsPage.T("A Hotkey Profile with this name already exists."),
                            SettingsPage.T("Hotkey Profile Already Exists"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        ).ShowModalAsync(this);
                    }
                }
            }
        };

        btnRemoveProfile.Click += (_, _) =>
        {
            if (request.Settings.HotkeyProfiles.Count > 1)
            {
                request.Settings.HotkeyProfiles.Remove(SelectedHotkeyProfile);
                SelectedHotkeyProfile = request.Settings.HotkeyProfiles.Keys.Last();
                UpdateProfileCombo();
            }
        };

        // Wire capture changes
        capSplit.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.SplitKey = capSplit.Hotkey; };
        capReset.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.ResetKey = capReset.Hotkey; };
        capSkip.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.SkipKey = capSkip.Hotkey; };
        capUndo.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.UndoKey = capUndo.Hotkey; };
        capPause.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.PauseKey = capPause.Hotkey; };
        capToggle.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.ToggleGlobalHotkeys = capToggle.Hotkey; };
        capPrevComp.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.SwitchComparisonPrevious = capPrevComp.Hotkey; };
        capNextComp.PropertyChanged += (_, e) => { if (!updatingProfile && e.Property == HotkeyCapture.HotkeyProperty) CurrentProfile.SwitchComparisonNext = capNextComp.Hotkey; };

        // Wire checkbox changes
        chkGlobalHotkeys.IsCheckedChanged += (_, _) =>
        {
            if (!updatingProfile)
            {
                bool enabled = chkGlobalHotkeys.IsChecked == true;
                CurrentProfile.GlobalHotkeysEnabled = enabled;
                chkDeactivateForOtherPrograms.IsEnabled = enabled;
                if (!enabled) chkDeactivateForOtherPrograms.IsChecked = false;
            }
        };

        chkDeactivateForOtherPrograms.IsCheckedChanged += (_, _) =>
        {
            if (!updatingProfile) CurrentProfile.DeactivateHotkeysForOtherPrograms = chkDeactivateForOtherPrograms.IsChecked == true;
        };

        chkDoubleTap.IsCheckedChanged += (_, _) =>
        {
            if (!updatingProfile) CurrentProfile.DoubleTapPrevention = chkDoubleTap.IsChecked == true;
        };

        chkAllowGamepads.IsCheckedChanged += (_, _) =>
        {
            if (!updatingProfile) CurrentProfile.AllowGamepadsAsHotkeys = chkAllowGamepads.IsChecked == true;
        };

        numDelay.ValueChanged += (_, _) =>
        {
            if (!updatingProfile && numDelay.Value.HasValue)
                CurrentProfile.HotkeyDelay = (float)numDelay.Value.Value;
        };
    }

    private void UpdateProfileCombo()
    {
        updatingProfile = true;
        cmbProfiles.ItemsSource = request.Settings.HotkeyProfiles.Keys.ToArray();
        cmbProfiles.SelectedItem = SelectedHotkeyProfile;
        btnRemoveProfile.IsEnabled = request.Settings.HotkeyProfiles.Count > 1;
        updatingProfile = false;
        LoadProfileData();
    }

    private void LoadProfileData()
    {
        updatingProfile = true;
        btnRemoveProfile.IsEnabled = request.Settings.HotkeyProfiles.Count > 1;

        var profile = CurrentProfile;
        capSplit.Hotkey = profile.SplitKey;
        capReset.Hotkey = profile.ResetKey;
        capSkip.Hotkey = profile.SkipKey;
        capUndo.Hotkey = profile.UndoKey;
        capPause.Hotkey = profile.PauseKey;
        capToggle.Hotkey = profile.ToggleGlobalHotkeys;
        capPrevComp.Hotkey = profile.SwitchComparisonPrevious;
        capNextComp.Hotkey = profile.SwitchComparisonNext;

        chkGlobalHotkeys.IsChecked = profile.GlobalHotkeysEnabled;
        chkDeactivateForOtherPrograms.IsEnabled = profile.GlobalHotkeysEnabled;
        chkDeactivateForOtherPrograms.IsChecked = profile.GlobalHotkeysEnabled && profile.DeactivateHotkeysForOtherPrograms;
        chkDoubleTap.IsChecked = profile.DoubleTapPrevention;
        chkAllowGamepads.IsChecked = profile.AllowGamepadsAsHotkeys;
        numDelay.Value = (decimal)profile.HotkeyDelay;

        updatingProfile = false;
    }
}
