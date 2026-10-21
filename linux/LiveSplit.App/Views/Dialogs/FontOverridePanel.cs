using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Options;
using LiveSplit.UI.Components;
using System;
using DrawingFont = System.Drawing.Font;

namespace LiveSplit.View.Dialogs;

public sealed class FontOverridePanel : UserControl
{
    private FontOverrides overrides;
    private LayoutSettings globalSettings;

    private readonly Border container;
    private readonly StackPanel rowsPanel;

    private readonly Grid timerRow;
    private readonly CheckBox chkTimer;
    private readonly FontPickerButton btnTimer;
    private readonly TextBlock lblTimerFallback;

    private readonly Grid timesRow;
    private readonly CheckBox chkTimes;
    private readonly FontPickerButton btnTimes;
    private readonly TextBlock lblTimesFallback;

    private readonly Grid textRow;
    private readonly CheckBox chkText;
    private readonly FontPickerButton btnText;
    private readonly TextBlock lblTextFallback;

    public FontOverridePanel()
    {
        rowsPanel = new StackPanel { Spacing = 6 };

        (timerRow, chkTimer, btnTimer, lblTimerFallback) = CreateRow("Override Timer Font", 7, 20);
        (timesRow, chkTimes, btnTimes, lblTimesFallback) = CreateRow("Override Times Font", 11, 26);
        (textRow, chkText, btnText, lblTextFallback) = CreateRow("Override Text Font", 11, 26);

        rowsPanel.Children.Add(timerRow);
        rowsPanel.Children.Add(timesRow);
        rowsPanel.Children.Add(textRow);

        var header = new TextBlock
        {
            Text = SettingsPage.T("Font Overrides"),
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var mainPanel = new StackPanel { Spacing = 4 };
        mainPanel.Children.Add(header);
        mainPanel.Children.Add(rowsPanel);

        container = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 4),
            Child = mainPanel
        };

        Content = container;
    }

    private (Grid row, CheckBox chk, FontPickerButton btn, TextBlock fallback) CreateRow(string label, int minSize, int maxSize)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*,Auto"),
            ColumnSpacing = 8,
            MinHeight = 32
        };

        var chk = new CheckBox
        {
            Content = new TextBlock { Text = SettingsPage.T(label), TextWrapping = TextWrapping.Wrap },
            VerticalAlignment = VerticalAlignment.Center
        };

        var btn = new FontPickerButton
        {
            MinimumSize = minSize,
            MaximumSize = maxSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        var fallback = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            Margin = new Thickness(4, 0, 0, 0)
        };

        Grid.SetColumn(chk, 0);
        Grid.SetColumn(btn, 1);
        Grid.SetColumn(fallback, 2);

        grid.Children.Add(chk);
        grid.Children.Add(btn);
        grid.Children.Add(fallback);

        return (grid, chk, btn, fallback);
    }

    public void Bind(FontOverrides fontOverrides, LayoutSettings settings, GlobalFont usedFonts = GlobalFont.All)
    {
        overrides = fontOverrides ?? new FontOverrides();
        globalSettings = settings;

        bool showTimer = usedFonts.HasFlag(GlobalFont.TimerFont);
        bool showTimes = usedFonts.HasFlag(GlobalFont.TimesFont);
        bool showText = usedFonts.HasFlag(GlobalFont.TextFont);

        timerRow.IsVisible = showTimer;
        timesRow.IsVisible = showTimes;
        textRow.IsVisible = showText;

        IsVisible = showTimer || showTimes || showText;

        // Timer
        chkTimer.IsChecked = overrides.OverrideTimerFont;
        btnTimer.IsEnabled = overrides.OverrideTimerFont;
        btnTimer.Font = overrides.TimerFont ?? globalSettings?.TimerFont;
        lblTimerFallback.Text = FormatFontFallback(globalSettings?.TimerFont);
        lblTimerFallback.IsVisible = !overrides.OverrideTimerFont;

        chkTimer.IsCheckedChanged += (_, _) =>
        {
            bool isChecked = chkTimer.IsChecked == true;
            overrides.OverrideTimerFont = isChecked;
            btnTimer.IsEnabled = isChecked;
            lblTimerFallback.IsVisible = !isChecked;
            if (isChecked && overrides.TimerFont == null && globalSettings?.TimerFont != null)
            {
                overrides.TimerFont = globalSettings.TimerFont.Clone() as DrawingFont;
                btnTimer.Font = overrides.TimerFont;
            }
        };
        btnTimer.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
            {
                overrides.TimerFont = btnTimer.Font;
            }
        };

        // Times
        chkTimes.IsChecked = overrides.OverrideTimesFont;
        btnTimes.IsEnabled = overrides.OverrideTimesFont;
        btnTimes.Font = overrides.TimesFont ?? globalSettings?.TimesFont;
        lblTimesFallback.Text = FormatFontFallback(globalSettings?.TimesFont);
        lblTimesFallback.IsVisible = !overrides.OverrideTimesFont;

        chkTimes.IsCheckedChanged += (_, _) =>
        {
            bool isChecked = chkTimes.IsChecked == true;
            overrides.OverrideTimesFont = isChecked;
            btnTimes.IsEnabled = isChecked;
            lblTimesFallback.IsVisible = !isChecked;
            if (isChecked && overrides.TimesFont == null && globalSettings?.TimesFont != null)
            {
                overrides.TimesFont = globalSettings.TimesFont.Clone() as DrawingFont;
                btnTimes.Font = overrides.TimesFont;
            }
        };
        btnTimes.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
            {
                overrides.TimesFont = btnTimes.Font;
            }
        };

        // Text
        chkText.IsChecked = overrides.OverrideTextFont;
        btnText.IsEnabled = overrides.OverrideTextFont;
        btnText.Font = overrides.TextFont ?? globalSettings?.TextFont;
        lblTextFallback.Text = FormatFontFallback(globalSettings?.TextFont);
        lblTextFallback.IsVisible = !overrides.OverrideTextFont;

        chkText.IsCheckedChanged += (_, _) =>
        {
            bool isChecked = chkText.IsChecked == true;
            overrides.OverrideTextFont = isChecked;
            btnText.IsEnabled = isChecked;
            lblTextFallback.IsVisible = !isChecked;
            if (isChecked && overrides.TextFont == null && globalSettings?.TextFont != null)
            {
                overrides.TextFont = globalSettings.TextFont.Clone() as DrawingFont;
                btnText.Font = overrides.TextFont;
            }
        };
        btnText.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
            {
                overrides.TextFont = btnText.Font;
            }
        };
    }

    private static string FormatFontFallback(DrawingFont font)
    {
        if (font == null) return string.Empty;
        return $"(Global: {font.Name}, {font.Size:0.#}pt)";
    }
}
