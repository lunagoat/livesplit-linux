using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class DeltaTests
{
    [Fact]
    public void RoundTripAllPersistedSettings()
    {
        var settings = new DeltaSettings
        {
            TextColor = Color.FromArgb(255, 21, 43, 65),
            OverrideTextColor = true,
            Accuracy = TimeAccuracy.Milliseconds,
            DropDecimals = true,
            BackgroundColor = Color.FromArgb(255, 21, 43, 65),
            BackgroundColor2 = Color.FromArgb(255, 21, 43, 65),
            BackgroundGradient = GradientType.Horizontal,
            Comparison = "Custom comparison",
            Display2Rows = true,
            OverrideText = true,
            DifferentialText = true,
            CustomText = "Custom comparison",
            CustomTextAhead = "Custom comparison",
        };
        var doc = new XmlDocument();
        XmlNode xml = settings.GetSettings(doc);
        var loaded = new DeltaSettings();
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(GradientType.Horizontal, loaded.BackgroundGradient);
    }


    [Fact]
    public void LoadFactoryThroughComponentManager()
    {
        var state = TestSupport.CreateState();
        using IComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.Delta.dll", state).Component;
        Assert.IsType<DeltaComponent>(component);
        Assert.IsType<DeltaSettings>(component.GetSettingsControl(LayoutMode.Vertical));
    }

    [Fact]
    public void KeepWindowsDefaults()
    {
        var settings = new DeltaSettings();
        Assert.Equal(Color.White.ToArgb(), settings.TextColor.ToArgb());
        Assert.Equal(Color.Transparent, settings.BackgroundColor);
        Assert.Equal(GradientType.Plain, settings.BackgroundGradient);
        Assert.False(settings.Display2Rows || settings.OverrideTextColor);
        Assert.Equal(TimeAccuracy.Tenths, settings.Accuracy);
        Assert.True(settings.DropDecimals);
        Assert.False(settings.OverrideText || settings.DifferentialText);
    }

    [Fact]
    public void ReadLegacySettingsWithMissingOptionalElements()
    {
        var source = new DeltaSettings();
        var xml = (XmlElement)source.GetSettings(new XmlDocument());
        xml["Version"].InnerText = "1.0";
        foreach (string name in new[] { "OverrideText", "DifferentialText", "CustomText", "CustomTextAhead" })
        {
            xml.RemoveChild(xml[name]);
        }

        var loaded = new DeltaSettings();
        loaded.SetSettings(xml);
        Assert.False(loaded.OverrideText || loaded.DifferentialText);
        Assert.Equal("", loaded.CustomText);
        Assert.Equal("", loaded.CustomTextAhead);
    }

    [Fact]
    public void PreserveLayoutAndSwatchBindings()
    {
        var state = TestSupport.CreateState();
        using var component = new DeltaComponent(state);
        var settings = component.Settings;
        Assert.Same(settings, component.GetSettingsControl(LayoutMode.Horizontal));
        Assert.True(settings.Display2RowsShown);
        Assert.False(settings.Display2Rows || settings.IsDisplay2RowsEnabled);
        component.GetSettingsControl(LayoutMode.Vertical);
        Assert.True(settings.IsDisplay2RowsEnabled);
        Assert.False(settings.Display2RowsShown);
        settings.Display2RowsShown = true;
        Assert.True(settings.Display2Rows);

        var changed = new List<string>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        settings.BackgroundSwatch2Color = Color.Red;
        Assert.Equal(Color.Red, settings.BackgroundColor);
        settings.BackgroundGradient = GradientType.Vertical;
        Assert.True(settings.IsColor1Visible);
        Assert.Contains(nameof(settings.IsColor1Visible), changed);
        Assert.Contains(nameof(settings.BackgroundSwatch2Color), changed);
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal(Color.Blue, settings.BackgroundColor2);
        Assert.Equal(Color.Red, settings.BackgroundColor);

        settings.OverrideTextColor = true;
        Assert.True(settings.IsTextColorEnabled);
        Assert.Contains(nameof(settings.IsTextColorEnabled), changed);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void RenderRunningComparison(LayoutMode mode)
    {
        var state = TestSupport.CreateState();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(95.4), 1);
        using var component = new DeltaComponent(state);

        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 70,
            "delta-" + mode.ToString().ToLowerInvariant() + ".png");
        Assert.True(bitmap.Width > 40 && bitmap.Height > 15);
        var background = Color.FromArgb(15, 15, 15);
        Rectangle left = mode == LayoutMode.Vertical
            ? new Rectangle(0, 0, bitmap.Width / 2, bitmap.Height)
            : new Rectangle(0, 0, bitmap.Width, bitmap.Height / 2);
        Rectangle right = mode == LayoutMode.Vertical
            ? new Rectangle(bitmap.Width / 2, 0, bitmap.Width - bitmap.Width / 2, bitmap.Height)
            : new Rectangle(0, bitmap.Height / 2, bitmap.Width, bitmap.Height - bitmap.Height / 2);
        Assert.NotNull(TestSupport.ContentBounds(bitmap, background, left));
        Assert.NotNull(TestSupport.ContentBounds(bitmap, background, right));
    }

    [Fact]
    public void FilterComparisonsAndKeepUnknownSelection()
    {
        var settings = new DeltaSettings { CurrentState = TestSupport.CreateState() };
        Assert.Equal("Current Comparison", settings.ComparisonOptions[0]);
        Assert.Contains(Run.PersonalBestComparisonName, settings.ComparisonOptions);
        Assert.DoesNotContain(NoneComparisonGenerator.ComparisonName, settings.ComparisonOptions);
        settings.Comparison = "Deleted comparison";
        Assert.Contains("Deleted comparison", settings.ComparisonOptions);
        settings.Comparison = NoneComparisonGenerator.ComparisonName;
        Assert.Contains(NoneComparisonGenerator.ComparisonName, settings.ComparisonOptions);
    }

    [Fact]
    public void NotifyCustomLabelDependencies()
    {
        var settings = new DeltaSettings();
        var changed = new List<string>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        settings.OverrideText = true;
        Assert.True(settings.IsCustomTextEnabled);
        Assert.False(settings.IsCustomTextAheadEnabled);
        settings.DifferentialText = true;
        Assert.True(settings.IsCustomTextAheadEnabled);
        Assert.Equal("Label When Behind:", settings.CustomTextLabel);
        Assert.Contains(nameof(settings.CustomTextLabel), changed);
        Assert.Contains(nameof(settings.IsCustomTextAheadEnabled), changed);
        settings.OverrideText = false;
        Assert.False(settings.IsCustomTextEnabled || settings.IsCustomTextAheadEnabled);
    }

}
