using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class DetailedTimerTests
{
    [Fact]
    public void PreserveSettingsXmlAndHash()
    {
        var settings = new DetailedTimerSettings
        {
            Height = 112, Width = 340, SegmentTimerSizeRatio = 45,
            TimerShowGradient = false, SegmentTimerShowGradient = false,
            OverrideTimerColors = true, ShowSplitName = true, DisplayIcon = true,
            IconSize = 55, DecimalsSize = 24, SegmentTimerDecimalsSize = 28,
            TimerColor = Color.FromArgb(Color.Red.ToArgb()), SegmentTimerColor = Color.FromArgb(Color.Green.ToArgb()),
            SegmentLabelsColor = Color.FromArgb(Color.Blue.ToArgb()), SegmentTimesColor = Color.FromArgb(Color.Yellow.ToArgb()),
            SplitNameColor = Color.FromArgb(Color.Cyan.ToArgb()), BackgroundColor = Color.FromArgb(120, 20, 30, 40),
            BackgroundColor2 = Color.FromArgb(Color.Purple.ToArgb()), BackgroundGradient = DeltasGradientType.Horizontal,
            DigitsFormat = "0:00:01", Accuracy = ".234", SegmentDigitsFormat = "00:01", SegmentAccuracy = "",
            SegmentTimesAccuracy = TimeAccuracy.Milliseconds, TimingMethod = "Game Time",
            Comparison = "Personal Best", Comparison2 = "Unavailable", HideComparison = true,
            SegmentLabelsFont = new Font("Arial", 14), SegmentTimesFont = new Font("Arial", 16, FontStyle.Bold),
            SplitNameFont = new Font("Arial", 18),
        };
        XmlNode xml = settings.GetSettings(new XmlDocument());
        var loaded = new DetailedTimerSettings();
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(TimeAccuracy.Milliseconds, loaded.SegmentTimesFormatter.Accuracy);
    }

    [Fact]
    public void PreserveDefaultsAndMigrateLegacyAccuracyAndColors()
    {
        var settings = new DetailedTimerSettings();
        Assert.Equal((75f, 200f, 40f, 35f, 35f), (settings.Height, settings.Width, settings.SegmentTimerSizeRatio, settings.DecimalsSize, settings.SegmentTimerDecimalsSize));
        Assert.True(settings.TimerShowGradient && settings.SegmentTimerShowGradient);
        Assert.False(settings.OverrideTimerColors || settings.DisplayIcon || settings.ShowSplitName || settings.HideComparison);
        Assert.Equal(("1", ".23", "1", ".23"), (settings.DigitsFormat, settings.Accuracy, settings.SegmentDigitsFormat, settings.SegmentAccuracy));
        var doc = new XmlDocument();
        doc.LoadXml("<Settings><Version>1.2</Version><TimerUseSplitColors>True</TimerUseSplitColors><TimerAccuracy>Tenths</TimerAccuracy><SegmentTimerAccuracy>Seconds</SegmentTimerAccuracy></Settings>");
        settings.SetSettings(doc.DocumentElement);
        Assert.False(settings.OverrideTimerColors);
        Assert.Equal(("1", ".2", "1", ""), (settings.DigitsFormat, settings.Accuracy, settings.SegmentDigitsFormat, settings.SegmentAccuracy));
        Assert.Equal("Segoe UI", settings.SegmentLabelsFont.Name);
    }

    [Fact]
    public void NotifyDependentViewStateAndSynchronizeFormatter()
    {
        var settings = new DetailedTimerSettings { Mode = LayoutMode.Vertical, CurrentState = TestSupport.CreateState(), Comparison2 = "Missing" };
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.ShowSplitName = settings.DisplayIcon = settings.OverrideTimerColors = settings.HideComparison = true;
        Assert.True(settings.IsSplitNameEnabled && settings.IsIconSizeEnabled && settings.IsTimerColorEnabled);
        Assert.False(settings.IsComparison2Enabled);
        Assert.Contains(nameof(settings.IsComparison2Enabled), raised);
        Assert.Contains("Missing", settings.Comparison2Options);
        settings.BackgroundSwatch2Color = Color.Red;
        settings.GradientString = "Vertical";
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal((Color.Red, Color.Blue), (settings.BackgroundColor, settings.BackgroundColor2));
        Assert.Contains(nameof(settings.IsColor1Visible), raised);
        settings.GradientString = "Plain With Delta Color";
        Assert.False(settings.IsColor1Visible || settings.IsColor2Visible);
        Assert.Equal((20f, 150f, 75f), (settings.SizeMinimum, settings.SizeMaximum, settings.SizeValue));
        settings.Mode = LayoutMode.Horizontal;
        settings.SizeValue = 400;
        Assert.Equal((50f, 500f, 400f, 75f), (settings.SizeMinimum, settings.SizeMaximum, settings.Width, settings.Height));
        Assert.Contains(nameof(settings.SizeLabel), raised);
        settings.SegmentTimesAccuracy = TimeAccuracy.Seconds;
        Assert.Equal(TimeAccuracy.Seconds, settings.SegmentTimesFormatter.Accuracy);
        settings.SegmentTimesFormatter = new GeneralTimeFormatter();
        Assert.Equal(TimeAccuracy.Seconds, settings.SegmentTimesFormatter.Accuracy);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical, false)]
    [InlineData(LayoutMode.Horizontal, false)]
    [InlineData(LayoutMode.Vertical, true)]
    [InlineData(LayoutMode.Horizontal, true)]
    public void LoadFactoryAndRenderTimersComparisonsAndOptionalIcon(LayoutMode mode, bool extras)
    {
        LiveSplitState state = TestSupport.CreateState();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(95), 1);
        using Bitmap icon = TestSupport.CreateIcon();
        state.Run[1].Icon = icon;
        using IComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.DetailedTimer.dll", state).Component;
        var detailed = Assert.IsType<DetailedTimer>(component);
        var settings = Assert.IsType<DetailedTimerSettings>(component.GetSettingsControl(mode));
        settings.Width = 360;
        settings.Height = 110;
        settings.DisplayIcon = settings.ShowSplitName = extras;
        settings.HideComparison = extras;
        settings.BackgroundColor = Color.FromArgb(20, 25, 40);
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 110, $"detailedtimer-{mode}-{extras}.png");
        Assert.Equal(110, bitmap.Height);
        Assert.True(bitmap.Width >= 360);
        Assert.NotNull(TestSupport.ContentBounds(bitmap, settings.BackgroundColor, new Rectangle(0, 0, bitmap.Width, bitmap.Height)));
        int bright = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (c.R > 170 && c.G > 170 && c.B > 170) { bright++; }
            }
        }
        Assert.True(bright > 100);
    }
}
