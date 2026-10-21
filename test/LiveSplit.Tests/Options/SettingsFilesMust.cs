using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.Hosting;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Xunit;

namespace LiveSplit.Tests.Options;

public class SettingsFilesMust
{
    public SettingsFilesMust()
    {
        ComponentManager.RaceProviderFactories ??= new Dictionary<string, IRaceProviderFactory>();
    }

    private static ISettings RoundTrip(ISettings settings)
    {
        using var stream = new MemoryStream();
        new XMLSettingsSaver().Save(settings, stream);
        stream.Position = 0;
        return new XMLSettingsFactory(stream).Create();
    }

    [Fact]
    public void RoundtripHotkeysByName()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName].SplitKey = new KeyOrButton(Keys.Oemtilde | Keys.Control);
        settings.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName].PauseKey = new KeyOrButton(Keys.F5);

        using var stream = new MemoryStream();
        new XMLSettingsSaver().Save(settings, stream);
        string xml = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Matches(@"<SplitKey>Oem(tilde|3), Control</SplitKey>", xml);
        Assert.Matches(@"<PauseKey>F5</PauseKey>", xml);

        ISettings loaded = RoundTrip(settings);
        HotkeyProfile profile = loaded.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName];
        Assert.Equal(Keys.Oemtilde | Keys.Control, profile.SplitKey.Key);
        Assert.Equal(Keys.F5, profile.PauseKey.Key);
        Assert.Equal(Keys.NumPad3, profile.ResetKey.Key);
        Assert.Equal(16834, loaded.ServerPort);
    }

    [Fact]
    public void ReadHotkeysAsWrittenByWindowsLiveSplit()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        using var stream = new MemoryStream();
        new XMLSettingsSaver().Save(settings, stream);
        string xml = Encoding.UTF8.GetString(stream.ToArray())
            .Replace("<SplitKey>NumPad1</SplitKey>", "<SplitKey>OemPeriod, Alt</SplitKey>")
            .Replace("<ResetKey>NumPad3</ResetKey>", "<ResetKey>Next</ResetKey>");

        using var modified = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        ISettings loaded = new XMLSettingsFactory(modified).Create();

        HotkeyProfile profile = loaded.HotkeyProfiles[HotkeyProfile.DefaultHotkeyProfileName];
        Assert.Equal(Keys.OemPeriod | Keys.Alt, profile.SplitKey.Key);
        Assert.Equal(Keys.PageDown, profile.ResetKey.Key);
    }

    [Fact]
    public void ConvertWindowsPathsOfRecentFiles()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.RecentLayouts.Add(@"Z:\home\user\layouts\my layout.lsl");
        settings.RecentLayouts.Add("/already/unix.lsl");

        ISettings loaded = RoundTrip(settings);

        Assert.Equal(["/home/user/layouts/my layout.lsl", "/already/unix.lsl"], loaded.RecentLayouts);
    }

    [Fact]
    public void ReadTheDefaultLayoutOfWindowsLiveSplit()
    {
        string path = RepositoryFiles.Find("src", "LiveSplit.View", "Resources", "DefaultLayout.lsl");
        string xml = Regex.Replace(File.ReadAllText(path), "<Components>.*</Components>", "<Components />", RegexOptions.Singleline);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        ILayout layout = new XMLLayoutFactory(stream).Create(null);

        Assert.Equal("Century Gothic", layout.Settings.TimerFont.Name);
        Assert.Equal("Segoe UI", layout.Settings.TimesFont.Name);
        Assert.Equal("Segoe UI", layout.Settings.TextFont.Name);
        Assert.Equal(LayoutMode.Vertical, layout.Mode);
        Assert.True(layout.VerticalWidth > 0);
    }
}
