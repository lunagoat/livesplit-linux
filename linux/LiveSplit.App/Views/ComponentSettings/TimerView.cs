using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class TimerView : SettingsPage
{
    public TimerView(object model) : base(model)
    {
        Add(Gradient(), Combo("Timing Method:", "TimingMethod"), Slider("", "SizeValue", 1, 1000, size: true),
    ColorOverride("Timer Color", "OverrideSplitColors", "TimerColor", "IsTimerColorEnabled"),
    Check("Show Gradient", "ShowGradient"), Check("Align to Center", "CenterTimer"),
    Row("Timer Format:", new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { ComboBox("DigitsFormat", "DigitsFormatOptions"), ComboBox("Accuracy", "AccuracyOptions") } }),
    Slider("Decimals Font Size:", "DecimalsSize", 10, 50));
    }
}
