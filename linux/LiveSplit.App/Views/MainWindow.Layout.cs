using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Linq;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace LiveSplit.View;

/// <summary>
/// Window sizing: FixSize, KeepLayoutSize, MaintainMinimumSize and SetLayout of the original TimerForm. The form size
/// is tracked in <see cref="formWidth"/>/<see cref="formHeight"/> (whole pixels at 100% scale, device independent
/// pixels otherwise) so reads see a requested size immediately, before the window system has acknowledged it.
/// </summary>
public partial class MainWindow
{
    private int formWidth;
    private int formHeight;
    private PixelPoint formLocation;
    private float? resizingInitialAspectRatio;

    private bool allowResizing;
    private bool allowMoving;

    protected Size FormSize
    {
        get => new(formWidth, formHeight);
        set => SetFormSize(value.Width, value.Height);
    }

    public PixelPoint FormLocation
    {
        get => formLocation;
        set
        {
            formLocation = value;
            Position = value;
        }
    }

    private void SetFormSize(int width, int height)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        if (width == formWidth && height == formHeight && Math.Abs(Width - width) < 0.5 && Math.Abs(Height - height) < 0.5)
        {
            return;
        }

        formWidth = width;
        formHeight = height;
        Width = width;
        Height = height;
    }

    private void SetMinimumSize(int width, int height)
    {
        MinWidth = width;
        MinHeight = height;
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);

        int width = (int)Math.Round(e.ClientSize.Width);
        int height = (int)Math.Round(e.ClientSize.Height);
        if (width < 1 || height < 1)
        {
            return;
        }

        if (resizingInitialAspectRatio.HasValue && lastModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            // Shift while resizing keeps the aspect ratio the window had when the drag began (WM_SIZING handling).
            float currentAspectRatio = (float)width / height;
            if (currentAspectRatio >= resizingInitialAspectRatio.Value)
            {
                width = (int)(height * resizingInitialAspectRatio.Value);
            }
            else
            {
                height = (int)(width / resizingInitialAspectRatio.Value);
            }

            formWidth = width;
            formHeight = height;
            Width = width;
            Height = height;
        }
        else
        {
            formWidth = width;
            formHeight = height;
        }

        TimerForm_SizeChanged();
    }

    private void TimerForm_SizeChanged()
    {
        if (Layout == null || ComponentRenderer == null)
        {
            return;
        }

        CreateBakedBackground();
        if (RefreshesRemaining <= 0)
        {
            if (Layout.Mode == LayoutMode.Vertical)
            {
                Layout.VerticalWidth = formWidth;
                Layout.VerticalHeight = formHeight;
            }
            else
            {
                Layout.HorizontalWidth = formWidth;
                Layout.HorizontalHeight = formHeight;
            }

            MaintainMinimumSize();
        }

        Invalidate();
    }

    private void FixSize()
    {
        ComponentRenderer.CalculateOverallSize(Layout.Mode);
        float currentSize = ComponentRenderer.OverallSize;
        if (RefreshesRemaining <= 0)
        {
            if (OldSize != currentSize)
            {
                SetMinimumSize(0, 0);
                if (Layout.Mode == LayoutMode.Vertical)
                {
                    SetFormSize(formWidth, (int)((currentSize / (double)OldSize * formHeight) + 0.5));
                }
                else
                {
                    SetFormSize((int)((currentSize / (double)OldSize * formWidth) + 0.5), formHeight);
                }

                OldSize = currentSize;
            }

            int minSize = (int)((currentSize / 5) + 0.5f);
            if (Layout.Mode == LayoutMode.Vertical)
            {
                SetMinimumSize(25, Math.Max(minSize, 25));
            }
            else
            {
                SetMinimumSize(Math.Max(minSize, 25), 25);
            }
        }
    }

    private void KeepLayoutSize()
    {
        if (RefreshesRemaining > 0)
        {
            FormSize = Layout.Mode == LayoutMode.Vertical
                ? new Size(Layout.VerticalWidth, Layout.VerticalHeight)
                : new Size(Layout.HorizontalWidth, Layout.HorizontalHeight);

            if (OldSize != ComponentRenderer.OverallSize)
            {
                UpdateRefreshesRemaining();
            }
            else
            {
                RefreshesRemaining--;
            }

            OldSize = ComponentRenderer.OverallSize;
        }
    }

    private void UpdateRefreshesRemaining()
    {
        RefreshesRemaining = 5;
    }

    private void MaintainMinimumSize()
    {
        if (Layout.Mode == LayoutMode.Vertical)
        {
            float minimumWidth = ComponentRenderer.MinimumWidth * (formHeight / ComponentRenderer.OverallSize);
            if (formWidth < minimumWidth)
            {
                SetFormSize(formWidth, (int)((formHeight / (minimumWidth / formWidth)) + 0.5f));
            }
        }
        else
        {
            float minimumHeight = ComponentRenderer.MinimumHeight * (formWidth / ComponentRenderer.OverallSize);
            if (formHeight < minimumHeight)
            {
                SetFormSize((int)((formWidth / (minimumHeight / formHeight)) + 0.5f), formHeight);
            }
        }
    }

    /// <summary>Stores size and position in the layout (what SaveLayout and EditLayout do before they use it).</summary>
    public void StoreWindowGeometryInLayout()
    {
        if (Layout.Mode == LayoutMode.Vertical)
        {
            Layout.VerticalWidth = formWidth;
            Layout.VerticalHeight = formHeight;
        }
        else
        {
            Layout.HorizontalWidth = formWidth;
            Layout.HorizontalHeight = formHeight;
        }

        Layout.X = formLocation.X;
        Layout.Y = formLocation.Y;
    }

    private void SetLayout(ILayout layout)
    {
        if (Layout != null && Layout != layout)
        {
            if (Layout.Settings.BackgroundImage != null && Layout.Settings.BackgroundImage != layout.Settings.BackgroundImage)
            {
                Layout.Settings.BackgroundImage.Dispose();
            }

            foreach (UI.Components.IComponent component in Layout.Components.Except(layout.Components))
            {
                component.Dispose();
            }

            foreach (IDeactivatableComponent component in layout.Components.Except(Layout.Components).OfType<IDeactivatableComponent>())
            {
                component.Activated = true;
            }
        }

        Layout = layout;
        ComponentRenderer.VisibleComponents = Layout.Components;
        CurrentState.LayoutSettings = layout.Settings;
        UpdateRefreshesRemaining();
        SetMinimumSize(0, 0);
        if (Layout.Mode == LayoutMode.Vertical)
        {
            if (Layout.VerticalWidth != UI.Layout.InvalidSize && Layout.VerticalHeight != UI.Layout.InvalidSize)
            {
                SetFormSize(Layout.VerticalWidth, Layout.VerticalHeight);
            }
        }
        else
        {
            if (Layout.HorizontalWidth != UI.Layout.InvalidSize && Layout.HorizontalHeight != UI.Layout.InvalidSize)
            {
                SetFormSize(Layout.HorizontalWidth, Layout.HorizontalHeight);
            }
        }

        FormLocation = EnsureVisibleOnScreen(new PixelPoint(Layout.X, Layout.Y));
        ApplyLayoutWindowSettings();
        SetInTimerOnlyMode();
        CreateBakedBackground();
        Invalidate();
    }

    /// <summary>Keeps at least 32 px of the window on a screen (a layout may come from another machine).</summary>
    private PixelPoint EnsureVisibleOnScreen(PixelPoint location)
    {
        const int minimumVisibleSize = 32;
        System.Collections.Generic.IReadOnlyList<Screen> screens = Screens?.All;
        if (screens == null || screens.Count == 0)
        {
            return location;
        }

        double scale = RenderScaling;
        int width = (int)Math.Round(formWidth * scale);
        int height = (int)Math.Round(formHeight * scale);
        var bounds = new PixelRect(location.X, location.Y, width, height);
        int minVisibleWidth = Math.Min(minimumVisibleSize, bounds.Width);
        int minVisibleHeight = Math.Min(minimumVisibleSize, bounds.Height);

        if (screens.Any(screen =>
        {
            PixelRect visible = screen.Bounds.Intersect(bounds);
            return visible.Width >= minVisibleWidth && visible.Height >= minVisibleHeight;
        }))
        {
            return location;
        }

        Screen target = Screens.ScreenFromPoint(location) ?? Screens.Primary ?? screens[0];
        PixelRect workingArea = target.WorkingArea;
        int x = Math.Max(workingArea.X, Math.Min(bounds.X, workingArea.Right - bounds.Width));
        int y = Math.Max(workingArea.Y, Math.Min(bounds.Y, workingArea.Bottom - bounds.Height));
        return new PixelPoint(x, y);
    }

    /// <summary>The per-layout window options that PaintForm applied on every paint.</summary>
    private void ApplyPaintWindowSettings()
    {
        LayoutSettings settings = Layout?.Settings;
        if (settings == null)
        {
            return;
        }

        double opacity = Math.Clamp(settings.Opacity, 0f, 1f);
        if (View.Opacity != opacity)
        {
            View.Opacity = opacity;
        }

        allowResizing = settings.AllowResizing;
        allowMoving = settings.AllowMoving;
    }

    /// <summary>Applies opacity, resize/move permissions and the always-on-top flag of the layout.</summary>
    private void ApplyLayoutWindowSettings()
    {
        if (Layout?.Settings == null)
        {
            return;
        }

        ApplyPaintWindowSettings();
        TopMost = Layout.Settings.AlwaysOnTop;
    }
}
