using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class GraphTests
{
    [Fact]
    public void PreserveSettingsXmlAndHash()
    {
        var settings = new GraphSettings
        {
            GraphHeight = 175, GraphWidth = 320, IsLiveGraph = false, FlipGraph = true, ShowBestSegments = true,
            Comparison = "Missing", BehindGraphColor = Color.FromArgb(Color.Red.ToArgb()), AheadGraphColor = Color.FromArgb(Color.Green.ToArgb()),
            GridlinesColor = Color.FromArgb(Color.Blue.ToArgb()), GraphColor = Color.FromArgb(Color.Yellow.ToArgb()), GraphGoldColor = Color.FromArgb(Color.Purple.ToArgb()),
            PartialFillColorAhead = Color.FromArgb(10, 20, 30, 40), CompleteFillColorAhead = Color.FromArgb(Color.Cyan.ToArgb()),
            PartialFillColorBehind = Color.FromArgb(70, 80, 90, 100), CompleteFillColorBehind = Color.FromArgb(Color.Pink.ToArgb()),
            ShadowsColor = Color.FromArgb(40, 10, 20, 30), GraphLinesColor = Color.FromArgb(Color.Orange.ToArgb()),
        };
        XmlNode xml = settings.GetSettings(new XmlDocument());
        var loaded = new GraphSettings();
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
    }

    [Fact]
    public void PreserveDefaultsAndLegacyFillColors()
    {
        var settings = new GraphSettings();
        Assert.Equal((120f, 180f), (settings.GraphHeight, settings.GraphWidth));
        Assert.True(settings.IsLiveGraph);
        Assert.False(settings.FlipGraph || settings.ShowBestSegments);
        Assert.Equal(Color.FromArgb(115, 40, 40), settings.BehindGraphColor);
        Assert.Equal(Color.FromArgb(40, 115, 52), settings.AheadGraphColor);
        var doc = new XmlDocument();
        doc.LoadXml("<Settings><Version>1.1</Version><PartialFillColor>19FFFFFF</PartialFillColor><CompleteFillColor>32FFFFFF</CompleteFillColor></Settings>");
        settings.SetSettings(doc.DocumentElement);
        Assert.Equal(Color.FromArgb(25, 255, 255, 255), settings.PartialFillColorAhead);
        Assert.Equal(settings.PartialFillColorAhead, settings.PartialFillColorBehind);
        Assert.Equal(Color.FromArgb(50, 255, 255, 255), settings.CompleteFillColorBehind);
        Assert.Equal(settings.CompleteFillColorAhead, settings.CompleteFillColorBehind);
    }

    [Fact]
    public void NotifyScaledDimensionsAndDependentViewState()
    {
        var settings = new GraphSettings { Mode = LayoutMode.Vertical, CurrentState = TestSupport.CreateState(), Comparison = "Missing" };
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.SizeValue = 30;
        Assert.Equal(150, settings.GraphHeight);
        Assert.Contains(nameof(settings.GraphHeightScaled), raised);
        Assert.Contains(nameof(settings.SizeValue), raised);
        settings.Mode = LayoutMode.Horizontal;
        settings.SizeValue = 32;
        Assert.Equal((320f, 150f), (settings.GraphWidth, settings.GraphHeight));
        Assert.Contains(nameof(settings.SizeLabel), raised);
        settings.ShowBestSegments = true;
        Assert.True(settings.IsBestSegmentColorEnabled);
        Assert.Contains(nameof(settings.IsBestSegmentColorEnabled), raised);
        Assert.Contains("Missing", settings.ComparisonOptions);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical, false, false)]
    [InlineData(LayoutMode.Vertical, true, false)]
    [InlineData(LayoutMode.Horizontal, false, false)]
    [InlineData(LayoutMode.Horizontal, true, false)]
    [InlineData(LayoutMode.Vertical, false, true)]
    [InlineData(LayoutMode.Horizontal, true, true)]
    public void RenderCrossingDeltasFillsGridlinesAndGold(LayoutMode mode, bool flipped, bool live)
    {
        LiveSplitState state = TestSupport.CreateState();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(live ? 450 : 400), 2);
        state.Run[0].SplitTime = new Time(TimeSpan.FromSeconds(55));
        state.Run[1].SplitTime = new Time(TimeSpan.FromSeconds(160));
        if (!live)
        {
            state.CurrentPhase = TimerPhase.Ended;
            state.CurrentSplitIndex = 3;
            state.Run[2].SplitTime = new Time(TimeSpan.FromSeconds(400));
        }
        using IComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.Graph.dll", state).Component;
        Assert.IsType<GraphCompositeComponent>(component);
        var settings = Assert.IsType<GraphSettings>(component.GetSettingsControl(mode));
        settings.GraphHeight = 180;
        settings.GraphWidth = 360;
        settings.FlipGraph = flipped;
        settings.IsLiveGraph = live;
        settings.ShowBestSegments = true;
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 180, $"graph-{mode}-flip{flipped}-live{live}.png");
        int red = 0, green = 0, gold = 0, white = 0;
        for (int y = 2; y < bitmap.Height - 2; y++)
        {
            for (int x = 2; x < bitmap.Width - 2; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (c.R > c.G * 1.5) { red++; }
                if (c.G > c.R * 1.5) { green++; }
                if (c.R > 170 && c.G > 130 && c.B < 80) { gold++; }
                if (c.R > 220 && c.G > 220 && c.B > 220) { white++; }
            }
        }
        Assert.True(red > 500 && green > 500, "Both ahead and behind backgrounds must render.");
        Assert.True(gold > 20, "Best segment graph line must render.");
        Assert.True(white > 20, "Regular graph line must render.");
        Color top = bitmap.GetPixel(bitmap.Width - 5, 4);
        Color bottom = bitmap.GetPixel(bitmap.Width - 5, bitmap.Height - 5);
        Assert.True(flipped ? top.G > top.R && bottom.R > bottom.G : top.R > top.G && bottom.G > bottom.R);
        var graph = ((GraphCompositeComponent)component).InternalComponent.VisibleComponents.OfType<GraphComponent>().Single();
        Assert.Equal(TimeSpan.FromSeconds(-5), graph.Deltas[0]);
        Assert.Equal(TimeSpan.FromSeconds(10), graph.Deltas[1]);
        Assert.Equal(live, graph.IsLiveDeltaActive);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void RenderIdleGraphWithSeparators(LayoutMode mode)
    {
        LiveSplitState state = TestSupport.CreateState();
        using var component = new GraphCompositeComponent(state);
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 120, $"graph-idle-{mode}.png");
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
        Assert.True(bitmap.GetPixel(10, 10).R > bitmap.GetPixel(10, 10).G);
        Assert.True(bitmap.GetPixel(bitmap.Width - 10, bitmap.Height - 10).G > bitmap.GetPixel(bitmap.Width - 10, bitmap.Height - 10).R);
    }
}
