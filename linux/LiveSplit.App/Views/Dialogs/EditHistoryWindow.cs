using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

/// <summary>Lets the user remove entries of the recent splits / layouts lists. Port of EditHistoryDialog.</summary>
public sealed class EditHistoryWindow : DialogWindow
{
    private readonly ObservableCollection<string> shown;
    private readonly ListBox list;
    private readonly List<string> history;

    /// <summary>The remaining entries in the original (oldest first) order after the dialog was accepted.</summary>
    public IList<string> History => history;

    /// <param name="mostRecentLast">The list as stored in the settings (most recent last).</param>
    public EditHistoryWindow(IEnumerable<string> mostRecentLast)
        : base("Edit History")
    {
        history = [.. mostRecentLast.Reverse()];
        shown = [.. history.Where(x => !string.IsNullOrEmpty(x))];

        list = new ListBox
        {
            ItemsSource = shown,
            SelectionMode = SelectionMode.Multiple,
            Width = 560,
            Height = 280,
        };
        var remove = new Button { Content = "Remove Selected", Margin = new Thickness(0, 8, 0, 0) };
        remove.Click += (_, _) => RemoveSelected();

        var body = new StackPanel();
        body.Children.Add(list);
        body.Children.Add(remove);
        SetBody(body);

        AddButton("OK", () =>
        {
            history.Reverse();
            Close(DialogResult.OK);
        }, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);
    }

    private void RemoveSelected()
    {
        foreach (string item in list.SelectedItems.Cast<string>().ToList())
        {
            history.Remove(item);
            shown.Remove(item);
        }
    }
}
