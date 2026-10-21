using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LiveSplit.Controls.Settings;
using LiveSplit.Services;
using LiveSplit.UI.Components;
using System.Linq;

namespace LiveSplit.View.Dialogs;

public sealed class LayoutComponentSettingsWindow : SettingsHostWindow
{
    public LayoutComponentSettingsWindow(LayoutEditorRequest request) : base(SettingsPage.T("Layout Settings"), Build(request))
    {
        Width = 880;
        MinWidth = 820;
    }

    private static Control Build(LayoutEditorRequest request)
    {
        var host = new ContentControl();
        var components = request.Layout.Components.ToArray();
        var list = new ListBox { ItemsSource = components.Select(c => c.ComponentName).ToArray(), MinWidth = 170, VerticalAlignment = VerticalAlignment.Stretch };
        list.SelectionChanged += (_, _) =>
        {
            host.Content = list.SelectedIndex >= 0 ? ComponentSettingsViews.Create(components[list.SelectedIndex], request.Layout.Mode) : null;
        };
        var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*"), ColumnSpacing = 12 };
        Grid.SetColumn(host, 1);
        panel.Children.Add(list);
        panel.Children.Add(host);
        if (components.Length > 0) list.SelectedIndex = 0;
        return panel;
    }
}
