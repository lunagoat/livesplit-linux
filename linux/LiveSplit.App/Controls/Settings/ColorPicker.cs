using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LiveSplit.UI;
using LiveSplit.View.Dialogs;
using System;
using System.Globalization;
using System.Windows.Forms;
using DrawingColor = System.Drawing.Color;

namespace LiveSplit.Controls.Settings;

public sealed class ColorSelection : SettingsBase
{
    private DrawingColor color;
    private double hue, saturation, brightness;
    private bool settingHsv;
    public DrawingColor Color
    {
        get => color;
        set
        {
            if (!SetField(ref color, value)) return;
            if (!settingHsv)
            {
                var hsv = Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B).ToHsv();
                if (hsv.S > 0) hue = hsv.H;
                saturation = hsv.S * 100;
                brightness = hsv.V * 100;
            }
            foreach (string name in new[] { nameof(Red), nameof(Green), nameof(Blue), nameof(Alpha), nameof(Hue), nameof(Saturation), nameof(Brightness), nameof(Hex) }) OnPropertyChanged(name);
        }
    }
    public int Red { get => color.R; set => Color = DrawingColor.FromArgb(Alpha, Math.Clamp(value, 0, 255), Green, Blue); }
    public int Green { get => color.G; set => Color = DrawingColor.FromArgb(Alpha, Red, Math.Clamp(value, 0, 255), Blue); }
    public int Blue { get => color.B; set => Color = DrawingColor.FromArgb(Alpha, Red, Green, Math.Clamp(value, 0, 255)); }
    public int Alpha { get => color.A; set => Color = DrawingColor.FromArgb(Math.Clamp(value, 0, 255), Red, Green, Blue); }
    public double Hue { get => hue; set => SetHsv(value, Saturation, Brightness); }
    public double Saturation { get => saturation; set => SetHsv(Hue, value, Brightness); }
    public double Brightness { get => brightness; set => SetHsv(Hue, Saturation, value); }
    public string Hex
    {
        get => color.ToArgb().ToString("X8", CultureInfo.InvariantCulture);
        set
        {
            string hex = value?.Trim().TrimStart('#');
            if (hex?.Length is 6 or 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                Color = DrawingColor.FromArgb(unchecked((int)(hex.Length == 6 ? argb | 0xff000000 : argb)));
        }
    }
    public void SetHsv(double hue, double saturation, double brightness)
    {
        this.hue = Math.Clamp(hue, 0, 360);
        this.saturation = Math.Clamp(saturation, 0, 100);
        this.brightness = Math.Clamp(brightness, 0, 100);
        var rgb = new HsvColor(Alpha / 255d, this.hue, this.saturation / 100, this.brightness / 100).ToRgb();
        settingHsv = true;
        Color = DrawingColor.FromArgb(rgb.A, rgb.R, rgb.G, rgb.B);
        settingHsv = false;
        OnPropertyChanged(nameof(Hue));
        OnPropertyChanged(nameof(Saturation));
        OnPropertyChanged(nameof(Brightness));
    }
}

public sealed class ColorSwatchButton : Button
{
    public static readonly StyledProperty<DrawingColor> ColorProperty = AvaloniaProperty.Register<ColorSwatchButton, DrawingColor>(nameof(Color), DrawingColor.Transparent, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public DrawingColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    private readonly ColorPreview preview = new() { Width = 28, Height = 24 };
    public ColorSwatchButton()
    {
        Padding = new Thickness(3);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        Content = preview;
        Click += async (_, _) =>
        {
            var dialog = new ColorPickerDialog(Color);
            if (await dialog.ShowModalAsync(TopLevel.GetTopLevel(this) as Window) == DialogResult.OK) SetCurrentValue(ColorProperty, dialog.Selection.Color);
        };
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty)
        {
            preview.Color = Color;
            ToolTip.SetTip(this, $"#{Color.ToArgb():X8}");
        }
    }
}

public sealed class ColorPreview : Control
{
    private DrawingColor color;
    public DrawingColor Color { get => color; set { color = value; InvalidateVisual(); } }
    public override void Render(DrawingContext context)
    {
        for (int y = 0; y < Bounds.Height; y += 8)
            for (int x = 0; x < Bounds.Width; x += 8)
                context.FillRectangle(((x + y) / 8 % 2 == 0) ? Brushes.White : Brushes.Gray, new Rect(x, y, Math.Min(8, Bounds.Width - x), Math.Min(8, Bounds.Height - y)));
        context.FillRectangle(new SolidColorBrush(Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B)), new Rect(Bounds.Size));
        context.DrawRectangle(new Pen(Brushes.Gray), new Rect(Bounds.Size));
    }
}

public sealed class ColorPickerDialog : DialogWindow
{
    public ColorSelection Selection { get; }
    public ColorPickerDialog(DrawingColor original) : base(SettingsPage.T("Color"))
    {
        Selection = new ColorSelection { Color = original };
        var page = new SettingsPage(Selection) { MinWidth = 440 };
        var square = new SaturationValuePanel(Selection) { Height = 180 };
        var old = new ColorPreview { Color = original, Height = 30 };
        var current = new ColorPreview { Color = original, Height = 30 };
        var reset = new Button { Content = old, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        reset.Click += (_, _) => Selection.Color = original;
        Selection.PropertyChanged += (_, _) => current.Color = Selection.Color;
        page.Add(square, page.Slider("Hue:", "Hue", 0, 360), page.Slider("Alpha:", "Alpha", 0, 255),
            page.Number("Red:", "Red", 0, 255), page.Number("Green:", "Green", 0, 255), page.Number("Blue:", "Blue", 0, 255),
            page.Number("Alpha:", "Alpha", 0, 255), page.Slider("Saturation:", "Saturation", 0, 100), page.Slider("Brightness:", "Brightness", 0, 100),
            page.Text("Hex (AARRGGBB):", "Hex"), page.Row("Original (click to restore):", reset), page.Row("Selected:", current));
        SetBody(page);
        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);
    }
}

internal sealed class SaturationValuePanel : Control
{
    private readonly ColorSelection selection;
    public SaturationValuePanel(ColorSelection selection)
    {
        this.selection = selection;
        selection.PropertyChanged += (_, _) => InvalidateVisual();
    }
    public override void Render(DrawingContext context)
    {
        var hue = new HsvColor(1, selection.Hue, 1, 1).ToRgb();
        context.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = new GradientStops { new GradientStop(Colors.White, 0), new GradientStop(hue, 1) } }, new Rect(Bounds.Size));
        context.FillRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = new GradientStops { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Black, 1) } }, new Rect(Bounds.Size));
        var center = new Point(selection.Saturation / 100 * Bounds.Width, (1 - selection.Brightness / 100) * Bounds.Height);
        context.DrawEllipse(null, new Pen(Brushes.Black, 3), center, 5, 5);
        context.DrawEllipse(null, new Pen(Brushes.White, 1), center, 5, 5);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Pointer.Capture(this);
        Select(e.GetPosition(this));
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (e.Pointer.Captured == this) Select(e.GetPosition(this));
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); e.Pointer.Capture(null); }
    private void Select(Point point) => selection.SetHsv(selection.Hue, point.X / Bounds.Width * 100, (1 - point.Y / Bounds.Height) * 100);
}
