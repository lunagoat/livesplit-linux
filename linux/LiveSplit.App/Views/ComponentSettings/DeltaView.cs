using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class DeltaView : InfoSettingsPage
{
    public DeltaView(object model) : base(model)
    {
        Add(Gradient(), Combo("Comparison:", "Comparison", translate: false), TwoRows(),
    Accuracy("Accuracy", "Delta Accuracy", before: Check("Drop Decimals When More Than 1 Minute", "DropDecimals")),
    Section("Text", TextColor("Color"), Check("Override Default Label", "OverrideText"),
        Check("Different Label When Ahead", "DifferentialText", "IsCustomTextEnabled"),
        Text("", "CustomText", "IsCustomTextEnabled", "CustomTextLabel", validation: true), Text("Label When Ahead:", "CustomTextAhead", "IsCustomTextAheadEnabled", validation: true)));
    }
}
