using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class WorldRecordView : InfoSettingsPage
{
    public WorldRecordView(object model) : base(model)
    {
        Add(Gradient(), TwoRows(), Check("Centered Text", "CenteredText", "IsCenteredTextEnabled", "CenteredTextShown"), TextColor(), TimeColor(),
    Section("Filter Leaderboard By", Check("Region", "FilterRegion"), Check("Platform", "FilterPlatform"), Check("Variables", "FilterVariables"), Check("Subcategories", "FilterSubcategories"), Combo("Timing Method:", "TimingMethod")),
    Section("Precision", Radios("WRPrecision", ("From Leaderboard", "FromLeaderboard"), ("Seconds", "Seconds"), ("Milliseconds", "Milliseconds"))));
    }
}
