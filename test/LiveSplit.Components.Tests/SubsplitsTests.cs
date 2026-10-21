extern alias Subsplits;

using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;
using SubSettings = Subsplits::LiveSplit.UI.Components.SplitsSettings;
using SubComponent = Subsplits::LiveSplit.UI.Components.SplitsComponent;
using SubColumn = Subsplits::LiveSplit.UI.Components.ColumnSettings;

namespace LiveSplit.Components.Tests;

public class SubsplitsTests
{
    internal static void PreparePropertyChange(SettingsBase model, string property)
    {
        if (model is not SubSettings settings) return;
        settings.Mode = LayoutMode.Vertical;
        foreach (string accuracy in new[] { "DeltasAccuracy", "SplitTimesAccuracy", "HeaderAccuracy", "SectionTimerAccuracy" })
        {
            string prefix = accuracy == "DeltasAccuracy" ? "IsDelta" : accuracy == "SplitTimesAccuracy" ? "IsSplitTimes" : "Is" + accuracy;
            if (property.StartsWith(prefix))
                typeof(SubSettings).GetProperty(accuracy).SetValue(settings, property.EndsWith("Seconds") ? TimeAccuracy.Tenths : TimeAccuracy.Seconds);
        }
        if (property == "IsNormalSubsplits") settings.IsShowSubsplits = true;
        if (property == "IsShowSubsplits" || property == "IsHideSubsplits") settings.IsNormalSubsplits = true;
    }

    [Fact]
    public void PreserveDefaultsAndRoundTripSectionsAndEveryColumnType()
    {
        var settings = new SubSettings(TestSupport.CreateState());
        Assert.Equal((8, 1, 0, 20f, 6f), (settings.VisualSplitCount, settings.SplitPreviewCount, settings.MinimumMajorSplits, settings.SplitWidth, settings.SplitHeight));
        Assert.True(settings.IsNormalSubsplits && settings.ShowHeader && settings.SectionTimer && settings.HeaderText && settings.HeaderTimes);
        Assert.False(settings.ShowThinSeparators);
        settings.BackgroundColor = Color.FromArgb(50, 12, 23, 34);
        settings.MinimumMajorSplits = 2;
        settings.VisualSplitCount = 10;
        settings.SplitPreviewCount = 3;
        settings.OverrideHeaderColor = settings.OverrideSubsplitColor = true;
        settings.HeaderComparison = "Missing <route & comparison>";
        settings.HeaderTimingMethod = "Game Time";
        settings.HeaderAccuracy = TimeAccuracy.Milliseconds;
        settings.SectionTimerAccuracy = TimeAccuracy.Hundredths;
        settings.HeaderGradientString = "Horizontal";
        settings.SubsplitGradientString = "Vertical";
        settings.HeaderTopColor = Color.FromArgb(120, 13, 24, 35);
        settings.SubsplitBottomColor = Color.FromArgb(150, 45, 56, 67);
        settings.ColumnsList.Clear();
        foreach (string type in SubColumn.ColumnTypeOptions)
        {
            var column = settings.AddColumn();
            column.ColumnName = "<route & time> " + type;
            column.Type = type;
            column.Comparison = "Missing comparison";
            column.TimingMethod = "Game Time";
        }
        XmlNode xml = settings.GetSettings(new XmlDocument());
        var loaded = new SubSettings(TestSupport.CreateState());
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(SubColumn.ColumnTypeOptions, loaded.ColumnsList.Select(x => x.Type));
        Assert.Contains(settings.HeaderComparison, loaded.HeaderComparisonOptions);
        loaded.ColumnsList.Clear();
        settings.SetSettings(loaded.GetSettings(new XmlDocument()));
        Assert.Empty(settings.ColumnsList);
    }

