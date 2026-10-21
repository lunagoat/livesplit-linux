using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class CurrentComparisonView : InfoSettingsPage
{
    public CurrentComparisonView(object model) : base(model)
    {
        Add(Gradient(), TwoRows(), Section("Comparing Against Label", TextColor("Color")), Section("Comparison Text", TimeColor("Color")));
    }
}
