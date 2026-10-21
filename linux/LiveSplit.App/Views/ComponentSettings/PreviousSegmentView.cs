using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class PreviousSegmentView : InfoSettingsPage
{
    public PreviousSegmentView(object model) : base(model)
    {
        Add(Gradient(), Combo("Comparison:", "Comparison", translate: false), TwoRows(), TextColor(),
    Section("Delta Accuracy", Check("Drop Decimals When More Than 1 Minute", "DropDecimals"), Accuracy("DeltaAccuracy")),
    Section("Possible Time Save", Check("Show Possible Time Save", "ShowPossibleTimeSave"), Accuracy("TimeSaveAccuracy", enabled: "IsTimeSaveAccuracyEnabled")));
    }
}
