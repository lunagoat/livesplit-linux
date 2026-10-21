using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.Components.Tests;

public class LayoutLoadingMust
{
    public LayoutLoadingMust()
    {
        TestSupport.EnsureFactories();
    }

    [Fact]
    public void RegisterAllFourFactoriesThroughComponentManager()
    {
        foreach (string name in new[] { "LiveSplit.Timer.dll", "LiveSplit.Title.dll", "LiveSplit.Text.dll", "LiveSplit.BlankSpace.dll" })
        {
            Assert.True(ComponentManager.ComponentFactories.ContainsKey(name), name);
        }

        IComponent timer = ComponentManager.LoadLayoutComponent("LiveSplit.Timer.dll", TestSupport.CreateState()).Component;
        Assert.IsType<Timer>(timer);
        Assert.IsType<TimerSettings>(timer.GetSettingsControl(LayoutMode.Horizontal));
        Assert.Equal(LayoutMode.Horizontal, ((TimerSettings)timer.GetSettingsControl(LayoutMode.Horizontal)).Mode);
        Assert.Same(((Timer)timer).Settings, timer.GetSettingsControl(LayoutMode.Vertical));
        Assert.Equal(LayoutMode.Vertical, ((Timer)timer).Settings.Mode);
    }

    [Fact]
    public void LoadTimerAndTitleFromTheWindowsDefaultLayout()
    {
        LiveSplitState state = TestSupport.CreateState();

        // The layout also holds Splits and PreviousSegment, which are not ported yet and make the factory throw: drop them.
        var document = new System.Xml.XmlDocument();
        document.Load(TestSupport.SamplePath("DefaultLayout.lsl"));
        foreach (System.Xml.XmlNode node in document.SelectNodes("//Component").Cast<System.Xml.XmlNode>().ToList())
        {
            if (node["Path"].InnerText is not ("LiveSplit.Title.dll" or "LiveSplit.Timer.dll"))
            {
                node.ParentNode.RemoveChild(node);
            }
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        stream.Position = 0;
        ILayout layout = new XMLLayoutFactory(stream).Create(state);

        var title = layout.LayoutComponents.Select(x => x.Component).OfType<Title>().Single();
        var timer = layout.LayoutComponents.Select(x => x.Component).OfType<Timer>().Single();

        Assert.True(title.Settings.ShowAttemptCount);
        Assert.False(title.Settings.ShowFinishedRunsCount);
        Assert.False(title.Settings.OverrideTitleColor);
        Assert.Equal(Color.FromArgb(255, 0x2A, 0x2A, 0x2A), title.Settings.BackgroundColor);
        Assert.Equal(Color.FromArgb(255, 0x13, 0x13, 0x13), title.Settings.BackgroundColor2);
        Assert.Equal(GradientType.Vertical, title.Settings.BackgroundGradient);
        Assert.True(title.Settings.DisplayGameIcon);
        Assert.Equal(AlignmentType.Auto, title.Settings.TextAlignment);
        Assert.True(title.Settings.ShowVariables);
        Assert.NotNull(title.Settings.TitleFont); // BinaryFormatter font blob written by .NET Framework
        Assert.False(title.Settings.OverrideTitleFont);

        Assert.Equal(69, timer.Settings.TimerHeight);
        Assert.Equal(225, timer.Settings.TimerWidth);
        Assert.Equal(("1", ".23"), (timer.Settings.DigitsFormat, timer.Settings.Accuracy)); // from TimerAccuracy = Hundredths
        Assert.True(timer.Settings.ShowGradient);
        Assert.False(timer.Settings.OverrideSplitColors);
        Assert.Equal(Color.FromArgb(255, 0xAA, 0xAA, 0xAA), timer.Settings.TimerColor);
        Assert.Equal(Color.FromArgb(0, 0, 0, 0), timer.Settings.BackgroundColor);
        Assert.Equal(DeltasGradientType.Plain, timer.Settings.BackgroundGradient);
        Assert.Equal("Current Timing Method", timer.Settings.TimingMethod);
    }

    private const string TextAndBlankSpaceLayout = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Layout version="1.6.1">
          <X>0</X><Y>0</Y><VerticalWidth>300</VerticalWidth><VerticalHeight>300</VerticalHeight>
          <HorizontalWidth>500</HorizontalWidth><HorizontalHeight>50</HorizontalHeight><Mode>Vertical</Mode>
          <Settings>
            <TextColor>FFFFFFFF</TextColor><BackgroundColor>FF000000</BackgroundColor><ThinSeparatorsColor>FFFFFFFF</ThinSeparatorsColor>
            <SeparatorsColor>FFFFFFFF</SeparatorsColor><PersonalBestColor>FF16A6FF</PersonalBestColor><AheadGainingTimeColor>FF00CC36</AheadGainingTimeColor>
            <AheadLosingTimeColor>FF52CC73</AheadLosingTimeColor><BehindGainingTimeColor>FFCC5C52</BehindGainingTimeColor><BehindLosingTimeColor>FFCC1200</BehindLosingTimeColor>
            <BestSegmentColor>FFD8AF1F</BestSegmentColor><NotRunningColor>FFACACAC</NotRunningColor><BackgroundColor2>FF000000</BackgroundColor2>
          </Settings>
          <Components>
            <Component>
              <Path>LiveSplit.Text.dll</Path>
              <Settings>
                <Version>1.5</Version>
                <TextColor>FFFF8000</TextColor><OverrideTextColor>True</OverrideTextColor>
                <TimeColor>FF00FF00</TimeColor><OverrideTimeColor>False</OverrideTimeColor>
                <BackgroundColor>FF101010</BackgroundColor><BackgroundColor2>FF202020</BackgroundColor2><BackgroundGradient>Horizontal</BackgroundGradient>
                <Text1>Deaths</Text1><Text2>12</Text2><Display2Rows>True</Display2Rows><CustomVariable>False</CustomVariable>
              </Settings>
            </Component>
            <Component>
              <Path>LiveSplit.BlankSpace.dll</Path>
              <Settings>
                <Version>1.7</Version>
                <SpaceHeight>42</SpaceHeight><SpaceWidth>77</SpaceWidth>
                <BackgroundColor>FF123456</BackgroundColor><BackgroundColor2>FF654321</BackgroundColor2><BackgroundGradient>Vertical</BackgroundGradient>
              </Settings>
            </Component>
          </Components>
        </Layout>
        """;

    [Fact]
    public void LoadTextAndBlankSpaceFromLayoutXml()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TextAndBlankSpaceLayout));
        ILayout layout = new XMLLayoutFactory(stream).Create(TestSupport.CreateState());

        var text = layout.LayoutComponents.Select(x => x.Component).OfType<TextComponent>().Single();
        Assert.Equal(("Deaths", "12"), (text.Settings.Text1, text.Settings.Text2));
        Assert.True(text.Settings.OverrideTextColor);
        Assert.Equal(Color.FromArgb(255, 0xFF, 0x80, 0), text.Settings.TextColor);
        Assert.True(text.Settings.Display2Rows);
        Assert.Equal(GradientType.Horizontal, text.Settings.BackgroundGradient);
        Assert.Equal("Deaths 12", text.ComponentName);

        var blank = layout.LayoutComponents.Select(x => x.Component).OfType<BlankSpace>().Single();
        Assert.Equal((42f, 77f), (blank.Settings.SpaceHeight, blank.Settings.SpaceWidth));
        Assert.Equal(42f, blank.VerticalHeight);
        Assert.Equal(77f, blank.HorizontalWidth);
        Assert.Equal(Color.FromArgb(255, 0x12, 0x34, 0x56), blank.Settings.BackgroundColor);
    }
}
