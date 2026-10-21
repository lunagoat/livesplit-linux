using Avalonia.Controls;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.View.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Image = System.Drawing.Image;

namespace LiveSplit.View;

/// <summary>Opening, saving and switching splits and layouts, and the recent file lists.</summary>
public partial class MainWindow
{
    private const string SplitsFilter = "LiveSplit Splits (*.lss)|*.lss|All files (*.*)|*.*";
    private const string LayoutFilter = "LiveSplit Layout (*.lsl)|*.lsl|All Files (*.*)|*.*";

    private void AddSplitsFileToLRU(string filePath, IRun run, TimingMethod lastTimingMethod, string lastHotkeyProfile)
    {
        Settings.AddToRecentSplits(filePath, run, lastTimingMethod, lastHotkeyProfile);
        UpdateRecentSplits();
    }

    private void AddLayoutFileToLRU(string filePath)
    {
        Settings.AddToRecentLayouts(filePath);
        UpdateRecentLayouts();
    }

    protected void SetInTimerOnlyMode()
    {
        if (Layout.Components.Count() != 1 || Layout.Components.FirstOrDefault().ComponentName != "Timer")
        {
            InTimerOnlyMode = false;
        }
    }

    private void EditSplitHistory()
    {
        var editHistoryDialog = new EditHistoryWindow(Settings.RecentSplits.Select(x => x.Path));
        if (ShowDialogSync(editHistoryDialog) != DialogResult.Cancel)
        {
            Settings.RecentSplits = [.. Settings.RecentSplits.Where(x => editHistoryDialog.History.Contains(x.Path))];
        }

        UpdateRecentSplits();
    }

    private void EditLayoutHistory()
    {
        var editHistoryDialog = new EditHistoryWindow(Settings.RecentLayouts);
        if (ShowDialogSync(editHistoryDialog) != DialogResult.Cancel)
        {
            Settings.RecentLayouts = editHistoryDialog.History;
        }

        UpdateRecentLayouts();
    }

    private DialogResult ShowDialogSync(DialogWindow dialog)
    {
        return UiThread.RunSync(() => dialog.ShowModalAsync(this));
    }

