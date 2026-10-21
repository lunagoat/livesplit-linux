using LiveSplit.UI;
using LiveSplit.UI.Components;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class HotkeyIndicatorTests
{
    [Fact]
    public void RoundTripAndPreserveIndependentDimensions()
    {
        var settings = new HotkeyIndicatorSettings();
        Assert.Equal(3, settings.IndicatorHeight);
        Assert.Equal(Color.FromArgb(41, 204, 84), settings.HotkeysOnColor);
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        settings.Mode = LayoutMode.Vertical;
        settings.SizeValue = 8;
        settings.Mode = LayoutMode.Horizontal;
        Assert.Equal("Width:", settings.SizeLabel);
        Assert.Equal(3, settings.SizeValue);
        settings.SizeValue = 12;
        Assert.Equal(8, settings.IndicatorHeight);
        Assert.Contains(nameof(settings.SizeLabel), raised);
        Assert.Contains(nameof(settings.SizeValue), raised);
        Assert.Equal((1, 50), (settings.SizeMinimum, settings.SizeMaximum));
        settings.HotkeysOnColor = Color.FromArgb(Color.Gold.ToArgb());
        settings.HotkeysOffColor = Color.FromArgb(Color.Blue.ToArgb());
        var xml = settings.GetSettings(new XmlDocument());
        var copy = new HotkeyIndicatorSettings();
        copy.SetSettings(xml);
        Assert.Equal(xml.OuterXml, copy.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), copy.GetSettingsHashCode());
        var old = new XmlDocument();
        old.LoadXml("<Settings><HotkeysOnColor>FF29CC54</HotkeysOnColor><HotkeysOffColor>FFCC3729</HotkeysOffColor></Settings>");
        copy.SetSettings(old.DocumentElement);
        Assert.Equal((3f, 3f), (copy.IndicatorHeight, copy.IndicatorWidth));
    }

    [Theory]
    [InlineData(LayoutMode.Vertical, true)]
    [InlineData(LayoutMode.Vertical, false)]
    [InlineData(LayoutMode.Horizontal, true)]
    [InlineData(LayoutMode.Horizontal, false)]
    public void RenderActualHotkeyProfileState(LayoutMode mode, bool enabled)
    {
        var state = TestSupport.CreateState();
        state.CurrentHotkeyProfile = state.Settings.HotkeyProfiles.Keys.First();
        state.Settings.HotkeyProfiles[state.CurrentHotkeyProfile].GlobalHotkeysEnabled = enabled;
        using var component = Assert.IsType<HotkeyIndicator>(ComponentManager.LoadLayoutComponent("LiveSplit.HotkeyIndicator.dll", state).Component);
        var settings = Assert.IsType<HotkeyIndicatorSettings>(component.GetSettingsControl(mode));
        Assert.Equal(mode, settings.Mode);
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 60, $"hotkeyindicator-{mode}-{enabled}.png");
        Color expected = enabled ? settings.HotkeysOnColor : settings.HotkeysOffColor;
        Assert.Equal(expected.ToArgb(), bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).ToArgb());
    }
}
