using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class SettingsMust
{
    private static string Save(Func<XmlDocument, XmlNode> getSettings)
    {
        var doc = new XmlDocument();
        return getSettings(doc).OuterXml;
    }

    private static XmlNode ToNode(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.DocumentElement;
    }

    [Fact]
    public void RoundtripTimerSettings()
    {
        var settings = new TimerSettings
        {
            TimerHeight = 77,
            TimerWidth = 311,
            DecimalsSize = 22,
            DigitsFormat = "0:00:01",
            Accuracy = ".234",
            TimerColor = Color.FromArgb(10, 20, 30, 40),
            OverrideSplitColors = true,
            CenterTimer = true,
            ShowGradient = false,
            TimingMethod = "Game Time",
            BackgroundColor = Color.FromArgb(255, 1, 2, 3),
            BackgroundColor2 = Color.FromArgb(128, 4, 5, 6),
            BackgroundGradient = DeltasGradientType.HorizontalWithDeltaColor,
        };

        string xml = Save(settings.GetSettings);
        var loaded = new TimerSettings();
        loaded.SetSettings(ToNode(xml));

        Assert.Equal(xml, Save(loaded.GetSettings));
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(77, loaded.TimerHeight);
        Assert.Equal(311, loaded.TimerWidth);
        Assert.Equal(22, loaded.DecimalsSize);
        Assert.Equal("0:00:01", loaded.DigitsFormat);
        Assert.Equal(".234", loaded.Accuracy);
        Assert.Equal(Color.FromArgb(10, 20, 30, 40), loaded.TimerColor);
        Assert.True(loaded.OverrideSplitColors);
        Assert.True(loaded.CenterTimer);
        Assert.False(loaded.ShowGradient);
        Assert.Equal("Game Time", loaded.TimingMethod);
        Assert.Equal(DeltasGradientType.HorizontalWithDeltaColor, loaded.BackgroundGradient);
        Assert.Equal("Horizontal With Delta Color", loaded.GradientString);
    }

    [Fact]
    public void RoundtripTitleSettings()
    {
        var settings = new TitleSettings
        {
            ShowGameName = false,
            ShowCategoryName = false,
            ShowAttemptCount = false,
            ShowFinishedRunsCount = true,
            TextAlignment = AlignmentType.Center,
            SingleLine = true,
            DisplayGameIcon = false,
            ShowRegion = true,
            ShowPlatform = true,
            ShowVariables = false,
            TitleColor = Color.FromArgb(255, 9, 8, 7),
            OverrideTitleColor = true,
            BackgroundColor = Color.FromArgb(255, 1, 2, 3),
            BackgroundColor2 = Color.FromArgb(255, 3, 2, 1),
            BackgroundGradient = GradientType.Horizontal,
        };

        string xml = Save(settings.GetSettings);
        var loaded = new TitleSettings();
        loaded.SetSettings(ToNode(xml));

        Assert.Equal(xml, Save(loaded.GetSettings));
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(AlignmentType.Center, loaded.TextAlignment);
        Assert.Equal(1, new TitleSettings { TextAlignmentIndex = 1 }.TextAlignmentIndex);
        Assert.True(loaded.ShowCount);
        Assert.Equal(GradientType.Horizontal, loaded.BackgroundGradient);
    }

    [Fact]
    public void RoundtripTextSettings()
    {
        var settings = new TextComponentSettings
        {
            TextColor = Color.FromArgb(255, 9, 8, 7),
            OverrideTextColor = true,
            TimeColor = Color.FromArgb(255, 7, 8, 9),
            OverrideTimeColor = true,
            BackgroundColor = Color.FromArgb(255, 1, 2, 3),
            BackgroundColor2 = Color.FromArgb(255, 3, 2, 1),
            BackgroundGradient = GradientType.Vertical,
            Text1 = "Deaths:",
            Text2 = "Deaths <var> & \"more\"",
            Display2Rows = true,
            CustomVariable = true,
        };

        string xml = Save(settings.GetSettings);
        var loaded = new TextComponentSettings();
        loaded.SetSettings(ToNode(xml));

        Assert.Equal(xml, Save(loaded.GetSettings));
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal("Deaths <var> & \"more\"", loaded.Text2);
        Assert.True(loaded.CustomVariable);
        Assert.True(loaded.Display2Rows);
    }

    [Fact]
    public void RoundtripBlankSpaceSettings()
    {
        var settings = new BlankSpaceSettings
        {
            SpaceHeight = 12.5f,
            SpaceWidth = 640,
            BackgroundColor = Color.FromArgb(255, 1, 2, 3),
            BackgroundColor2 = Color.FromArgb(255, 3, 2, 1),
            BackgroundGradient = GradientType.Vertical,
        };

        string xml = Save(settings.GetSettings);
        var loaded = new BlankSpaceSettings();
        loaded.SetSettings(ToNode(xml));

        Assert.Equal(xml, Save(loaded.GetSettings));
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal(12.5f, loaded.SpaceHeight);
        Assert.Equal(640, loaded.SpaceWidth);
    }

    [Fact]
    public void KeepWindowsDefaultValues()
    {
        var timer = new TimerSettings();
        Assert.Equal((225f, 50f, "1", ".23", 35f), (timer.TimerWidth, timer.TimerHeight, timer.DigitsFormat, timer.Accuracy, timer.DecimalsSize));
        Assert.Equal(Color.FromArgb(170, 170, 170), timer.TimerColor);
        Assert.True(timer.ShowGradient);
        Assert.Equal("Current Timing Method", timer.TimingMethod);

        var title = new TitleSettings();
        Assert.True(title.ShowGameName && title.ShowCategoryName && title.ShowAttemptCount && title.DisplayGameIcon && title.ShowVariables);
        Assert.Equal(GradientType.Vertical, title.BackgroundGradient);
        Assert.Equal(Color.FromArgb(255, 42, 42, 42), title.BackgroundColor);

        var text = new TextComponentSettings();
        Assert.Equal(("Text", ""), (text.Text1, text.Text2));

        var blank = new BlankSpaceSettings();
        Assert.Equal((100f, 100f), (blank.SpaceHeight, blank.SpaceWidth));
    }

    [Fact]
    public void ReadOldTimerSettingsVersions()
    {
        var settings = new TimerSettings();
        settings.SetSettings(ToNode("<Settings><Version>1.3</Version><TimerHeight>69</TimerHeight><TimerWidth>225</TimerWidth><TimerAccuracy>Tenths</TimerAccuracy><UseSplitColors>True</UseSplitColors></Settings>"));
        Assert.Equal(".2", settings.Accuracy);
        Assert.Equal("1", settings.DigitsFormat);
        Assert.Equal(69, settings.TimerHeight);

        settings.SetSettings(ToNode("<Settings><Version>1.1</Version><TimerHeight>10</TimerHeight><TimerWidth>20</TimerWidth></Settings>"));
        Assert.Equal(".23", settings.Accuracy);

        settings.SetSettings(ToNode("<Settings><Version>1.5</Version><TimerHeight>10</TimerHeight><TimerWidth>20</TimerWidth><TimerFormat>00:01</TimerFormat></Settings>"));
        Assert.Equal(("00:01", ""), (settings.DigitsFormat, settings.Accuracy));
    }

    public static IEnumerable<object[]> SettingsTypes() =>
    [
        [typeof(TimerSettings)],
        [typeof(TitleSettings)],
        [typeof(TextComponentSettings)],
        [typeof(BlankSpaceSettings)],
        [typeof(SplitsSettings)],
    ];

    /// <summary>Every settable property must raise PropertyChanged with its own name, so Avalonia two-way bindings work.</summary>
    [Theory]
    [MemberData(nameof(SettingsTypes))]
    public void RaisePropertyChangedForEverySettableProperty(Type type)
    {
        var settings = type == typeof(SplitsSettings) ? new SplitsSettings(TestSupport.CreateState()) : (SettingsBase)Activator.CreateInstance(type);
        var raised = new List<string>();
        settings.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        foreach (System.Reflection.PropertyInfo property in TestSupport.SettableProperties(type))
        {
            if (property.Name == "CurrentState")
            {
                continue; // set by the component, not a user setting
            }

            SplitsTests.PreparePropertyChange(settings, property.Name);

            raised.Clear();
            object current = property.GetValue(settings);
            property.SetValue(settings, Different(property, current));
            Assert.Contains(property.Name, raised);

            // Setting the same value again must not notify.
            raised.Clear();
            property.SetValue(settings, property.GetValue(settings));
            Assert.DoesNotContain(property.Name, raised);
        }
    }

    private static object Different(System.Reflection.PropertyInfo property, object current)
    {
        Type t = property.PropertyType;
        if (property.Name is "GradientString" or "SplitGradientString")
        {
            return (string)current == "Vertical" ? "Horizontal" : "Vertical";
        }

        if (t == typeof(bool))
        {
            return !(bool)current;
        }

        if (t == typeof(float))
        {
            return (float)current + 3;
        }

        if (t == typeof(int))
        {
            return (int)current + 1;
        }

        if (t == typeof(string))
        {
            return current + "x";
        }

        if (t == typeof(Color))
        {
            return Color.FromArgb(255, 11, 22, 33).Equals(current) ? Color.FromArgb(255, 33, 22, 11) : Color.FromArgb(255, 11, 22, 33);
        }

        if (t == typeof(Font))
        {
            return new Font("Arial", 12f);
        }

        if (t.IsEnum)
        {
            Array values = Enum.GetValues(t);
            return values.GetValue(((int)Array.IndexOf(values, current) + 1) % values.Length);
        }

        throw new NotSupportedException($"{property.DeclaringType.Name}.{property.Name} : {t}");
    }

    [Fact]
    public void ExposeTimerViewState()
    {
        var settings = new TimerSettings { Mode = LayoutMode.Vertical };
        var raised = new List<string>();
        ((INotifyPropertyChanged)settings).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Equal(["Plain", "Vertical", "Horizontal", "Plain With Delta Color", "Vertical With Delta Color", "Horizontal With Delta Color"], TimerSettings.GradientOptions);

        // Plain: only the second swatch, and it edits BackgroundColor.
        Assert.False(settings.IsColor1Visible);
        Assert.True(settings.IsColor2Visible);
        settings.BackgroundSwatch2Color = Color.Red;
        Assert.Equal(Color.Red, settings.BackgroundColor);

        raised.Clear();
        settings.GradientString = "Vertical";
        Assert.True(settings.IsColor1Visible);
        Assert.True(settings.IsColor2Visible);
        Assert.Contains("IsColor1Visible", raised);
        Assert.Contains("BackgroundSwatch2Color", raised);
        settings.BackgroundSwatch2Color = Color.Blue;
        Assert.Equal(Color.Blue, settings.BackgroundColor2);
        Assert.Equal(Color.Red, settings.BackgroundColor);

        settings.GradientString = "Plain With Delta Color";
        Assert.False(settings.IsColor1Visible);
        Assert.False(settings.IsColor2Visible);

        settings.OverrideSplitColors = true;
        Assert.True(settings.IsTimerColorEnabled);

        // The size slider follows the layout mode.
        settings.TimerHeight = 66;
        settings.TimerWidth = 333;
        Assert.Equal((20f, 150f, 66f), (settings.SizeMinimum, settings.SizeMaximum, settings.SizeValue));
        raised.Clear();
        settings.Mode = LayoutMode.Horizontal;
        Assert.Equal((50f, 500f, 333f), (settings.SizeMinimum, settings.SizeMaximum, settings.SizeValue));
        Assert.Contains("SizeValue", raised);
        Assert.Contains("SizeLabel", raised);
        settings.SizeValue = 444;
        Assert.Equal(444, settings.TimerWidth);
        Assert.Equal(66, settings.TimerHeight);
    }

    [Fact]
    public void ExposeTextViewState()
    {
        var settings = new TextComponentSettings { Mode = LayoutMode.Horizontal };
        Assert.False(settings.IsDisplay2RowsEnabled);
        Assert.True(settings.Display2RowsShown);
        Assert.False(settings.Display2Rows);

        settings.Mode = LayoutMode.Vertical;
        Assert.True(settings.IsDisplay2RowsEnabled);
        Assert.False(settings.Display2RowsShown);

        Assert.Equal("Text:", settings.Text2Label);
        settings.CustomVariable = true;
        Assert.Equal("Custom Variable Name:", settings.Text2Label);
    }
}