    /// <summary>Downloads a file with the UI staying responsive.</summary>
    private static byte[] Download(Uri uri)
    {
        return UiThread.RunSync(async () =>
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(LiveSplit.Updates.UpdateHelper.UserAgent);
            return await client.GetByteArrayAsync(uri);
        });
    }

    private void OpenLayoutFromUrl()
    {
        try
        {
            IsInDialogMode = true;
            TopMost = false;
            string url = null;

            if (DialogResult.OK == InputBox.Show(this, T("Open Layout from URL"), T("URL:"), ref url))
            {
                try
                {
                    var uri = new Uri(url);
                    if (uri.Host.ToLowerInvariant() == "ge.tt"
                        && uri.LocalPath.Length > 0
                        && !uri.LocalPath[1..].Contains('/'))
                    {
                        uri = new Uri(string.Format("http://ge.tt/api/1/files{0}/0/blob?download", uri.LocalPath));
                    }

                    byte[] data = Download(uri);
                    using var memoryStream = new MemoryStream(data);

                    try
                    {
                        ILayout layout = new XMLLayoutFactory(memoryStream).Create(CurrentState);
                        layout.HasChanged = true;
                        SetLayout(layout);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        dontRedraw = true;
                        ShowMessage(T("The selected file was not recognized as a layout file. (") + ex.Message + ")", T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        dontRedraw = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    dontRedraw = true;
                    ShowMessage(T("The layout file couldn't be downloaded."), T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    dontRedraw = false;
                }
            }
        }
        finally
        {
            IsInDialogMode = false;
            TopMost = Layout.Settings.AlwaysOnTop;
        }
    }

    private void OpenSplitsFromUrl()
    {
        IRun run = ImportRunFromUrl();

        if (run != null)
        {
            if (!WarnUserAboutSplitsSave())
            {
                return;
            }

            if (!WarnAndRemoveTimerOnly(true))
            {
                return;
            }

            run.HasChanged = true;
            SetRun(run);
            CurrentState.CallRunManuallyModified();
        }
    }

    /// <summary>Port of URLRunImporter.Import: asks for a URL and parses the downloaded splits.</summary>
    public IRun ImportRunFromUrl()
    {
        string url = null;
        if (DialogResult.OK != InputBox.Show(this, "Open Splits from URL", "URL:", ref url))
        {
            return null;
        }

        try
        {
            byte[] data = Download(new Uri(url));
            using var memoryStream = new MemoryStream(data);
            var runFactory = new StandardFormatsRunFactory
            {
                Stream = memoryStream,
                FilePath = "",
            };

            try
            {
                return runFactory.Create(new StandardComparisonGeneratorsFactory());
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                ShowMessage("The selected file was not recognized as a splits file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            ShowMessage("The splits file couldn't be downloaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return null;
    }

    private void SetRun(IRun run)
    {
        foreach (Image icon in CurrentState.Run.Select(x => x.Icon).Except(run.Select(x => x.Icon)))
        {
            icon?.Dispose();
        }

        if (CurrentState.Run.GameIcon != null && CurrentState.Run.GameIcon != run.GameIcon)
        {
            CurrentState.Run.GameIcon.Dispose();
        }

        run.ComparisonGenerators = [.. CurrentState.Run.ComparisonGenerators];
        foreach (IComparisonGenerator generator in run.ComparisonGenerators)
        {
            generator.Run = run;
        }

        run.FixSplits();
        DeactivateAutoSplitter();
        CurrentState.Run = run;
        InvalidationRequired = true;
        RegenerateComparisons();
        SwitchComparison(CurrentState.CurrentComparison);
        CreateAutoSplitter();
        UpdateRefreshesRemaining();
        if (!string.IsNullOrEmpty(run.LayoutPath))
        {
            if (run.LayoutPath == "?default")
            {
                LoadDefaultLayout();
            }
            else if (CurrentState.Layout.FilePath != run.LayoutPath)
            {
                OpenLayoutFromFile(run.LayoutPath);
            }
        }
    }

    private void CreateAutoSplitter()
    {
        AutoSplitter splitter = AutoSplitterFactory.Instance.Create(CurrentState.Run.GameName);
        CurrentState.Run.AutoSplitter = splitter;
        if (splitter != null && CurrentState.Settings.ActiveAutoSplitters.Contains(CurrentState.Run.GameName))
        {
            splitter.Activate(CurrentState);
            if (splitter.IsActivated
            && CurrentState.Run.AutoSplitterSettings != null
            && CurrentState.Run.AutoSplitterSettings.GetAttribute("gameName") == CurrentState.Run.GameName)
            {
                CurrentState.Run.AutoSplitter.Component.SetSettings(CurrentState.Run.AutoSplitterSettings);
            }
        }
    }

    private void DeactivateAutoSplitter()
    {
        CurrentState.Run.AutoSplitter?.Deactivate();
    }

    private void AddCurrentSplitsToLRU(TimingMethod lastTimingMethod, string lastHotkeyProfile)
    {
        if (CurrentState.Run != null && Settings.RecentSplits.Any(x => x.Path == CurrentState.Run.FilePath))
        {
            AddSplitsFileToLRU(CurrentState.Run.FilePath, CurrentState.Run, lastTimingMethod, lastHotkeyProfile);
        }
    }

    private IRun LoadRunFromFile(string filePath, TimingMethod? previousTimingMethod = null, string previousHotkeyProfile = null)
    {
        IRun run;

        using FileStream stream = File.OpenRead(filePath);
        RunFactory.Stream = stream;
        RunFactory.FilePath = filePath;

        run = RunFactory.Create(ComparisonGeneratorsFactory);

        if (previousTimingMethod.HasValue && previousHotkeyProfile != null)
        {
            AddCurrentSplitsToLRU(previousTimingMethod.Value, previousHotkeyProfile);
        }

        AddSplitsFileToLRU(filePath, run, CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);

        return run;
    }

    private ILayout LoadLayoutFromFile(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        ILayout layout = new XMLLayoutFactory(stream).Create(CurrentState);
        layout.FilePath = filePath;
        AddLayoutFileToLRU(filePath);
        return layout;
    }

    public bool OpenRunFromFile(string filePath, bool suppressPrompts = false)
    {
        bool success = false;
        try
        {
            if (!suppressPrompts)
            {
                if (!WarnUserAboutSplitsSave())
                {
                    return false;
                }

                if (!WarnAndRemoveTimerOnly(true))
                {
                    return false;
                }
            }
            else
            {
                Model.Reset();
                if (!WarnAndRemoveTimerOnly(true, true))
                {
                    return false;
                }
            }

            TimingMethod previousTimingMethod = CurrentState.CurrentTimingMethod;
            string previousHotkeyProfile = CurrentState.CurrentHotkeyProfile;

            UpdateStateFromSplitsPath(filePath);

            IRun run = LoadRunFromFile(filePath, previousTimingMethod, previousHotkeyProfile);
            SetRun(run);
            CurrentState.CallRunManuallyModified();

            success = true;
        }
        catch (Exception e)
        {
            if (!suppressPrompts)
            {
                Log.Error(e);
                dontRedraw = true;
                ShowMessage(T("The selected file was not recognized as a splits file."), T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                dontRedraw = false;
            }
            else
            {
                Log.Error($"The selected file was not recognized as a splits file. ({e.Message})");
            }
        }

        return success;
    }

    private void UpdateStateFromSplitsPath(string filePath)
    {
        RecentSplitsFile recentSplitsFile = Settings.RecentSplits.LastOrDefault(splitsFile => splitsFile.Path == filePath);
        if (recentSplitsFile.Path != null)
        {
            CurrentState.CurrentTimingMethod = recentSplitsFile.LastTimingMethod;
            if (Settings.HotkeyProfiles.ContainsKey(recentSplitsFile.LastHotkeyProfile))
            {
                CurrentState.CurrentHotkeyProfile = recentSplitsFile.LastHotkeyProfile;
                if (Hook != null)
                {
                    RefreshHotkeyHooks();
                }
            }
        }
    }

    private string PickOpenFile(string title, string filter, string lastFile)
    {
        SetPickerDirectory(lastFile);
        return PlatformServices.Current.ChooseOpenFile(this, title, filter);
    }

    private string PickSaveFile(string title, string filter, string suggestedName, string lastFile)
    {
        SetPickerDirectory(lastFile);
        return PlatformServices.Current.ChooseSaveFile(this, title, filter, suggestedName);
    }

    private static void SetPickerDirectory(string lastFile)
    {
        if (PlatformServices.Current is AvaloniaPlatformServices services && !string.IsNullOrEmpty(lastFile))
        {
            services.InitialDirectory = Path.GetDirectoryName(lastFile);
        }
    }

    private void OpenSplits()
    {
        IsInDialogMode = true;
        try
        {
            string lastFile = Settings.RecentSplits.Count > 0 ? Settings.RecentSplits[^1].Path : null;
            string fileName = PickOpenFile(T("Open Splits"), SplitsFilter, lastFile);
            if (fileName != null)
            {
                OpenRunFromFile(fileName);
            }
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    private bool SaveSplitsAs(bool promptPBMessage)
    {
        IsInDialogMode = true;
        try
        {
            string lastFile = Settings.RecentSplits.Count > 0 ? Settings.RecentSplits[^1].Path : null;
            string fileName = PickSaveFile(T("Save Splits As"), SplitsFilter, CurrentState.Run.GetExtendedFileName(), lastFile);
            if (fileName != null)
            {
                if (!fileName.EndsWith(".lss"))
                {
                    ShowMessage(T("Cannot save splits with a file type that is not .lss"), T("Save Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                CurrentState.Run.FilePath = fileName;
                return SaveSplits(promptPBMessage);
            }

            return false;
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    public bool SaveSplits(bool promptPBMessage, bool suppressPrompts = false)
    {
        string savePath = CurrentState.Run.FilePath;

        if (savePath == null)
        {
            if (suppressPrompts)
            {
                string defaultFilename = !string.IsNullOrEmpty(CurrentState.Run.GameName) || !string.IsNullOrEmpty(CurrentState.Run.CategoryName)
                    ? string.Join(" - ", new[] { CurrentState.Run.GameName, CurrentState.Run.CategoryName }.Where(s => !string.IsNullOrEmpty(s)))
                    : "Splits";
                savePath = Path.Combine(Paths.EnsureDirectory(Paths.DataDirectory), $"{defaultFilename}.lss");
            }
            else
            {
                return SaveSplitsAs(promptPBMessage);
            }
        }

        CurrentState.Run.FixSplits();

        DialogResult result = DialogResult.No;

        if (!suppressPrompts && promptPBMessage && ((CurrentState.CurrentPhase == TimerPhase.Ended
            && CurrentState.Run[^1].PersonalBestSplitTime[CurrentState.CurrentTimingMethod] != null
            && CurrentState.Run[^1].SplitTime[CurrentState.CurrentTimingMethod] >= CurrentState.Run[^1].PersonalBestSplitTime[CurrentState.CurrentTimingMethod])
            || CurrentState.CurrentPhase == TimerPhase.Running
            || CurrentState.CurrentPhase == TimerPhase.Paused))
        {
            dontRedraw = true;
            result = ShowMessage(T("This run did not beat your current splits. Would you like to save this run as a Personal Best?"), T("Save as Personal Best?"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            dontRedraw = false;
            if (result == DialogResult.Yes)
            {
                Model.ResetAndSetAttemptAsPB();
            }
            else if (result == DialogResult.Cancel)
            {
                return false;
            }
        }

        LiveSplitState stateCopy = CurrentState;
        if (result == DialogResult.No)
        {
            var modelCopy = new TimerModel();
            stateCopy = CurrentState.Clone() as LiveSplitState;
            modelCopy.CurrentState = stateCopy;
            modelCopy.Reset();
        }

        try
        {
            if (!File.Exists(savePath))
            {
                File.Create(savePath).Close();
            }

            using var memoryStream = new MemoryStream();
            RunSaver.Save(stateCopy.Run, memoryStream);
            byte[] buffer = memoryStream.GetBuffer();

            using var stream = File.Open(savePath, FileMode.Create, FileAccess.Write);
            stream.Write(buffer, 0, (int)memoryStream.Length);

            CurrentState.Run.HasChanged = false;

            AddSplitsFileToLRU(savePath, stateCopy.Run, CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);
        }
        catch (Exception ex)
        {
            if (!suppressPrompts)
            {
                ShowMessage(T("Splits could not be saved!"), T("Save Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.Error(ex);
            }
            else
            {
                Log.Error($"Splits could not be saved! ({ex.Message})");
            }

            return false;
        }

        return true;
    }

    public bool SaveLayout(bool suppressPrompts = false)
    {
        string savePath = Layout.FilePath;
        StoreWindowGeometryInLayout();

        if (savePath == null)
        {
            if (suppressPrompts)
            {
                savePath = Path.Combine(Paths.EnsureDirectory(Paths.DataDirectory), "Layout.lsl");
            }
            else
            {
                return SaveLayoutAs();
            }
        }

        try
        {
            if (!File.Exists(savePath))
            {
                File.Create(savePath).Close();
            }

            using var memoryStream = new MemoryStream();
            LayoutSaver.Save(Layout, memoryStream);
            byte[] buffer = memoryStream.GetBuffer();

            using var stream = File.Open(savePath, FileMode.Create, FileAccess.Write);
            stream.Write(buffer, 0, (int)memoryStream.Length);

            Layout.HasChanged = false;

            AddLayoutFileToLRU(savePath);
        }
        catch (Exception ex)
        {
            if (!suppressPrompts)
            {
                ShowMessage(T("Layout could not be saved!"), T("Save Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log.Error(ex);
            }
            else
            {
                Log.Error($"Layout could not be saved! ({ex.Message})");
            }

            return false;
        }

        return true;
    }

    private bool SaveLayoutAs()
    {
        IsInDialogMode = true;
        try
        {
            string lastFile = Settings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x));
            string fileName = PickSaveFile(T("Save Layout As"), LayoutFilter, Layout.FilePath != null ? Path.GetFileName(Layout.FilePath) : "Layout.lsl", lastFile);
            if (fileName != null)
            {
                if (!fileName.EndsWith(".lsl"))
                {
                    ShowMessage(T("Cannot save layout with a file type that is not .lsl"), T("Save Failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                Layout.FilePath = fileName;
                return SaveLayout();
            }

            return false;
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    protected bool WarnAndRemoveTimerOnly(bool canCancel, bool suppressPrompts = false)
    {
        if (InTimerOnlyMode)
        {
            if (Layout.HasChanged && suppressPrompts)
            {
                if (!SaveLayout(true))
                {
                    return false;
                }
            }
            else
            {
                if (!WarnUserAboutLayoutSave(canCancel))
                {
                    return false;
                }
            }

            InTimerOnlyMode = false;
            ILayout layout;
            try
            {
                string lastLayoutPath = Settings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x));
                layout = lastLayoutPath != null ? LoadLayoutFromFile(lastLayoutPath) : new StandardLayoutFactory().Create(CurrentState);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                layout = new StandardLayoutFactory().Create(CurrentState);
            }

            layout.X = Position.X;
            layout.Y = Position.Y;
            SetLayout(layout);
        }

        return true;
    }

    private void OpenLayout()
    {
        IsInDialogMode = true;
        try
        {
            string lastFile = Settings.RecentLayouts.Count > 0 ? Settings.RecentLayouts[^1] : null;
            string fileName = PickOpenFile(T("Open Layout"), LayoutFilter, lastFile);
            if (fileName != null)
            {
                OpenLayoutFromFile(fileName);
            }
        }
        finally
        {
            IsInDialogMode = false;
        }
    }

    public bool OpenLayoutFromFile(string filePath, bool suppressPrompts = false)
    {
        bool success = false;
        if ((!suppressPrompts && WarnUserAboutLayoutSave(true)) || suppressPrompts)
        {
            try
            {
                ILayout layout = LoadLayoutFromFile(filePath);
                SetLayout(layout);
                success = true;
            }
            catch (Exception e)
            {
                if (!suppressPrompts)
                {
                    Log.Error(e);
                    dontRedraw = true;
                    ShowMessage(T("The selected file was not recognized as a layout file. (") + e.Message + ")", T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    dontRedraw = false;
                }
                else
                {
                    Log.Error($"The selected file was not recognized as a layout file. ({e.Message})");
                }
            }
        }

        return success;
    }

    public void LoadDefaultLayout()
    {
        if (WarnUserAboutLayoutSave(true))
        {
            ILayout layout = new StandardLayoutFactory().Create(CurrentState);
            layout.X = Position.X;
            layout.Y = Position.Y;
            SetLayout(layout);
            Settings.AddToRecentLayouts("");
        }
    }

    private void CloseSplits()
    {
        bool needToChangeLayout = Layout.Components.Count() != 1 || Layout.Components.FirstOrDefault().ComponentName != "Timer";

        if (!WarnUserAboutSplitsSave())
        {
            return;
        }

        if (needToChangeLayout && !WarnUserAboutLayoutSave(true))
        {
            return;
        }

        AddCurrentSplitsToLRU(CurrentState.CurrentTimingMethod, CurrentState.CurrentHotkeyProfile);

        IRun run = new StandardRunFactory().Create(ComparisonGeneratorsFactory);
        Model.Reset();
        SetRun(run);
        Settings.AddToRecentSplits("", null, TimingMethod.RealTime, CurrentState.CurrentHotkeyProfile);
        InTimerOnlyMode = true;

        if (needToChangeLayout)
        {
            ILayout layout = new TimerOnlyLayoutFactory().Create(CurrentState);
            layout.Settings = Layout.Settings;
            layout.X = Position.X;
            layout.Y = Position.Y;
            layout.Mode = Layout.Mode;
            SetLayout(layout);
            Settings.AddToRecentLayouts("");
        }
    }

    private bool WarnUserAboutSplitsSave()
    {
        if (InTimerOnlyMode)
        {
            Model.Reset();
            return true;
        }

        bool safeToContinue = true;
        if (CurrentState.Run.HasChanged)
        {
            try
            {
                dontRedraw = true;
                DialogResult result = ShowMessage(T("Your splits have been updated but not yet saved.\nDo you want to save your splits now?"), T("Save Splits?"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    safeToContinue = SaveSplits(false);
                }
                else if (result == DialogResult.Cancel)
                {
                    return false;
                }
            }
            finally
            {
                dontRedraw = false;
            }
        }

        if (safeToContinue)
        {
            Model.Reset();
        }

        return safeToContinue;
    }

    private bool WarnUserAboutLayoutSave(bool canCancel)
    {
        bool safeToContinue = true;
        if (Layout.HasChanged)
        {
            try
            {
                dontRedraw = true;
                MessageBoxButtons buttons = canCancel ? MessageBoxButtons.YesNoCancel : MessageBoxButtons.YesNo;
                DialogResult result = ShowMessage(T("Your layout has been updated but not yet saved.\nDo you want to save your layout now?"), T("Save Layout?"), buttons, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    safeToContinue = SaveLayout();
                }
                else if (result == DialogResult.Cancel)
                {
                    return false;
                }
            }
            finally
            {
                dontRedraw = false;
            }
        }

        return safeToContinue;
    }
}
