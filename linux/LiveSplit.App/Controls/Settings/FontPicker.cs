using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.View.Dialogs;
using System;
using System.Linq;
using System.Windows.Forms;
using DrawingFont = System.Drawing.Font;
using DrawingFamily = System.Drawing.FontFamily;
using DrawingStyle = System.Drawing.FontStyle;

namespace LiveSplit.Controls.Settings;

public sealed class FontPickerButton : Button
{
    public static readonly StyledProperty<DrawingFont> FontProperty = AvaloniaProperty.Register<FontPickerButton, DrawingFont>(nameof(Font), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public DrawingFont Font { get => GetValue(FontProperty); set => SetValue(FontProperty, value); }
    public int MinimumSize { get; set; } = 7;
    public int MaximumSize { get; set; } = 26;
    public FontPickerButton()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Click += async (_, _) =>
        {
            var original = Font;
            var dialog = new FontPickerDialog(original, MinimumSize, MaximumSize);
            dialog.FontChanged += (_, _) => SetCurrentValue(FontProperty, dialog.SelectedFont);
            if (await dialog.ShowModalAsync(TopLevel.GetTopLevel(this) as Window) != DialogResult.OK) SetCurrentValue(FontProperty, original);
        };
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontProperty) Content = Font == null ? SettingsPage.T("Choose...") : $"{Font.Name}, {Font.Size:0.#}, {Font.Style}  —  {SettingsPage.T("Choose...")}";
    }
}

public sealed class FontPickerDialog : DialogWindow
{
    public DrawingFont SelectedFont { get; private set; }
    public event EventHandler FontChanged;
    public FontPickerDialog(DrawingFont original, int minSize = 7, int maxSize = 26) : base(SettingsPage.T("Font"))
    {
        original ??= new DrawingFont("Segoe UI", 13, DrawingStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        SelectedFont = original;
        var names = DrawingFamily.Families.Select(f => f.Name).Append(original.Name).Distinct().OrderBy(n => n).ToArray();
        var families = new ListBox { ItemsSource = names, SelectedItem = original.Name, Height = 220 };
        var search = new TextBox { Watermark = SettingsPage.T("Font") };
        search.TextChanged += (_, _) => families.ItemsSource = names.Where(n => n.Contains(search.Text ?? "", StringComparison.CurrentCultureIgnoreCase)).ToArray();
        var size = new NumericUpDown { Minimum = minSize, Maximum = maxSize, Increment = 1, Value = (decimal)Math.Clamp(original.Size, minSize, maxSize), FormatString = "0.##" };
        var sizes = new ComboBox { ItemsSource = new[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 }.Where(n => n >= minSize && n <= maxSize).ToArray(), PlaceholderText = SettingsPage.T("Size") };
        sizes.SelectionChanged += (_, _) => { if (sizes.SelectedItem is int value) size.Value = value; };
        var bold = new CheckBox { Content = SettingsPage.T("Bold"), IsChecked = original.Bold };
        var italic = new CheckBox { Content = SettingsPage.T("Italic"), IsChecked = original.Italic };
        var preview = new TextBlock { Text = "AaBbYyZz 0123456789", MinHeight = 60, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        bool updating = false;
        void Update()
        {
            if (updating || families.SelectedItem is not string name || size.Value == null) return;
            updating = true;
            var family = new DrawingFamily(name);
            bool regularAvailable = family.IsStyleAvailable(DrawingStyle.Regular);
            bool boldAvailable = family.IsStyleAvailable(DrawingStyle.Bold);
            bool italicAvailable = family.IsStyleAvailable(DrawingStyle.Italic);
            bold.IsEnabled = boldAvailable && (regularAvailable || italicAvailable);
            if (!boldAvailable) bold.IsChecked = false;
            else if (!regularAvailable && !italicAvailable) bold.IsChecked = true;
            italic.IsEnabled = italicAvailable && (regularAvailable || boldAvailable);
            if (!italicAvailable) italic.IsChecked = false;
            else if (!regularAvailable && !boldAvailable) italic.IsChecked = true;
            var style = (bold.IsChecked == true ? DrawingStyle.Bold : 0) | (italic.IsChecked == true ? DrawingStyle.Italic : 0);
            SelectedFont = new DrawingFont(family, (float)size.Value.Value, style, System.Drawing.GraphicsUnit.Pixel);
            preview.FontFamily = new FontFamily(name);
            preview.FontSize = SelectedFont.Size;
            preview.FontWeight = SelectedFont.Bold ? FontWeight.Bold : FontWeight.Normal;
            preview.FontStyle = SelectedFont.Italic ? FontStyle.Italic : FontStyle.Normal;
            updating = false;
            FontChanged?.Invoke(this, EventArgs.Empty);
        }
        families.SelectionChanged += (_, _) => Update();
        size.ValueChanged += (_, _) => Update();
        bold.IsCheckedChanged += (_, _) => Update();
        italic.IsCheckedChanged += (_, _) => Update();
        var page = new SettingsPage(new object()) { MinWidth = 460 };
        page.Add(search, families, page.Row("Size:", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { size, sizes } }), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { bold, italic } }, page.Section("Preview", preview));
        SetBody(page);
        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);
        Update();
    }
}
