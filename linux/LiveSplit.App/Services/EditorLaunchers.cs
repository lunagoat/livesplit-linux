using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.View;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiveSplit.Services;

/*
 * Entry points for the dialogs that are implemented elsewhere (run editor, layout editor, settings, share).
 *
 * MainWindow keeps the surrounding original flow (leaving TopMost, IsInDialogMode, reverting on Cancel, disposing
 * images and components). A launcher only has to show the dialog modally (blocking the caller is fine, use
 * UiThread.RunSync(...) to wait for an async Avalonia dialog) and report back. While the dialog is open it calls the
 * Notify* methods of the host, which are the ported editor_* callbacks of TimerForm.
 *
 * Replace a default with: AppServices.RunEditor = new MyRunEditorLauncher();
 */

public sealed class RunEditorRequest
{
    public MainWindow Host { get; init; }
    public LiveSplitState State { get; init; }

    /// <summary>True when the timer is not running, segments may be added, removed and moved.</summary>
    public bool AllowChangingSegments { get; init; }
}

public sealed class RunEditorResult
{
    /// <summary>OK to keep the edits, Cancel to revert to the run as it was before opening the editor.</summary>
    public DialogResult Result { get; init; } = DialogResult.Cancel;

    /// <summary>Images the editor created and replaced; the host disposes them (minus the ones of the reverted run).</summary>
    public IList<Image> ImagesToDispose { get; init; } = [];
}

public interface IRunEditorLauncher
{
    /// <summary>Call request.Host.NotifyRunEdited / NotifyComparisonRenamed / NotifySegmentRemovedOrAdded while editing.</summary>
    RunEditorResult ShowRunEditor(RunEditorRequest request);
}

public sealed class LayoutEditorRequest
{
    public MainWindow Host { get; init; }
    public ILayout Layout { get; init; }
    public LiveSplitState State { get; init; }
}

public sealed class LayoutEditorResult
{
    public DialogResult Result { get; init; } = DialogResult.Cancel;
    public IList<UI.Components.IComponent> ComponentsToDispose { get; init; } = [];
    public IList<Image> ImagesToDispose { get; init; } = [];
}

public interface ILayoutEditorLauncher
{
    /// <summary>
    /// Call request.Host.NotifyOrientationSwitched / NotifyLayoutResized / NotifyLayoutSettingsAssigned while editing.
    /// The editor edits request.Layout in place; the host reverts from a copy on Cancel.
    /// </summary>
    LayoutEditorResult ShowLayoutEditor(LayoutEditorRequest request);
}

public sealed class SettingsDialogRequest
{
    public MainWindow Host { get; init; }
    public CompositeHook Hook { get; init; }
    public ISettings Settings { get; init; }
    public string CurrentHotkeyProfile { get; init; }
}

public sealed class SettingsDialogResult
{
    public DialogResult Result { get; init; } = DialogResult.Cancel;
    public string SelectedHotkeyProfile { get; init; }
}

public interface ISettingsDialogLauncher
{
    /// <summary>Edits request.Settings in place (call request.Host.NotifySumOfBestModeChanged when it changes).</summary>
    SettingsDialogResult ShowSettings(SettingsDialogRequest request);
}

public sealed class ShareDialogRequest
{
    public MainWindow Host { get; init; }

    /// <summary>A clone of the current state.</summary>
    public LiveSplitState State { get; init; }
    public ISettings Settings { get; init; }

    /// <summary>Renders the timer window to an image.</summary>
    public Func<Image> MakeScreenShot { get; init; }
}

public interface IShareDialogLauncher
{
    void ShowShareDialog(ShareDialogRequest request);
}

public interface IRaceDialogLauncher
{
    void JoinRace(MainWindow host, ITimerModel model, string raceId);
    void NewRace(MainWindow host, ITimerModel model);
}

/// <summary>
/// Maps a component's settings object (what <see cref="UI.Components.IComponent.GetSettingsControl"/> returns) to an
/// Avalonia control. Register one per settings type with <see cref="ComponentSettingsViews"/>.
/// </summary>
public interface IComponentSettingsViewFactory
{
    bool CanCreate(object settings);

    Avalonia.Controls.Control Create(object settings, LayoutMode mode);
}

public sealed class ComponentSettingsViewFactory<TSettings> : IComponentSettingsViewFactory
{
    private readonly Func<TSettings, LayoutMode, Avalonia.Controls.Control> create;

    public ComponentSettingsViewFactory(Func<TSettings, LayoutMode, Avalonia.Controls.Control> create)
    {
        this.create = create;
    }

    public bool CanCreate(object settings)
    {
        return settings is TSettings;
    }

