using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

/// <summary>Base class for the small modal dialogs: a content area and a right aligned button row.</summary>
public abstract class DialogWindow : Window
{
    private readonly StackPanel buttonRow;
    private readonly Border contentHost;

    public DialogResult Result { get; protected set; } = DialogResult.Cancel;

    protected DialogWindow(string title)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 280;
        Icon = MainWindow.AppIcon;

        buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };

        contentHost = new Border();
        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(buttonRow);
        root.Children.Add(contentHost);
        Content = root;
    }

    protected void SetBody(Control body)
    {
        contentHost.Child = body;
    }

    protected Button AddButton(string text, DialogResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        button.Click += (_, _) => Close(result);
        buttonRow.Children.Add(button);
        return button;
    }

    protected Button AddButton(string text, Action click, bool isDefault = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = isDefault,
        };
        button.Click += (_, _) => click();
        buttonRow.Children.Add(button);
        return button;
    }

    protected void Close(DialogResult result)
    {
        Result = result;
        Close();
    }

    protected static TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4),
        };
    }

    /// <summary>Shows the dialog modally and completes with <see cref="Result"/>. Without an owner it is shown standalone.</summary>
    public async Task<DialogResult> ShowModalAsync(Window owner)
    {
        if (owner != null && owner.IsVisible)
        {
            await ShowDialog(owner);
        }
        else
        {
            var closed = new TaskCompletionSource();
            Closed += (_, _) => closed.TrySetResult();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Show();
            await closed.Task;
        }

        return Result;
    }
}
