using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.WorldRecord.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class WorldRecordTests
{
    [Fact]
    public void PreserveSettingsXmlAndHash()
    {
        var settings = new WorldRecordSettings
        {
            TextColor = Color.FromArgb(128, 12, 34, 56), OverrideTextColor = true,
            TimeColor = Color.FromArgb(255, 67, 89, 10), OverrideTimeColor = true,
            BackgroundColor = Color.FromArgb(255, 22, 33, 44),
            BackgroundColor2 = Color.FromArgb(255, 55, 66, 77),
            BackgroundGradient = GradientType.Horizontal, Display2Rows = true,
            CenteredText = false, FilterRegion = true, FilterPlatform = true,
            FilterVariables = true, FilterSubcategories = false, TimingMethod = "Game Time",
            WRPrecision = WorldRecordPrecisionType.Milliseconds,
        };
        var doc = new XmlDocument();
        XmlNode xml = settings.GetSettings(doc);
        var loaded = new WorldRecordSettings();
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
    }

    [Fact]
    public void PreserveDefaultsAndReadOlderLayouts()
    {
        var settings = new WorldRecordSettings();
        Assert.Equal(Color.White.ToArgb(), settings.TextColor.ToArgb());
        Assert.Equal(Color.White.ToArgb(), settings.TimeColor.ToArgb());
        Assert.False(settings.OverrideTextColor || settings.OverrideTimeColor || settings.Display2Rows);
        Assert.Equal(GradientType.Plain, settings.BackgroundGradient);
        Assert.Equal(0, settings.BackgroundColor.A);
        var doc = new XmlDocument();
        doc.LoadXml("<Settings><Version>1.0</Version><BackgroundGradient>Plain</BackgroundGradient></Settings>");
        settings.SetSettings(doc.DocumentElement);
        Assert.False(settings.Display2Rows);
        Assert.True(new WorldRecordSettings().CenteredText);
        Assert.True(settings.FilterSubcategories);
        Assert.Equal("Default for Leaderboard", settings.TimingMethod);
        Assert.Equal(WorldRecordPrecisionType.FromLeaderboard, settings.WRPrecision);
    }

    [Fact]
    public void NotifyComputedViewStateWithoutOverwritingSavedChoices()
    {
        var settings = new WorldRecordSettings { Mode = LayoutMode.Vertical };
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.BackgroundSwatch2Color = Color.Red;
        Assert.Equal(Color.Red, settings.BackgroundColor);
        settings.GradientString = "Vertical";
        Assert.True(settings.IsColor1Visible);
        Assert.Contains(nameof(settings.IsColor1Visible), raised);
        Assert.Contains(nameof(settings.BackgroundSwatch2Color), raised);
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal(Color.Blue, settings.BackgroundColor2);
        settings.OverrideTextColor = settings.OverrideTimeColor = true;
        Assert.True(settings.IsTextColorEnabled && settings.IsTimeColorEnabled);
        Assert.Contains(nameof(settings.IsTextColorEnabled), raised);
        Assert.Contains(nameof(settings.IsTimeColorEnabled), raised);
        raised.Clear();
        settings.Mode = LayoutMode.Horizontal;
        Assert.False(settings.IsDisplay2RowsEnabled);
        Assert.True(settings.Display2RowsShown);
        Assert.False(settings.Display2Rows);
        Assert.Contains(nameof(settings.Display2RowsShown), raised);
        settings.Mode = LayoutMode.Vertical;
        Assert.False(settings.Display2RowsShown);
        Assert.True(settings.CenteredTextShown);
        settings.Display2Rows = true;
        Assert.False(settings.IsCenteredTextEnabled || settings.CenteredTextShown);
        Assert.True(settings.CenteredText);
        Assert.Contains(nameof(settings.CenteredTextShown), raised);
        settings.Display2Rows = false;
        Assert.True(settings.CenteredTextShown);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void LoadFactoryAndRenderThroughComponentRenderer(LayoutMode mode)
    {
        // Empty names keep RunMetadata's lazy Game/Category values null. Neither metadata
        // resolution nor RefreshWorldRecord can reach a network API with this fixture.
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.Add(new Segment("Offline", new Time(TimeSpan.FromSeconds(60)), new Time(TimeSpan.FromSeconds(58))));
        LiveSplitState state = TestSupport.CreateState(run);
        Assert.Null(state.Run.Metadata.Game);
        Assert.Null(state.Run.Metadata.Category);
        using IComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.WorldRecord.dll", state).Component;
        Assert.IsType<WorldRecordComponent>(component);
        var settings = Assert.IsType<WorldRecordSettings>(component.GetSettingsControl(mode));
        Assert.Equal(mode, settings.Mode);
        settings.BackgroundColor = Color.FromArgb(30, 30, 60);
        settings.BackgroundColor2 = Color.FromArgb(10, 10, 20);
        settings.BackgroundGradient = GradientType.Vertical;
        settings.Display2Rows = mode == LayoutMode.Horizontal;
        // Await the public Update's missing-metadata refresh before drawing, so the
        // real unknown-record presentation is stable and no background work is left.
        state.Layout.Mode = mode;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        component.Update(null, state, 360, 60, mode);
        Assert.True(System.Threading.SpinWait.SpinUntil(
            () => !(bool)typeof(WorldRecordComponent).GetProperty("IsLoading", flags).GetValue(component), 5000));
        Assert.Null(((WorldRecordComponent)component).WorldRecord);
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 60, "worldrecord-" + mode + ".png");
        Assert.True(bitmap.Width > 50 && bitmap.Height > 15);
        Assert.True(bitmap.GetPixel(1, 1).B > bitmap.GetPixel(1, bitmap.Height - 2).B);
        int brightPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).R > 150) { brightPixels++; }
            }
        }
        Assert.True(brightPixels > 30, "Expected visible label and value text");
        Assert.NotNull(TestSupport.ContentBounds(bitmap, bitmap.GetPixel(1, 1), new Rectangle(0, 0, bitmap.Width, bitmap.Height)));
    }
}