    public Avalonia.Controls.Control Create(object settings, LayoutMode mode)
    {
        return create((TSettings)settings, mode);
    }
}

public static class ComponentSettingsViews
{
    private static readonly List<IComponentSettingsViewFactory> Factories = [];

    public static void Register(IComponentSettingsViewFactory factory)
    {
        lock (Factories)
        {
            Factories.Insert(0, factory);
        }
    }

    public static void Register<TSettings>(Func<TSettings, Avalonia.Controls.Control> create)
    {
        Register(new ComponentSettingsViewFactory<TSettings>((settings, _) => create(settings)));
    }

    public static void Register<TSettings>(Func<TSettings, LayoutMode, Avalonia.Controls.Control> create)
    {
        Register(new ComponentSettingsViewFactory<TSettings>(create));
    }

    /// <summary>
    /// Creates the view for a component. When the component hands out an Avalonia control itself it is returned as is,
    /// otherwise the last registered matching factory wins. Returns a placeholder when nothing matches.
    /// </summary>
    public static Avalonia.Controls.Control Create(UI.Components.IComponent component, LayoutMode mode)
    {
        object settings = component.GetSettingsControl(mode);
        return Create(settings, mode);
    }

    public static Avalonia.Controls.Control Create(object settings, LayoutMode mode)
    {
        if (settings is Avalonia.Controls.Control control)
        {
            return control;
        }

        if (settings != null)
        {
            lock (Factories)
            {
                foreach (IComponentSettingsViewFactory factory in Factories)
                {
                    if (factory.CanCreate(settings))
                    {
                        return factory.Create(settings, mode);
                    }
                }
            }
        }

        return new Avalonia.Controls.TextBlock
        {
            Text = settings == null
                ? "This component has no settings."
                : $"No settings view is registered for {settings.GetType().Name}.",
            Margin = new Avalonia.Thickness(12),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
    }
}

internal static class NotImplementedDialog
{
    public static void Show(MainWindow host, string what)
    {
        PlatformServices_ShowMessage(host, $"{what} is not available in the Linux port yet.", "Not implemented yet");
    }

    private static void PlatformServices_ShowMessage(object owner, string text, string caption)
    {
        LiveSplit.Hosting.PlatformServices.Current.ShowMessage(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

public sealed class DefaultRunEditorLauncher : IRunEditorLauncher
{
    public RunEditorResult ShowRunEditor(RunEditorRequest request)
    {
        NotImplementedDialog.Show(request.Host, "The run editor");
        return new RunEditorResult { Result = DialogResult.OK };
    }
}

public sealed class DefaultLayoutEditorLauncher : ILayoutEditorLauncher
{
    public LayoutEditorResult ShowLayoutEditor(LayoutEditorRequest request)
    {
        NotImplementedDialog.Show(request.Host, "The layout editor");
        return new LayoutEditorResult { Result = DialogResult.OK };
    }
}

public sealed class DefaultSettingsDialogLauncher : ISettingsDialogLauncher
{
    public SettingsDialogResult ShowSettings(SettingsDialogRequest request)
    {
        NotImplementedDialog.Show(request.Host, "The settings dialog");
        return new SettingsDialogResult { Result = DialogResult.OK, SelectedHotkeyProfile = request.CurrentHotkeyProfile };
    }
}

public sealed class DefaultShareDialogLauncher : IShareDialogLauncher
{
    public void ShowShareDialog(ShareDialogRequest request)
    {
        NotImplementedDialog.Show(request.Host, "The share dialog");
    }
}

public sealed class DefaultRaceDialogLauncher : IRaceDialogLauncher
{
    public void JoinRace(MainWindow host, ITimerModel model, string raceId)
    {
        NotImplementedDialog.Show(host, "Racing");
    }

    public void NewRace(MainWindow host, ITimerModel model)
    {
        NotImplementedDialog.Show(host, "Racing");
    }
}

/// <summary>Service locator for the pluggable dialogs. Replace the defaults at startup, before the window is created.</summary>
public static class AppServices
{
    public static IRunEditorLauncher RunEditor { get; set; } = new DefaultRunEditorLauncher();
    public static ILayoutEditorLauncher LayoutEditor { get; set; } = new DefaultLayoutEditorLauncher();
    public static ISettingsDialogLauncher SettingsDialog { get; set; } = new DefaultSettingsDialogLauncher();
    public static IShareDialogLauncher ShareDialog { get; set; } = new DefaultShareDialogLauncher();
    public static IRaceDialogLauncher RaceDialog { get; set; } = new DefaultRaceDialogLauncher();
}
