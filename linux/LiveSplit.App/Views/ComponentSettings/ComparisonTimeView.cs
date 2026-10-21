using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class ComparisonTimeView : InfoSettingsPage
{
    public ComparisonTimeView(object model) : base(model)
    {
        Add(Gradient(), Combo("Comparison:", "Comparison", translate: false), Combo("Timing Method:", "TimingMethod"), TwoRows(), TextColor(),
    Section("Time", TimeColor("Color"), Accuracy("Accuracy"), Section("Type", Radios("Type", ("Final Time", "FinalTime"), ("Split Time", "SplitTime"), ("Segment Time", "SegmentTime")))));
    }
}
