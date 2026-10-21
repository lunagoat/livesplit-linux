using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

/// <summary>One or two text fields (InputBox).</summary>
public sealed class PromptWindow : DialogWindow
{
    private readonly TextBox first;
    private readonly TextBox second;

    public string Value1 => first.Text ?? string.Empty;
    public string Value2 => second?.Text ?? string.Empty;

    public PromptWindow(string title, string prompt1, string value1, string prompt2 = null, string value2 = null)
        : base(title)
    {
        MinWidth = 380;
        var body = new StackPanel { Spacing = 2 };
        body.Children.Add(Label(prompt1));
        first = new TextBox { Text = value1 ?? string.Empty, MinWidth = 340 };
        body.Children.Add(first);

        if (prompt2 != null)
        {
            second = new TextBox { Text = value2 ?? string.Empty, MinWidth = 340 };
            body.Children.Add(new Border { Height = 8 });
            body.Children.Add(Label(prompt2));
            body.Children.Add(second);
        }

        SetBody(body);
        AddButton("OK", DialogResult.OK, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);

        Opened += (_, _) =>
        {
            first.Focus();
            first.SelectAll();
        };
    }
}
