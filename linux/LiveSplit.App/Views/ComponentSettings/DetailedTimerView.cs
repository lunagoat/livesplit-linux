using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class DetailedTimerView : SettingsPage
{
    public DetailedTimerView(object model) : base(model)
    {
        Add(Gradient(), Combo("Timing Method:", "TimingMethod"),
    Section("Segment Time Comparisons", Check("Hide Comparison 2", "HideComparison"), Combo("Comparison 1:", "Comparison", translate: false), Combo("Comparison 2:", "Comparison2", enabled: "IsComparison2Enabled", translate: false)),
    Slider("", "SizeValue", 1, 1000, size: true), Slider("Segment Timer Size:", "SegmentTimerSizeRatio", 10, 90),
    Section("Timer", ColorOverride("Color", "OverrideTimerColors", "TimerColor", "IsTimerColorEnabled"), Check("Show Gradient", "TimerShowGradient"),
        Row("Format:", new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { ComboBox("DigitsFormat", "DigitsFormatOptions"), ComboBox("Accuracy", "AccuracyOptions") } }), Slider("Decimals Font Size:", "DecimalsSize", 10, 50)),
    Section("Segment Timer", Color("Color:", "SegmentTimerColor"), Check("Show Gradient", "SegmentTimerShowGradient"),
        Row("Format:", new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { ComboBox("SegmentDigitsFormat", "DigitsFormatOptions"), ComboBox("SegmentAccuracy", "AccuracyOptions") } }), Slider("Decimals Font Size:", "SegmentTimerDecimalsSize", 10, 50)),
    Section("Segment Labels", Font("Font:", "SegmentLabelsFont"), Color("Color:", "SegmentLabelsColor")),
    Section("Segment Times", Font("Font:", "SegmentTimesFont"), Color("Color:", "SegmentTimesColor"), Accuracy("SegmentTimesAccuracy")),
    Section("Icon", Check("Display Icon", "DisplayIcon"), Slider("Size:", "IconSize", 20, 100, "IsIconSizeEnabled")),
    Section("Split Name", Check("Show Split Name", "ShowSplitName"), Font("Font:", "SplitNameFont", "IsSplitNameEnabled"), Color("Split Name Color:", "SplitNameColor", "IsSplitNameEnabled")));
    }
}
