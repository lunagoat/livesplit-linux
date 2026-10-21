using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using LiveSplit.Controls;
using LiveSplit.Hosting;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Server;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;
using LiveSplit.Web.Share;
using LiveSplit.Web.SRL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;
using Region = System.Drawing.Region;

namespace LiveSplit.View;

/// <summary>
/// The LiveSplit timer window: a port of the WinForms TimerForm. This part holds the state, startup and the refresh loop;
/// see MainWindow.*.cs for menus, files, sizing, painting, input and the editor entry points.
/// </summary>
public partial class MainWindow : Window, IFormHost
{
    public const string SettingsPath = "settings.cfg";

    private static WindowIcon appIcon;

    public static WindowIcon AppIcon
    {
        get
        {
            if (appIcon == null)
            {
                try
                {
                    appIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://LiveSplit/Assets/Icon.png")));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            return appIcon;
        }
    }

    protected IComparisonGeneratorsFactory ComparisonGeneratorsFactory { get; set; }
    protected ComponentRenderer ComponentRenderer { get; set; }
    public LiveSplitState CurrentState { get; set; }
    public ITimerModel Model { get; protected set; }
    public CompositeHook Hook { get; protected set; }
    protected bool IsInDialogMode { get; set; }
    protected bool ResetMessageShown { get; set; }

    public new ILayout Layout
    {
        get => CurrentState.Layout;
        set => CurrentState.Layout = value;
    }

    protected float OldSize { get; set; }
    protected int RefreshesRemaining { get; set; }
    public ISettings Settings { get; set; }
    protected Invalidator Invalidator { get; set; }
    protected bool InTimerOnlyMode { get; set; }
    public CommandServer Server { get; set; }
    protected GraphicsCache GlobalCache { get; set; }
    protected StandardFormatsRunFactory RunFactory { get; set; }
    protected IRunSaver RunSaver { get; set; }
    protected ILayoutSaver LayoutSaver { get; set; }
    protected SettingsStore SettingsStore { get; set; }
    protected bool InvalidationRequired { get; set; }

    public CommandLineOptions Options { get; }

    public LayoutView View { get; }

    private readonly Canvas controlHost;
    private bool dontRedraw;
    private bool closing;
    private bool shown;

    public MainWindow(CommandLineOptions options)
    {
        Options = options ?? new CommandLineOptions();
        Title = "LiveSplit";
        Icon = AppIcon;
        SystemDecorations = SystemDecorations.None;
        CanResize = true;
        ShowInTaskbar = true;
        Background = Avalonia.Media.Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None];
        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeToContent = SizeToContent.Manual;
        Width = 150;
        Height = 150;
        formWidth = 150;
        formHeight = 150;

        View = new LayoutView { Painter = PaintView };
        controlHost = new Canvas { IsHitTestVisible = false };
        var root = new Panel { Background = Avalonia.Media.Brushes.Transparent };
        root.Children.Add(View);
        root.Children.Add(controlHost);
        Content = root;

        PositionChanged += (_, e) => formLocation = e.Point;

        BuildMenu();
        View.ContextMenu = RightClickMenu;

        Init(Options.SplitsPath, Options.LayoutPath);
        InitInput();
    }

    private void Init(string splitsPath, string layoutPath)
    {
        LiveSplitCoreFactory.LoadLiveSplitCore();

        SpeedrunCom.Authenticator = new SpeedrunComApiKeyPrompt();

        GlobalCache = new GraphicsCache();
        Invalidator = new Invalidator(this);

        ComponentCatalog.Initialize();
        ComponentCatalog.InitializeRaceProviders();

        CurrentState = new LiveSplitState(null, this, null, null, null);

        ComparisonGeneratorsFactory = new StandardComparisonGeneratorsFactory();

        Model = new DoubleTapPrevention(new TimerModel());

        RunFactory = new StandardFormatsRunFactory();
        RunSaver = new XMLRunSaver();
        LayoutSaver = new XMLLayoutSaver();
        SettingsStore = new SettingsStore(Paths.SettingsFile);
        Settings = SettingsStore.Load();
        InitializeLanguageMenu();

        CurrentState.CurrentHotkeyProfile = Settings.HotkeyProfiles.First().Key;

        UpdateRecentSplits();
        UpdateRecentLayouts();

        IRun timerOnlyRun = new StandardRunFactory().Create(ComparisonGeneratorsFactory);

        IRun run = timerOnlyRun;
        try
        {
            if (!string.IsNullOrEmpty(splitsPath))
            {
                UpdateStateFromSplitsPath(splitsPath);

                run = LoadRunFromFile(splitsPath);
            }
            else if (Settings.RecentSplits.Count > 0)
            {
                RecentSplitsFile lastSplitFile = Settings.RecentSplits[^1];
                if (!string.IsNullOrEmpty(lastSplitFile.Path))
                {
                    UpdateStateFromSplitsPath(lastSplitFile.Path);

                    run = LoadRunFromFile(lastSplitFile.Path);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        run.FixSplits();
        CurrentState.Run = run;
        CurrentState.Settings = Settings;

        try
        {
            if (!string.IsNullOrEmpty(layoutPath))
            {
                Layout = LoadLayoutFromFile(layoutPath);
            }
            else
            {
                if (Settings.RecentLayouts.Count > 0
                    && !string.IsNullOrEmpty(Settings.RecentLayouts[^1]))
                {
                    Layout = LoadLayoutFromFile(Settings.RecentLayouts[^1]);
                }
                else if (run == timerOnlyRun)
                {
                    Layout = new TimerOnlyLayoutFactory().Create(CurrentState);
                    InTimerOnlyMode = true;
                }
                else
                {
                    Layout = new StandardLayoutFactory().Create(CurrentState);
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            Layout = new StandardLayoutFactory().Create(CurrentState);
        }

        InTimerOnlyMode = run == timerOnlyRun;
        if (InTimerOnlyMode)
        {
            SetInTimerOnlyMode();
        }

        CurrentState.LayoutSettings = Layout.Settings;
        CreateAutoSplitter();

        SwitchComparisonGenerators();
        SwitchComparison(Settings.LastComparison);
        Model.CurrentState = CurrentState;

        CurrentState.OnReset += CurrentState_OnReset;
        CurrentState.OnStart += CurrentState_OnStart;
        CurrentState.OnSplit += CurrentState_OnSplit;
        CurrentState.OnSkipSplit += CurrentState_OnSkipSplit;
        CurrentState.OnUndoSplit += CurrentState_OnUndoSplit;
        CurrentState.OnPause += CurrentState_OnPause;
        CurrentState.OnUndoAllPauses += CurrentState_OnUndoAllPauses;
        CurrentState.OnResume += CurrentState_OnResume;
        CurrentState.OnSwitchComparisonPrevious += CurrentState_OnSwitchComparisonPrevious;
        CurrentState.OnSwitchComparisonNext += CurrentState_OnSwitchComparisonNext;

        ComponentRenderer = new ComponentRenderer();

        SetLayout(Layout);

        InvalidationRequired = false;

        if (Options.ScreenshotPath == null)
        {
            StartHotkeysAndServers();
            StartTimers();
        }
    }

    private void StartHotkeysAndServers()
    {
        Hook = new CompositeHook(false);
        Hook.KeyOrButtonPressed += hook_KeyOrButtonPressed;
        Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);

        if (!Hook.IsKeyboardAvailable)
        {
            Log.Warning("Global hotkeys unavailable: " + Hook.KeyboardStatusMessage);
        }

        Server = new CommandServer(
            CurrentState,
            RefreshHotkeyHooks,
            () => OnUi(MakeScreenShot),
            suppressPrompts => OnUi(() => SaveLayout(suppressPrompts)),
            (promptPBMessage, suppressPrompts) => OnUi(() => SaveSplits(promptPBMessage, suppressPrompts)),
            (path, suppressPrompts) => OnUi(() => OpenLayoutFromFile(path, suppressPrompts)),
            (path, suppressPrompts) => OnUi(() => OpenRunFromFile(path, suppressPrompts)));

        if (!Options.NoServer)
        {
            try
            {
                Server.StartNamedPipe();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            try
            {
                switch (Settings.ServerStartup)
                {
                    case ServerStartupType.TCP:
                        Server.StartTcp();
                        break;
                    case ServerStartupType.Websocket:
                        Server.StartWs();
                        break;
                    case ServerStartupType.PreviousState:
                        switch (Settings.ServerState)
                        {
                            case ServerStateType.TCP:
                                Server.StartTcp();
                                break;
                            case ServerStateType.Websocket:
                                Server.StartWs();
                                break;
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        UpdateServerMenuItems();
    }

    private void StartTimers()
    {
        var perSecondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        perSecondTimer.Tick += PerSecondTimer_Tick;
        perSecondTimer.Start();

        _ = RefreshTimerWorker();
    }

    /// <summary>Runs <paramref name="func"/> on the UI thread and returns its result.</summary>
    private static T OnUi<T>(Func<T> func)
    {
        if (UiThread.IsUiThread)
        {
            return func();
        }

        return Dispatcher.UIThread.Invoke(func);
    }

    // ---------------------------------------------------------------- IFormHost

    bool IFormHost.InvokeRequired => !UiThread.IsUiThread;

    bool IFormHost.IsActive => UiThread.IsUiThread ? IsActive : OnUi(() => IsActive);

    public bool InvokeRequired => !UiThread.IsUiThread;

    void IFormHost.Invoke(Action action)
    {
        UiThread.Invoke(action);
    }

    void IFormHost.BeginInvoke(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    void IFormHost.Invalidate(Rectangle rectangle)
    {
        Invalidate();
    }

    void IFormHost.AddControl(object control)
    {
        if (control is Control avaloniaControl)
        {
            UiThread.Invoke(() =>
            {
                if (avaloniaControl.Parent == null)
                {
                    controlHost.Children.Add(avaloniaControl);
                }
            });
        }
    }

    void IFormHost.RemoveControl(object control)
    {
        if (control is Control avaloniaControl)
        {
            UiThread.Invoke(() => controlHost.Children.Remove(avaloniaControl));
        }
    }

    void IFormHost.SetControlBounds(object control, Rectangle bounds)
    {
        if (control is Control avaloniaControl)
        {
            UiThread.Invoke(() =>
            {
                Canvas.SetLeft(avaloniaControl, bounds.X);
                Canvas.SetTop(avaloniaControl, bounds.Y);
                avaloniaControl.Width = bounds.Width;
                avaloniaControl.Height = bounds.Height;
            });
        }
    }

    bool IFormHost.GetControlVisible(object control)
    {
        return control is Control avaloniaControl && OnUi(() => avaloniaControl.IsVisible);
    }

    void IFormHost.SetControlVisible(object control, bool visible)
    {
        if (control is Control avaloniaControl)
        {
            UiThread.Invoke(() => avaloniaControl.IsVisible = visible);
        }
    }

    /// <summary>Marks the whole window for repaint (safe from any thread).</summary>
    public void Invalidate()
    {
        if (UiThread.IsUiThread)
        {
            View.InvalidateVisual();
        }
        else
        {
            Dispatcher.UIThread.Post(View.InvalidateVisual);
        }
    }

    private void InvokeIfRequired(Action action)
    {
        UiThread.Invoke(action);
    }

    /// <summary>TopMost of the original form.</summary>
    public bool TopMost
    {
        get => Topmost;
        set => Topmost = value;
    }

    // ---------------------------------------------------------------- lifetime

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        shown = true;
        UpdateRaceProviderIntegration();
        ApplyLayoutWindowSettings();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || closing)
        {
            return;
        }

        if (!WarnUserAboutSplitsSave())
        {
            e.Cancel = true;
            return;
        }

        if (!WarnUserAboutLayoutSave(true))
        {
            e.Cancel = true;
            return;
        }

        closing = true;
        Settings.LastComparison = CurrentState.CurrentComparison;
        AddCurrentSplitsToLRU(CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);
        SaveSettingsToDisk();

        foreach (UI.Components.IComponent component in Layout.Components)
        {
            component.Dispose();
        }

        DeactivateAutoSplitter();
        Server?.StopAll();
        Hook?.Dispose();
    }

    private void Exit()
    {
        Close();
    }

    private bool SaveSettingsToDisk()
    {
        if (SettingsStore.Save(Settings, out Exception ex))
        {
            return true;
        }

        Log.Error(ex);
        ShowMessage(T("Settings could not be saved!"), T("Save Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    // ---------------------------------------------------------------- hotkeys

    private void RefreshHotkeyHooks()
    {
        if (!UiThread.IsUiThread)
        {
            Dispatcher.UIThread.Invoke(RefreshHotkeyHooks);
            return;
        }

        Settings.UnregisterAllHotkeys(Hook);
        Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
    }

    private void StartOrSplit()
    {
        if (CurrentState.CurrentPhase == TimerPhase.Running)
        {
            Model.Split();
        }
        else if (CurrentState.CurrentPhase == TimerPhase.Paused)
        {
            Model.Pause();
        }
        else if (CurrentState.CurrentPhase == TimerPhase.NotRunning)
        {
            Model.Start();
        }
        else if (CurrentState.CurrentPhase == TimerPhase.Ended)
        {
            Model.Reset();
        }
    }

    private void hook_KeyOrButtonPressed(object sender, KeyOrButton e)
    {
        // Raised on the input thread of the keyboard backend.
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                HandleHotkey(e);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        });
    }

    /// <summary>The body of TimerForm.hook_KeyOrButtonPressed; runs on the UI thread.</summary>
    public void HandleHotkey(KeyOrButton e)
    {
        HotkeyProfile hotkeyProfile = Settings.HotkeyProfiles[CurrentState.CurrentHotkeyProfile];

        if ((IsActive || hotkeyProfile.GlobalHotkeysEnabled) && !ResetMessageShown && !IsInDialogMode)
        {
            if (hotkeyProfile.SplitKey == e)
            {
                if (hotkeyProfile.HotkeyDelay > 0)
                {
                    DelayedAction(hotkeyProfile.HotkeyDelay, StartOrSplit);
                }
                else
                {
                    StartOrSplit();
                }
            }
            else if (hotkeyProfile.UndoKey == e)
            {
                Model.UndoSplit();
            }
            else if (hotkeyProfile.SkipKey == e)
            {
                Model.SkipSplit();
            }
            else if (hotkeyProfile.ResetKey == e)
            {
                Reset();
            }
            else if (hotkeyProfile.PauseKey == e)
            {
                if (hotkeyProfile.HotkeyDelay > 0)
                {
                    DelayedAction(hotkeyProfile.HotkeyDelay, Model.Pause);
                }
                else
                {
                    Model.Pause();
                }
            }
            else if (hotkeyProfile.SwitchComparisonPrevious == e)
            {
                Model.SwitchComparisonPrevious();
            }
            else if (hotkeyProfile.SwitchComparisonNext == e)
            {
                Model.SwitchComparisonNext();
            }
        }

        if (hotkeyProfile.ToggleGlobalHotkeys == e)
        {
            hotkeyProfile.GlobalHotkeysEnabled = !hotkeyProfile.GlobalHotkeysEnabled;
        }
    }

    private static void DelayedAction(float seconds, Action action)
    {
        DispatcherTimer timer = null;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private void Reset()
    {
        if (!UiThread.IsUiThread)
        {
            Dispatcher.UIThread.Invoke(Reset);
            return;
        }

        if (!ResetMessageShown)
        {
            DialogResult result = DialogResult.Yes;
            if (Settings.WarnOnReset && !InTimerOnlyMode)
            {
                ResetMessageShown = true;
                result = WarnAboutResetting();
            }

            if (result == DialogResult.Yes)
            {
                Model.Reset();
            }
            else if (result == DialogResult.No)
            {
                Model.Reset(false);
            }

            ResetMessageShown = false;
        }
    }

    private DialogResult WarnAboutResetting()
    {
        bool warnUser = false;
        for (int index = 0; index < CurrentState.Run.Count; index++)
        {
            if (LiveSplitStateHelper.CheckBestSegment(CurrentState, index, CurrentState.CurrentTimingMethod))
            {
                warnUser = true;
                break;
            }
        }

        if ((!warnUser && CurrentState.Run[^1].SplitTime[CurrentState.CurrentTimingMethod] != null && CurrentState.Run[^1].PersonalBestSplitTime[CurrentState.CurrentTimingMethod] == null)
            || CurrentState.Run[^1].SplitTime[CurrentState.CurrentTimingMethod] < CurrentState.Run[^1].PersonalBestSplitTime[CurrentState.CurrentTimingMethod])
        {
            warnUser = true;
        }

        if (warnUser)
        {
            dontRedraw = true;
            DialogResult result = ShowMessage(T("You have beaten some of your best times.\r\nDo you want to update them?"), T("Update Times?"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            dontRedraw = false;
            return result;
        }

        return DialogResult.Yes;
    }

    private void PerSecondTimer_Tick(object sender, EventArgs e)
    {
        // Invalidate the entire form at least once per second, to avoid parts of the form not being redrawn when necessary in some cases
        InvalidationRequired = true;

        if (shouldRefreshRaces)
        {
            for (int i = 0; i < racesToRefresh.Count; i++)
            {
                racesToRefresh[i]();
            }
        }
    }

    // ---------------------------------------------------------------- refresh loop

    private async System.Threading.Tasks.Task RefreshTimerWorker()
    {
        while (!closing)
        {
            int rate = Math.Clamp(Settings?.RefreshRate ?? 60, 1, 1000);
            await System.Threading.Tasks.Task.Delay(1000 / rate);
            try
            {
                TimerElapsed();
            }
            catch
            {
            }
        }
    }

    private void TimerElapsed()
    {
        try
        {
            Hook?.Poll();

            if (CurrentState.Run.IsAutoSplitterActive())
            {
                CurrentState.Run.AutoSplitter.Component.Update(null, CurrentState, 0, 0, Layout.Mode);
            }

            if (dontRedraw)
            {
                return;
            }

            if (RefreshesRemaining > 0 || InvalidationRequired)
            {
                InvalidateForm();
                if (InvalidationRequired)
                {
                    InvalidationRequired = false;
                }
            }
            else
            {
                GlobalCache.Restart();
                GlobalCache["LayoutHashCode"] = new XMLLayoutSaver().CreateLayoutNode(null, null, Layout);

                if (GlobalCache.HasChanged)
                {
                    InvalidateForm();
                }
                else
                {
                    Invalidator.Restart();

                    try
                    {
                        ComponentRenderer.Update(Invalidator, CurrentState, formWidth, formHeight, Layout.Mode);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        Invalidate();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Invalidate();
        }
    }

    protected void InvalidateForm()
    {
        try
        {
            UpdateAllComponents();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        Invalidate();
    }

    protected void UpdateAllComponents()
    {
        foreach (UI.Components.IComponent component in Layout.Components)
        {
            component.Update(null, CurrentState, formWidth, formHeight, Layout.Mode);
        }
    }

    // ---------------------------------------------------------------- dialogs

    private string T(string source)
    {
        return UiLocalizer.Translate(source, CurrentLanguage);
    }

    private string TK(string key, string fallback)
    {
        return UiLocalizer.TranslateKey(key, fallback, CurrentLanguage);
    }

    private AppLanguage CurrentLanguage => LanguageResolver.ResolveCurrentCultureLanguage();

    private AppLanguage ConfiguredLanguage => LanguageResolver.Resolve(Settings?.UILanguage);

    private DialogResult ShowMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        return PlatformServices.Current.ShowMessage(this, text, caption, buttons, icon);
    }
}
