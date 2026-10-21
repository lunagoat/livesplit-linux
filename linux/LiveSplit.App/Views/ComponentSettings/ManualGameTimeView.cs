using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class ManualGameTimeView : SettingsPage
{
    public ManualGameTimeView(object model) : base(model)
    {
        Add(Section("Game Time Input", Radios("UseSegmentTimes", ("Segment Times", "True"), ("Split Times", "False"))));
    }
}
