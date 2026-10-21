using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class TotalPlaytimeView : InfoSettingsPage
{
    public TotalPlaytimeView(object model) : base(model)
    {
        Add(Gradient(), TwoRows(), Check("Show Total Hours", "ShowTotalHours"), TextColor(), TimeColor());
    }
}
