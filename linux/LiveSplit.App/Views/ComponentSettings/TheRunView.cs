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
using System.Threading;
using System.Threading.Tasks;

namespace LiveSplit.View.ComponentSettings;

public sealed class TheRunView : SettingsPage
{
    public TheRunView(object model) : base(model)
    {
        var header = new TextBlock
        {
            Text = T("therun.gg syncs your splits automatically and shows your live runs alongside your personal bests, stats, and history."),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Add(header);

        var linkBtn = new Button
        {
            Content = new TextBlock
            {
                Text = T("Get your upload key at therun.gg/livesplit"),
                TextDecorations = TextDecorations.Underline,
                Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#58a6ff"))
            },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(0, 0, 0, 6)
        };
        linkBtn.Click += (_, _) => PlatformServices.Current.OpenUrl("https://therun.gg/livesplit");
        Add(linkBtn);

        var passBox = new TextBox
        {
            PasswordChar = '•',
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Bind(passBox, TextBox.TextProperty, "UploadKey");

        var testBtn = new Button
        {
            MinWidth = 70,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        Bind(testBtn, Button.ContentProperty, "TestLabel", BindingMode.OneWay);
        if (Has("CanValidate"))
            Bind(testBtn, IsEnabledProperty, "CanValidate", BindingMode.OneWay);

        testBtn.Click += async (_, _) =>
        {
            var method = Model.GetType().GetMethod("ValidateKeyAsync", [typeof(CancellationToken)]);
            if (method != null)
            {
                var task = method.Invoke(Model, [default(CancellationToken)]) as Task;
                if (task != null) await task;
            }
        };

        var keyGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        keyGrid.Children.Add(passBox);
        Grid.SetColumn(testBtn, 1);
        keyGrid.Children.Add(testBtn);
        Add(Row("Upload Key", keyGrid));

        var statusStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4) };

        var statusError = new TextBlock
        {
            Foreground = Brushes.Tomato,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        Bind(statusError, TextBlock.TextProperty, "StatusMessage", BindingMode.OneWay);
        statusError.Bind(IsVisibleProperty, new Binding("StatusMessage")
        {
            Source = Model,
            Converter = new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s))
        });
        statusStack.Children.Add(statusError);

        var userBtn = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        var userText = new TextBlock
        {
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Avalonia.Media.Color.Parse("#58a6ff")),
            VerticalAlignment = VerticalAlignment.Center
        };
        userBtn.Bind(IsVisibleProperty, new Binding("ValidatedUsername")
        {
            Source = Model,
            Converter = new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s))
        });
        userBtn.Content = userText;
        userText.Bind(TextBlock.TextProperty, new Binding("ValidatedUsername")
        {
            Source = Model,
            StringFormat = "therun.gg/{0}"
        });
        userBtn.Click += (_, _) =>
        {
            string url = Read("ProfileUrl")?.ToString();
            if (!string.IsNullOrEmpty(url)) PlatformServices.Current.OpenUrl(url);
        };
        statusStack.Children.Add(userBtn);

        var liveBtn = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        var liveText = new TextBlock
        {
            Text = "Live",
            TextDecorations = TextDecorations.Underline,
            Foreground = Brushes.IndianRed,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        liveBtn.Bind(IsVisibleProperty, new Binding("ValidatedUsername")
        {
            Source = Model,
            Converter = new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s))
        });
        liveBtn.Content = liveText;
        liveBtn.Click += (_, _) =>
        {
            string url = Read("LiveUrl")?.ToString();
            if (!string.IsNullOrEmpty(url)) PlatformServices.Current.OpenUrl(url);
        };
        statusStack.Children.Add(liveBtn);

        Add(statusStack);

        var chkStats = Check("Enable Stats Sync", "IsStatsUploadingEnabled");
        ToolTip.SetTip(chkStats, T("Syncs your splits file to therun.gg after each completed run, on reset, and when closing LiveSplit.\nThis powers your profile stats, personal bests, and history."));
        Add(chkStats);

        var chkReset = Check("Sync on Reset", "IsUploadOnResetEnabled");
        ToolTip.SetTip(chkReset, T("When enabled, stats are synced every time you reset the timer.\nWhen disabled, stats are only synced after a completed run or when closing LiveSplit."));
        Add(chkReset);

        var chkLive = Check("Enable Live Tracking", "IsLiveTrackingEnabled");
        ToolTip.SetTip(chkLive, T("Sends real-time split data to therun.gg so your current run appears on the live page.\nViewers and other runners can follow your progress as it happens."));
        Add(chkLive);

        var chkLayout = Check("Upload Layout Path", "IsLayoutPathUploadEnabled");
        ToolTip.SetTip(chkLayout, T("Includes the layout file path in the uploaded splits file.\nDisabled by default because the path may contain personal information such as your Windows username.\nIf disabled, you will need to reload your layout when re-downloading your splits from therun.gg."));
        Add(chkLayout);
    }
}
