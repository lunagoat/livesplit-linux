using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class CounterTests
{
    [Fact]
    public void PreserveSettingsAndLegacyFontMigration()
    {
        using var settings = new CounterComponentSettings
        {
            CounterText = "Deaths:", InitialValue = -12, Increment = 3,
            GlobalHotkeysEnabled = true, IncrementKey = new KeyOrButton(Keys.Control | Keys.F1),
            DecrementKey = null, ResetKey = new KeyOrButton(Keys.F2),
            OverrideTextColor = true, CounterTextColor = Color.FromArgb(Color.Gold.ToArgb()), CounterValueColor = Color.FromArgb(Color.Cyan.ToArgb()),
            BackgroundColor = Color.FromArgb(Color.Navy.ToArgb()), BackgroundColor2 = Color.FromArgb(Color.DarkRed.ToArgb()), BackgroundGradient = GradientType.Horizontal,
        };
        var xml = settings.GetSettings(new XmlDocument());
        using var copy = new CounterComponentSettings();
        copy.SetSettings(xml);
        Assert.Equal(xml.OuterXml, copy.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), copy.GetSettingsHashCode());
        Assert.Null(copy.DecrementKey);
        Assert.DoesNotContain("CounterFont", xml.OuterXml);
        using var component = new CounterComponent(TestSupport.CreateState());
        var old = (XmlElement)xml;
        var doc = old.OwnerDocument;
        SettingsHelper.CreateSetting(doc, old, "OverrideCounterFont", true);
        SettingsHelper.CreateSetting(doc, old, "CounterFont", new Font("Arial", 13));
        component.SetSettings(old);
        Assert.True(component.Settings.OverrideCounterFont);
        var overrides = new LiveSplit.Options.FontOverrides();
        component.MigrateFontOverrides(overrides);
        Assert.True(overrides.OverrideTextFont);
        Assert.False(component.Settings.OverrideCounterFont);
        Assert.Equal(13, overrides.TextFont.Size);
    }

    [Fact]
    public void ApplyCounterEditsAndNotifyViewDependencies()
    {
        using var component = new CounterComponent(TestSupport.CreateState());
        var settings = component.Settings;
        Assert.Equal("Counter:", settings.CounterText);
        Assert.Equal(1, settings.Increment);
        Assert.Equal(Keys.Add, settings.IncrementKey.Key);
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.InitialValue = 10;
        Assert.Equal(10, component.Counter.Count);
        component.Counter.Increment();
        settings.Increment = 4;
        Assert.Equal(11, component.Counter.Count);
        component.Counter.Decrement();
        Assert.Equal(7, component.Counter.Count);
        component.Counter.Reset();
        Assert.Equal(10, component.Counter.Count);
        settings.OverrideTextColor = true;
        Assert.True(settings.IsCounterColorEnabled);
        Assert.Contains(nameof(settings.IsCounterColorEnabled), raised);
        settings.BackgroundSwatch2Color = Color.Red;
        Assert.Equal(Color.Red, settings.BackgroundColor);
        settings.GradientString = "Vertical";
        Assert.True(settings.IsColor1Visible);
        Assert.Contains(nameof(settings.BackgroundSwatch2Color), raised);
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal(Color.Blue, settings.BackgroundColor2);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void LoadFactoryAndRender(LayoutMode mode)
    {
        var state = TestSupport.CreateState();
        using var component = Assert.IsType<CounterComponent>(ComponentManager.LoadLayoutComponent("LiveSplit.Counter.dll", state).Component);
        Assert.Same(component.Settings, component.GetSettingsControl(mode));
        component.Settings.CounterText = "Deaths:";
        component.Settings.BackgroundColor = Color.Navy;
        component.Counter.SetCount(123);
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 60, "counter-" + mode + ".png");
        Assert.True(bitmap.Width > 50 && bitmap.Height > 15);
        Assert.NotNull(TestSupport.ContentBounds(bitmap, Color.Navy, new Rectangle(0, 0, bitmap.Width, bitmap.Height)));
    }
}
