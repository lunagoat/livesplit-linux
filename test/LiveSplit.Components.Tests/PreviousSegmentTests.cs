using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using LiveSplit.UI.LayoutFactories;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class PreviousSegmentTests
{
    [Fact]
    public void RoundTripAllPersistedSettings()
    {
        var settings = new PreviousSegmentSettings
        {
            TextColor = Color.FromArgb(255, 21, 43, 65),
            OverrideTextColor = true,
            BackgroundColor = Color.FromArgb(255, 21, 43, 65),
            BackgroundColor2 = Color.FromArgb(255, 21, 43, 65),
            BackgroundGradient = GradientType.Horizontal,
            DeltaAccuracy = TimeAccuracy.Milliseconds,
            DropDecimals = true,
            Display2Rows = true,
            ShowPossibleTimeSave = true,
            TimeSaveAccuracy = TimeAccuracy.Milliseconds,
            Comparison = "Custom comparison",
        };
        var doc = new XmlDocument();
        XmlNode xml = settings.GetSettings(doc);
        var loaded = new PreviousSegmentSettings();
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(GradientType.Horizontal, loaded.BackgroundGradient);
    }


    [Fact]
    public void LoadFactoryThroughComponentManager()
    {
        var state = TestSupport.CreateState();
        using IComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.PreviousSegment.dll", state).Component;
        Assert.IsType<PreviousSegment>(component);
        Assert.IsType<PreviousSegmentSettings>(component.GetSettingsControl(LayoutMode.Vertical));
    }


    [Fact]
    public void LoadPreviousSegmentFromWindowsDefaultLayout()
    {
        var document = new XmlDocument();
        document.Load(TestSupport.SamplePath("DefaultLayout.lsl"));
        foreach (XmlNode node in document.SelectNodes("//Component").Cast<XmlNode>().ToList())
        {
            if (node["Path"].InnerText != "LiveSplit.PreviousSegment.dll")
            {
                node.ParentNode.RemoveChild(node);
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        stream.Position = 0;
        ILayout layout = new XMLLayoutFactory(stream).Create(TestSupport.CreateState());
        var component = Assert.IsType<PreviousSegment>(Assert.Single(layout.LayoutComponents).Component);
        Assert.Equal(TimeAccuracy.Tenths, component.Settings.DeltaAccuracy);
        Assert.True(component.Settings.DropDecimals);
        Assert.Equal("Current Comparison", component.Settings.Comparison);
    }

    [Fact]
    public void KeepWindowsDefaults()
    {
        var settings = new PreviousSegmentSettings();
        Assert.Equal(Color.White.ToArgb(), settings.TextColor.ToArgb());
        Assert.Equal(Color.Transparent, settings.BackgroundColor);
        Assert.Equal(GradientType.Plain, settings.BackgroundGradient);
        Assert.False(settings.Display2Rows || settings.OverrideTextColor);
        Assert.Equal(TimeAccuracy.Tenths, settings.DeltaAccuracy);
        Assert.Equal(TimeAccuracy.Tenths, settings.TimeSaveAccuracy);
        Assert.True(settings.DropDecimals);
        Assert.False(settings.ShowPossibleTimeSave);
    }

    [Fact]
    public void ReadLegacySettingsWithMissingOptionalElements()
    {
        var source = new PreviousSegmentSettings();
        var xml = (XmlElement)source.GetSettings(new XmlDocument());
        xml["Version"].InnerText = "1.0";
        foreach (string name in new[] { "Display2Rows", "ShowPossibleTimeSave", "TimeSaveAccuracy" })
        {
            xml.RemoveChild(xml[name]);
        }

        var loaded = new PreviousSegmentSettings();
        loaded.SetSettings(xml);
        Assert.False(loaded.ShowPossibleTimeSave);
        Assert.Equal(TimeAccuracy.Tenths, loaded.TimeSaveAccuracy);
    }

    [Fact]
    public void PreserveLayoutAndSwatchBindings()
    {
        var state = TestSupport.CreateState();
        using var component = new PreviousSegment(state);
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
        using var component = new PreviousSegment(state);
        component.Settings.ShowPossibleTimeSave = true;
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 70,
            "previoussegment-" + mode.ToString().ToLowerInvariant() + ".png");
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
        var settings = new PreviousSegmentSettings { CurrentState = TestSupport.CreateState() };
        Assert.Equal("Current Comparison", settings.ComparisonOptions[0]);
        Assert.Contains(Run.PersonalBestComparisonName, settings.ComparisonOptions);
        Assert.DoesNotContain(NoneComparisonGenerator.ComparisonName, settings.ComparisonOptions);
        Assert.DoesNotContain(BestSplitTimesComparisonGenerator.ComparisonName, settings.ComparisonOptions);
        settings.Comparison = "Deleted comparison";
        Assert.Contains("Deleted comparison", settings.ComparisonOptions);
        settings.Comparison = NoneComparisonGenerator.ComparisonName;
        Assert.Contains(NoneComparisonGenerator.ComparisonName, settings.ComparisonOptions);
    }

    [Fact]
    public void NotifyTimeSaveAccuracyDependency()
    {
        var settings = new PreviousSegmentSettings();
        var changed = new List<string>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(settings.IsTimeSaveAccuracyEnabled);
        settings.ShowPossibleTimeSave = true;
        Assert.True(settings.IsTimeSaveAccuracyEnabled);
        Assert.Contains(nameof(settings.IsTimeSaveAccuracyEnabled), changed);
    }

}
