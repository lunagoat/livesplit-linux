using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LiveSplit.Controls.Settings;
using LiveSplit.Options;
using LiveSplit.UI;
using System;
using System.IO;
using System.Linq;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using Image = System.Drawing.Image;

namespace LiveSplit.View.Dialogs;

public sealed class LayoutSettingsControl : UserControl
{
    public LayoutSettings Settings { get; }
    public ILayout Layout { get; }
    public Image OriginalBackgroundImage { get; }

    private readonly ComboBox cmbBackgroundType;
    private readonly ColorSwatchButton btnBgColor1;
    private readonly ColorSwatchButton btnBgColor2;
    private readonly TextBlock lblBgColor1;
    private readonly TextBlock lblBgColor2;
    private readonly Grid imageControlsRow;
    private readonly TextBlock lblImageName;
    private readonly Slider trkImageOpacity;
    private readonly Slider trkImageBlur;
    private readonly TextBlock lblImageOpacityVal;
    private readonly TextBlock lblImageBlurVal;

    private readonly ColorSwatchButton btnGlod;
    private readonly TextBlock lblGlod;
    private readonly ColorSwatchButton btnTextOutline;
    private readonly TextBlock lblTextOutline;

    public LayoutSettingsControl(LayoutSettings settings, ILayout layout)
    {
        Settings = settings;
        Layout = layout;
        OriginalBackgroundImage = settings.BackgroundImage;

        var mainStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        // ---------------- BACKGROUND SECTION ----------------
        var bgPanel = new StackPanel { Spacing = 8 };
        bgPanel.Children.Add(new TextBlock { Text = SettingsPage.T("Background"), FontWeight = FontWeight.SemiBold });

        var bgTypeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*"), ColumnSpacing = 8 };
        bgTypeRow.Children.Add(new TextBlock { Text = SettingsPage.T("Background Type:"), VerticalAlignment = VerticalAlignment.Center });
        cmbBackgroundType = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "Solid Color", "Vertical Gradient", "Horizontal Gradient", "Image" }
        };
        cmbBackgroundType.SelectedItem = GetBackgroundTypeString(settings.BackgroundType);
        Grid.SetColumn(cmbBackgroundType, 1);
        bgTypeRow.Children.Add(cmbBackgroundType);
        bgPanel.Children.Add(bgTypeRow);

        // Color swatches row
        var colorRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,Auto,20,Auto,Auto"), ColumnSpacing = 8 };
        lblBgColor1 = new TextBlock { Text = SettingsPage.T("Color:"), VerticalAlignment = VerticalAlignment.Center };
        btnBgColor1 = new ColorSwatchButton { Color = settings.BackgroundColor };
        lblBgColor2 = new TextBlock { Text = SettingsPage.T("Color 2:"), VerticalAlignment = VerticalAlignment.Center };
        btnBgColor2 = new ColorSwatchButton { Color = settings.BackgroundColor2 };

        Grid.SetColumn(lblBgColor1, 0);
        Grid.SetColumn(btnBgColor1, 1);
        Grid.SetColumn(lblBgColor2, 3);
        Grid.SetColumn(btnBgColor2, 4);
        colorRow.Children.Add(lblBgColor1);
        colorRow.Children.Add(btnBgColor1);
        colorRow.Children.Add(lblBgColor2);
        colorRow.Children.Add(btnBgColor2);
        bgPanel.Children.Add(colorRow);

        btnBgColor1.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSwatchButton.ColorProperty)
                settings.BackgroundColor = btnBgColor1.Color;
        };
        btnBgColor2.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSwatchButton.ColorProperty)
                settings.BackgroundColor2 = btnBgColor2.Color;
        };

        // Image controls
        imageControlsRow = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnSpacing = 8, RowSpacing = 6 };

        var btnBrowseImage = new Button { Content = SettingsPage.T("Browse Image..."), MinWidth = 120 };
        var btnClearImage = new Button { Content = SettingsPage.T("Clear"), MinWidth = 60 };
        lblImageName = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.Parse("#888888")), Margin = new Thickness(6, 0, 0, 0) };
        var imageBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        imageBtns.Children.Add(btnBrowseImage);
        imageBtns.Children.Add(btnClearImage);
        imageBtns.Children.Add(lblImageName);

        imageControlsRow.Children.Add(new TextBlock { Text = SettingsPage.T("Image:"), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(imageBtns, 1);
        imageControlsRow.Children.Add(imageBtns);

        btnBrowseImage.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = SettingsPage.T("Set Background Image..."),
                    AllowMultiple = false,
                    FileTypeFilter = [
                        new FilePickerFileType("Image Files")
                        {
                            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"]
                        },
                        FilePickerFileTypes.All
                    ]
                });
                var file = files.FirstOrDefault();
                if (file != null)
                {
                    string localPath = file.TryGetLocalPath();
                    if (localPath != null && File.Exists(localPath))
                    {
                        try
                        {
                            var img = Image.FromFile(localPath);
                            if (settings.BackgroundImage != null && settings.BackgroundImage != OriginalBackgroundImage)
                                settings.BackgroundImage.Dispose();
                            settings.BackgroundImage = img;
                            lblImageName.Text = Path.GetFileName(localPath);
                        }
                        catch { }
                    }
                }
            }
        };

        btnClearImage.Click += (_, _) =>
        {
            if (settings.BackgroundImage != null && settings.BackgroundImage != OriginalBackgroundImage)
                settings.BackgroundImage.Dispose();
            settings.BackgroundImage = null;
            lblImageName.Text = SettingsPage.T("(None)");
        };

        // Image Opacity slider
        var lblImgOpacity = new TextBlock { Text = SettingsPage.T("Image Opacity:"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(lblImgOpacity, 1);
        imageControlsRow.Children.Add(lblImgOpacity);

        var opacityStack = new Grid { ColumnDefinitions = new ColumnDefinitions("*,45"), ColumnSpacing = 8 };
        trkImageOpacity = new Slider { Minimum = 0, Maximum = 100, Value = settings.ImageOpacity * 100f, TickFrequency = 1, IsSnapToTickEnabled = true };
        lblImageOpacityVal = new TextBlock { Text = $"{(int)(settings.ImageOpacity * 100f)}%", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(trkImageOpacity, 0);
        Grid.SetColumn(lblImageOpacityVal, 1);
        opacityStack.Children.Add(trkImageOpacity);
        opacityStack.Children.Add(lblImageOpacityVal);
        Grid.SetRow(opacityStack, 1);
        Grid.SetColumn(opacityStack, 1);
        imageControlsRow.Children.Add(opacityStack);

        trkImageOpacity.ValueChanged += (_, _) =>
        {
            settings.ImageOpacity = (float)(trkImageOpacity.Value / 100f);
            lblImageOpacityVal.Text = $"{(int)trkImageOpacity.Value}%";
        };

        // Image Blur slider
        var lblImgBlur = new TextBlock { Text = SettingsPage.T("Image Blur:"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(lblImgBlur, 2);
        imageControlsRow.Children.Add(lblImgBlur);

        var blurStack = new Grid { ColumnDefinitions = new ColumnDefinitions("*,45"), ColumnSpacing = 8 };
        trkImageBlur = new Slider { Minimum = 0, Maximum = 100, Value = settings.ImageBlur * 100f, TickFrequency = 1, IsSnapToTickEnabled = true };
        lblImageBlurVal = new TextBlock { Text = $"{(int)(settings.ImageBlur * 100f)}%", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(trkImageBlur, 0);
        Grid.SetColumn(lblImageBlurVal, 1);
        blurStack.Children.Add(trkImageBlur);
        blurStack.Children.Add(lblImageBlurVal);
        Grid.SetRow(blurStack, 2);
        Grid.SetColumn(blurStack, 1);
        imageControlsRow.Children.Add(blurStack);

        trkImageBlur.ValueChanged += (_, _) =>
        {
            settings.ImageBlur = (float)(trkImageBlur.Value / 100f);
            lblImageBlurVal.Text = $"{(int)trkImageBlur.Value}%";
        };

        bgPanel.Children.Add(imageControlsRow);

        cmbBackgroundType.SelectionChanged += (_, _) => UpdateBackgroundTypeVisibility();
        UpdateBackgroundTypeVisibility();

        mainStack.Children.Add(Card(bgPanel));

        // ---------------- COLORS SECTION ----------------
        var colorsPanel = new StackPanel { Spacing = 8 };
        colorsPanel.Children.Add(new TextBlock { Text = SettingsPage.T("Colors"), FontWeight = FontWeight.SemiBold });

        var colorsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,Auto,24,160,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 8
        };

        // Row 0: Text Color, Personal Best
        AddColorCell(colorsGrid, 0, 0, "Text Color:", settings.TextColor, c => settings.TextColor = c);
        AddColorCell(colorsGrid, 0, 3, "Personal Best:", settings.PersonalBestColor, c => settings.PersonalBestColor = c);

        // Row 1: Thin Separators, Best Segment (Glod)
        AddColorCell(colorsGrid, 1, 0, "Thin Separators:", settings.ThinSeparatorsColor, c => settings.ThinSeparatorsColor = c);
        (lblGlod, btnGlod) = AddColorCell(colorsGrid, 1, 3, "Best Segment:", settings.BestSegmentColor, c => settings.BestSegmentColor = c);

        // Row 2: Separators, Ahead (Gaining)
        AddColorCell(colorsGrid, 2, 0, "Separators:", settings.SeparatorsColor, c => settings.SeparatorsColor = c);
        AddColorCell(colorsGrid, 2, 3, "Ahead (Gaining):", settings.AheadGainingTimeColor, c => settings.AheadGainingTimeColor = c);

        // Row 3: Shadows, Ahead (Losing)
        AddColorCell(colorsGrid, 3, 0, "Shadows:", settings.ShadowsColor, c => settings.ShadowsColor = c);
        AddColorCell(colorsGrid, 3, 3, "Ahead (Losing):", settings.AheadLosingTimeColor, c => settings.AheadLosingTimeColor = c);

        // Row 4: Outlines, Behind (Gaining)
        (lblTextOutline, btnTextOutline) = AddColorCell(colorsGrid, 4, 0, "Text Outlines:", settings.TextOutlineColor, c => settings.TextOutlineColor = c);
        AddColorCell(colorsGrid, 4, 3, "Behind (Gaining):", settings.BehindGainingTimeColor, c => settings.BehindGainingTimeColor = c);

        // Row 5: Not Running, Behind (Losing)
        AddColorCell(colorsGrid, 5, 0, "Not Running:", settings.NotRunningColor, c => settings.NotRunningColor = c);
        AddColorCell(colorsGrid, 5, 3, "Behind (Losing):", settings.BehindLosingTimeColor, c => settings.BehindLosingTimeColor = c);

        // Row 6: Paused
        AddColorCell(colorsGrid, 6, 0, "Paused:", settings.PausedColor, c => settings.PausedColor = c);

        colorsPanel.Children.Add(colorsGrid);
        mainStack.Children.Add(Card(colorsPanel));

        // ---------------- FONTS SECTION ----------------
        var fontsPanel = new StackPanel { Spacing = 8 };
        fontsPanel.Children.Add(new TextBlock { Text = SettingsPage.T("Fonts"), FontWeight = FontWeight.SemiBold });

        var fontsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("160,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 8
        };

        // Timer Font
        fontsGrid.Children.Add(new TextBlock { Text = SettingsPage.T("Timer Font:"), VerticalAlignment = VerticalAlignment.Center });
        var btnTimerFont = new FontPickerButton { Font = settings.TimerFont, MinimumSize = 7, MaximumSize = 26 };
        btnTimerFont.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
                settings.TimerFont = btnTimerFont.Font;
        };
        Grid.SetColumn(btnTimerFont, 1);
        fontsGrid.Children.Add(btnTimerFont);

        // Times Font
        var lblTimesFont = new TextBlock { Text = SettingsPage.T("Times Font:"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(lblTimesFont, 1);
        fontsGrid.Children.Add(lblTimesFont);
        var btnTimesFont = new FontPickerButton { Font = settings.TimesFont, MinimumSize = 11, MaximumSize = 26 };
        btnTimesFont.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
                settings.TimesFont = btnTimesFont.Font;
        };
        Grid.SetRow(btnTimesFont, 1);
        Grid.SetColumn(btnTimesFont, 1);
        fontsGrid.Children.Add(btnTimesFont);

        // Text Font
        var lblTextFont = new TextBlock { Text = SettingsPage.T("Text Font:"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(lblTextFont, 2);
        fontsGrid.Children.Add(lblTextFont);
        var btnTextFont = new FontPickerButton { Font = settings.TextFont, MinimumSize = 11, MaximumSize = 26 };
        btnTextFont.PropertyChanged += (_, e) =>
        {
            if (e.Property == FontPickerButton.FontProperty)
                settings.TextFont = btnTextFont.Font;
        };
        Grid.SetRow(btnTextFont, 2);
        Grid.SetColumn(btnTextFont, 1);
        fontsGrid.Children.Add(btnTextFont);

        fontsPanel.Children.Add(fontsGrid);
        mainStack.Children.Add(Card(fontsPanel));

        // ---------------- GENERAL OPTIONS SECTION ----------------
        var optionsPanel = new StackPanel { Spacing = 8 };
        optionsPanel.Children.Add(new TextBlock { Text = SettingsPage.T("General Options"), FontWeight = FontWeight.SemiBold });

        // Opacity slider
        var opGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*,45"), ColumnSpacing = 8 };
        opGrid.Children.Add(new TextBlock { Text = SettingsPage.T("Window Opacity:"), VerticalAlignment = VerticalAlignment.Center });
        var trkOpacity = new Slider { Minimum = 10, Maximum = 100, Value = Math.Max(10, settings.Opacity * 100f), TickFrequency = 1, IsSnapToTickEnabled = true };
        var lblOpacityVal = new TextBlock { Text = $"{(int)trkOpacity.Value}%", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(trkOpacity, 1);
        Grid.SetColumn(lblOpacityVal, 2);
        opGrid.Children.Add(trkOpacity);
        opGrid.Children.Add(lblOpacityVal);
        trkOpacity.ValueChanged += (_, _) =>
        {
            settings.Opacity = (float)(trkOpacity.Value / 100f);
            lblOpacityVal.Text = $"{(int)trkOpacity.Value}%";
        };
        optionsPanel.Children.Add(opGrid);

        var chkGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 6,
            ColumnSpacing = 16
        };

        var chkAlwaysOnTop = CheckBoxOption("Always on Top", settings.AlwaysOnTop, v => settings.AlwaysOnTop = v);
        var chkShowBest = CheckBoxOption("Show Best Segments", settings.ShowBestSegments, v => settings.ShowBestSegments = v);
        var chkAntiAliasing = CheckBoxOption("Anti-Aliasing", settings.AntiAliasing, v =>
        {
            settings.AntiAliasing = v;
            lblTextOutline.IsEnabled = btnTextOutline.IsEnabled = v;
        });
        var chkDropShadows = CheckBoxOption("Drop Shadows", settings.DropShadows, v => settings.DropShadows = v);
        var chkRainbow = CheckBoxOption("Rainbow Best Segment", settings.UseRainbowColor, v =>
        {
            settings.UseRainbowColor = v;
            lblGlod.IsEnabled = btnGlod.IsEnabled = !v;
        });
        var chkPassThrough = CheckBoxOption("Mouse Pass-Through While Running", settings.MousePassThroughWhileRunning, v => settings.MousePassThroughWhileRunning = v);
        var chkAllowResizing = CheckBoxOption("Allow Resizing", settings.AllowResizing, v => settings.AllowResizing = v);
        var chkAllowMoving = CheckBoxOption("Allow Moving", settings.AllowMoving, v => settings.AllowMoving = v);

        lblTextOutline.IsEnabled = btnTextOutline.IsEnabled = settings.AntiAliasing;
        lblGlod.IsEnabled = btnGlod.IsEnabled = !settings.UseRainbowColor;

        chkGrid.Children.Add(chkAlwaysOnTop);
        Grid.SetColumn(chkShowBest, 1);
        chkGrid.Children.Add(chkShowBest);

        Grid.SetRow(chkAntiAliasing, 1);
        chkGrid.Children.Add(chkAntiAliasing);
        Grid.SetRow(chkDropShadows, 1);
        Grid.SetColumn(chkDropShadows, 1);
        chkGrid.Children.Add(chkDropShadows);

        Grid.SetRow(chkRainbow, 2);
        chkGrid.Children.Add(chkRainbow);
        Grid.SetRow(chkPassThrough, 2);
        Grid.SetColumn(chkPassThrough, 1);
        chkGrid.Children.Add(chkPassThrough);

        Grid.SetRow(chkAllowResizing, 3);
        chkGrid.Children.Add(chkAllowResizing);
        Grid.SetRow(chkAllowMoving, 3);
        Grid.SetColumn(chkAllowMoving, 1);
        chkGrid.Children.Add(chkAllowMoving);

        optionsPanel.Children.Add(chkGrid);
        mainStack.Children.Add(Card(optionsPanel));

        Content = new ScrollViewer
        {
            Content = mainStack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
    }

    private static (TextBlock lbl, ColorSwatchButton btn) AddColorCell(Grid grid, int row, int col, string label, DrawingColor initial, Action<DrawingColor> setter)
    {
        var lbl = new TextBlock { Text = SettingsPage.T(label), VerticalAlignment = VerticalAlignment.Center };
        var btn = new ColorSwatchButton { Color = initial };
        btn.PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorSwatchButton.ColorProperty)
                setter(btn.Color);
        };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, col);
        Grid.SetRow(btn, row);
        Grid.SetColumn(btn, col + 1);
        grid.Children.Add(lbl);
        grid.Children.Add(btn);
        return (lbl, btn);
    }

    private static CheckBox CheckBoxOption(string label, bool initial, Action<bool> setter)
    {
        var chk = new CheckBox
        {
            Content = new TextBlock { Text = SettingsPage.T(label), TextWrapping = TextWrapping.Wrap },
            IsChecked = initial
        };
        chk.IsCheckedChanged += (_, _) => setter(chk.IsChecked == true);
        return chk;
    }

    private static Border Card(Control child)
    {
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = child
        };
    }

    private string GetBackgroundTypeString(BackgroundType type)
    {
        return type switch
        {
            BackgroundType.HorizontalGradient => "Horizontal Gradient",
            BackgroundType.VerticalGradient => "Vertical Gradient",
            BackgroundType.Image => "Image",
            _ => "Solid Color",
        };
    }

    private void UpdateBackgroundTypeVisibility()
    {
        string selected = cmbBackgroundType.SelectedItem?.ToString() ?? "Solid Color";
        bool isGradient = selected is "Vertical Gradient" or "Horizontal Gradient";
        bool isImage = selected == "Image";

        lblBgColor1.IsVisible = btnBgColor1.IsVisible = !isImage;
        lblBgColor2.IsVisible = btnBgColor2.IsVisible = isGradient;
        imageControlsRow.IsVisible = isImage;

        if (isImage)
        {
            Settings.BackgroundType = BackgroundType.Image;
            lblImageName.Text = Settings.BackgroundImage != null ? SettingsPage.T("Image loaded") : SettingsPage.T("(None)");
        }
        else if (isGradient)
        {
            Settings.BackgroundType = selected == "VerticalGradient" || selected == "Vertical Gradient"
                ? BackgroundType.VerticalGradient
                : BackgroundType.HorizontalGradient;
        }
        else
        {
            Settings.BackgroundType = BackgroundType.SolidColor;
        }
    }
}
