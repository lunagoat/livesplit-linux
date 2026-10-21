using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class RunPredictionView : InfoSettingsPage
{
    public RunPredictionView(object model) : base(model)
    {
        Add(Gradient(), Combo("Comparison:", "Comparison", translate: false), TwoRows(), TextColor(), Section("Time", TimeColor("Color"), Accuracy("Accuracy")));
    }
}
