using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.View.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Image = System.Drawing.Image;

namespace LiveSplit.View;

/// <summary>
/// The surrounding flow of the editor dialogs (run editor, layout editor, settings, share, about) and the callbacks the
/// editors invoke. The dialogs themselves come from <see cref="AppServices"/>.
/// </summary>
public partial class MainWindow
{
    // ---------------------------------------------------------------- callbacks for the editors

    /// <summary>editor_RunEdited: the run was changed in the run editor.</summary>
    public void NotifyRunEdited()
    {
        UiThread.Invoke(() =>
        {
            RegenerateComparisons();
            CurrentState.CallRunManuallyModified();
            WarnAndRemoveTimerOnly(false);
        });
    }

    /// <summary>editor_ComparisonRenamed.</summary>
    public void NotifyComparisonRenamed(EventArgs e = null)
    {
        UiThread.Invoke(() => CurrentState.CallComparisonRenamed(e ?? EventArgs.Empty));
    }

    /// <summary>editor_SegmentRemovedOrAdded: forces a full repaint.</summary>
    public void NotifySegmentRemovedOrAdded()
    {
        InvalidationRequired = true;
    }

    /// <summary>editor_LayoutSettingsAssigned.</summary>
    public void NotifyLayoutSettingsAssigned()
    {
        InvalidationRequired = true;
    }

    /// <summary>editor_LayoutResized: the layout editor changed the component sizes.</summary>
    public void NotifyLayoutResized()
    {
        UiThread.Invoke(() =>
        {
            SetInTimerOnlyMode();
            if (Layout.Mode == LayoutMode.Horizontal)
            {
                Layout.VerticalWidth = UI.Layout.InvalidSize;
                Layout.VerticalHeight = UI.Layout.InvalidSize;
            }
            else
            {
                Layout.HorizontalWidth = UI.Layout.InvalidSize;
                Layout.HorizontalHeight = UI.Layout.InvalidSize;
            }
        });
    }

    /// <summary>editor_OrientationSwitched: the layout editor toggled vertical/horizontal.</summary>
    public void NotifyOrientationSwitched()
    {
        UiThread.Invoke(() =>
        {
            ComponentRenderer.CalculateOverallSize(Layout.Mode);
            if (Layout.Mode == LayoutMode.Vertical)
            {
                Layout.HorizontalWidth = formWidth;
                Layout.HorizontalHeight = formHeight;
                if (Layout.VerticalHeight == UI.Layout.InvalidSize || Layout.VerticalWidth == UI.Layout.InvalidSize)
                {
                    Layout.VerticalWidth = 300;
                    Layout.VerticalHeight = (int)(ComponentRenderer.OverallSize + 0.5);
                }
            }
            else
            {
                Layout.VerticalWidth = formWidth;
                Layout.VerticalHeight = formHeight;
                if (Layout.HorizontalWidth == UI.Layout.InvalidSize || Layout.HorizontalHeight == UI.Layout.InvalidSize)
                {
                    Layout.HorizontalWidth = (int)(ComponentRenderer.OverallSize + 0.5);
                    Layout.HorizontalHeight = 45;
                }
            }

            TopMost = false;
            SetLayout(Layout);
        });
    }

    /// <summary>editor_SumOfBestModeChanged.</summary>
    public void NotifySumOfBestModeChanged()
    {
        UiThread.Invoke(RegenerateComparisons);
    }

    /// <summary>Re-reads the hotkeys of the current profile into the hook (after the settings dialog changed them).</summary>
    public void ReregisterHotkeys()
    {
        if (Hook != null)
        {
            Settings.UnregisterAllHotkeys(Hook);
            Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
        }
    }

    // ---------------------------------------------------------------- dialogs

    private void EditSplits()
    {
        var runCopy = CurrentState.Run.Clone() as IRun;
        List<string> activeAutoSplitters = [.. CurrentState.Settings.ActiveAutoSplitters];
        try
        {
            TopMost = false;
            IsInDialogMode = true;

            RunEditorResult result = AppServices.RunEditor.ShowRunEditor(new RunEditorRequest
            {
                Host = this,
                State = CurrentState,
                AllowChangingSegments = CurrentState.CurrentPhase == TimerPhase.NotRunning,
            });

            if (result.Result == DialogResult.Cancel)
            {
                foreach (Image image in runCopy.Select(x => x.Icon))
                {
                    result.ImagesToDispose.Remove(image);
                }

                result.ImagesToDispose.Remove(runCopy.GameIcon);

                CurrentState.Settings.ActiveAutoSplitters = activeAutoSplitters;
                SetRun(runCopy);
                CurrentState.CallRunManuallyModified();
            }

            foreach (Image image in result.ImagesToDispose)
            {
                image?.Dispose();
            }
        }
        finally
        {
            TopMost = Layout.Settings.AlwaysOnTop;
            IsInDialogMode = false;
        }
    }

