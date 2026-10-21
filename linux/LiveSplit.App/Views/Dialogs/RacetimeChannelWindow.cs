using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Hosting;
using System;
using System.ComponentModel;

namespace LiveSplit.View.Dialogs;

public class RacetimeChannelWindow : Window
{
    private readonly object form;
    private bool closingFromRequest;

    public RacetimeChannelWindow(object channelForm)
    {
        form = channelForm ?? throw new ArgumentNullException(nameof(channelForm));
        DataContext = form;

        Width = 1145;
        Height = 619;
        MinWidth = 600;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = MainWindow.AppIcon;
        Background = new SolidColorBrush(Color.Parse("#1e1e1e"));

        Bind(TitleProperty, new Binding("Title") { Source = form });
        if (form is INotifyPropertyChanged notify)
        {
            notify.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Title")
                {
                    string t = form.GetType().GetProperty("Title")?.GetValue(form)?.ToString();
                    if (!string.IsNullOrEmpty(t)) Title = t;
                }
            };
        }
        string initialTitle = form.GetType().GetProperty("Title")?.GetValue(form)?.ToString();
        if (!string.IsNullOrEmpty(initialTitle)) Title = initialTitle;

        if (form.GetType().GetProperty("AlwaysOnTop") != null)
        {
            Bind(TopmostProperty, new Binding("AlwaysOnTop") { Source = form, Mode = BindingMode.OneWay });
        }

        Content = BuildUi();
    }

    private Control BuildUi()
    {
        var root = new DockPanel { Margin = new Thickness(16) };

        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3e3e42")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(headerBorder, Dock.Top);

        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };

        var titleBlock = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White
        };
        titleBlock.Bind(TextBlock.TextProperty, new Binding("Title") { Source = form });
        headerGrid.Children.Add(titleBlock);

        var openBrowserBtn = new Button
        {
            Content = SettingsPage.T("Open in Browser"),
            Background = new SolidColorBrush(Color.Parse("#1f6feb")),
            Foreground = Brushes.White,
            Padding = new Thickness(12, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        openBrowserBtn.Click += (_, _) => OpenInBrowser();
        Grid.SetColumn(openBrowserBtn, 1);
        headerGrid.Children.Add(openBrowserBtn);

        headerBorder.Child = headerGrid;
        root.Children.Add(headerBorder);

        var bottomBar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(bottomBar, Dock.Bottom);

        var closeBtn = new Button
        {
            Content = SettingsPage.T("Close"),
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        closeBtn.Click += (_, _) => Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        bottomBar.Children.Add(closeBtn);

        var forfeitBtn = new Button
        {
            Content = SettingsPage.T("Forfeit"),
            MinWidth = 80,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        forfeitBtn.Click += (_, _) => Forfeit();
        DockPanel.SetDock(forfeitBtn, Dock.Right);
        bottomBar.Children.Add(forfeitBtn);

        var statusInfo = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray
        };
        statusInfo.Bind(TextBlock.TextProperty, new Binding("LoadingMessage") { Source = form });
        bottomBar.Children.Add(statusInfo);

        root.Children.Add(bottomBar);

        var centerPanel = new Grid();

        var loadingText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.Parse("#cccccc")),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        };
        loadingText.Bind(TextBlock.TextProperty, new Binding("LoadingMessage") { Source = form });
        centerPanel.Children.Add(loadingText);

        var activeBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3e3e42")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 600
        };
        activeBorder.Bind(IsVisibleProperty, new Binding("IsPageVisible") { Source = form });

        var cardStack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };

        var cardTitle = new TextBlock
        {
            Text = SettingsPage.T("Racetime.gg Race Room"),
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White
        };
        cardStack.Children.Add(cardTitle);

        var cardDesc = new TextBlock
        {
            Text = SettingsPage.T("Live race tracking, entrant status, and chat are mirrored in your default web browser."),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#aaaaaa")),
            Margin = new Thickness(0, 4, 0, 8)
        };
        cardStack.Children.Add(cardDesc);

        var urlBtn = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var urlText = new TextBlock
        {
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.Parse("#58a6ff")),
            FontSize = 14
        };
        urlText.Bind(TextBlock.TextProperty, new Binding("PageUrl") { Source = form });
        urlBtn.Content = urlText;
        urlBtn.Click += (_, _) => OpenInBrowser();
        cardStack.Children.Add(urlBtn);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };

        var launchBtn = new Button
        {
            Content = SettingsPage.T("Open Race Room in Browser"),
            Background = new SolidColorBrush(Color.Parse("#238636")),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8),
            FontWeight = FontWeight.SemiBold
        };
        launchBtn.Click += (_, _) => OpenInBrowser();
        btnRow.Children.Add(launchBtn);

        var copyBtn = new Button
        {
            Content = SettingsPage.T("Copy Link"),
            Padding = new Thickness(14, 8)
        };
        copyBtn.Click += (_, _) => CopyLink();
        btnRow.Children.Add(copyBtn);

        cardStack.Children.Add(btnRow);
        activeBorder.Child = cardStack;
        centerPanel.Children.Add(activeBorder);

        root.Children.Add(centerPanel);
        return root;
    }

    private void OpenInBrowser()
    {
        string url = form.GetType().GetProperty("PageUrl")?.GetValue(form)?.ToString();
        if (!string.IsNullOrEmpty(url))
        {
            PlatformServices.Current.OpenUrl(url);
        }
    }

    private void CopyLink()
    {
        string url = form.GetType().GetProperty("PageUrl")?.GetValue(form)?.ToString();
        if (!string.IsNullOrEmpty(url))
        {
            PlatformServices.Current.SetClipboardText(url);
        }
    }

    private void Forfeit()
    {
        var channel = form.GetType().GetProperty("Channel")?.GetValue(form);
        channel?.GetType().GetMethod("Forfeit")?.Invoke(channel, null);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!closingFromRequest)
        {
            var method = form.GetType().GetMethod("RequestClose");
            bool canClose = (bool)(method?.Invoke(form, null) ?? true);
            if (!canClose)
            {
                e.Cancel = true;
                return;
            }
            closingFromRequest = true;
        }
    }
}
