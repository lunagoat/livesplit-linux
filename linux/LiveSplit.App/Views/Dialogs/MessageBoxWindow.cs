using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class MessageBoxWindow : DialogWindow
{
    private readonly DialogResult escapeResult;

    public MessageBoxWindow(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, bool scrollable = false)
        : base(caption)
    {
        var body = new DockPanel { LastChildFill = true };

        if (icon != MessageBoxIcon.None)
        {
            Control glyph = CreateIcon(icon);
            DockPanel.SetDock(glyph, Dock.Left);
            glyph.Margin = new Thickness(0, 0, 14, 0);
            glyph.VerticalAlignment = VerticalAlignment.Top;
            body.Children.Add(glyph);
        }

        Control message;
        if (scrollable)
        {
            message = new ScrollViewer
            {
                Width = 520,
                Height = 320,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            };
        }
        else
        {
            message = new SelectableTextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        body.Children.Add(message);
        SetBody(body);

        switch (buttons)
        {
            case MessageBoxButtons.OKCancel:
                AddButton("OK", DialogResult.OK, isDefault: true);
                AddButton("Cancel", DialogResult.Cancel, isCancel: true);
                escapeResult = DialogResult.Cancel;
                break;
            case MessageBoxButtons.YesNo:
                AddButton("Yes", DialogResult.Yes, isDefault: true);
                AddButton("No", DialogResult.No, isCancel: true);
                escapeResult = DialogResult.No;
                break;
            case MessageBoxButtons.YesNoCancel:
                AddButton("Yes", DialogResult.Yes, isDefault: true);
                AddButton("No", DialogResult.No);
                AddButton("Cancel", DialogResult.Cancel, isCancel: true);
                escapeResult = DialogResult.Cancel;
                break;
            case MessageBoxButtons.RetryCancel:
                AddButton("Retry", DialogResult.Retry, isDefault: true);
                AddButton("Cancel", DialogResult.Cancel, isCancel: true);
                escapeResult = DialogResult.Cancel;
                break;
            case MessageBoxButtons.AbortRetryIgnore:
                AddButton("Abort", DialogResult.Abort);
                AddButton("Retry", DialogResult.Retry, isDefault: true);
                AddButton("Ignore", DialogResult.Ignore, isCancel: true);
                escapeResult = DialogResult.Ignore;
                break;
            case MessageBoxButtons.CancelTryContinue:
                AddButton("Cancel", DialogResult.Cancel, isCancel: true);
                AddButton("Try Again", DialogResult.TryAgain);
                AddButton("Continue", DialogResult.Continue, isDefault: true);
                escapeResult = DialogResult.Cancel;
                break;
            default:
                AddButton("OK", DialogResult.OK, isDefault: true, isCancel: true);
                escapeResult = DialogResult.OK;
                break;
        }

        Result = escapeResult;
    }

    private static Control CreateIcon(MessageBoxIcon icon)
    {
        (string glyph, string color) = icon switch
        {
            MessageBoxIcon.Error => ("×", "#D13438"),
            MessageBoxIcon.Warning => ("!", "#F7A600"),
            MessageBoxIcon.Question => ("?", "#3A96DD"),
            _ => ("i", "#3A96DD"),
        };

        return new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.Parse(color)),
            Child = new TextBlock
            {
                Text = glyph,
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}
