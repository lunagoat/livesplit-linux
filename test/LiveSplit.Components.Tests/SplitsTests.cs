using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class SplitsTests
{
    internal static void PreparePropertyChange(SettingsBase settings, string property)
    {
        if (settings is not SplitsSettings splits) return;
        // Horizontal proxies and radio deselection intentionally ignore writes.
        splits.Mode = LayoutMode.Vertical;
        if (property.StartsWith("IsDelta"))
        {
            splits.DeltasAccuracy = property == "IsDeltaSeconds" ? TimeAccuracy.Tenths : TimeAccuracy.Seconds;
        }
        if (property.StartsWith("IsSplitTimes"))
        {
            splits.SplitTimesAccuracy = property == "IsSplitTimesSeconds" ? TimeAccuracy.Tenths : TimeAccuracy.Seconds;
        }
    }

    [Fact]
    public void PreserveDefaultsAndRoundTripEveryColumnType()
    {
        var settings = new SplitsSettings(TestSupport.CreateState());
        Assert.Equal((8, 1, 20f, 3.6f, 24f), (settings.VisualSplitCount, settings.SplitPreviewCount, settings.SplitWidth, settings.SplitHeight, settings.IconSize));
        Assert.True(settings.DisplayIcons && settings.IconShadows && settings.LockLastSplit && settings.ShowBlankSplits);
        Assert.Equal(new[] { "Delta", "Split Time" }, settings.ColumnsList.Select(x => x.Type));
        settings.VisualSplitCount = 6;
        settings.SplitPreviewCount = 2;
        settings.BackgroundGradient = ExtendedGradientType.Horizontal;
        settings.BackgroundColor = Color.FromArgb(80, 10, 20, 30);
        settings.CurrentSplitGradient = GradientType.Plain;
        settings.CurrentSplitTopColor = Color.FromArgb(Color.Orange.ToArgb());
        settings.DeltasAccuracy = TimeAccuracy.Milliseconds;
        settings.SplitTimesAccuracy = TimeAccuracy.Hundredths;
        settings.OverrideTextColor = settings.OverrideTimesColor = settings.OverrideDeltasColor = true;
        settings.BeforeNamesColor = Color.FromArgb(Color.Red.ToArgb());
        settings.CurrentTimesColor = Color.FromArgb(Color.Lime.ToArgb());
        settings.ColumnsList.Clear();
        foreach (string type in ColumnSettings.ColumnTypeOptions)
        {
            ColumnSettings column = settings.AddColumn();
            column.ColumnName = "<route & \"time\"> " + type;
            column.Type = type;
            column.Comparison = "Missing comparison";
            column.TimingMethod = "Game Time";
        }

        XmlNode xml = settings.GetSettings(new XmlDocument());
        var loaded = new SplitsSettings(TestSupport.CreateState());
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(ColumnSettings.ColumnTypeOptions, loaded.ColumnsList.Select(x => x.Type));
        Assert.All(loaded.ColumnsList, x => Assert.Equal("Game Time", x.TimingMethod));
        loaded.ColumnsList.Clear();
        settings.SetSettings(loaded.GetSettings(new XmlDocument()));
        Assert.Empty(settings.ColumnsList);
    }

    [Theory]
    [InlineData("1.0", false)]
    [InlineData("1.2", true)]
    [InlineData("1.4", true)]
    public void UpgradeLegacySettings(string version, bool separateTimes)
    {
        var document = new XmlDocument();
        document.LoadXml($"<Settings><Version>{version}</Version><VisualSplitCount>6</VisualSplitCount><SplitPreviewCount>2</SplitPreviewCount><Comparison>Personal Best</Comparison><ShowSplitTimes>{separateTimes}</ShowSplitTimes><SplitNamesColor>FF123456</SplitNamesColor><UseTextColor>False</UseTextColor></Settings>");
        var settings = new SplitsSettings(TestSupport.CreateState());
        settings.SetSettings(document.DocumentElement);
        Assert.Equal(separateTimes ? 2 : 1, settings.ColumnsList.Count);
        Assert.Equal(separateTimes ? "Delta" : "Delta or Split Time", settings.ColumnsList[0].Type);
        Assert.Equal("Personal Best", settings.ColumnsList[0].Comparison);
        Assert.Equal(6f, settings.SplitHeight);
        Assert.Equal(24f, settings.IconSize);
        if (version == "1.2")
        {
            Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), settings.BeforeNamesColor);
            Assert.True(settings.OverrideTextColor);
        }
    }

    [Fact]
    public void LoadSplitsThroughFactoryFromWindowsLayout()
    {
        var document = new XmlDocument();
        document.Load(TestSupport.SamplePath("DefaultLayout.lsl"));
        foreach (XmlNode node in document.SelectNodes("//Component").Cast<XmlNode>().ToList())
        {
            if (node["Path"].InnerText != "LiveSplit.Splits.dll")
            {
                node.ParentNode.RemoveChild(node);
            }
        }
        using var stream = new MemoryStream();
        document.Save(stream);
        stream.Position = 0;
        ILayout layout = new XMLLayoutFactory(stream).Create(TestSupport.CreateState());
        var component = Assert.IsType<SplitsComponent>(Assert.Single(layout.LayoutComponents).Component);
        var settings = Assert.IsType<SplitsSettings>(component.GetSettingsControl(LayoutMode.Vertical));
        Assert.True(settings.DisplayIcons);
        Assert.Equal(2, settings.ColumnsList.Count);
        Assert.Same(settings, component.GetSettingsControl(LayoutMode.Horizontal));
    }

    [Fact]
    public void ExposeModeDependenciesAndSwatches()
    {
        var settings = new SplitsSettings(TestSupport.CreateState()) { Mode = LayoutMode.Vertical, ShowColumnLabels = true };
        settings.GradientString = "Plain";
        settings.BackgroundSwatch2Color = Color.Red;
        Assert.Equal(Color.Red, settings.BackgroundColor);
        Assert.False(settings.IsColor1Visible);
        settings.GradientString = "Vertical";
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal(Color.Blue, settings.BackgroundColor2);
        settings.SplitGradientString = "Plain";
        settings.CurrentSplitBottomSwatchColor = Color.Green;
        Assert.Equal(Color.Green, settings.CurrentSplitTopColor);
        settings.SizeValue = 42;
        Assert.Equal(4.2f, settings.SplitHeight);
        settings.Mode = LayoutMode.Horizontal;
        Assert.True(settings.Display2RowsShown);
        Assert.False(settings.IsDisplay2RowsEnabled || settings.ShowColumnLabelsShown || settings.IsLabelsColorEnabled);
        settings.Display2RowsShown = false;
        settings.ShowColumnLabelsShown = false;
        Assert.False(settings.Display2Rows);
        Assert.True(settings.ShowColumnLabels);
        settings.SizeValue = 85;
        Assert.Equal(85f, settings.SplitWidth);
        Assert.Equal((5f, 120f), (settings.SizeMinimum, settings.SizeMaximum));
        settings.VisualSplitCount = 3;
        settings.SplitPreviewCount = 1;
        settings.VisualSplitCount = 1;
        Assert.Equal(0, settings.SplitPreviewCount);
        settings.ShowBlankSplits = false;
        settings.DisplayIcons = false;
        Assert.False(settings.IsLockLastSplitEnabled || settings.IsIconOptionsEnabled);
    }

    [Fact]
    public void EditMoveRemoveAndClearObservableColumns()
    {
        var settings = new SplitsSettings(TestSupport.CreateState());
        var actions = new List<NotifyCollectionChangedAction>();
        settings.ColumnsList.CollectionChanged += (_, e) => actions.Add(e.Action);
        ColumnSettings first = settings.ColumnsList[0];
        ColumnSettings last = settings.AddColumn();
        Assert.True(last.CanMoveUp);
        Assert.False(last.CanMoveDown);
        last.MoveDown();
        last.MoveUp();
        Assert.Same(last, settings.ColumnsList[1]);
        last.MoveUp();
        Assert.Same(last, settings.ColumnsList[0]);
        Assert.False(last.CanMoveUp);
        Assert.True(last.CanMoveDown);
        last.Remove();
        Assert.DoesNotContain(last, settings.ColumnsList);
        settings.ColumnsList.Clear();
        first.Remove();
        first.MoveUp();
        Assert.Empty(settings.ColumnsList);
        Assert.Contains(NotifyCollectionChangedAction.Move, actions);
        Assert.Contains(NotifyCollectionChangedAction.Reset, actions);
        Assert.False(settings.AddColumn().CanMoveUp);
    }

    [Theory]
    [InlineData("ColumnName", "Route")]
    [InlineData("Type", "Segment Time")]
    [InlineData("Comparison", "Personal Best")]
    [InlineData("TimingMethod", "Game Time")]
    public void NotifyColumnEdits(string propertyName, string value)
    {
        var settings = new SplitsSettings(TestSupport.CreateState());
        ColumnSettings column = settings.ColumnsList[0];
        var raised = new List<string>();
        column.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        var property = typeof(ColumnSettings).GetProperty(propertyName);
        property.SetValue(column, value);
        Assert.Contains(propertyName, raised);
        raised.Clear();
        property.SetValue(column, value);
        Assert.Empty(raised);
        column.ColumnName = "New name";
        Assert.Contains("New name", column.GroupTitle);
        column.Type = "Delta";
        column.Comparison = BestSplitTimesComparisonGenerator.ComparisonName;
        column.Type = "Segment Delta";
        Assert.Equal("Current Comparison", column.Comparison);
        Assert.DoesNotContain(BestSplitTimesComparisonGenerator.ComparisonName, column.ComparisonOptions);
        column.Comparison = "Deleted comparison";
        column.Initialize();
        Assert.Contains("Deleted comparison", column.ComparisonOptions);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical, false)]
    [InlineData(LayoutMode.Vertical, true)]
    [InlineData(LayoutMode.Horizontal, false)]
    public void RenderEightSegmentsMidRunWithIcons(LayoutMode mode, bool twoRows)
    {
        IRun run = TestSupport.CreateRun();
        string[] names = ["Phantoon", "Gravity Suit", "Draygon", "Ridley", "Mother Brain"];
        for (int i = 0; i < names.Length; i++)
        {
            run.Add(new Segment(names[i], new Time(TimeSpan.FromSeconds(500 + (i * 130))), new Time(TimeSpan.FromSeconds(100))));
        }
        foreach (ISegment segment in run)
        {
            segment.Icon = TestSupport.CreateIcon();
        }
        LiveSplitState state = TestSupport.CreateState(run);
        state.Layout.Mode = mode;
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(300), 4);
        using var component = new SplitsComponent(state);
        var settings = (SplitsSettings)component.GetSettingsControl(mode);
        settings.VisualSplitCount = 6;
        settings.Display2Rows = twoRows;
        settings.ShowColumnLabels = true;
        settings.SplitWidth = 65;
        string suffix = mode == LayoutMode.Horizontal ? "horizontal" : twoRows ? "vertical-two-rows" : "vertical";
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 460 : 90, "splits-eight-midrun-" + suffix + ".png");
        int gold = 0, blue = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 230 && pixel.G > 180 && pixel.B < 40) gold++;
                if (pixel.B > 90 && pixel.B > pixel.R * 1.5 && pixel.B > pixel.G * 1.3) blue++;
            }
        }
        Assert.True(gold > 600, "six visible segment icons should contain gold centers");
        Assert.True(blue > 600, "the current segment should have a blue highlight");
        Assert.True(mode == LayoutMode.Vertical ? bitmap.Height > 180 : bitmap.Width > 600);
    }
}
