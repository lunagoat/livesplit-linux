using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiveSplit.View;

/// <summary>Moving, resizing, the mouse wheel, file drops and the keyboard fallback for hotkeys.</summary>
public partial class MainWindow
{
    private const int ResizeHandleSize = 10;

    private KeyModifiers lastModifiers;

    private void InitInput()
    {
        AddHandler(PointerPressedEvent, MainWindow_PointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerMovedEvent, MainWindow_PointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, MainWindow_PointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, MainWindow_PointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, MainWindow_DragOver);
        AddHandler(DragDrop.DropEvent, MainWindow_Drop);
    }

    /// <summary>Which edge or corner of the borderless window lies under <paramref name="point"/> (WM_NCHITTEST).</summary>
    private WindowEdge? HitTestEdge(Point point)
    {
        if (!allowResizing)
        {
            return null;
        }

        bool left = point.X < ResizeHandleSize;
        bool right = point.X >= formWidth - ResizeHandleSize;
        bool top = point.Y < ResizeHandleSize;
        bool bottom = point.Y >= formHeight - ResizeHandleSize;

        if (top && left)
        {
            return WindowEdge.NorthWest;
        }

        if (top && right)
        {
            return WindowEdge.NorthEast;
        }

        if (bottom && left)
        {
            return WindowEdge.SouthWest;
        }

        if (bottom && right)
        {
            return WindowEdge.SouthEast;
        }

        if (left)
        {
            return WindowEdge.West;
        }

        if (right)
        {
            return WindowEdge.East;
        }

        if (top)
        {
            return WindowEdge.North;
        }

        if (bottom)
        {
            return WindowEdge.South;
        }

        return null;
    }

    private static StandardCursorType CursorFor(WindowEdge edge)
    {
        return edge switch
        {
            WindowEdge.North => StandardCursorType.TopSide,
            WindowEdge.South => StandardCursorType.BottomSide,
            WindowEdge.West => StandardCursorType.LeftSide,
            WindowEdge.East => StandardCursorType.RightSide,
            WindowEdge.NorthWest => StandardCursorType.TopLeftCorner,
            WindowEdge.NorthEast => StandardCursorType.TopRightCorner,
            WindowEdge.SouthWest => StandardCursorType.BottomLeftCorner,
            _ => StandardCursorType.BottomRightCorner,
        };
    }

    private void MainWindow_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        lastModifiers = e.KeyModifiers;
        PointerPoint point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // The window manager does the actual moving and resizing (_NET_WM_MOVERESIZE), the same way a title bar or
        // frame would; this is what keeps it smooth under XWayland compositors.
        WindowEdge? edge = HitTestEdge(point.Position);
        if (edge.HasValue)
        {
            if (formHeight > 0)
            {
                resizingInitialAspectRatio = (float)formWidth / formHeight;
            }

            BeginResizeDrag(edge.Value, e);
        }
        else if (allowMoving)
        {
            BeginMoveDrag(e);
        }
    }

    private void MainWindow_PointerMoved(object sender, PointerEventArgs e)
    {
        lastModifiers = e.KeyModifiers;
        PointerPoint point = e.GetCurrentPoint(this);

        if (resizingInitialAspectRatio.HasValue && !point.Properties.IsLeftButtonPressed)
        {
            // ResizeEnd: the drag ran inside the window manager, we only notice once the pointer is back.
            resizingInitialAspectRatio = null;
        }

        WindowEdge? edge = HitTestEdge(point.Position);
        Cursor = edge.HasValue ? new Cursor(CursorFor(edge.Value)) : null;
    }

    private void MainWindow_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        lastModifiers = e.KeyModifiers;
        resizingInitialAspectRatio = null;
    }

    private void MainWindow_PointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y > 0)
        {
            Model.ScrollUp();
        }
        else if (e.Delta.Y < 0)
        {
            Model.ScrollDown();
        }
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        IEnumerable<IStorageItem> items = e.Data.GetFiles();
        if (items == null)
        {
            return;
        }

        OpenDroppedFiles(items.Select(x => x.TryGetLocalPath()).Where(x => x != null).ToList());
    }

    /// <summary>
    /// Opens the dropped files: the first layout (.lsl) as a layout, the first other file as splits
    /// (TimerForm_DragDrop). Deferred so the drag source is released before any prompt shows.
    /// </summary>
    public void OpenDroppedFiles(IReadOnlyList<string> fileList)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            bool lssOpened = false;
            bool lslOpened = false;

            foreach (string fileToOpen in fileList)
            {
                if (File.Exists(fileToOpen))
                {
                    string extension = Path.GetExtension(fileToOpen).ToLower();

                    if (!lslOpened && extension == ".lsl")
                    {
                        lslOpened = true;
                        OpenLayoutFromFile(fileToOpen);
                    }
                    else if (!lssOpened)
                    {
                        lssOpened = true;
                        OpenRunFromFile(fileToOpen);
                    }
                }
            }
        });
    }

    /// <summary>
    /// When no global keyboard backend is available (no access to /dev/input) the hotkeys still work while the window
    /// has the focus, fed from the toolkit's key events.
    /// </summary>
    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        lastModifiers = e.KeyModifiers;
        if (Hook == null || Hook.IsKeyboardAvailable || e.Handled)
        {
            return;
        }

        System.Windows.Forms.Keys keys = KeyMap.ToKeys(e.Key, e.KeyModifiers);
        if (keys == System.Windows.Forms.Keys.None)
        {
            return;
        }

        try
        {
            HandleHotkey(new KeyOrButton(keys));
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}
