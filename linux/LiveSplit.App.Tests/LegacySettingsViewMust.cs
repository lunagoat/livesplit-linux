using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.View.ComponentSettings;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace LiveSplit.AppTests;

public class LegacySettingsViewMust
{
    private static Assembly Stub()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "LiveSplit.Compat.WinForms.dll");
        return File.Exists(path) ? Assembly.LoadFrom(path) : null;
    }

    private static object New(Assembly stub, string name) => Activator.CreateInstance(stub.GetType("System.Windows.Forms." + name, true));

    // Like designer code: Controls.Add(Control), not the untyped IList.Add.
    private static void Put(object parent, object child)
    {
        object controls = parent.GetType().GetProperty("Controls").GetValue(parent);
        controls.GetType().GetMethod("Add", [child.GetType().GetProperty("Parent").PropertyType]).Invoke(controls, [child]);
    }

    private static void Prop(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

    private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

    [AvaloniaFact]
    public void DrawAStubControlTreeAndWriteEditsBack()
    {
        Assembly stub = Stub();
        if (stub == null)
        {
            return;
        }

        object root = New(stub, "UserControl");
        object group = New(stub, "GroupBox");
        Prop(group, "Text", "Splits");
        object check = New(stub, "CheckBox");
        Prop(check, "Text", "&Split on level 1");
        object label = New(stub, "Label");
        Prop(label, "Text", "Delay (ms)");
        object number = New(stub, "NumericUpDown");
        Prop(number, "Maximum", 500m);
        Prop(number, "Value", 20m);
        object text = New(stub, "TextBox");
        Prop(text, "Text", "hello");
        Put(group, check);
        Put(group, label);
        Put(group, number);
        Put(root, group);
        Put(root, text);

        Assert.Equal(2, ((IEnumerable)Prop(root, "Controls")).Cast<object>().Count());
        Assert.Equal(3, ((IEnumerable)Prop(group, "Controls")).Cast<object>().Count());
        Assert.True(LegacySettingsView.IsStubControl(root.GetType()));
        var view = new LegacySettingsView(root);
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        CheckBox box = view.GetVisualDescendants().OfType<CheckBox>().Single();
        Assert.Equal("Split on level 1", box.Content);
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Delay (ms)");
        NumericUpDown spinner = view.GetVisualDescendants().OfType<NumericUpDown>().Single();
        Assert.Equal(20m, spinner.Value);
        Assert.Equal(500m, spinner.Maximum);

        box.IsChecked = true;
        spinner.Value = 42m;
        view.GetVisualDescendants().OfType<TextBox>().Single(t => t.Text == "hello").Text = "changed";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(true, Prop(check, "Checked"));
        Assert.Equal(42m, Prop(number, "Value"));
        Assert.Equal("changed", Prop(text, "Text"));
        window.Close();
    }
}
