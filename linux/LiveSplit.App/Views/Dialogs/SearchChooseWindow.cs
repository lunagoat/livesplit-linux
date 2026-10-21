using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

/// <summary>Search box plus result list (the Twitch game picker). SelectedIndex -1 with OK means "no game".</summary>
public sealed class SearchChooseWindow : DialogWindow
{
    private readonly TextBox query;
    private readonly ListBox results;
    private readonly Func<string, IReadOnlyList<string>> search;

    public int SelectedIndex { get; private set; } = -1;

    public SearchChooseWindow(string title, string text, string initialQuery, Func<string, IReadOnlyList<string>> search)
        : base(title)
    {
        this.search = search;
        MinWidth = 440;

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(Label(text));

        query = new TextBox { Text = initialQuery ?? string.Empty, MinWidth = 300 };
        var searchButton = new Button { Content = "Search" };
        searchButton.Click += (_, _) => Search();
        var row = new DockPanel();
        DockPanel.SetDock(searchButton, Dock.Right);
        searchButton.Margin = new Thickness(6, 0, 0, 0);
        row.Children.Add(searchButton);
        row.Children.Add(query);
        body.Children.Add(row);

        results = new ListBox { Height = 220 };
        results.DoubleTapped += (_, _) => Accept();
        body.Children.Add(results);
        SetBody(body);

        AddButton("OK", Accept, isDefault: false);
        AddButton("No Game", () =>
        {
            SelectedIndex = -1;
            Close(DialogResult.OK);
        });
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);

        query.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Search();
                e.Handled = true;
            }
        };

        Opened += (_, _) =>
        {
            if (!string.IsNullOrEmpty(query.Text))
            {
                Search();
            }

            query.Focus();
        };
    }

    private void Search()
    {
        try
        {
            IReadOnlyList<string> found = search(query.Text ?? string.Empty) ?? [];
            results.ItemsSource = new List<string>(found);
            if (found.Count > 0)
            {
                results.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            LiveSplit.Options.Log.Error(ex);
            results.ItemsSource = new List<string>();
        }
    }

    private void Accept()
    {
        if (results.SelectedIndex >= 0)
        {
            SelectedIndex = results.SelectedIndex;
            Close(DialogResult.OK);
        }
    }
}
