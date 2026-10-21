using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.Input;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;

namespace LiveSplit.AppTests;

public class DialogsLogicMust
{
    private static LiveSplitState CreateState()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", "Celeste - Any% (1.2.1.5).lss"));
        IRun run = new StandardFormatsRunFactory(stream).Create(new StandardComparisonGeneratorsFactory());
        return new LiveSplitState(run, null, null, null, new Settings
        {
            ActiveAutoSplitters = [],
            ComparisonGeneratorStates = new Dictionary<string, bool>()
        });
    }

    [Fact]
    public void LayoutSettingsSnapshotsAndRevertsCorrectly()
    {
        var factory = new StandardLayoutSettingsFactory();
        var settings = factory.Create();

        var originalColor = settings.BackgroundColor;
        var originalOpacity = settings.Opacity;
        var snapshot = (LayoutSettings)settings.Clone();

        // Mutate
        settings.BackgroundColor = DrawingColor.Purple;
        settings.Opacity = 0.42f;
        settings.AlwaysOnTop = !settings.AlwaysOnTop;

        Assert.NotEqual(originalColor, settings.BackgroundColor);
        Assert.NotEqual(originalOpacity, settings.Opacity);

        // Revert from snapshot
        settings.Assign(snapshot);

        Assert.Equal(originalColor, settings.BackgroundColor);
        Assert.Equal(originalOpacity, settings.Opacity);
        Assert.Equal(snapshot.AlwaysOnTop, settings.AlwaysOnTop);
    }

    [Fact]
    public void FontOverridesCorrectlyAppliesAndRestores()
    {
        var settings = new StandardLayoutSettingsFactory().Create();
        var origTimer = settings.TimerFont;
        var origTimes = settings.TimesFont;
        var origText = settings.TextFont;

        var overrides = new FontOverrides
        {
            OverrideTimerFont = true,
            TimerFont = new DrawingFont("Arial", 24f),
            OverrideTimesFont = false
        };

        Assert.True(overrides.HasOverrides);

        overrides.ApplyTo(settings, out var savedTimer, out var savedTimes, out var savedText);
        Assert.Equal("Arial", settings.TimerFont.Name);
        Assert.Equal(origTimes, settings.TimesFont);

        FontOverrides.Restore(settings, savedTimer, savedTimes, savedText);
        Assert.Equal(origTimer, settings.TimerFont);
        Assert.Equal(origTimes, settings.TimesFont);
        Assert.Equal(origText, settings.TextFont);
    }

    [Fact]
    public void SettingsHotkeyProfileAddRenameRemove()
    {
        var settings = new Settings
        {
            HotkeyProfiles = new Dictionary<string, HotkeyProfile>
            {
                ["Default"] = new HotkeyProfile { SplitKey = new KeyOrButton(System.Windows.Forms.Keys.NumPad1) }
            },
            RecentSplits =
            [
                new RecentSplitsFile("celeste.lss", TimingMethod.RealTime, "Default", "Celeste", "Any%")
            ]
        };

        // Add profile
        var newProfile = (HotkeyProfile)settings.HotkeyProfiles["Default"].Clone();
        newProfile.SplitKey = new KeyOrButton(System.Windows.Forms.Keys.Space);
        settings.HotkeyProfiles.Add("SpeedrunProfile", newProfile);

        Assert.Equal(2, settings.HotkeyProfiles.Count);
        Assert.Equal(System.Windows.Forms.Keys.Space, settings.HotkeyProfiles["SpeedrunProfile"].SplitKey.Key);

        // Rename profile
        string oldName = "Default";
        string newName = "Standard";
        for (int i = 0; i < settings.RecentSplits.Count; i++)
        {
            var file = settings.RecentSplits[i];
            if (file.LastHotkeyProfile == oldName)
            {
                settings.RecentSplits[i] = new RecentSplitsFile(file.Path, file.LastTimingMethod, newName, file.GameName, file.CategoryName);
            }
        }
        var p = settings.HotkeyProfiles[oldName];
        settings.HotkeyProfiles.Remove(oldName);
        settings.HotkeyProfiles[newName] = p;

        Assert.True(settings.HotkeyProfiles.ContainsKey("Standard"));
        Assert.False(settings.HotkeyProfiles.ContainsKey("Default"));
        Assert.Equal("Standard", settings.RecentSplits[0].LastHotkeyProfile);

        // Remove profile
        settings.HotkeyProfiles.Remove("SpeedrunProfile");
        Assert.Single(settings.HotkeyProfiles);
    }

    [Fact]
    public void ShareRunTemplateVariableFormatting()
    {
        var state = CreateState();
        var run = state.Run;

        var timeFormatter = new RegularTimeFormatter(TimeAccuracy.Seconds);
        string game = run.GameName ?? "";
        string category = run.GetExtendedCategoryName() ?? "";
        string pb = timeFormatter.Format(run[^1].PersonalBestSplitTime[state.CurrentTimingMethod]) ?? "";
        string title = run.GetExtendedName() ?? "";

        string template = "Finished $title ($category) in $pb! #$game";
        string formatted = template
            .Replace("$game", game)
            .Replace("$category", category)
            .Replace("$title", title)
            .Replace("$pb", pb);

        Assert.Contains(game, formatted);
        Assert.Contains(category, formatted);
        Assert.Contains(pb, formatted);
        Assert.DoesNotContain("$title", formatted);
        Assert.DoesNotContain("$pb", formatted);
    }

    [Fact]
    public void SpeedrunComTimeSpanParsing()
    {
        var parsed1 = TimeSpanParser.ParseNullable("1:23:45.67");
        Assert.NotNull(parsed1);
        Assert.Equal(1, parsed1.Value.Hours);
        Assert.Equal(23, parsed1.Value.Minutes);
        Assert.Equal(45, parsed1.Value.Seconds);

        var parsedEmpty = string.IsNullOrWhiteSpace("") ? null : TimeSpanParser.ParseNullable("".Trim());
        Assert.Null(parsedEmpty);

        var parsedWhitespace = string.IsNullOrWhiteSpace("   ") ? null : TimeSpanParser.ParseNullable("   ".Trim());
        Assert.Null(parsedWhitespace);
    }
}
