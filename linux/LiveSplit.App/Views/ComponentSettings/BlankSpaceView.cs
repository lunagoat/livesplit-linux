using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class BlankSpaceView : SettingsPage
{
    public BlankSpaceView(object model) : base(model)
    {
        Add(Gradient(), Slider("", "SizeValue", 1, 1000, size: true));
    }
}
