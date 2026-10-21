using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class HotkeyIndicatorView : SettingsPage
{
    public HotkeyIndicatorView(object model) : base(model)
    {
        Add(Color("Global Hotkeys On Color:", "HotkeysOnColor"), Color("Global Hotkeys Off Color:", "HotkeysOffColor"), Slider("", "SizeValue", 1, 50, size: true));
    }
}