    [Theory]
    [InlineData("1.0", false)]
    [InlineData("1.2", true)]
    [InlineData("1.6", true)]
    public void UpgradeLegacyXml(string version, bool splitTimes)
    {
        var document = new XmlDocument();
        document.LoadXml($"<Settings><Version>{version}</Version><Comparison>Personal Best</Comparison><ShowSplitTimes>{splitTimes}</ShowSplitTimes><SplitNamesColor>FF123456</SplitNamesColor><UseTextColor>False</UseTextColor></Settings>");
        var settings = new SubSettings(TestSupport.CreateState());
        settings.SetSettings(document.DocumentElement);
        Assert.Equal("Personal Best", settings.HeaderComparison);
        Assert.Equal(splitTimes ? 2 : 1, settings.ColumnsList.Count);
        Assert.Equal(splitTimes ? "Delta" : "Delta or Split Time", settings.ColumnsList[0].Type);
        Assert.Equal(3.6f, settings.SplitHeight);
        if (version == "1.2")
        {
            Assert.True(settings.OverrideTextColor);
            Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), settings.BeforeNamesColor);
        }
    }

    [Fact]
    public void ExposeSectionDependenciesAndNotifyComputedProperties()
    {
        var settings = new SubSettings(TestSupport.CreateState()) { Mode = LayoutMode.Vertical };
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.IsHideSubsplits = true;
        Assert.False(settings.IsIndentSubsplitsEnabled || settings.IsIndentSectionSplitEnabled || settings.IsMinimumMajorSplitsEnabled);
        Assert.Contains(nameof(settings.IsIndentSectionSplitEnabled), raised);
        settings.CurrentSectionOnly = true;
        settings.IsShowSubsplits = true;
        Assert.False(settings.CurrentSectionOnly || settings.IsCurrentSectionOnlyEnabled);
        Assert.True(settings.IsIndentSubsplitsEnabled);
        settings.IsNormalSubsplits = true;
        settings.IndentSubsplits = false;
        Assert.False(settings.IsIndentSectionSplitEnabled);
        settings.OverrideHeaderColor = true;
        settings.ShowHeader = false;
        Assert.False(settings.IsHeaderOptionsEnabled || settings.IsHeaderTextColorEnabled || settings.IsSectionTimerOptionsEnabled);
        settings.ShowHeader = true;
        settings.SectionTimer = false;
        Assert.True(settings.IsHeaderTextColorEnabled);
        Assert.False(settings.IsSectionTimerOptionsEnabled);
        settings.SubsplitGradientString = settings.HeaderGradientString = "Plain";
        settings.SubsplitBottomSwatchColor = Color.Red;
        settings.HeaderBottomSwatchColor = Color.Blue;
        Assert.Equal(Color.Red, settings.SubsplitTopColor);
        Assert.Equal(Color.Blue, settings.HeaderTopColor);
        settings.HeaderGradientString = "Vertical";
        settings.HeaderBottomSwatchColor = Color.Green;
        Assert.Equal(Color.Green, settings.HeaderBottomColor);
        int layoutChanges = 0;
        settings.SplitLayoutChanged += (_, _) => layoutChanges++;
        settings.IndentBlankIcons = false;
        Assert.Equal(1, layoutChanges);
        settings.SetSettings(new SubSettings(TestSupport.CreateState()).GetSettings(new XmlDocument()));
        Assert.Equal(1, layoutChanges);
    }

    [Fact]
    public void EditReorderAndRemoveObservableColumns()
    {
        var settings = new SubSettings(TestSupport.CreateState());
        var column = settings.AddColumn();
        Assert.True(column.CanMoveUp);
        Assert.False(column.CanMoveDown);
        column.MoveUp();
        column.MoveUp();
        Assert.Same(column, settings.ColumnsList[0]);
        Assert.False(column.CanMoveUp);
        var raised = new List<string>();
        column.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        column.ColumnName = "Route";
        Assert.Contains(nameof(column.ColumnName), raised);
        Assert.Contains(nameof(column.GroupTitle), raised);
        column.Type = "Segment Time";
        Assert.Contains(nameof(column.ComparisonOptions), raised);
        column.MoveDown();
        Assert.Same(column, settings.ColumnsList[1]);
        column.Remove();
        Assert.DoesNotContain(column, settings.ColumnsList);
        settings.ColumnsList.Clear();
        column.Remove();
        Assert.Empty(settings.ColumnsList);
        Assert.False(settings.AddColumn().CanMoveUp);
    }

    [Fact]
    public void LoadSubsplitsFromWindowsLayoutThroughFactory()
    {
        var document = new XmlDocument();
        document.Load(TestSupport.SamplePath("DefaultLayout.lsl"));
        foreach (XmlNode node in document.SelectNodes("//Component").Cast<XmlNode>().ToList())
        {
            if (node["Path"].InnerText != "LiveSplit.Splits.dll") node.ParentNode.RemoveChild(node);
            else
            {
                node["Path"].InnerText = "LiveSplit.Subsplits.dll";
                node.ReplaceChild(document.ImportNode(new SubSettings(TestSupport.CreateState()).GetSettings(new XmlDocument()), true), node["Settings"]);
            }
        }
        using var stream = new MemoryStream();
        document.Save(stream);
        stream.Position = 0;
        ILayout layout = new XMLLayoutFactory(stream).Create(TestSupport.CreateState());
        using var component = Assert.IsType<SubComponent>(Assert.Single(layout.LayoutComponents).Component);
        var settings = Assert.IsType<SubSettings>(component.GetSettingsControl(LayoutMode.Vertical));
        Assert.True(settings.ShowHeader);
        Assert.Equal(2, settings.ColumnsList.Count);
        Assert.Same(settings, component.GetSettingsControl(LayoutMode.Horizontal));
    }

    [Theory]
    [InlineData(LayoutMode.Vertical, "contextual")]
    [InlineData(LayoutMode.Vertical, "show")]
    [InlineData(LayoutMode.Vertical, "hide")]
    [InlineData(LayoutMode.Horizontal, "contextual")]
    public void RenderRunWithSubsplitSections(LayoutMode mode, string visibility)
    {
        IRun run = TestSupport.CreateRun();
        run.Clear();
        string[] names = ["-Ceres", "-Morph Ball", "{Crateria} Bombs", "-Charge Beam", "-Spazer", "{Brinstar} Kraid", "-Ice Beam", "{Norfair} Ridley", "Escape"];
        for (int i = 0; i < names.Length; i++)
            run.Add(new Segment(names[i], new Time(TimeSpan.FromSeconds(60 * (i + 1))), new Time(TimeSpan.FromSeconds(55))) { Icon = TestSupport.CreateIcon() });
        LiveSplitState state = TestSupport.CreateState(run);
        state.Layout.Mode = mode;
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(240), 4);
        using var component = new SubComponent(state);
        var settings = (SubSettings)component.GetSettingsControl(mode);
        settings.VisualSplitCount = 8;
        settings.MinimumMajorSplits = 1;
        settings.ShowColumnLabels = true;
        settings.SplitWidth = 60;
        settings.IsShowSubsplits = visibility == "show";
        settings.IsHideSubsplits = visibility == "hide";
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 500 : 110, $"subsplits-sections-{mode}-{visibility}.png");
        Assert.NotNull(TestSupport.ContentBounds(bitmap, Color.FromArgb(15, 15, 15), new Rectangle(0, 0, bitmap.Width, bitmap.Height)));
        int gold = 0, blue = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R > 230 && pixel.G > 180 && pixel.B < 40) gold++;
                if (pixel.B > 90 && pixel.B > pixel.R * 1.5 && pixel.B > pixel.G * 1.3) blue++;
            }
        Assert.True(gold > 500, "section run icons must be visible");
        Assert.True(blue > 500, "current subsplit or containing section must be highlighted");
    }
}