    private void EditLayout()
    {
        Layout.X = formLocation.X;
        Layout.Y = formLocation.Y;
        StoreWindowGeometryInLayout();

        var layoutCopy = (ILayout)Layout.Clone();
        var document = new XmlDocument();
        var componentSettings = Layout.Components.Select(x =>
            {
                try
                {
                    return x.GetSettings(document);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return null;
                }
            }).ToList();
        try
        {
            TopMost = false;
            LayoutEditorResult result = AppServices.LayoutEditor.ShowLayoutEditor(new LayoutEditorRequest
            {
                Host = this,
                Layout = Layout,
                State = CurrentState,
            });

            if (result.Result == DialogResult.Cancel)
            {
                foreach (UI.Components.IComponent component in layoutCopy.Components)
                {
                    result.ComponentsToDispose.Remove(component);
                }

                result.ImagesToDispose.Remove(layoutCopy.Settings.BackgroundImage);

                using List<XmlNode>.Enumerator enumerator = componentSettings.GetEnumerator();
                foreach (UI.Components.IComponent component in layoutCopy.Components)
                {
                    if (enumerator.MoveNext())
                    {
                        component.SetSettings(enumerator.Current);
                    }
                }

                SetLayout(layoutCopy);
            }

            foreach (UI.Components.IComponent component in result.ComponentsToDispose)
            {
                component.Dispose();
            }

            foreach (Image image in result.ImagesToDispose)
            {
                image?.Dispose();
            }
        }
        finally
        {
            TopMost = Layout.Settings.AlwaysOnTop;
        }
    }

    private void ShowSettingsDialog()
    {
        try
        {
            TopMost = false;
            var oldSettings = (ISettings)Settings.Clone();
            Settings.UnregisterAllHotkeys(Hook);

            SettingsDialogResult result = AppServices.SettingsDialog.ShowSettings(new SettingsDialogRequest
            {
                Host = this,
                Hook = Hook,
                Settings = Settings,
                CurrentHotkeyProfile = CurrentState.CurrentHotkeyProfile,
            });

            if (result.Result == DialogResult.Cancel)
            {
                bool regenerate = Settings.SimpleSumOfBest != oldSettings.SimpleSumOfBest;
                CurrentState.Settings = Settings = oldSettings;
                if (regenerate)
                {
                    RegenerateComparisons();
                }
            }
            else
            {
                SwitchComparisonGenerators();
                if (!string.IsNullOrEmpty(result.SelectedHotkeyProfile) && Settings.HotkeyProfiles.ContainsKey(result.SelectedHotkeyProfile))
                {
                    CurrentState.CurrentHotkeyProfile = result.SelectedHotkeyProfile;
                }
            }

            Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
            UpdateRaceProviderIntegration();
        }
        finally
        {
            TopMost = Layout.Settings.AlwaysOnTop;
        }
    }

    private void ShowShareDialog()
    {
        try
        {
            TopMost = false;
            IsInDialogMode = true;
            Settings.UnregisterAllHotkeys(Hook);
            AppServices.ShareDialog.ShowShareDialog(new ShareDialogRequest
            {
                Host = this,
                State = (LiveSplitState)CurrentState.Clone(),
                Settings = Settings,
                MakeScreenShot = MakeScreenShot,
            });
            Settings.RegisterHotkeys(Hook, CurrentState.CurrentHotkeyProfile);
        }
        finally
        {
            TopMost = Layout.Settings.AlwaysOnTop;
            IsInDialogMode = false;
        }
    }

    private void OpenAboutBox()
    {
        try
        {
            TopMost = false;
            ShowDialogSync(new AboutWindow());
        }
        finally
        {
            TopMost = Layout.Settings.AlwaysOnTop;
        }
    }
}
