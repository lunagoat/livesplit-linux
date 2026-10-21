using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class RaceProviderManagingDialog : DialogWindow
{
    public IList<RaceProviderSettings> Settings { get; }

    private readonly ListBox providerListBox;
    private readonly Button btnWebsite;
    private readonly Button btnRules;
    private readonly ContentControl settingsHost;

    public RaceProviderManagingDialog(IList<RaceProviderSettings> settings)
        : base(SettingsPage.T("Manage Racing Services"))
    {
        Settings = settings;

        Width = 600;
        Height = 440;
        MinWidth = 500;
        MinHeight = 350;
        CanResize = true;

        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*"),
            ColumnSpacing = 12
        };

        // Left list of providers
        providerListBox = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var items = new List<CheckBox>();
        for (int i = 0; i < Settings.Count; i++)
        {
            int index = i;
            var provider = Settings[index];
            var chk = new CheckBox
            {
                Content = provider.DisplayName,
                IsChecked = provider.Enabled
            };
            chk.IsCheckedChanged += (_, _) =>
            {
                provider.Enabled = chk.IsChecked == true;
            };
            items.Add(chk);
        }
        providerListBox.ItemsSource = items;

        var leftBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Child = providerListBox
        };
        mainGrid.Children.Add(leftBorder);

        // Right details panel
        var rightStack = new StackPanel { Spacing = 10, Margin = new Thickness(4) };

        var linkRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnWebsite = new Button { Content = SettingsPage.T("Website"), IsVisible = false };
        btnRules = new Button { Content = SettingsPage.T("Rules"), IsVisible = false };
        linkRow.Children.Add(btnWebsite);
        linkRow.Children.Add(btnRules);
        rightStack.Children.Add(linkRow);

        settingsHost = new ContentControl();
        var detailsBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = settingsHost
        };
        rightStack.Children.Add(detailsBorder);

        Grid.SetColumn(rightStack, 1);
        mainGrid.Children.Add(rightStack);

        SetBody(mainGrid);

        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);

        providerListBox.SelectionChanged += (_, _) =>
        {
            int idx = providerListBox.SelectedIndex;
            if (idx >= 0 && idx < Settings.Count)
            {
                var provider = Settings[idx];
                btnWebsite.IsVisible = !string.IsNullOrEmpty(provider.WebsiteLink);
                btnRules.IsVisible = !string.IsNullOrEmpty(provider.RulesLink);

                btnWebsite.Click -= OnWebsiteClick;
                btnRules.Click -= OnRulesClick;

                btnWebsite.Click += OnWebsiteClick;
                btnRules.Click += OnRulesClick;

                object control = provider.GetSettingsControl();
                if (control is Control avaloniaControl)
                {
                    settingsHost.Content = avaloniaControl;
                }
                else
                {
                    settingsHost.Content = new TextBlock
                    {
                        Text = provider.SettingsMessage ?? SettingsPage.T("There are no options available for this racing service."),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#888888"))
                    };
                }
            }
            else
            {
                btnWebsite.IsVisible = false;
                btnRules.IsVisible = false;
                settingsHost.Content = null;
            }
        };

        if (items.Count > 0)
        {
            providerListBox.SelectedIndex = 0;
        }
    }

    private void OnWebsiteClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int idx = providerListBox.SelectedIndex;
        if (idx >= 0 && idx < Settings.Count && !string.IsNullOrEmpty(Settings[idx].WebsiteLink))
        {
            try { Process.Start(new ProcessStartInfo(Settings[idx].WebsiteLink) { UseShellExecute = true }); } catch { }
        }
    }

    private void OnRulesClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        int idx = providerListBox.SelectedIndex;
        if (idx >= 0 && idx < Settings.Count && !string.IsNullOrEmpty(Settings[idx].RulesLink))
        {
            try { Process.Start(new ProcessStartInfo(Settings[idx].RulesLink) { UseShellExecute = true }); } catch { }
        }
    }
}
