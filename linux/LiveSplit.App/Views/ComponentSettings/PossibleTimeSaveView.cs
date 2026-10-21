using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class PossibleTimeSaveView : InfoSettingsPage
{
    public PossibleTimeSaveView(object model) : base(model)
    {
        Add(Gradient(), Combo("Comparison:", "Comparison", translate: false), TwoRows(), Check("Show Total Possible Time Save", "TotalTimeSave"), TextColor(),
    Section("Time", TimeColor("Color"), Accuracy("Accuracy", before: Check("Drop Decimals When More Than 1 Minute", "DropDecimals"))));
    }
}
