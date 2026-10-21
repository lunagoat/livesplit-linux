using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.View.ComponentSettings;
using LiveSplit.View.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Xunit;

namespace LiveSplit.AppTests;

public sealed class SettingsViewsMust
{
    [Fact]
    public void RegisterEveryPortedSettingsType()
    {
        var registry = Registry.Default;
        var types = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Components"), "LiveSplit.*.dll")
            .Select(path => AssemblyLoadContext.Default.LoadFromAssemblyPath(path))
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && typeof(SettingsBase).IsAssignableFrom(type) && type.Name.EndsWith("Settings"))
            .ToArray();
        Assert.True(types.Length >= 26);
        foreach (var type in types) Assert.True(registry.HasView(type), $"Missing view: {type.AssemblyQualifiedName}");
        Assert.Equal(2, types.Count(type => type.Name == "SplitsSettings"));
    }

    [Fact]
    public void PreserveAlphaAndIgnoreInvalidHex()
    {
        var selection = new ColorSelection { Hex = "3373AABB" };
        Assert.Equal(0x33, selection.Alpha);
        selection.Red = 12;
        Assert.Equal(0x33, selection.Alpha);
        selection.Hue = 180;
        Assert.Equal(0x33, selection.Alpha);
        string previous = selection.Hex;
        selection.Hex = "invalid";
        Assert.Equal(previous, selection.Hex);
        selection.Hex = "#123456";
        Assert.Equal("FF123456", selection.Hex);
    }

    [Fact]
    public void ColorSelectionClampsValues()
    {
        var selection = new ColorSelection();
        selection.Red = -50;
        Assert.Equal(0, selection.Red);
        selection.Red = 300;
        Assert.Equal(255, selection.Red);

        selection.Green = -20;
        Assert.Equal(0, selection.Green);
        selection.Green = 400;
        Assert.Equal(255, selection.Green);

        selection.Blue = -1;
        Assert.Equal(0, selection.Blue);
        selection.Blue = 500;
        Assert.Equal(255, selection.Blue);

        selection.Alpha = -10;
        Assert.Equal(0, selection.Alpha);
        selection.Alpha = 999;
        Assert.Equal(255, selection.Alpha);

        selection.Hue = -10;
        Assert.Equal(0, selection.Hue);
        selection.Hue = 400;
        Assert.Equal(360, selection.Hue);

        selection.Saturation = -5;
        Assert.Equal(0, selection.Saturation);
        selection.Saturation = 150;
        Assert.Equal(100, selection.Saturation);

        selection.Brightness = -15;
        Assert.Equal(0, selection.Brightness);
        selection.Brightness = 200;
        Assert.Equal(100, selection.Brightness);
    }

    [Fact]
    public void RegistryRejectsUnregisteredTypes()
    {
        var registry = Registry.Default;
        Assert.False(registry.CanCreate(new object()));
        Assert.False(registry.CanCreate(null));
        Assert.False(registry.HasView(typeof(string)));
        Assert.Throws<ArgumentException>(() => registry.Create(new object(), LayoutMode.Vertical));
    }

    [AvaloniaFact]
    public void ComponentSettingsViewsCreatesFallback()
    {
        var fallback = ComponentSettingsViews.Create(null, LayoutMode.Vertical);
        Assert.IsType<TextBlock>(fallback);
    }

    [AvaloniaFact]
    public void RenderEveryPageAndExerciseBindings()
    {
        string scratchDir = "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ui-compsettings";
        string output = Environment.GetEnvironmentVariable("LS_SETTINGS_TEST_OUT")
            ?? (Directory.Exists(scratchDir) ? scratchDir : "/tmp/livesplit-settings-views");
        Directory.CreateDirectory(output);
        var registry = Registry.Default;
        foreach (var type in registry.SettingsTypes.Where(t => t.Name != "ColumnSettings" && t.Name != "SoundFileSettings" && t.Name != "SoundVolumeSettings"))
        {
            object model = type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type) : Activator.CreateInstance(type, new object[] { null });
            try
            {
                var view = registry.Create(model, LayoutMode.Vertical);
                string name = type.Assembly.GetName().Name.Replace("LiveSplit.", "");
                Render(view, Path.Combine(output, name + ".png"));
                if (name == "Sound")
                {
                    var tabs = view.GetVisualDescendants().OfType<TabControl>().Single();
                    tabs.SelectedIndex = 1;
                    Render(view, Path.Combine(output, "Sound-Volume.png"));
                }
                ExerciseCommonBindings(model, view);
                if (name is "Splits" or "Subsplits") ExerciseColumns(model);
                registry.Create(model, LayoutMode.Horizontal);
                if (model.GetType().GetProperty("Display2RowsShown") != null)
                    Assert.True((bool)model.GetType().GetProperty("Display2RowsShown").GetValue(model));
            }
            finally { (model as IDisposable)?.Dispose(); }
        }
        RenderWindow(new ColorPickerDialog(System.Drawing.Color.FromArgb(100, 51, 115, 244)), Path.Combine(output, "ColorPicker.png"));
        RenderWindow(new FontPickerDialog(new System.Drawing.Font("DejaVu Sans", 13, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel)), Path.Combine(output, "FontPicker.png"));

        var mock = new MockComponent();
        var dialog = new ComponentSettingsDialog(mock);
        dialog.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.True(mock.WasRestored);
        Assert.Equal("Original", mock.SavedSettings.InnerText);
    }

    private static void Render(Control view, string path)
    {
        var window = new Window { Content = view, Width = 720, Height = 900, Background = new SolidColorBrush(Color.Parse("#202020")) };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        view.Measure(new Size(720, double.PositiveInfinity));
        window.Height = Math.Max(100, Math.Ceiling(view.DesiredSize.Height));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        using var bitmap = new RenderTargetBitmap(new PixelSize(720, (int)Math.Ceiling(window.Height)), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path);
        window.Content = null;
        window.Close();
    }

    private static void RenderWindow(Window window, string path)
    {
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(path);
        window.Close();
    }

    private static void ExerciseCommonBindings(object model, Control view)
    {
        var gradient = model.GetType().GetProperty("GradientString");
        if (gradient != null && model.GetType().GetProperty("BackgroundSwatch2Color") != null)
        {
            gradient.SetValue(model, "Vertical");
            var swatches = view.GetVisualDescendants().OfType<ColorSwatchButton>().ToArray();
            if (swatches.Length > 0) Assert.True(swatches[0].IsVisible);
            gradient.SetValue(model, "Plain");
            if (swatches.Length > 0) Assert.False(swatches[0].IsVisible);
            var color = System.Drawing.Color.FromArgb(54, 12, 34, 56);
            model.GetType().GetProperty("BackgroundSwatch2Color").SetValue(model, color);
            Assert.Equal(color, model.GetType().GetProperty("BackgroundColor").GetValue(model));
        }
    }

    private static void ExerciseColumns(object model)
    {
        var list = (IList)model.GetType().GetProperty("ColumnsList").GetValue(model);
        int before = list.Count;
        model.GetType().GetMethod("AddColumn").Invoke(model, null);
        Assert.Equal(before + 1, list.Count);
        var column = list[list.Count - 1];
        column.GetType().GetProperty("ColumnName").SetValue(column, "Custom");
        Assert.Contains("Custom", column.GetType().GetProperty("GroupTitle").GetValue(column).ToString());
        column.GetType().GetMethod("MoveUp").Invoke(column, null);
        Assert.Same(column, list[list.Count - 2]);
        column.GetType().GetMethod("Remove").Invoke(column, null);
        Assert.Equal(before, list.Count);
    }
}

internal sealed class MockComponent : IComponent
{
    public string ComponentName => "Mock";
    public float HorizontalWidth => 10;
    public float MinimumHeight => 10;
    public float VerticalHeight => 10;
    public float MinimumWidth => 10;
    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;
    public IDictionary<string, Action> ContextMenuControls => null;
    public XmlNode SavedSettings { get; set; }
    public bool WasRestored { get; set; }

    public void DrawHorizontal(System.Drawing.Graphics g, LiveSplitState state, float height, System.Drawing.Region clipRegion) { }
    public void DrawVertical(System.Drawing.Graphics g, LiveSplitState state, float width, System.Drawing.Region clipRegion) { }
    public object GetSettingsControl(LayoutMode mode) => null;
    public XmlNode GetSettings(XmlDocument document)
    {
        var node = document.CreateElement("Settings");
        node.InnerText = "Original";
        return node;
    }
    public void SetSettings(XmlNode settings)
    {
        SavedSettings = settings;
        WasRestored = true;
    }
    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
    public void Dispose() { }
}
