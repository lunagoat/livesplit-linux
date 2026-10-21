using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Localization;
using LiveSplit.UI;
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace LiveSplit.Controls.Settings;

public class SettingsPage : StackPanel
{
    public object Model { get; }

    public SettingsPage(object model)
    {
        Model = model;
        DataContext = model;
        Spacing = 6;
        Margin = new Thickness(4);
        MinWidth = 460;
    }

    public static string T(string text) => UiLocalizer.Translate(text);
    public bool Has(string name) => Model.GetType().GetProperty(name) != null;
    public object Read(string name) => Model.GetType().GetProperty(name)?.GetValue(Model)
        ?? Model.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

    public void Bind(Control target, AvaloniaProperty property, string path, BindingMode mode = BindingMode.TwoWay, IValueConverter converter = null)
    {
        if (!Has(path)) throw new ArgumentException($"{Model.GetType().FullName} has no property {path}");
        target.Bind(property, new Binding(path) { Source = Model, Mode = mode, Converter = converter });
    }

    public Control Enabled(Control control, string enabled)
    {
        if (enabled != null) Bind(control, IsEnabledProperty, enabled, BindingMode.OneWay);
        return control;
    }

    public Control Row(string label, Control editor, string enabled = null, string labelProperty = null)
    {
        var text = new TextBlock { Text = T(label), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (labelProperty != null) Bind(text, TextBlock.TextProperty, labelProperty, BindingMode.OneWay);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("190,*"), ColumnSpacing = 10, MinHeight = 32 };
        Grid.SetColumn(editor, 1);
        grid.Children.Add(text);
        grid.Children.Add(editor);
        return Enabled(grid, enabled);
    }

    public SettingsPage Add(params Control[] controls)
    {
        foreach (var control in controls) Children.Add(control);
        return this;
    }

