using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class SumOfBestView : InfoSettingsPage
{
    public SumOfBestView(object model) : base(model)
    {
        Add(Gradient(), TwoRows(), TextColor("Text"), Section("Time", TimeColor("Color"), Accuracy("Accuracy")));
    }
}
