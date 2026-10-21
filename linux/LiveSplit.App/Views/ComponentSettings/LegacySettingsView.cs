using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Services;
using LiveSplit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace LiveSplit.View.ComponentSettings;

/// <summary>
/// Settings window for components that came from Windows .NET Framework LiveSplit. Their settings are a
/// Windows Forms UserControl, which at runtime is a headless stub (LiveSplit.Compat.WinForms) that only holds
/// state. This view walks that control tree by reflection and draws the common controls with Avalonia; edits
/// are written back to the stub's properties, which raises the same change events WinForms would.
/// </summary>
public sealed class LegacySettingsView : ScrollViewer
{
    private const string StubAssembly = "LiveSplit.Compat.WinForms";

    public LegacySettingsView(object stubControl)
    {
        MinWidth = 460;
        Content = new Border { Padding = new Thickness(8), Child = Build(stubControl) };
    }

    public static bool IsStubControl(Type type)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            if (t.FullName == "System.Windows.Forms.Control" && t.Assembly.GetName().Name == StubAssembly)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Is(Type type, string name)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            if (t.Name == name && t.Namespace == "System.Windows.Forms")
            {
                return true;
            }
        }

        return false;
    }

    private static object Get(object target, string property)
    {
        try
        {
            return target.GetType().GetProperty(property)?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static void Set(object target, string property, object value)
    {
        try
        {
            PropertyInfo info = target.GetType().GetProperty(property);
            if (info?.CanWrite == true)
            {
                info.SetValue(target, Convert.ChangeType(value, Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType, CultureInfo.InvariantCulture));
            }
        }
        catch
        {
            // A handler in the component threw; keep the UI usable.
        }
    }

    private static string Text(object control) => (Get(control, "Text") as string ?? string.Empty).Replace("&&", "\u0001").Replace("&", string.Empty).Replace("\u0001", "&");

    private static IEnumerable<object> Children(object control)
    {
        return Get(control, "Controls") is IEnumerable items ? items.Cast<object>().ToList() : [];
    }

    public static Control Build(object control)
    {
        Type type = control.GetType();
        if (Get(control, "Visible") is false)
        {
            return null;
        }

        Control result = BuildCore(control, type);
        if (result != null && Get(control, "Enabled") is false)
        {
            result.IsEnabled = false;
        }

        return result;
    }

    private static Control BuildCore(object control, Type type)
    {
        if (Is(type, "RadioButton") || Is(type, "CheckBox"))
        {
            var box = new CheckBox { Content = Text(control), IsChecked = Get(control, "Checked") as bool? ?? false };
            box.IsCheckedChanged += (_, _) => Set(control, "Checked", box.IsChecked == true);
            return box;
        }

        if (Is(type, "Button"))
        {
            var button = new Button { Content = Text(control) };
            button.Click += (_, _) => Click(control);
            return button;
        }

        if (Is(type, "Label"))
        {
            return new TextBlock { Text = Text(control), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        }

        if (Is(type, "NumericUpDown"))
        {
            var numeric = new NumericUpDown
            {
                Minimum = Get(control, "Minimum") as decimal? ?? 0,
                Maximum = Get(control, "Maximum") as decimal? ?? 100,
                Increment = Get(control, "Increment") as decimal? ?? 1,
                Value = Get(control, "Value") as decimal? ?? 0,
                MinWidth = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            if (Get(control, "DecimalPlaces") is int places && places > 0)
            {
                numeric.FormatString = "F" + places;
            }

            numeric.ValueChanged += (_, e) =>
            {
                if (e.NewValue.HasValue)
                {
                    Set(control, "Value", e.NewValue.Value);
                }
            };
            return numeric;
        }

        if (Is(type, "TrackBar"))
        {
            var slider = new Slider
            {
                Minimum = Convert.ToDouble(Get(control, "Minimum") ?? 0),
                Maximum = Convert.ToDouble(Get(control, "Maximum") ?? 10),
                Value = Convert.ToDouble(Get(control, "Value") ?? 0),
                MinWidth = 160,
            };
            slider.ValueChanged += (_, e) => Set(control, "Value", (int)Math.Round(e.NewValue));
            return slider;
        }

        if (Is(type, "ComboBox") || Is(type, "ListBox"))
        {
            List<string> items = (Get(control, "Items") is IEnumerable entries ? entries.Cast<object>() : []).Select(i => i?.ToString() ?? string.Empty).ToList();
            var combo = new ComboBox { ItemsSource = items, MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left };
            int selected = Get(control, "SelectedIndex") as int? ?? -1;
            combo.SelectedIndex = selected;
            if (selected < 0 && Is(type, "ComboBox") && Get(control, "Text") is string current && items.IndexOf(current) is int found and >= 0)
            {
                combo.SelectedIndex = found;
            }

            combo.SelectionChanged += (_, _) => Set(control, "SelectedIndex", combo.SelectedIndex);
            return combo;
        }

        if (Is(type, "TextBoxBase") || Is(type, "TextBox") || Is(type, "RichTextBox"))
        {
            var box = new TextBox
            {
                Text = Get(control, "Text") as string ?? string.Empty,
                MinWidth = 200,
                AcceptsReturn = Get(control, "Multiline") is true,
                IsReadOnly = Get(control, "ReadOnly") is true,
            };
            box.TextChanged += (_, _) => Set(control, "Text", box.Text ?? string.Empty);
            return box;
        }

        if (Is(type, "GroupBox"))
        {
            var panel = new StackPanel { Spacing = 4 };
            AddChildren(panel, control);
            return new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children = { new TextBlock { Text = Text(control), FontWeight = FontWeight.SemiBold }, panel },
                },
            };
        }

        if (Is(type, "TabControl"))
        {
            var tabs = new TabControl();
            foreach (object page in Children(control))
            {
                if (Get(page, "Visible") is false)
                {
                    continue;
                }

                var content = new StackPanel { Spacing = 4, Margin = new Thickness(6) };
                AddChildren(content, page);
                tabs.Items.Add(new TabItem { Header = Text(page), Content = new ScrollViewer { Content = content } });
            }

            return tabs;
        }

        if (Is(type, "PictureBox") || Is(type, "DataGridView") || Is(type, "ListView") || Is(type, "TreeView"))
        {
            // Not drawn: these need data the headless stub does not model.
            return Is(type, "PictureBox") ? null : new TextBlock { Text = $"({type.Name} is not shown in the Linux port)", Opacity = 0.6 };
        }

        // Panels and every other container: flow the children vertically (horizontally for flow panels).
        bool horizontal = Is(type, "FlowLayoutPanel") && Convert.ToInt32(Get(control, "FlowDirection") ?? 0) is 0 or 2;
        var container = horizontal
            ? (Panel)new WrapPanel { Orientation = Orientation.Horizontal }
            : new StackPanel { Spacing = 4 };
        AddChildren(container, control);
        return container;
    }

    private static void AddChildren(Panel panel, object control)
    {
        foreach (object child in Children(control))
        {
            Control view = Build(child);
            if (view != null)
            {
                panel.Children.Add(view);
            }
        }
    }

    private static void Click(object button)
    {
        foreach (string name in new[] { "PerformClick", "OnClick" })
        {
            MethodInfo method = button.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, name == "OnClick" ? [typeof(EventArgs)] : Type.EmptyTypes, null);
            if (method != null)
            {
                try
                {
                    method.Invoke(button, name == "OnClick" ? [EventArgs.Empty] : null);
                }
                catch
                {
                    // The component's click handler failed; nothing useful to show.
                }

                return;
            }
        }
    }
}

public sealed class LegacySettingsViewFactory : IComponentSettingsViewFactory
{
    public bool CanCreate(object settings) => settings != null && LegacySettingsView.IsStubControl(settings.GetType());

    public Control Create(object settings, LayoutMode mode) => new LegacySettingsView(settings);
}