    public Control Section(string title, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = T(title), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        foreach (var control in controls) panel.Children.Add(control);
        return new Border { BorderBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#555555")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Margin = new Thickness(0, 4), Child = panel };
    }

    public static Control Tabs(params (string Title, Control Body)[] pages)
    {
        var tabs = new TabControl();
        foreach (var page in pages) tabs.Items.Add(new TabItem { Header = T(page.Title), Content = new ScrollViewer { Content = page.Body } });
        return tabs;
    }

    public Control Check(string label, string path, string enabled = null, string shown = null)
    {
        var box = new CheckBox { Content = new TextBlock { Text = T(label), TextWrapping = TextWrapping.Wrap } };
        if (shown != null && !Model.GetType().GetProperty(shown).CanWrite)
        {
            Bind(box, CheckBox.IsCheckedProperty, shown, BindingMode.OneWay);
            box.Click += (_, _) =>
            {
                if (box.IsEnabled) Model.GetType().GetProperty(path).SetValue(Model, box.IsChecked == true);
            };
        }
        else Bind(box, CheckBox.IsCheckedProperty, shown ?? path);
        return Enabled(box, enabled);
    }

    public Control Text(string label, string path, string enabled = null, string labelProperty = null, bool validation = false)
    {
        var box = new TextBox();
        Bind(box, TextBox.TextProperty, path, validation ? BindingMode.OneWay : BindingMode.TwoWay);
        if (validation) box.LostFocus += (_, _) =>
        {
            var property = Model.GetType().GetProperty(path);
            property.SetValue(Model, box.Text);
            box.SetCurrentValue(TextBox.TextProperty, property.GetValue(Model)?.ToString());
        };
        return Row(label, box, enabled, labelProperty);
    }

    public Control Combo(string label, string path, string options = null, string enabled = null, bool index = false, bool translate = true)
        => Row(label, ComboBox(path, options ?? path + "Options", index, translate), enabled);

    public ComboBox ComboBox(string path, string options, bool index = false, bool translate = true)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 100 };
        if (Model.GetType().GetProperty(options) != null) Bind(box, ItemsControl.ItemsSourceProperty, options, BindingMode.OneWay);
        else box.ItemsSource = (IEnumerable)Read(options) ?? throw new ArgumentException($"Missing options {options}");
        box.ItemTemplate = new FuncDataTemplate<object>((value, _) => new TextBlock { Text = translate ? T(value?.ToString() ?? "") : value?.ToString() });
        Bind(box, index ? SelectingItemsControlSelectedIndex : ComboBoxSelectedItem, path);
        return box;
    }

    private static AvaloniaProperty SelectingItemsControlSelectedIndex => Avalonia.Controls.Primitives.SelectingItemsControl.SelectedIndexProperty;
    private static AvaloniaProperty ComboBoxSelectedItem => Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty;

    public Control Number(string label, string path, decimal min, decimal max, string enabled = null, string maximum = null)
    {
        var number = new NumericUpDown { Minimum = min, Maximum = max, Increment = 1, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
        if (maximum != null) Bind(number, NumericUpDown.MaximumProperty, maximum, BindingMode.OneWay);
        Bind(number, NumericUpDown.ValueProperty, path);
        return Row(label, number, enabled);
    }

    public Control Slider(string label, string path, double min, double max, string enabled = null, bool size = false, bool volume = false)
    {
        var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = volume ? 10 : 1, IsSnapToTickEnabled = true };
        if (size)
        {
            if (Has("SizeMinimum")) Bind(slider, Avalonia.Controls.Primitives.RangeBase.MinimumProperty, "SizeMinimum", BindingMode.OneWay);
            Bind(slider, Avalonia.Controls.Primitives.RangeBase.MaximumProperty, "SizeMaximum", BindingMode.OneWay);
        }
        Bind(slider, Avalonia.Controls.Primitives.RangeBase.ValueProperty, path);
        slider.Bind(ToolTip.TipProperty, new Binding("Value") { Source = slider, StringFormat = "{0:0}" });
        return Row(label, slider, enabled, size ? "SizeLabel" : null);
    }

    public ColorSwatchButton Swatch(string path, string visible = null)
    {
        var button = new ColorSwatchButton();
        Bind(button, ColorSwatchButton.ColorProperty, path);
        if (visible != null) Bind(button, IsVisibleProperty, visible, BindingMode.OneWay);
        return button;
    }

    public Control Color(string label, string path, string enabled = null) => Row(label, Swatch(path), enabled);
    public Control ColorOverride(string title, string toggle, string color, string enabled)
        => Section(title, Check("Override Layout Settings", toggle), Color("Color:", color, enabled));

    public Control Gradient(string label = "Background Color:", string first = "BackgroundColor", string second = "BackgroundSwatch2Color", string path = "GradientString", string options = "GradientOptions", string visible = "IsColor1Visible", string visible2 = null, string enabled = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 6 };
        var a = Swatch(first, visible);
        var b = Swatch(second, visible2 ?? (Has("IsColor2Visible") && path == "GradientString" ? "IsColor2Visible" : null));
        var combo = ComboBox(path, options);
        Grid.SetColumn(b, 1);
        Grid.SetColumn(combo, 2);
        grid.Children.Add(a); grid.Children.Add(b); grid.Children.Add(combo);
        return Row(label, grid, enabled);
    }

    public Control Radios(string path, params (string Label, string Value)[] choices)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        string group = Guid.NewGuid().ToString();
        foreach (var choice in choices)
        {
            var radio = new RadioButton { Content = T(choice.Label), GroupName = group, Margin = new Thickness(0, 0, 12, 0) };
            Bind(radio, RadioButton.IsCheckedProperty, path, BindingMode.TwoWay, new ChoiceConverter(choice.Value));
            panel.Children.Add(radio);
        }
        return panel;
    }

    public Control Accuracy(string path, string title = "Accuracy", string enabled = null, Control before = null)
    {
        var radios = Radios(path, ("Seconds", "Seconds"), ("Tenths", "Tenths"), ("Hundredths", "Hundredths"), ("Milliseconds", "Milliseconds"));
        return Enabled(before == null ? Section(title, radios) : Section(title, before, radios), enabled);
    }

    public Control Font(string label, string path, string enabled = null)
    {
        var button = new FontPickerButton();
        Bind(button, FontPickerButton.FontProperty, path);
        return Row(label, button, enabled);
    }

    public Control Action(string label, string method, string enabled = null)
    {
        var button = new Button { Content = T(label) };
        if (Model.GetType().GetMethod(method, Type.EmptyTypes) == null) throw new ArgumentException($"Missing command {method}");
        button.Click += (_, _) => Model.GetType().GetMethod(method, Type.EmptyTypes).Invoke(Model, null);
        return Enabled(button, enabled);
    }

    public static Control Tooltip(Control control, string title, string text)
    {
        ToolTip.SetTip(control, new TextBlock { Text = T(title) + "\n" + T(text), MaxWidth = 360, TextWrapping = TextWrapping.Wrap });
        return control;
    }

    private sealed class ChoiceConverter(string value) : IValueConverter
    {
        public object Convert(object input, Type targetType, object parameter, CultureInfo culture) => input?.ToString() == value;
        public object ConvertBack(object input, Type targetType, object parameter, CultureInfo culture)
            => input is true ? targetType == typeof(bool) ? bool.Parse(value) : Enum.Parse(targetType, value) : BindingOperations.DoNothing;
    }
}
